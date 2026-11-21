![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Object Versioning Concept

Object versioning in **KleeneStar** extends object management with a fully traceable, commit-based history. Every change to an object (creation, field updates, workflow transitions, archiving, restoration, or deletion) produces a commit. A commit records exclusively the fields changed by that action, together with timestamp, initiating user, and a change type. The current state of an object is held in its `Value` instances, which always reflect the head of the commit chain and are updated atomically with each commit. Historical states are derived by replaying the chain: starting from the creation commit, each subsequent commit applies its changed fields on top of the previous state, so the complete field set is inspectable at any commit without storing redundant snapshots.

Versioning adds two entities to the core data model: `Commit`, the atomic unit of change belonging to an object, and `Change`, a single field modification within a commit. The `Value` entity remains the read-optimized current state, while the commit chain is the authoritative history from which every state can be reconstructed. Versioning is implemented fully server-side, is multi-tenant capable, and is enforced by the `CommitManager` so that no change path can bypass history recording.

## Commit Model

A commit is the atomic unit of change for a single object. Each commit belongs to exactly one object and contains an ordered set of changes. Commits form an append-only, chronological chain per object; the first commit (genesis commit) captures all initial field values, while subsequent commits capture only the delta.

Only changed fields are stored within a commit. A change consists of the field reference, the previous value, and the new value. Fields that were not touched by the action do not appear in the commit. When an object is mutated, the `ObjectManager` instructs the `CommitManager` to append the commit, and the affected `Value` instances are updated within the same transaction, guaranteeing that the current state and the head of the chain never diverge. The deletion of an object is represented by a terminal commit of type `Deleted`; its prior history remains preserved for audit purposes.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                          KleeneStar Object Commit Chain                              ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║   ┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐       ║
║   │  Commit #1   │    │  Commit #2   │    │  Commit #3   │    │  Commit #4   │       ║
║   │  (genesis)   ├───►│  (updated)   ├───►│(transitioned)├───►│  (archived)  │       ║
║   ├──────────────┤    ├──────────────┤    ├──────────────┤    ├──────────────┤       ║
║   │ Summary: "…" │    │ Priority:    │    │ State:       │    │ (no field    │       ║
║   │ Priority: M  │    │  M → High    │    │  Open→InProg.│    │  changes)    │       ║
║   │ State: Open  │    │              │    │              │    │              │       ║
║   │ Assignee: -  │    │              │    │              │    │              │       ║
║   └──────────────┘    └──────────────┘    └──────────────┘    └──────────────┘       ║
║                                                                                      ║
║   values (current state) = head = merge(#1…#4)                                       ║
║   state at commit #2 = merge(#1,#2)                                                  ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Data Model

The versioning data model attaches to the existing core data model. The object retains its `Value` instances, which store the current field values and are kept consistent with the head of the commit chain. Each `Commit` references the object it belongs to and aggregates the `Change` entries produced by that action. Every `Change` points to the `Field` definition it modifies, which keeps the history schema-aware and enables localized display names and type-specific formatting in the UI.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                             KleeneStar Core Data Model                               ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                         ┌────────────────┬────────────────────────────────┐          ║
║                         │ *              │ *                              │          ║
║                   ┌─────▼────┐     ┌─────▼────┐      ┌──────┐ 1           │          ║
║                   │ Workflow │     │ Priority │      │ Form ├───┐         │          ║
║                   └─────┬────┘     └─────┬────┘      └───┬──┘   │         │          ║
║                         │ *              │ *             │ *    │         │          ║
║                         └────────────────┼───────────────┘      │         │          ║
║                                          │                      │         │          ║
║                                          │ 1                    │ *       │          ║
║          ┌───────────┐ *           * ┌───▼───┐ 1          * ┌───▼───┐ 0,1 │          ║
║          │ Workspace ├───────────────► Class ◄──────────────┤ Field ├─────┘          ║
║          └─────┬─────┘               └───▲───┘              └─▲───▲─┘                ║
║                │ 1                       │ 1                  │ 1 │ 1                ║
║                └────────────────────┐    │              ┌─────┘   └──────┐           ║
║                                     │ *  │ *            │ *              │ *         ║
║    ┌─────────┐    ┌──────┐ *    2 ┌─▼────┴─┐ 1    * ┌───┴───┐        ┌───┴────┐      ║
║    │ Insight │    │ Link ├────────► Object ◄────────┤ Value │        │ Change │      ║
║    └────┬────┘    └──────┘        └─▲──▲──▲┘        └───────┘        └───┬────┘      ║
║         │ 1                       1 │  │ 1│ 1                            │ *         ║
║         │              ┌────────────┘  │  └──────────────┐    ┌──────────┘           ║
║         │ *            │ *             │ *               │ *  │ 1                    ║
║  ┌──────▼──────┐  ┌────┴────┐    ┌─────┴─────────┐     ┌─┴────▼─┐                    ║
║  │ InsightView │  │ Comment │    │ FileReference │     │ Commit │                    ║
║  └─────────────┘  └─────────┘    └───────────────┘     └────────┘                    ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The `Commit` entity carries a unique identifier and a reference to its predecessor commit, which together form a stable, human‑readable revision reference (for example `INC-00123#4`) in addition to the commit’s own id. The `CommitType` enumeration distinguishes `Created`, `Updated`, `Transitioned`, `Archived`, `Restored`, and `Deleted`. An optional `Message` allows the initiating user to describe the intent of the change, analogous to a commit message in source control. The genesis commit (with no predecessor) contains a `Change` entry for every populated field, ensuring that the chain is self‑contained and replayable from the start. The `Change` entity references the `Field` and stores the previous and new value of exactly one field modification.

## Software Architecture

The versioning architecture follows the modular, decoupled principle of the **KleeneStar** platform. At its center is the `CommitManager`, which is exclusively responsible for the lifecycle and access to all commits. It manages commit chains per object and provides a controlled interface for all versioning interactions. New commits are created exclusively via the `CommitManager` to ensure data integrity and consistent access rules; no mutation of an object can bypass it.

The `ObjectManager` and the `CommitManager` are separate components with distinct responsibilities. The `ObjectManager` handles the business operations on objects (creation, updates, transitions, deletion). It delegates the history recording to the `CommitManager`, which appends the commit and applies the new values to the object's `Value` instances within the same transaction. This separation ensures that the versioning logic is isolated, testable, and reusable, while the transactional boundary guarantees that the current state and the head of the chain never diverge.

For a loosely coupled, reactive architecture, the `CommitManager` provides the events `CommitAdded`, `CommitRestored`, and `CommitDiffed`. Other components can subscribe to these and react to changes without being directly dependent on the manager. This event system fosters modularity and high cohesion.

The `CommitManager` handles server-side tasks such as the persistent storage of all commits and changes in a transactional, append-only store. At system startup, stored commits are loaded, commit chains are validated for integrity, and event subscriptions are initialized. To support fast temporal queries, the `CommitManager` may maintain materialized projections of object states at selected commits, which are always subordinate to and derivable from the authoritative commit chain.

On every request, the `CommitManager` enforces authorization for the calling module or user. Access is governed by policies that may include context-dependent filters, ensuring that historical data is only visible to users holding the appropriate permissions.

An integrated audit system documents all relevant actions around commits: accesses, reconstructions, restorations, and permission checks are logged with timestamp, user identity, object key, and action type. This data supports analysis, troubleshooting, compliance verification, and state restoration.

```
╔KleeneStar.Core═══════════════════════════════════════════════════════════════════════╗
║                                                                                      ║
║                  ┌───────────────────┐                                               ║
║                  │ <<Interface>>     │                                               ║
║                  │ IComponentManager │                                               ║
║                  ├───────────────────┤                                               ║
║                  └────────Δ──────────┘                                               ║
║                           ¦                                                          ║
║                           ¦                                                          ║
║           ┌───────────────┴────────────────┐                                         ║
║           │ <<Interface>>                  │                                         ║
║  ┌--------┤ ICommitManager                 │                                         ║
║  ¦        ├────────────────────────────────┤                                         ║
║  ¦        │ CommitAdded:Event              │                                         ║
║  ¦        │ CommitRestored:Event           │                                         ║
║  ¦        │ CommitDiffed:Event             │                                         ║
║  ¦        ├────────────────────────────────┤ 1                                       ║
║  ¦        │ Commits:IEnumerable<ICommit>   ├────────┐                                ║
║  ¦        ├────────────────────────────────┤        │                                ║
║  ¦        │ AddCommit(IObject,IChange[]):  │        │             ┌──────────────┐   ║
║  ¦        │   ICommit                      │        │             │ <<Enum>>     │   ║
║  ¦        │ GetHistory(IObject):           │        │             │ CommitType   │   ║
║  ¦        │   IEnumerable<ICommit>         │        │             ├──────────────┤   ║
║  ¦        │ GetCommit(IObject,number):     │        │             │ Created      │   ║
║  ¦        │   ICommit                      │        │             │ Updated      │   ║
║  ¦        │ GetStateAt(IObject,number):    │        │             │ Transitioned │   ║
║  ¦        │   IObjectState                 │        │             │ Archived     │   ║
║  ¦        │ DiffCommits(IObject,from,to):  │        │             │ Restored     │   ║
║  ¦        │   IEnumerable<IChange>         │        │             │ Deleted      │   ║
║  ¦        │ RestoreCommit(IObject,number): │        │             └──────────────┘   ║
║  ¦        │   ICommit                      │        │                                ║
║  ¦        └────────────────────────────────┘        │                                ║
║  ¦                                                  │                                ║
║  ¦                              ┌───────────────┐   │                                ║
║  ¦                              │ <<Interface>> │   │                                ║
║  ¦                              │ IModel        │   │                                ║
║  ¦                              ├───────────────┤   │                                ║
║  ¦                              └──────Δ────────┘   │                                ║
║  ¦                                     ¦            │                                ║
║  ¦                                     ¦            │ *                              ║
║  ¦                                 ┌────────────────▼────┐                           ║
║  ¦                                 │ <<Interface>>       │                           ║
║  ¦                                 │ ICommit             │                           ║
║  ¦                                 ├─────────────────────┤                           ║
║  ¦                                 │ Id:Guid             │                           ║
║  ¦                                 │ Object:IObject      │                           ║
║  ¦                                 │ ParentId:Guid       │                           ║
║  ¦                                 │ Type:CommitType     │                           ║
║  ¦                                 │ CreatedBy:IUser     │                           ║
║  ¦                                 │ Created:DateTime    │                           ║
║  ¦                                 │ Message:String      │ 1                         ║
║  ¦                                 │ Changes:IEnumerable ├────────┐                  ║
║  ¦                                 │   <IChange>         │        │                  ║
║  ¦                                 └───Δ─────────────────┘        │                  ║
║  ¦                                     ¦                          │ *                ║
║  ¦                                     ¦             ┌────────────▼────┐             ║
║  ¦                                     ¦             │ <<Interface>>   │             ║
║  ¦                                     ¦             │ IChange         │             ║
║  ¦                                     ¦             ├─────────────────┤             ║
║  ¦                                     ¦             │ Id:Guid         │             ║
║  ¦                                     ¦             │ Field:IField    │             ║
║  ¦                                     ¦             │ OldValue:Object │             ║
║  ¦                                     ¦             │ NewValue:Object │             ║
║  ¦                                     ¦             └─────────────────┘             ║
║  ¦                                     ¦                                             ║
║  ¦                                     ¦                                             ║
║  ¦ create                  ┌───────────┴──────────┐                                  ║
║  └-------------------------► Commit               │                                  ║
║                            ├──────────────────────┤                                  ║
║                            │ (implements ICommit) │                                  ║
║                            └──────────────────────┘                                  ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The `ICommit` interface derives from `IModel` and defines the contract for a single commit: a unique identifier, the referenced object, the identifier of its predecessor commit, the commit type, the author, the timestamp, the optional message, and the ordered set of changes. The concrete `Commit` class implements `ICommit`. `IChange` describes a single field modification with its field reference, old value, and new value. The `Commits` collection of the `ICommitManager` exposes all managed commits, while the query methods provide object‑scoped access.

## UI Concepts and Pages

The user interface integrates the version history into the existing object detail view via its actions menu and presents it in a dedicated modal. The presentation follows the established **KleeneStar** design patterns.

### Object Management - History Access (Actions Menu)

The version history of an object is accessed via the actions menu ("…") in the header of the object detail view. The menu contains the entry "History", positioned alongside the other object actions such as "Move", "Export", and "Permissions". Selecting the entry opens the history modal without leaving the object context. The entry is visible only to users holding the `object_read_history` permission.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / INC-00123                                                         │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workspace─────────────┐ ┌Object Content────────────────────────────┬────────────────┐║
║│[Name]                │░│ INC-00123: VPN connection disrupted  […] │   Status: Open▼│║
║│                      │░│                        ┌──────────────┴┐ │ Priority: High │║
║│      [Icon]          │░│ Description: Users cann│ Edit          │ │ Assignee: Max  │║
║│           [ Search ] │░│ Affected CI: vpn-gatewa│ Clone         │ │           Power│║
║│ Issue                │░│ ...                    │ Add Link      │ │  Created: 2025 │║
║│ ├─ Incident          │░│                        │ Add Subobject │ │           -01  │║
║│ ├─ Problem           │░│ Attachments:           │ Show as...    │ │           -16  │║
║│ └─ ServiceRequest    │░│   - Screenshot.png     │ Move          │ │                │║
║│                      │░│                        │ Export        │ │  Watchers:  [+]│║
║│                      │<│                        │ History       │ │   - Erika Mus x│║
║│                      │<│ Comments:              │ Permissions   │ │                │║
║│                      │<│   - Max Power (2025-01-│ <section>     │ │  Link:      [+]│║
║│                      │░│       Can you please ch├───────────────┤ │  - INC-00321  x│║
║│                      │░│       status?          │ Delete        │ │                │║
║│                      │░│                        └───────────────┘ │                │║
║│                      │░│   - Erika Mustermann (2025-01-16 09:15)  │                │║
║│                      │░│       I have checked the logs, no        │                │║
║│                      │░│       errors found.                      │                │║
║├──────────────────────┤░│                                          │                │║
║│ [+] | [Settings]  << │░│ [ Add new comment...                   ] │                │║
║└──────────────────────┘ └──────────────────────────────────────────┴────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - History (Modal)

The history modal presents the complete version history of an object in a master-detail layout. The left column lists all commits in reverse chronological order, each with its commit number, change type, author, and timestamp. A search field filters the list, and pagination keeps long histories manageable.

Selecting a commit displays its details in the right column: the commit reference, author, timestamp, change type, and the optional commit message. Below, the "Changed fields" table lists only the fields modified by that commit as before/after pairs. Since a commit stores only changed fields, the section "All fields at this commit" replays the history and renders the complete field set of the object at exactly this commit. This makes the object fully inspectable for every commit while the stored delta remains minimal.

The "Restore" button at the bottom of the modal creates a new commit that reapplies the field values of the selected commit, preserving the append-only nature of the history. The button is enabled only when a commit older than the head is selected and the user holds the `object_restore_state` permission; for the head commit it is disabled, since restoring the current state would have no effect. After a successful restore, the commit list refreshes and shows the newly appended commit of type `Restored`. The "Close" button dismisses the modal without changes.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔ObjectHistoryModal══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ History: INC-00123                                          [Search] │║     │║
║└─────║├───────────────────┬──────────────────────────────────────────────────┤║─────┘║
║┌Works║│                   │ INC-00123#3 - transitioned                       │║─────┐║
║│[Name║│  #4 updated       │ Erika Mustermann, 2025-01-17 09:15               │║     │║
║│     ║│      Max Power    │ "Taking over for analysis"                       │║ […] │║
║│     ║│      2025-01-18   │                                                  │║     │║
║│     ║│ ▾#3 transitioned  │ ┌Changed fields────────────────────────────────┐ │║-----│║
║│ Issu║│      Erika Must.  │ │ Field  | Old value     | New value           │ │║ […] │║
║│ ├─ I║│      2025-01-17   │ │--------|---------------|---------------------│ │║ […] │║
║│ ├─ P║│  #2 updated       │ │ State  | Open          | In Progress         │ │║ […] │║
║│ └─ S║│  #1 created       │ └──────────────────────────────────────────────┘ │║ […] │║
║│     ║│                   │ ┌All fields at this commit─────────────────────┐ │║ […] │║
║│     ║│                   │ │ Summary:  VPN connection disrupted           │ │║ […] │║
║│     ║│                   │ │ State:    In Progress                        │ │║ […] │║
║│     ║│                   │ │ Priority: Medium                             │ │║ […] │║
║│     ║│                   │ │ Assignee: Max Power                          │ │║ […] │║
║│     ║│                   │ │ Reported: 2025-01-16 08:30                   │ │║ […] │║
║│     ║│                   │ └──────────────────────────────────────────────┘ │║     │║
║│     ║└───────────────────┴──────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│ [+] ║                                              [Restore] [Close]         ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Sitemap

The history routes extend the object sitemap. Since the history is presented in a modal, the routes serve as deep links that open the object detail view with the history modal already displayed. Deep links to individual commits remain stable and preselect the referenced commit within the modal.

|Path                                            |Page/View         |Description
|------------------------------------------------|------------------|---------------------------------------------
|`/issue/{objectKey}/history`                    |Object history    |The history dialog listing all commits of an object. Opened from the actions menu as a modal; reachable directly as a full page.
|`/issue/{objectKey}/historydetail?commit={n}`   |Commit detail     |One commit with its changed fields and the complete field set replayed at that commit. Loaded into the detail side of the dialog, and addressable on its own.
|`/issue/{objectKey}/historyrestore?commit={n}`  |State restoration |Reapplies the historical values as a new commit and returns to the object.

The object detail views are keyed by object key under the route of their kind (`/issue/…`,
`/asset/…`, …), so the history routes hang below the same branch rather than below a separate
`/objects/` one. They are not restricted to the issue kind: like the sibling `permission` route
they address an object by key alone, and the actions menu of every kind opens them.

## API Interfaces (REST Endpoints)

The versioning endpoints extend the object REST API. They are read-oriented; commits are created implicitly by the mutating object endpoints, which return the created commit reference in their response.

|Endpoint                                       |HTTP Method |Description
|-----------------------------------------------|------------|-----------------------------------------------
|`/api/1/history/{objectKey}`                   |GET         |Lists all commits of an object, newest first. Paged with `?start=` and `?count=`.
|`/api/1/history/{objectKey}/{commit}`          |GET         |Returns a single commit including its changed fields.
|`/api/1/history/{objectKey}/{commit}/state`    |GET         |Returns the complete replayed field state at the given commit.
|`/api/1/history/{objectKey}/{from}/{to}/diff`  |GET         |Returns the aggregated field difference between two commits.
|`/api/1/history/{objectKey}/{commit}/restore`  |POST        |Creates a new commit that reapplies the historical field values.

A `{commit}` is addressed either by its revision number (`4`) or by its commit id, so a deep link
into the dialog and a numbered reference reach the same resource.

The endpoints are mounted below `/api/1/history/{objectKey}` rather than below
`/api/1/objects/{objectKey}/`: the `objects` branch already owns a variable segment
(`/api/1/objects/{workspaceKey}/…`), and a second variable sibling beside it cannot be routed
unambiguously. It is also where every other per-object resource of this platform already lives —
`/api/1/comments/{objectKey}`, `/api/1/hierarchy/{objectKey}`, `/api/1/assignee/{objectKey}`.

Standard error responses follow the object API conventions: `400 Bad Request` for an invalid commit reference, `401 Unauthorized` for missing authentication, `403 Forbidden` for insufficient permissions, `404 Not Found` when the object or commit does not exist, and `409 Conflict` when a restore addresses the head, whose state is already the current one.

## Events

Versioning events are published by the `CommitManager` via the **WebExpress** `EventManager`. These events allow other modules, plugins, or external systems to subscribe to relevant changes without being directly coupled to the `CommitManager`.

|Event name        |Description
|------------------|------------------------------------------------------------------------
|`CommitAdded`     |Signals that a new commit has been appended to an object's history. The payload contains the object key, the commit number, the change type, the changed field keys, the timestamp, and the initiating user or module.
|`CommitRestored`  |Indicates that a historical state was restored as a new commit. The payload contains the object key, the new commit number, the restored commit number, the timestamp, and the initiating user or module.
|`CommitDiffed`    |Triggered when a diff between two commits is computed. The payload contains the object key, the commit numbers, the differing field keys, and the requesting user or module.

## Permissions Model

Access to the version history is derived from the object permissions. Reading the history requires `object_read`; the changed field values within a commit are subject to field-level read permissions (`field_read_values`), so fields hidden from the user are masked in the history as well. Restoring a historical state requires `object_update` and write permission on all affected fields. History entries are immutable for all roles; there is no permission that allows modifying or deleting commits.

|Permission            |Description
|----------------------|-------------------------------------------------------------------
|`object_read_history` |Grants read access to an object's commit history and controls the visibility of the "History" entry in the actions menu. Included in `object_view_policy`, `object_edit_policy`, and `object_admin_policy`.
|`object_restore_state`|Authorizes restoring a historical state as a new commit via the "Restore" button in the history modal. Included in `object_edit_policy` and `object_admin_policy`.

## Conclusion

Object versioning in **KleeneStar** establishes an append-only, commit-based history alongside the current-state `Value` store. Commits store only the changed fields, which keeps the history compact, while the `Value` instances serve the current state with predictable read performance. The `CommitManager` is the exclusive owner of the commit chain, ensuring that all mutations are recorded atomically and no change bypasses the history. Both the `Value` store and the commit chain are written within the same transaction, so the head of the chain and the current values can never diverge. Replaying the chain reconstructs the complete field state at any commit. In the user interface, the history is reachable through the actions menu of the object detail view and is presented in a modal with an integrated restore function. The concept integrates with the existing UI, REST API, event system, and permissions model, and satisfies the audit and traceability requirements of the platform. Implementation details such as value serialization per field type, retention of deleted object histories, and storage optimization for long chains are left to the reference implementation.
