![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Audit Log Concept

The audit log in **KleeneStar** is the installation's record of itself: a single, append-only, hash-chained sequence of every event worth reconstructing later, whoever or whatever caused it. It exists to answer questions that are asked after the fact and under pressure — what happened, in which order, on whose authority, and what state did that leave behind — and to answer them in a way a reader who trusts nobody can check.

Auditing adds two entities to the core data model: `AuditEvent`, one fact the installation records about itself, and `AuditDelta`, one attribute-level state change inside it. It is implemented fully server-side and is enforced by the `AuditManager`, which is the only writer of the audit store and offers no operation that changes or removes what it has written. The user interface is a reader of the log and nothing more; replacing it would change nothing about what is recorded.

## What the log is not

The audit log sits beside the per-object `Commit` chain (see `kleenestar.commit.md`), and the two are deliberately not the same thing.

|            | `Commit` chain                            | Audit log
|------------|-------------------------------------------|-------------------------------
| Scope      | one object                                | the whole installation
| Purpose    | version history a user reads and restores | forensic record a reviewer verifies
| Covers     | field values of objects                   | objects, schema, identities, permissions, credentials, lifecycle
| Mutability | append-only per object                    | append-only, hash-chained
| Restorable | yes                                       | no — the log describes, it does not undo

Object mutations therefore appear in both, and that is not redundancy. The commit says what the object looked like; the audit event says that somebody changed it, when, from where, and as part of which action. The two are joined by `AuditEvent.TargetRevision`, which carries the commit number the change produced.

The log is also not a message log. There is no free-form text field anywhere in the model. An entry reading `"user admin deleted class Bug"` cannot be filtered, counted, aggregated, or read in another language, and it cannot be replayed into a state. Everything the sentence carries is stored as typed fields instead, and the sentence is composed for display from those fields.

## Event Model

Every event is classified along five independent axes. They are independent because collapsing any two of them loses a question a reader needs to be able to ask.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                          KleeneStar Audit Event Typing                               ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║   Origin      ┌─ System      the installation acting on itself                       ║
║   who set it  ├─ User        a person through the interface                          ║
║   in motion   ├─ Automation  a scheduled process, possibly in a user's name          ║
║               └─ External    the REST API, the portal, a webhook                     ║
║                                                                                      ║
║   Category    Lifecycle · Security · Identity · Authorization ·                      ║
║   what area   Configuration · Content · Workflow · Integration                       ║
║                                                                                      ║
║   Action      Created · Updated · Deleted · Archived · Restored · Transitioned ·     ║
║   what was    SignedIn · SignedOut · SignInFailed · SessionRevoked ·                 ║
║   done        PermissionGranted · PermissionRevoked · AccessDenied ·                 ║
║               TokenIssued · TokenRevoked · Started · Stopped · Migrated ·            ║
║               Seeded · Pruned · Escalated · Breached · Imported · Exported ·         ║
║               Invoked                                                                ║
║                                                                                      ║
║   Outcome     Succeeded · Failed · Denied                                            ║
║   did it take  — the attempt is recorded, not the success                            ║
║   effect                                                                             ║
║                                                                                      ║
║   Severity    Info · Notice · Warning · Critical                                     ║
║   should      — orthogonal to Outcome: a successful self-granted permission          ║
║   somebody      is Critical; a failed save is not                                    ║
║   look                                                                               ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

`Origin` is not derivable from the actor. A change carrying an identity may have been made by a scheduled escalation running in that identity's name, and a change with no identity may be an anonymous API call rather than a system task. Recording the two separately is what lets *"what did this user do"* and *"what did the system do on its own"* be two different questions.

`Severity` is not derivable from `Outcome`. An administrator granting themselves a policy succeeds every time, and it is the single most important line in the log.

## Time base and ordering

`Timestamp` is always UTC, taken once at the moment of recording and never rewritten. It is not sufficient on its own: two events inside the same clock tick would be indistinguishable, and a clock adjustment — an NTP correction, a daylight-saving boundary, a restored backup on another host — would reorder the past.

`Sequence` is therefore the authoritative order. It is a gap-free counter assigned **inside the append transaction** from the current head, protected by a unique index so a race between two concurrent appenders becomes a failed transaction rather than an ambiguous order.

> **Reconstruct by sequence. Display by timestamp.**

Because the sequence is gap-free, a missing event is visible as a gap — which is what makes the one deliberate weakness of the design acceptable (see *Failure behaviour* below).

## Identity and sequences of events

Three durable identifiers link events into the sequences a reader actually wants.

- **`TargetId` + `TargetType`** — the durable id of the record the event is about. Reading every event for one id gives the complete trail of that record, including the events after it was deleted. The type is recorded beside the id so the trail is selectable and readable once the record itself is gone.
- **`TargetRevision`** — the version the record reached through the event. For an object this is the commit number, which ties an audit entry to a revision a user can open and restore.
- **`CorrelationId`** and **`CausationId`** — one action rarely produces one event. Deleting a class removes its fields, its forms and its objects; each is a fact worth recording on its own, and all belong to the same decision. The correlation id recovers the decision from its consequences; the causation id turns the correlated set into a tree.

Correlation is supplied by an ambient `IAuditActivity`, opened once where the request is understood and inherited by every event recorded inside it, however deep the call went. Activities nest: an inner caller that knows the actor fills in a blank the outer one left, but may not relabel what the outer one established.

## Delta storage

An event carries an ordered set of `AuditDelta` rows. Each names one attribute, what happened to it, and how its payloads are to be read back.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                    Delta kinds and what distinguishes them                           ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║   Added      the attribute did not exist before and does now                         ║
║              OldValue carries no meaning                                             ║
║                                                                                      ║
║   Modified   it existed before and after, and its value moved                        ║
║              OldValue may itself be null — "was explicitly empty"                    ║
║                                                                                      ║
║   Removed    it existed before and does not now                                      ║
║              OldValue preserves what was lost; NewValue carries no meaning           ║
║                                                                                      ║
║   The kind is STORED, never inferred from whether the payloads are null.             ║
║   Inference cannot tell:                                                             ║
║     · an attribute set to nothing   from  · one that ceased to exist                 ║
║     · one created empty             from  · one never touched                        ║
║   Those are different facts. A replay that confuses them produces a different        ║
║   state, and a diff view that confuses them shows something that never happened.     ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

Each delta also carries an `AuditValueKind` — `Text`, `Number`, `Boolean`, `Timestamp`, `Reference`, `Enumeration`, `Collection`, `Redacted`, `Binary`. The store keeps values as text, because an audit row has to survive the deletion of the field definition that gave it its type; without a recorded kind a later reader is left guessing from the characters, and a comparison between two revisions would depend on which guess it made. Enumerations are recorded by **member name**, never by ordinal: an ordinal is only meaningful against the version of the enumeration that wrote it.

Values are serialized with the invariant culture, timestamps in round-trip ISO 8601 UTC, so the same state always produces the same text.

### Replay

Replaying the deltas of one target in sequence order reproduces the state that target held at any point — `IAuditManager.Project(type, id, atSequence)`. This is what makes delta storage *sufficient* rather than merely compact: the log never holds a full snapshot of anything, yet any past state is recoverable from it. It also makes the log self-checking, since the projection at the head can be compared against the record itself; a difference means the log missed a change.

The projection is the log's account of the record, not the record. That distinction has one visible consequence: the **first modification of a record the log has never seen** records its attributes as `Added` rather than as modifications from an unknown state. That is not a lie about the record; it is the truth about the log. A `Modified` "from nothing" would claim knowledge of a previous value that was never recorded.

## Coverage

Recording is wired centrally rather than sprinkled through the managers, in `AuditManager.Connect()`:

```
CommitManager.CommitAdded ────────► Content   / Created·Updated·Transitioned·…
CommitManager.CommitRestored ─────► Content   / Restored
WorkflowManager.TransitionExecuted► Workflow  / Transitioned  (refusals included)

Class·Field·Form·Workflow·Status·Priority·Template·Sla·Calendar·
Insight·ObjectView·NavigatorLink·Workspace    Added/Updated/Removed
                                  ► Configuration
Branding·Maintenance Updated ─────► Configuration

Identity·Group·Tenant  Added/Updated/Removed
                                  ► Identity
PermissionManager.Assigned/Revoked► Authorization / Critical
ShareManager.Added/Removed ───────► Authorization
AccessTokenManager ───────────────► Security  / TokenIssued·TokenRevoked
IdentitySessionManager.Removed ───► Security  / SessionRevoked
Comment·Attachment·Link·Tag·Sprint► Content

Session endpoint ─────────────────► Security  / SignedIn·SignInFailed·SignedOut
KleeneStarApplication.Run ────────► Lifecycle / Started
KleeneStarDbSeeder ───────────────► Lifecycle / Seeded   (genesis event)
AuditManager.Prune ───────────────► Lifecycle / Pruned
```

Sprinkling `Record(...)` calls through thirty managers would put the completeness of the trail at the mercy of every future edit to any of them — and a missing call is invisible, because a hole in an audit log looks exactly like a period of inactivity. Subscribing centrally means a change reaches the log by the same path it reaches the rest of the application, and a new manager is audited by adding one line.

Two areas are deliberately excluded. **Field values and object mutations** arrive through `CommitAdded` instead of through the value manager, because the commit carries the exact before and after of every attribute the action touched — better than a diff reconstructed afterwards. **Per-identity conveniences** (notifications marked read, quickfilters, saved searches, recent visits) are not recorded at all: they change nothing anybody else can observe, and burying the events that matter under them would make the log less useful, not more complete.

## Secrets

Attributes are recorded by reflection over the scalar properties of the entity, so a new column on an audited entity is audited without anybody remembering to do so. Two attributes control the exceptions:

- `[AuditRedacted]` — the change is recorded, the value is not. Both payloads read as `[redacted]` and the value kind is `Redacted`. An administrator quietly resetting somebody's password is exactly the event an audit log exists to surface, and it would be invisible if the property were simply skipped. Applied to `Identity.PasswordHash` and `AccessToken.TokenHash`.
- `[AuditIgnore]` — the property never appears. Reserved for properties that carry no information about the record's state; sensitivity alone is a reason to redact, not to ignore.

Navigation properties and collections are skipped automatically: the related records audit themselves, and following the graph would record the same change from several directions at once.

## Integrity

Each event carries `Hash`, the SHA-256 of its own canonical content folded together with `PreviousHash`. Because the predecessor's hash is folded *in* rather than merely stored beside it, every event depends on the complete history before it: a row cannot be altered without altering every row after it.

The canonical form (`AuditSeal`) is a text encoding with a fixed field order, control-character separators that cannot occur inside a field, invariant formatting, an explicit null marker so an absent value and an empty one do not hash alike, and the deltas folded in in their recorded order. It is deliberately **not** JSON: a serializer is free to reorder properties, change its escaping, or omit defaults between versions, and any of those would silently invalidate every hash written before the change. There is exactly one implementation, shared by writer and verifier — a verifier that canonicalizes differently reports every row as tampered, one that canonicalizes less reports none, and both failures are silent.

`IAuditManager.Verify(from, count)` walks the chain and reports `BrokenAt` — the first position whose seal did not match — together with any `MissingSequences`. Verification is anchored at the first event in range rather than always at the genesis event, so a pruned log stays verifiable; refusing otherwise would make retention and integrity mutually exclusive.

> **What this buys:** any edit, deletion, insertion or reordering inside the verified range is detected and located.
>
> **What it does not:** it cannot stop somebody with write access to the database from rewriting the whole chain from the point they altered. Detecting that requires an anchor the installation does not control — an off-box copy of a recent hash, or a signature over it. The chain is what makes such an anchor cheap: **one hash pins every event before it**, and `AuditVerification.HeadHash` is that value.

The log is therefore *tamper-evident*, not tamper-proof, and the documentation says so rather than implying more.

## Failure behaviour

Recording is best-effort with respect to the action being audited: a failure to write the log never propagates into the operation that raised it.

This is a deliberate trade. Failing the operation instead would make the log a single point of failure for the whole installation and would hand anybody who can break the audit store the ability to stop all work. Letting the operation proceed means a storage failure can leave a hole — and the hole is detectable, because the sequence is gap-free and a missing event shows up as a break in `Verify`. That detectability is what makes the trade acceptable.

## Retention

Pruning is not automatic and is not reachable from the managers that write to the log. An audit trail that trims itself is one an attacker can make trim the evidence, and a retention rule belongs to the operator rather than to the code that happens to record an event.

`Prune(before, actorId)` removes the events older than the horizon and then records a `Lifecycle / Pruned` event at `Critical` severity, naming how many events went, the range they occupied, and the hash the removed range ended on. Without that marker the log would simply start later than it used to and nothing would say why — indistinguishable from somebody having deleted the beginning of the trail. With it, the gap is accounted for by the log itself, and the recorded terminal hash lets an operator holding an older copy prove the removed range was the one they had.

## Data Model

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                          KleeneStar Audit Data Model                                 ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║    ┌──────────────┐                                                                  ║
║    │  Identity    │◄╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌┐  (no FK — resolved on read, may be gone)        ║
║    └──────────────┘                ┊                                                 ║
║                                    ┊ ActorId                                         ║
║    ┌──────────────┐   TargetId   ┌─┴────────────┐ 1        * ┌──────────────┐        ║
║    │ any record   │◄╌╌╌╌╌╌╌╌╌╌╌╌─┤  AuditEvent  ├────────────►  AuditDelta  │        ║
║    │ (Object,     │              ├──────────────┤            ├──────────────┤        ║
║    │  Class,      │              │ Sequence  ▲  │            │ Kind         │        ║
║    │  Identity,   │              │ Timestamp │  │            │ Attribute    │        ║
║    │  Group, …)   │              │ Origin    │  │            │ AttributeId ╌┼╌► Field║
║    └──────────────┘              │ Category  │  │            │ ValueKind    │        ║
║                                  │ Action    │  │            │ OldValue     │        ║
║        ┌──────────────┐          │ Outcome   │  │            │ NewValue     │        ║
║        │ AuditEvent   │◄─────────┤ Severity  │  │            │ Ordinal      │        ║
║        │ (predecessor)│ Previous │ TargetType│  │            └──────────────┘        ║
║        └──────────────┘   Hash   │ TargetRev.│  │                                    ║
║                                  │ Correlatn.│  │      ▲ hash chain: each event's    ║
║                                  │ Causation │  │        Hash covers its content     ║
║                                  │ Hash ─────┘  │        AND its predecessor's Hash  ║
║                                  └──────────────┘                                    ║
║                                                                                      ║
║    No foreign key leaves the audit tables. The trail has to outlive every row        ║
║    it describes — which is exactly the case it is needed in.                         ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

Neither `ActorId` nor `TargetId` nor `AuditDelta.AttributeId` is a foreign key. A cascade would let deleting an identity erase the record of what they did; a restrict would make deleting anything impossible once something had been recorded about it. Both are wrong, so each id is a plain column beside a snapshot of the name it resolved to at the time of writing — `ActorName`, `TargetKey`, `Attribute`. The navigation properties are resolved on read by the `AuditManager` and are `null` once the referenced row is gone.

Indexes serve the five ways the log is actually read: chronologically, filtered by category, filtered by origin, as the trail of one record `(TargetType, TargetId, Sequence)`, and as the events of one activity.

## Software Architecture

The `AuditManager` owns the log. It is the only writer, and `ModelHub.AddAuditEvent` is its only write path — the sequence number, the predecessor hash and the event's own hash are all resolved *inside* the transaction rather than by the caller, because a caller that could choose its own position could insert into the past and one that could choose its own hash could seal a lie.

```
┌────────────────┐  events   ┌──────────────┐   Record()   ┌───────────────────────┐
│ 30-odd domain  ├──────────►│ AuditManager ├─────────────►│ ModelHub.AddAuditEvent│
│ managers       │           │              │              │  · assigns Sequence   │
└────────────────┘           │  Connect()   │              │  · reads head hash    │
┌────────────────┐  commits  │  BeginActiv. │              │  · seals via AuditSeal│
│ CommitManager  ├──────────►│  Record      │              │  · one transaction    │
└────────────────┘           │  RecordChange│              └───────────┬───────────┘
┌────────────────┐  sign-in  │  Project     │                          │
│ Session (REST) ├──────────►│  Verify      │◄─── reads ───────────────┤
└────────────────┘           │  Prune       │                          ▼
┌────────────────┐  startup  └──────┬───────┘              ┌───────────────────────┐
│ Application    ├──────────────────┘                      │ AuditEvent/AuditDelta │
└────────────────┘                                         └───────────────────────┘
                                    │
                                    ▼  (read-only)
                        Settings page · REST table · detail dialog
```

The interface exposes no way to modify or delete an event. That asymmetry is the design: a log that can be corrected is a log whose contents are an opinion.

## Surface

The user interface is a reader. `/kleenestar/settings/audit` lists the events newest-first through the REST table at `/kleenestar/api/1/audit/table`, with quickfilters for the security-relevant, the not-succeeded and the critical. A row opens `/kleenestar/settings/audit/detail?event={sequence|id}` in a dialog, which shows the composed sentence, the context identifiers, the deltas as a before/after table naming each kind and value type, and the two hashes — because the claim of tamper-evidence has to be visible to a reader for it to mean anything to them.

The list carries no create, update, delete or row reordering, unlike every other settings table in the application. That is not an omission to be filled in later.
