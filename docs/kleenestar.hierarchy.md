# Object Hierarchy, Recursive Operations and Moving Across Workspaces

Objects form trees through `Object.ParentId`: a page below a page, a sub-issue below an issue. This
document describes how that tree is kept sound, how an object is operated on together with
everything below it, and how an object moves into another workspace.

> **History.** These parts were built as phases 0 and 3 and as the *Move Across Workspaces*
> extension of a container concept (folders as an object kind with an explorer, folder trees, folder
> settings and a workspace start in the explorer). The containers themselves were removed again on
> 2026-11-04 - they are not a fit for KleeneStar - together with everything that only served them
> (the `container` kind, the explorer pages and scripts, the folder rules, the folder settings and
> ranks, the workspace start target, the folder filter of the lists, the folder automation trigger
> and action). What follows is what stayed, because it holds for every object tree.

## The tree is kept sound

| Rule | Where | Notes |
|------|-------|-------|
| **No cycles, bounded depth** | `ObjectManager.Move` (behind both `SetParent` overloads), `MeasureDepth`, `MeasureHeight`; `WebManager/HierarchySettings` (`Plugins:kleenestar.core:Hierarchy`, bound into `CoreHub.HierarchySettings`) | The ancestry of the target is walked unrestricted (≤ `MaxDepth`, default 32, root = level 1); a move that would put any object of the moved subtree past the limit is refused. Rule violations are plain `InvalidOperationException`s. |
| **One move at a time** | `WebManager/HierarchyLock` | A process monitor per workspace, plus the cluster lock `kleenestar.core.hierarchy:{workspace}` only when clustered; re-entrant. `SetParent(objectId, parentId, expectedParentId)` refuses a stale move with `HierarchyConflictException`. |
| **Rights on all three records** | `WebRestApi/ContentAuthorization.MayMove`, `WebRestApi/ObjectMove` (shared by `/api/1/objects/move` and `PUT /api/1/hierarchy/{objectkey}`) | Write on the object, the new parent and the old parent (read unrestricted). Answers `403` / `400 { error }` / `409 { error }` / `404`; optional `expectedParent` in the body. `/api/1/objects` carries a `ParentId` in an update out as such a move, never as a field binding. |
| **No orphans** | `ObjectManager.Remove`, `/api/1/objects` `Delete` | An object with children is refused **before** the `Deleted` commit (`HierarchyConflictException`, `Count`; the endpoint answers a `RestApiRefusal` with `object.delete.refused.children`). |
| **Moves are events** | `IObjectManager.ObjectMoved` | Raised after a real move; a move stamps the acting identity as `UpdaterId`. |
| **Repair at start** | `WebMaintenance/HierarchyVerifier`, `ModelHub.GetObjectHierarchy` | Breaks persisted cycles at the member changed last; depth and cross-workspace violations are logged. |

The grants of `MayMove` were verified on a scratch host (the fixtures have no policy registry).

## The path index

`ObjectPath` (`Entities/ObjectHierarchy.cs`, `ModelHub.ObjectPath.cs`, `WebManager/ObjectPathIndex`)
stores per object its ancestor ids (`N` format, `/`-joined, led and closed by `/`) and its depth. The
subtree of X is one range query on `(Workspace, Path)`; a move rewrites the subtree in one
`ExecuteUpdate`. It is written by `ObjectManager.Add` and `Update` whenever `ParentId` changed - not
in the same transaction: a failed index write switches the index off instead of failing the object
write. `ObjectPathIndex.Rebuild` runs in `KleeneStarApplication.Run()` after the verifier and turns
the index on (`Ready`); readers use it only while it is ready (the test fixture clears it), otherwise
the level walks answer. `WebManager/ObjectHierarchy` is the shared structural read: an object
unrestricted (`Read`) and the ids of a subtree (`GetDescendantIds`, at most 50,000).

## Recursive operations

`IObjectOperationManager` / `ObjectOperationManager` run operations on an object and its subtree as
resumable runs (`ObjectOperation` + `ObjectOperationItem`):

- **archive** - the subtree's active objects, deepest first, recorded as items;
- **restore** - exactly the items of the root's last completed archive run, parents first
  (objects archived on their own stay archived; refused below an archived parent);
- **delete** - the subtree deepest first through `Remove`;
- **dissolve** - the direct children move to the parent, then the object is deleted;
- **clone** - `root` (the object alone; `container` is accepted as its former name) or `contents`
  (everything the caller sees below it), parents first, attributes and field values copied, new keys,
  `ObjectOperationItem.ResultId` maps the copies so a resumed run continues;
- **transfer** - see below.

Hidden or unchangeable members block archive, restore, delete, dissolve and transfer with their
numbers (`operation.refused.hidden` / `.permission`). `Hierarchy:RecursiveLimit` (10,000) refuses
larger subtrees; up to `Hierarchy:InlineLimit` (200) objects run in the request, larger runs wait for
`AutomationWorker` (`ProcessDue(500)` per tick; a stuck run is taken over after two minutes without
progress) and record one summary in the starter's notification center. Processing holds the
workspace's hierarchy lock, acts as the starting identity, reads unrestricted and silences
notifications; a failing object stops the run as `Failed`, what is done stays done.

**REST** `/api/1/operations/{workspacekey}`: `GET ?kind=&object=&mode=` answers the plan,
`POST { kind, object, mode?, key? }` starts a run (`200`/`202 { operation }`, `400 { message }`,
`403`, `404`), `GET ?operation=` the progress for the identity that started it.
`DELETE /api/1/objects?id=…&mode=recursive|dissolve` starts a delete or dissolve run and answers the
same way; without a mode an object with children is refused as before.

**WQL `within("KEY")`** / `not within(…)` is the WebIndex predicate `WebQuickfilter/WqlWithin`, a
`WqlIdPredicate<Object>`: the parser binds the call to the subtree ids of the key
(`ObjectHierarchy.GetDescendantIds`); the key is read through `ObjectManager`, so an unknown or
unreadable key binds the empty set and selects nothing; an argument that is no string is a parse
error. `KleeneStarApplication.Run()` registers it with the index manager
(`IndexManager.RegisterWqlPredicate`), so every WebApp REST endpoint over objects (search table,
lists, quickfilters) and the WQL prompt know it; `WqlFilter` parses through
`WqlWithin.CreateParser`, which knows it with or without a host.

**Breadcrumb.** The object key segment (`ObjectKeyUriPathSegmentVariable`) is an
`IUriPathSegmentTrail`: the breadcrumb of an object page shows the visible ancestry
(`ObjectHierarchy.GetVisibleAncestry`, root first, read through `ObjectManager`, bounded by
`Hierarchy:MaxDepth`) with key, class icon and detail page per crumb.

**Organize dialog.** *Organize objects* (`Issues/{workspacekey}/organize`, opened as
`modal-organize` from the overflow menu with `?select=<object id>`) is the framework's
`ControlDataTree` over `/api/1/organize/{workspacekey}`, a `RestApiTree<Object>`: one level per
expansion, node id = object id, label key and summary, class icon, no address. The roots are the
objects without a parent and those whose parent the caller may not read; the home page is no node.
A drop is `PUT { node, target, position }`: `child` puts the node under the target, `above`/`below`
under the target's visible parent; the move runs through `ObjectMove.Attempt` (authorization,
hierarchy rules, lock), and a refusal is a `RestApiRefusal` the tree takes back.

**Live updates.** `WebManager/ObjectChangeNotices` announces every `ObjectManager.Add/Update/Remove`
on the data change channel (domain of `Object`), so lists of other readers reload; bursts are folded
by `ObjectChangeNotices.Batch()` (runs and batch endpoints use it).

## Move Across Workspaces

Moving an object into another workspace is not a change of `ParentId`: the key, the class, the
fields, the workflow state and the permissions belong to the source workspace. It is a **transfer**,
run as an operation (`ObjectOperationKinds.Transfer`), with the object's subtree along.

| Aspect | Rule |
|--------|------|
| **Key** | A new key in the target workspace (`NextObjectKey`). The old key stays an **alias** (`ObjectKeyAlias`, `Key` unique, cascade with the object): `ObjectManager.GetObjectByKey` falls back to it, the issue, asset, document and blog pages redirect to the current key, and `NextObjectKey` never hands a former key out again. |
| **Class** | A class mapping source → target: same name by default, else chosen in the dialog; an unmapped class refuses the plan. |
| **Fields** | Values move by field name (case-insensitive) onto the target class; a field it lacks is listed as *dropped* (the commit records the removal). |
| **Workflow state** | By status name, else the first active status of the same category, else the target workflow's entry state - no transition guards, it is not a transition. |
| **Security level** | By name among the target class's levels, else refused unless the caller declassifies. |
| **Hierarchy** | The object lands at the **root** of the target workspace, its subtree below it; the depth limit holds. |
| **What ends** | Sprint membership, the milestone and pending approvals (all per workspace). Relations, comments, attachments, watchers and the history hang off the id and move along. |
| **Permissions** | Write on every object of the subtree and create permission (`ContentAuthorization.MayCreateIn`) for every mapped class; hidden members block. |

Per object (parents first) everything is **one commit** (`CommitType.Updated`) through
`ObjectManager.Update`, which the audit log picks up through the commit bridge; then the alias and the
path row. The run keeps its options on `ObjectOperation.TargetWorkspaceId` and `Options` (JSON: class
map, declassify), so a resumed run continues with exactly them; it takes the hierarchy lock of both
workspaces.

**REST** on `/api/1/operations/{workspacekey}`: `GET ?kind=transfer&object=…` lists the readable
target workspaces; with `targetWorkspace=…` (optional `map=source:target,…`, `declassify=true`) it
answers the plan - classes with their candidates, dropped fields, state mapping, levels.
`POST { kind: "transfer", object, targetWorkspace, classMap, declassify, key? }` starts it; a finished
run's `result` is the moved object's new address.

**Dialog.** *Move to another workspace…* in the overflow menu of the issue, asset, document and blog
pages and of the preview pane (`ObjectTransferMoreFragment`) opens the modal
`WWW/Issue/{objectkey}/Transfer`, a framework wizard (`ObjectTransferFormFragment`) built like the
classification dialog:

1. `workspace`: the target workspace as a searchable tile selection (active workspaces the caller
   may read, without the object's own).
2. `classes`: one tile selection per class of the subtree. The first tile keeps the class: its value
   is the source class itself, and a class mapped onto itself is kept by the manager (the objects
   move into the target workspace and stay objects of the source workspace's class, with every
   value, state and security level). The other tiles are the candidates of the target. Preset with
   the earlier answer, else the class of the same name, else the kept class.
3. `review`: the number of objects or the refusal, the security levels without counterpart in the
   chosen class (the move removes them; the wizard always declassifies), the class mapping, the
   changed states and the dropped fields.

Steps 2 and 3 are rendered by `POST /api/1/transfers/form?part=classes|review` from the
answers so far; the wizard reloads a step whenever the answers changed. A class mapping counts only
for the workspace the class step was rendered for (hidden `TransferContext`). The finish posts the
answers to `POST /api/1/transfers`, which starts the run with the dialog's idempotency key and
answers the page of the moved object (`{ data, uri }`, followed by the wizard), or
`400 { message }`. A run the worker processes answers the object's current page; its summary arrives
in the notification center. `WebRestApi/ObjectTransferForm` reads the answers for both endpoints.

Not built: an own commit type and an automation trigger for the transfer; re-matching a priority
against the target's priorities; ending SLA clocks explicitly (they follow the class, which changed).

## Model and migration

`ObjectPath`, `ObjectOperation`, `ObjectOperationItem`, `ObjectKeyAlias` - part of the
`InitialCreate` migration (the migrations were squashed on 2026-11-04).

## Tests

`UnitTestObjectManagerHierarchy`, `UnitTestHierarchyMaintenance` (the rules, the lock, the verifier),
`UnitTestObjectOperations` (path index, random moves, archive/restore, blocking members, delete,
dissolve, clone, `within`, change notices, background summary), `UnitTestObjectTransfer` (plan,
run, aliases, key numbering), `Load/UnitTestHierarchyLoad` (opt-in `KLEENESTAR_LOAD=1`),
`UnitTestProcessIntegrity` and `UnitTestRecovery` in the model.
