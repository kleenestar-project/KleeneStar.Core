![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Comment Management

Comment management in **KleeneStar** introduces an independent, lightweight discussion thread per object. A comment is a free-text note authored by an identity and attached to exactly one object; replies form a one-level deep thread under the comment they answer. The goal is a single, consistent collaboration surface that lives directly on the object detail page and that can be reused from every class in the system without any per-class configuration.

The `CommentManager` is responsible for the entire lifecycle of comment entries within an object. It ensures that:
- Comments are consistent, valid, and attributable (every comment has an author and a target object).
- Edits transparently bump the state from `Active` to `Edited` so the UI can render an "(edited)" marker.
- Deletions are soft by default so reply threads remain navigable and the parent FK is never orphaned.
- A reactive event surface (`CommentAdded`, `CommentUpdated`, `CommentRemoved`) is published so subscribers (UI, analytics, notifications) can react without depending on the manager.

The `CommentManager` is complementary to the `ObjectManager` and the `IdentityManager`. Objects provide the target. Identities provide the author. Comments add the conversation layer on top of both.

## Lifecycle and States

Comment management follows a default lifecycle with the states active, edited, deleted, and hidden. The transition from `Active` to `Edited` is automatic on the first content update; subsequent edits keep the state at `Edited`.

- **active:** The comment has just been posted. Its body is shown verbatim.
- **edited:** The comment's body has been updated by its author at least once. The UI renders an "(edited)" marker with the timestamp of the last edit.
- **deleted:** The comment has been soft-deleted by its author or a moderator. The row is preserved (`DeletedAt` is set, `Content` is cleared) so child replies still resolve to a parent; the UI renders a "[deleted]" placeholder.
- **hidden:** The comment has been hidden by a moderator pending review. The row is preserved; the body is suppressed for non-moderators.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Comment State Diagram                               ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                                                                                      ║
║         new   ╔════════╗       edit          ╔════════╗                              ║
║          ───► ║ active ├─────────────────────► edited ║                              ║
║               ╚═╤════╤═╝                     ╚════╤═══╝                              ║
║                 │    │                            │                                  ║
║           hide  │    │ delete              delete │                                  ║
║                 │    │                            │                                  ║
║                 │    │        ╔═════════╗         │                                  ║
║                 │    └────────► deleted ◄─────────┘                                  ║
║                 │             ╚═════════╝                                            ║
║                 │                                                                    ║
║                 │   ╔════════╗                                                       ║
║                 └───► hidden ║                                                       ║
║                     ╚════════╝                                                       ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

`Remove` (hard delete) exists for governance and tests but is not surfaced in the UI. Reply threads should always be torn down via soft delete to preserve the parent FK and the thread structure.

## Data Model and Relationships

A comment is uniquely bound to an object and to the identity that authored it. Replies are modelled by a self-referencing foreign key on the comment table; the relationship is intentionally restricted to one level deep in the UI (top-level comment plus replies) but the data model itself accepts arbitrarily nested chains.

- Key attributes: id (stable Guid), content (free text / markdown), state, created, updated, deletedAt.
- Required references: object (cascade delete; removing the object removes its comments), author (restrict delete; identities cannot be hard-deleted while they still own comments).
- Optional reference: parentComment (self-FK, restrict delete; reply threads are dismantled via soft delete).

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Comment Data Model                                  ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║       ┌───────────┐ 1                 * ┌────────┐ 1            * ┌─────────┐        ║
║       │ Workspace ├─────────────────────► Object ◄────────────────┤ Comment │        ║
║       └───────────┘                     └────▲───┘                └────┬────┘        ║
║                                              │ 1                       │ 0,1         ║
║                                              │                         │             ║
║                                              │ 1                       │ 1           ║
║                                          ┌───┴───┐               ┌─────▼─────┐       ║
║                                          │ Class │               │ Comment   │       ║
║                                          └───────┘               │ (parent)  │       ║
║                                                                  └───────────┘       ║
║                                                                                      ║
║       ┌──────────┐ 1                                 * ┌─────────┐                   ║
║       │ Identity ├─────────────────────────────────────► Comment │                   ║
║       └──────────┘                                     └─────────┘                   ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

A composite index on `(ObjectId, Created)` powers the per-object retrieval that backs the comment thread render path so the list can be returned in chronological order without a full scan.

## Software Architecture

The `CommentManager` mirrors the architecture of the other domain managers. It is a WebExpress component registered via reflection from `KleeneStar.Core.WebManager.CommentManager`. The public API is exposed via the `ICommentManager` interface and resolved by callers through `CoreHub.CommentManager`. Persistence is delegated to `ModelHub.Comment` (the partial class that holds the comment query, insert, update, and delete code), which in turn talks to the `KleeneStarDbContext`.

A reactive event surface is provided through the `CommentAdded`, `CommentUpdated`, and `CommentRemoved` events. The REST surface is delivered through the `WWW/Api/_1_/Comments/_objectkey_/` endpoint; the HTML surface is delivered through the `WebFragment/Object/ObjectCommentFragment` (list) and `WebFragment/Object/ObjectCommentComposerFragment` (composer), both of which host the existing `ControlRestComment` / `ControlRestCommentComposer` controls from `WebExpress.WebApp`.

```
╔KleeneStar.Core═══════════════════════════════════════════════════════════════════════╗
║                                                                                      ║
║                              ┌────────────────────┐                                  ║
║                              │ <<Interface>>      │                                  ║
║                              │ IComponentManager  │                                  ║
║                              ├────────────────────┤                                  ║
║                              └────────Δ───────────┘                                  ║
║                                       ¦                                              ║
║                     ┌─────────────────┴─────────────────────┐                        ║
║                     │ <<Interface>>                         │                        ║
║    ┌----------------┤ ICommentManager                       │                        ║
║    ¦                ├───────────────────────────────────────┤                        ║
║    ¦                │ CommentAdded:Event                    │                        ║
║    ¦                │ CommentUpdated:Event                  │                        ║
║    ¦                │ CommentRemoved:Event                  │                        ║
║    ¦              1 ├───────────────────────────────────────┤                        ║
║    ¦                │ GetComment(Guid):Comment              │                        ║
║    ¦                │ GetComments(Guid):                    │                        ║
║    ¦                │   IEnumerable<Comment>                │                        ║
║    ¦                │ GetComments(IQuery):                  │                        ║
║    ¦                │   IEnumerable<Comment>                │                        ║
║    ¦                │ Add(Comment):ICommentManager          │                        ║
║    ¦                │ Update(Comment):ICommentManager       │                        ║
║    ¦                │ SoftDelete(Guid):ICommentManager      │                        ║
║    ¦                │ Remove(Guid):ICommentManager          │                        ║
║    ¦                └────────────────Δ──────────────────────┘                        ║
║    ¦                                 ¦                                               ║
║    ¦ create         ┌────────────────┴──────────────────────┐                        ║
║    └----------------► Comment                               │                        ║
║                     ├───────────────────────────────────────┤                        ║
║                     │ Id:Guid                               │                        ║
║                     │ Content:String                        │                        ║
║                     │ State:CommentState                    │                        ║
║                     │ Created:DateTime                      │                        ║
║                     │ Updated:DateTime                      │                        ║
║                     │ DeletedAt:DateTime?                   │  ┌─────────────────┐   ║
║                     │ ObjectId:Guid                         │  │ <<Enum>>        │   ║
║                     │ Object:Object                         │  │ CommentState    │   ║
║                     │ AuthorId:Guid                         │  ├─────────────────┤   ║
║                     │ Author:Identity                       │  │ Active          │   ║
║                     │ ParentCommentId:Guid?                 │  │ Edited          │   ║
║                     │ ParentComment:Comment                 │  │ Deleted         │   ║
║                     │ Replies:IEnumerable<Comment>          │  │ Hidden          │   ║
║                     └───────────────────────────────────────┘  └─────────────────┘   ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Persistence

The `Comment` entity is configured via `CommentConfiguration` and lives in the `Comment` table. Three foreign keys are established:

|Foreign key       |Target              |Delete behaviour |Rationale
|------------------|--------------------|-----------------|------------------------------------------------------------
|`ObjectId`        |`Object.Id`         |Cascade          |Deleting an object removes its comment thread in one step.
|`AuthorId`        |`Identity.Id`       |Restrict         |Prevents hard-deleting an identity that still owns comments.
|`ParentCommentId` |`Comment.Id` (self) |Restrict         |Reply threads are dismantled via soft delete so the parent FK is preserved.

### Soft delete

Soft delete is the supported delete path for comments. The `SoftDelete(Guid)` flow sets `State = CommentState.Deleted`, populates `DeletedAt`, and clears `Content`. The REST GET path returns soft-deleted comments with an empty body so the UI renders a "[deleted]" placeholder while the surrounding thread structure stays intact. `Remove(Guid)` (hard delete) is reserved for governance, tests, and the rare case where a comment must disappear without trace.

## UI Concepts and Pages

Comments are not managed on a dedicated page. They live inside the object detail view: the comment thread renders in the secondary content section of `WWW.Object._objectkey_.Index`, with the composer pinned to the bottom of the same section. Both the thread and the composer are delivered by reusable WebExpress controls so the experience is identical for every class in the system.

### Object detail page — Comment thread and composer

The `ObjectCommentFragment` and `ObjectCommentComposerFragment` are registered for the object detail page via the `[Scope<WWW.Object._objectkey_.Index>]` attribute and rendered into `SectionContentSecondary`. The composer carries `[Order(99)]` so it always lands below the thread.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incidents / INC-1042 — VPN drop                                   │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Sidebar───────────────┐ ┌Object Content─────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - Overview          │░│ INC-1042 — VPN drop                            [Edit] […] │║
║│  - Comments          │░│ Status: In Progress    Priority: High    Assignee: alice  │║
║│  - History           │░│ ───────────────────────────────────────────────────────── │║
║│                      │░│ Description                                               │║
║│                      │░│   Users in the Berlin office report intermittent VPN      │║
║│                      │░│   disconnects since the firewall upgrade.                 │║
║│                      │░│                                                           │║
║│                      │░│ ┌Comments─────────────────────────────────────────────┐   │║
║│                      │░│ │ alice  · 2026-05-01 09:14                           │   │║
║│                      │░│ │   Triaging from the network team's side — please    │   │║
║│                      │░│ │   share an MTR to vpn1.kleenestar.org while it      │   │║
║│                      │░│ │   reproduces.                          [Reply] […]  │   │║
║│                      │░│ │     └ admin · 2026-05-01 09:32                      │   │║
║│                      │░│ │         MTR attached. Workaround: switch to vpn2.   │   │║
║│                      │░│ │ support · 2026-05-01 11:08  (edited)                │   │║
║│                      │░│ │   Customer success notified — they're routing       │   │║
║│                      │░│ │   travellers to the workaround SSID.   [Reply] […]  │   │║
║│                      │░│ │ ─────────────────────────────────────────────────── │   │║
║│                      │░│ │ ┌Composer────────────────────────────────────────┐  │   │║
║│                      │░│ │ │ Add a comment …                                │  │   │║
║│                      │░│ │ │                                       [Post]   │  │   │║
║│                      │░│ │ └────────────────────────────────────────────────┘  │   │║
║│                      │░│ └─────────────────────────────────────────────────────┘   │║
║├──────────────────────┤░│                                                           │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Behaviour

- `ControlRestComment` issues a `GET` on the bound `RestUri` and renders the comment list, including the nested reply collection and the `(edited)` / `[deleted]` markers driven by `CommentState`.
- Each comment row exposes `Edit`, `Delete`, and `Reply` row actions. `Edit` and `Delete` are gated to the comment's author. `Edit` PUTs the new body, which bumps `State` from `Active` to `Edited` server-side. `Delete` calls the soft-delete path.
- `Reply` POSTs to the parent comment id and renders the new entry inline under its parent.
- `ControlRestCommentComposer` POSTs new top-level comments. Its placeholder is sourced from the `kleenestar.core:comment.composer.placeholder` translation key.

### Reaction / pin / like

*Resolved* — the three were once left out of the model, and are now part of it. A comment carries `IsPinned` (a pinned comment sorts before everything else in the thread and survives the ordering by date), a `Likes` collection (`CommentLike`, one row per identity) and a `Reactions` collection (`CommentReaction`, one row per identity and emoji). `ICommentManager.TogglePin`, `ToggleLike` and `ToggleReaction` flip them and answer the new state — the pin flag, the names of everybody who likes the comment, the full emoji → names map — which is what the control redraws from. The REST endpoint implements the framework's three toggle overrides on top of them. Who liked or reacted is read off the session, never off the name the browser sends along; an anonymous caller can do neither, nor pin.

A comment also carries a `Visibility` (`Public` or `InternalTeam`), sent as the composer's category and shown as a label on the entry; a reply inherits the visibility of its parent. And every add, edit, delete and pin raises a notification through `CoreHub.AddNotification`, so the change reaches the notification center and the toast the way every other change does.

## Sitemap Comment Management

Comments are rendered as a section inside the object detail page rather than as a separate sitemap branch. The only addressable resource is the REST endpoint itself.

|Path                                                       |Page                         |Description
|-----------------------------------------------------------|-----------------------------|---------------------------------------------------------------
|`/workspaces/{workspaceKey}/objects/{objectKey}`           |Object detail                |Hosts the comment thread and composer in the secondary section.
|`/api/1/comments/{objectKey}`                              |Comment REST endpoint        |GET/POST/PUT/DELETE for the comment thread of a single object.

## API Interfaces (REST Endpoints) — Comment Management

For programmatic interaction, the comment thread of a single object is exposed via a versioned REST endpoint rooted at `/api/1/comments/{objectKey}`. The endpoint is the back-end for both `ControlRestComment` (list) and `ControlRestCommentComposer` (create). It follows REST conventions, uses JSON, and is protected by the standard **KleeneStar** authentication and authorization stack.

|Endpoint                                          |HTTP Method |Description
|--------------------------------------------------|------------|------------------------------------------------------------
|`/api/1/comments/{objectKey}`                     |GET         |Returns every comment attached to the object, oldest first. Top-level comments are returned with their `Replies` nested in the DTO. Soft-deleted comments are returned with an empty body so the UI can render the placeholder while the thread structure stays intact.
|`/api/1/comments/{objectKey}`                     |POST        |Creates a new top-level comment on the addressed object. Requires a non-empty `Body`. Returns the created comment mapped to the REST DTO.
|`/api/1/comments/{objectKey}/{commentId}`         |PUT         |Updates the body of an existing comment. The server automatically sets `State = Edited` and refreshes `Updated`.
|`/api/1/comments/{objectKey}/{commentId}`         |DELETE      |Soft-deletes a comment via `CommentManager.SoftDelete` — sets `State = Deleted`, populates `DeletedAt`, clears `Content`. The row is kept so replies still resolve.
|`/api/1/comments/{objectKey}/{commentId}/reply`   |POST        |Appends a reply to the parent comment identified by `commentId`. Returns the created reply mapped to the REST DTO.
|`/api/1/comments/{objectKey}/{commentId}/likes`   |POST        |Toggles the like of the signed-in identity on the comment. Returns the names of everybody who currently likes it.
|`/api/1/comments/{objectKey}/{commentId}/pin`     |POST        |Toggles the pin of the comment. Returns the new pin state; an anonymous caller is refused.
|`/api/1/comments/{objectKey}/{commentId}/reactions`|POST        |Toggles an emoji reaction of the signed-in identity on the comment. Returns the full reaction map (emoji → names).

The endpoint maps the persisted `Comment` entity to the `RestApiCommentItem` / `RestApiCommentReply` DTOs from `WebExpress.WebApp`:

|`Comment` field          |`RestApiCommentItem` field |Notes
|-------------------------|---------------------------|------------------------------------------------------------
|`Id`                     |`Id`                       |Guid serialised as string.
|`Author.Name`            |`Author`                   |Resolved via FK include; empty string when no author is set.
|`Content`                |`Body`                     |Empty when `State == Deleted`.
|`Created`                |`When`                     |ISO-8601 round-trip string in UTC.
|`State`                  |`Category`                 |The state name (`Active`, `Edited`, `Deleted`, `Hidden`).
|`Updated` + author       |`Edited`                   |Populated only when `State == Edited`.
|`Replies`                |`Replies`                  |Nested reply DTOs; only populated for top-level items.

Standard HTTP status codes apply: `200`/`201`/`204` for success, `400` for validation errors (empty body, unknown comment id), `401` for unauthenticated, `403` for forbidden, `404` for unknown object key.

## Comment Events

The `CommentManager` publishes the following events via the **WebExpress** `EventManager`. UI components, notification subsystems, and analytics pipelines can subscribe to react to changes without depending on the manager directly:

|Event Name          |Description
|--------------------|----------------------------------------------------------------
|`CommentAdded`      |Triggered when a new comment (top-level or reply) has been persisted.
|`CommentUpdated`    |Signals an edit, a soft-delete, or any other change to an existing comment.
|`CommentRemoved`    |Indicates the hard removal of a comment from the data store.

Each event payload carries the affected `Comment` entity, allowing subscribers to invalidate caches, push live notifications to the object's watchers, or update the UI without re-querying.

## Comment Management Permission Model

Comments inherit the access rules of the object they are attached to. A user who can read an object can read its comment thread; a user who can post to an object can author comments on it. There is no per-comment permission table — the permission model is intentionally narrow so the comment feature stays a thin collaboration layer on top of the object permissions:

|Permission                  |Description
|----------------------------|-----------------------------------------------------------------------------------
|`comment_read`              |Read the comment thread of an object. Implied by `object_read`.
|`comment_create`            |Create a new comment or reply on an object. Implied by `object_update`.
|`comment_edit_own`          |Edit the body of a comment the user authored.
|`comment_delete_own`        |Soft-delete a comment the user authored.
|`comment_moderate`          |Edit, hide, or soft-delete any comment irrespective of authorship.
|`comment_remove`            |Hard-delete a comment. Reserved for governance; not surfaced in the UI by default.

Assignment is performed via the standard group-policy mechanism. Most deployments only need to grant `comment_moderate` to a dedicated moderator group and rely on the implied per-object grants for everyone else.

## Conclusion

This document describes the comment concept in **KleeneStar** as a thin, reusable conversation layer that lives directly on the object detail page. The reference implementation comprises the `Comment` entity, the `CommentManager` component, a single REST endpoint at `/api/1/comments/{objectkey}`, two object-scoped WebFragments hosting the existing `ControlRestComment` / `ControlRestCommentComposer` controls, and a seeded set of per-object class-flavoured threads that exercise both the top-level and the reply rendering paths. The model started deliberately narrow, and the additions it was left open for have since landed without revisiting the storage schema: pins, likes and emoji reactions (`IsPinned`, `CommentLike`, `CommentReaction`), a public / internal-team visibility per comment, and notifications on every change. Per-comment permissions beyond that visibility, attachments on a comment and @-mentions remain open.
