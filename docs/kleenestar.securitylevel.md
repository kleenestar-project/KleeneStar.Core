# Security levels

A **security level** is a classification an object may carry, together with the groups whose
members are cleared to see it. It answers one question and only one: *who sees this record at
all.*

Security levels are defined **per class, the way fields are**. The class is the catalog: there
is no enum of levels anywhere in the code, and an administrator decides what a class classifies
its objects with. A class that defines none classifies nothing, and every object of it is
visible to everyone — which is what every installation looks like before somebody decides
otherwise.

**They replaced the permissions an individual object used to carry.** There is no per-object
permission dialog any more, and no grant is stored against a record. The two halves of the
access model now divide cleanly:

| Question | Answered by | Administered on |
| --- | --- | --- |
| Who may see *this record*? | its security level | the record, in its classification dialog |
| Who may read and change *the records of this data structure*? | the `object_…` policies | the **class** |
| Who may change the data structure itself? | the `class_…` policies | the class |

`PermissionScope.Object` survives only as the prefix those `object_…` policies carry;
`PolicyCatalog.Administered` is what makes the class dialog offer them, and `GetLabel` keeps the
word *Object* in their labels so `class_admin_policy` and `object_admin_policy` do not both read
"Admin" on the same dialog. Nothing stores a grant with that scope, and the chain
`ObjectRelationAuthorization` evaluates runs **class → workspace**, not through the object.

## The rule

> An object without a level is visible to everyone.
> An object with a level is visible to an identity that belongs to at least one of the groups
> the level names.

That is the whole rule. Two consequences follow that are worth stating out loud, because both
are decisions rather than accidents:

**A level that names no group is closed.** Nobody is cleared for it, and every object carrying
it disappears from every list. This is the opposite reading of `IPermissionManager.IsGranted`,
where a resource nobody administered is unrestricted — and deliberately so. There, "nobody said
anything" is the normal state of a fresh installation and must not lock everyone out. Here,
creating a level and putting it on an object *is* the act of administering: there is no silence
left to interpret. An administrator who has not yet said who may see a level has restricted the
records, not left them open.

**A level the running system cannot resolve is closed too.** Deleting a level declassifies the
objects that carried it (`ModelHub.Remove` clears them by name, and the foreign key is declared
`SetNull` as well), so the case only arises when a row was removed behind the manager's back.
Reading a dangling classification as "cleared for nobody" is the safe direction.

The rank of a level orders the display and nothing else. A higher rank does **not** imply the
clearance of a lower one — a level clears exactly the groups it names.

## Where the rule is enforced

**In `ObjectManager`, once.** `GetObject`, `GetObjectByKey`, `GetObjects`, `CountObjects` and
`GetRecentObjects` all answer only what the identity behind the current request is cleared to
see. That is what makes the rule a property of the system rather than of the lists somebody
remembered to guard: an overview written tomorrow obeys it without its author knowing it exists.

The narrowing is a **predicate on the query**, not a filter over its result
(`ISecurityLevelManager.Restrict`), so it lands before paging and a page of classified records
does not come back short. It is expressed over the level ids rather than over the groups,
because the clearance itself lives in a serialized column no store can filter on.

### Lifting it

Some reads are the system's own rather than a user's, and those must see every record or they
answer wrongly:

```csharp
using (CoreHub.SecurityLevelManager.BeginUnrestricted())
{
    // issuing the next object key, a relation guard, a commit replay, a usage count
}
```

The scope is ambient to the logical call and nests, the way `CommitManager.BeginCommit` does.
The callers that open one today, and why:

| Caller | Why it must see everything |
| --- | --- |
| `ObjectManager.NextObjectKey` | A key derived from what the caller happens to be cleared for would be handed out twice. |
| `WorkflowManager.ExecuteTransition` | A transition also runs on the follower a relation closes, which is somebody else's record. |
| `ObjectRelationWorkflowRules.FindClosingTarget` | Same: the follower has to move whether or not the caller is cleared for it. |
| `CommitManager.Build` / `Hydrate` | Resolving the object of a chain already reached is plumbing; whether the reader may see it was decided upstream. |
| The usage counts on the level table and its delete dialog | A level guarding twenty records must not report three because that is all the reader may open. |

**A read that is not on this list should not lift the filter.** The user-facing paths — the
detail page, the history, the drafts, the attachments, the relations — all resolve their object
through the manager and are guarded by that alone.

## The write side

`/api/1/objects` refuses a classification the caller may not assign
(`securitylevel.object.restricted`). The form only offers the levels they are cleared for, so
this is not what stops an honest mistake — it is what makes the rule true of the endpoint rather
than of one dialog. Clearing a classification is always allowed: it makes the record more
visible, never less. An unchanged classification is not a new decision and is left alone, so an
edit of a record somebody was cleared for yesterday does not become unsavable today.

A create that says nothing about the classification gets the class's **default level**
(`SecurityLevel.IsDefault`, at most one per class — `SecurityLevelManager` demotes the rivals on
every write). Otherwise every record filed through an interface that does not ask — the api, a
template, an import — would silently come out unclassified. The default is only applied when the
caller is cleared for it; where they are not, the object stays unclassified rather than
disappearing from the list of the person who just created it.

A **clone keeps the classification of its original**. A duplicate that quietly came out readable
by more people than its original would be a leak dressed up as a convenience.

## What the user sees

**In its own dialog** — `WWW/Issue/{objectkey}/SecurityLevel`, opened from the entry in the
overflow menu where the object's permission dialog used to sit. `ObjectSecurityLevelFormFragment`
is a one-field edit form over the object CRUD endpoint (no endpoint of its own, the same
arrangement `DocumentHomeFormFragment` uses for the workspace home page), and
`ObjectFormLayout.CreateSecurityLevelInput` builds the selection, fed by
`/api/1/securitylevels/{classid}/selection`, which offers only the levels the caller may assign
plus an entry standing for *unclassified* (the empty guid, which the form binder reads as "clear
this property").

**The classification is deliberately not a field on any object content form.** The create
wizard, the edit dialog and the clone dialog write what a record *says*; who may see it is not
part of that, and it is set where the permissions it replaced were set. A new object starts on
the default level of its class and is reclassified afterwards.

Those forms do still *report* it: `ObjectFormLayout.CreateSecurityLevelNotice` puts a **warning**
on them, and on the classification dialog itself, in exactly two situations — both ones the form
would otherwise leave to be discovered by the record disappearing:

- The class classifies its objects but the caller is cleared for **none** of its levels. In the
  dialog the input is then absent altogether, and the notice says why
  (`securitylevel.object.unavailable`). On the create wizard it is the warning that the record
  about to be filed may leave the filer's own lists the moment it exists.
- The object **already carries** a level the caller cannot assign. Saving keeps the
  classification, and with it the chance that the record leaves their own view
  (`securitylevel.object.hint`).

Where there is nothing to say, there is no notice. A warning that is always there stops being a
signal.

**On the object detail page**, `ObjectPropertySecurityLevelCardFragment` shows the level and the
groups cleared for it — and is absent on an unclassified object. Anybody reading the page is by
definition cleared for the level, so naming it discloses nothing they do not already have; what
it does disclose is *who else* sees the record, which is the question somebody about to write on
it needs answered.

**In object tables**, `securitylevel` is a read-only column of the system column catalog. It is
read-only on purpose: the classification decides who sees the row at all, so it is changed on
the object's own form where the hint about it can be shown, not by an inline edit in a list.

## Administering the levels

The levels of a class are administered at `WWW/SecurityLevels/{classid}/`, reached from the
class sidebar, and follow the field administration one-for-one: an overview with a table, an
*add* modal, and *edit* / *clone* / *delete* modals per row under
`WWW/SecurityLevel/{securitylevelid}/`. The endpoints sit under `/api/1/securitylevels`:

| Route | Purpose |
| --- | --- |
| `/api/1/securitylevels` | CRUD over the levels (`RestApiCrud<SecurityLevel>`) |
| `/api/1/securitylevels/{classid}/table` | The overview table, including the usage count per level |
| `/api/1/securitylevels/{classid}/quickfilter` | *Active* and *Closed* — the levels nobody is cleared for are the ones worth finding |
| `/api/1/securitylevels/{classid}/selection` | The levels an object form may offer |
| `/api/1/securitylevels/groups` | The groups a clearance can name |
| `/api/1/securitylevels/state` | Active / Archived |
| `/api/1/securitylevels/wql` | The advanced-search prompt |

**Deleting a level declassifies what it guarded**, so the confirmation dialog states how many
objects that is — the one consequence the list it was opened from cannot show.

**Archiving is the softer move**: an archived level can no longer be assigned but keeps guarding
the objects already classified with it.

The administration surface is gated by `securitylevel_read` / `_create` / `_update` / `_delete`
/ `_clone`, which hang off the **class** policies (`class_view_policy`, `class_edit_policy`,
`class_admin_policy`) rather than a scope of their own — a security level is part of a class's
configuration and is administered on the class's pages.

Note that being able to *read the catalog* is not the same as being *cleared for a level*. The
clearance is the group list and is evaluated by `ISecurityLevelManager`; the permissions above
govern who may look at and change what a class classifies its objects with.

## Model

`SecurityLevel` carries `Name`, `Description`, `State`, `Rank`, `IsDefault`, `Icon`, the usual
timestamps, its `ClassId`, and `PermittedGroupIds` — a serialized list of group ids rather than a
join table, because it is read as a whole on every visibility check and written as a whole by
the one form that edits it. `(ClassId, Name)` is unique.

`Object.SecurityLevelId` is nullable and points at one of the levels of the object's class.
`null` means unclassified.

The changes to the catalog reach the audit log through the ordinary
`AuditManager.Connect()` bridge — one line in `ConnectConfiguration`, like every other
configuration manager. Changing the classification of an *object* is an object mutation and
reaches the log through `CommitManager.CommitAdded` like any other property.

## The seed

`KleeneStarDbSeeder.SecurityLevels` gives every concrete class three levels — *Public* (the
default, all groups), *Internal* (Admin, Engineering, Support) and *Confidential* (Admin) — and
`SeedObjectSecurityLevels` classifies a share of the seeded objects with them, by position
rather than at random so the same checkout always produces the same classified records. A fresh
installation therefore shows what a classification *does*, not merely that one can be
configured.

The object pass is guarded on **nothing being classified yet**, not on the objects being new.
Left unguarded it would re-classify on every start, putting a classification back on a record
an administrator had deliberately declassified.
