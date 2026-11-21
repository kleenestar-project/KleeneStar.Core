![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Draft and Publication Concept

A work item and a document are saved for different reasons. On an issue the save *is* the change: the assignee moves, the status advances, and everybody who looks at the issue should see that immediately — a half-finished edit of a status field is not a state anybody wants to protect. On a document the save is two separate things that a single button has to pretend are one: *do not lose what I have written* and *let the readers see this*. Conflating them costs something either way. A form that only saves on submit loses an afternoon to a closed tab. A form that saves continuously publishes every sentence in its unfinished state to everyone reading the page.

**KleeneStar** separates the two. The prose editor of the document and blog kinds writes every change into an unpublished **draft** within a second of the typing stopping, and the reading view keeps showing the last **published** text until the author says otherwise. The button that ends the editing session is therefore labelled *publish*, not *save*: by the time it is pressed the text has been safe for some time, and what the press decides is who else gets to see it.

Drafts bundle:
- One unpublished working copy per object, holding the two prose attributes — the title and the rich-text body.
- Continuous, debounced persistence from the editor, with the save state reported in the editor's footer.
- A publication step that copies the draft onto the object as a single commit and ends the draft.
- An abandonment that keeps the draft and leaves the published text untouched, so the next edit resumes rather than restarts.

## One Draft per Object, not per Author

The draft is a property of the object, not of the person editing it: `ObjectDraft` carries a unique index on `ObjectId`, and `UpdaterId` records who wrote last rather than who owns the row.

That is a consequence of the editor being a collaborative surface. Two authors who open the same document see each other's presence, cursors and text (see [the collaborative container](#the-collaborative-surface) below), which only makes sense if they are working on *one* text. A per-author draft would fork it silently: both would see the shared surface while writing into private copies, and the second publication would erase the first without anything having warned either of them.

The price is that concurrent edits resolve last-write-wins. That is the honest limit of the model, and it is stated rather than hidden: this is presence-level collaboration, not operational transformation.

## A Draft Is Not a Revision

The version history of an object is its [commit chain](kleenestar.commit.md), and a draft is deliberately not part of it. It has no commit, no revision number and nothing to replay; `CommitManager` never sees it. The history begins where publishing ends.

The reason is that a revision is a statement about what the object *was*, and a draft has never been anything — nobody has read it, nothing has referred to it, and restoring "to" it would mean restoring to a state that was never in effect. Recording one commit per autosave would also bury every real change under a chain of keystroke batches, and the same is true of the [audit log](kleenestar.audit.md): `ObjectDraftManager` is deliberately absent from `AuditManager.Connect()`, and the publication reaches the log through the ordinary bridge from `CommitManager.CommitAdded`, with the exact before/after of the published text.

What the history *does* show is that a draft exists. The commit list carries a leading entry — *Draft (unpublished)* with its author and its time — above the newest revision, without a primary action, because there is no state to load and offering one would suggest a restore that publication is for. It is what tells a second author that the document has unpublished changes before they start their own; a history that stopped at the last publication would omit exactly the part somebody is still writing.

## The Life of a Draft

| Moment                              | Draft row                        | Published object          | History
|-------------------------------------|----------------------------------|---------------------------|--------------------------
| Editor opens, nothing typed         | none                             | unchanged                 | unchanged
| First change                        | created                          | **unchanged**             | pending entry appears
| Every later change                  | overwritten                      | **unchanged**             | pending entry updates
| Editor abandoned (closed, cancelled)| **kept**                         | unchanged                 | pending entry remains
| Editor reopened                     | kept — it is what the form loads | unchanged                 | unchanged
| Publish                             | dropped                          | takes the draft's text    | one new commit
| Discard                             | dropped                          | unchanged                 | pending entry disappears

The row that carries the whole design is the fourth one. Abandoning the editor is neither a save nor a loss: the readers keep seeing what was published, and the author keeps what they wrote.

A draft column left `null` means *unchanged*, so a draft that touched only the body still opens the editor on the published title rather than blanking it.

## Where the Pieces Live

| Concern                                    | Where
|--------------------------------------------|--------------------------------------------------
| The unpublished copy                        | `ObjectDraft` (`KleeneStar.Model/Entities`)
| Its whole life cycle                        | `IObjectDraftManager` / `ObjectDraftManager`
| The draft endpoint — `GET`, `PUT`, `DELETE` | `/api/1/drafts/{objectkey}`
| The record endpoint — load and publish      | `/api/1/prose`
| The editor itself                           | `ObjectProseEditorFragment` / `…PageFragment`, both configuring the framework's `ControlDataModalEditor`
| What the draft would change                 | `/issue/{objectkey}/draft` + `ObjectDraftChangesFragment`

The split between the two endpoints is the split between the two meanings of save. The draft endpoint writes no commit and touches no object — an autosave every few seconds must not produce a revision every few seconds, and a reader must keep seeing the published text while somebody writes. The prose endpoint's `PUT` *is* the publication: it copies the text onto the object inside one commit and drops the draft. Its `GET` is what makes "editing resumes the draft" true without any client-side logic — it answers the draft's text when there is one, and the published text otherwise.

Publishing trusts the submitted payload over the stored draft, because the editor submits exactly what it is showing: the draft it loaded plus whatever was typed since the last autosave. Publishing what the author is looking at is the only reading that cannot surprise them. A publication that arrives before the first autosave therefore still lands.

## The Editor Is a Framework Control

The writing surface itself is **not** KleeneStar's. It is the framework's `ControlDataModalEditor`: a
fullscreen dialog whose title bar holds the document's name as an editable field, whose content
is the writing surface and nothing else, and whose footer bar reads *state · presence · ⋯ ·
publish · close*. The autosave, the save indicator, the discard, the resumed draft and the
shared surface all belong to it.

What KleeneStar contributes is only what the framework cannot know:

- which two endpoints carry the two meanings of save — `/api/1/prose` and
  `/api/1/drafts/{objectkey}`;
- which row is being edited, from the object key in the route;
- which channel two authors of the same object share (the object id, so they meet whether they
  arrived from the document tree or from a search result);
- one entry in the overflow menu beside the discard the control owns: *show changes*.

`ObjectProseEditorFragment` puts the dialog on the reading views, closed, where the headline's
edit button opens it by id; `ObjectProseEditorPageFragment` puts the same dialog on the
`/…/edit` routes with `Show`, so the editor stays linkable. Both derive from
`ObjectProseEditorFragmentBase`, which holds the configuration above.

Before the control existed this was assembled here — a recomposed form, an autosave script, a
status fragment and a menu fragment. All of that is gone; what stayed is the model, the two
endpoints and the comparison view.

## The Collaborative Surface

The editor turns the framework's collaborative container on, keyed by the object id, so everyone editing the same document shares one channel: presence chips name who is here — docked onto the dialog's footer bar rather than floating over the first line — remote pointers and carets show where they are, and text arrives as it is typed: the title per keystroke, the rich-text body as coalesced markup applied through the editor's own value API, so tables and add-ons keep their frames.

The same container wraps the field structure of the issue and asset detail views, where the inline smart-edits are the shared editing surface. There it is composed by hand (`ObjectCollaborativeHost`), because that surface is a page rather than a dialog.

A field the local user is currently typing in is never overwritten by an incoming message; the state converges when they leave it. Beyond that, convergence is the draft's job: one row per object, last write wins.

## Reading View

The published text is rendered by the framework's content control rather than emitted as raw markup. What the editor stores is its whole working surface — add-on frames, column resizers, the empty paragraphs a caret needs beside a block it must not type into — and printing that verbatim would show the reader the scaffolding. One stored value therefore serves both the author and the reader, instead of a second, hand-maintained representation.

Under the text stand the object's tags: on a document they read as what the piece is about, the way a post ends on its labels, rather than as one more property of a record. What the document links to and what is attached to it are *not* under the text — they are pages of their own, reached from the toolbar above it and from the actions menu, because they are questions a reader asks after reading rather than columns of the article.

## Related Concepts

- [Objects](kleenestar.object.md) — what a draft is a draft *of*.
- [Commits](kleenestar.commit.md) — the version history publication writes into.
- [Audit](kleenestar.audit.md) — where the publication is recorded installation-wide.
