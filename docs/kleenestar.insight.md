![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Insight Management Concept

Insights are the user-defined views on the data of a **KleeneStar** installation. An insight is a set of objects, chosen by a WQL **query** across workspaces (`Insight.Query`), and shown through **tabs** the way a workspace overview (for example `/issues/SD`) shows its issues. A dashboard is one possible tab, not what an insight is. Insights replaced the former *dashboards* on 2026-10-04 and became tab hosts on 2026-10-05; every dashboard that existed carries on as an insight with one dashboard tab, with its id, name, description, categories, state, permissions, columns and widgets.

An insight consists of tabs (`InsightView`) that are added from a template picker, dragged into order, renamed, coloured and closed. Every tab shows the same objects - those the query selects, narrowed to what the reader may see - through a different surface, so the same set serves both operational work (moving cards, planning sprints, dragging dates) and analysis (reports, key figures).

The tab types the core ships:

- Objects - table and list (toggle) with search, quickfilters and paging; the default tab
- Dashboard - columns of freely arranged widgets such as key figures, charts, lists and notes
- Kanban - a board in columns by workflow state; dropping a card is a workflow transition
- Scrum - the sprint board and the backlog of the workspaces the objects live in
- Gantt - a timeline from the classes' date fields; dragging a bar writes the dates
- Calendar - a month, week or agenda grid; moving an entry writes the dates
- Reports - charts computed from the history of the objects' workflow field

The set of tab types is extensible. There is deliberately no enum of them: `WebManager/InsightViewTypeManager` is an open registry beside the kind, renderer and authentication source managers, and a plugin adds a tab type by shipping a public sealed `IInsightViewType` with a public parameterless constructor (found without registration) and declaring a tab template scoped to `InsightTabFragment`. Nothing already written is edited.

Insights are not embedded in a workspace. They exist on their own in the installation, so a view can aggregate objects across workspaces - restricted, as every read is, to what the reader may see. An insight grants no rights on what it shows: it only selects and presents objects the reader could open anyway, and changing an object through an insight needs the same right it needs anywhere else.

## Lifecycle of the Insight

Insights know two states, **active** and **deleted** (`InsightState`), chosen in the add, edit and clone dialogs (a create without a state is active) and shown in the state column of the overview. **Only an active insight is in use** (`InsightScope.IsInUse`). One set to *deleted* is taken out of use without being removed: the header menu and the sidebar of the insight page leave it out, its page redirects to the overview, and its tabs, boards, reports and exports answer as if it did not exist (`InsightScope.Resolve` answers `null`). It stays in the overview, where it can be edited, cloned, set active again or deleted for good. Taking an insight out of use needs `insight_archive`, putting it back `insight_restore` - each beside `insight_update`, refused on the state field of the edit dialog.

An active insight is configured and usable. Changes are made in place: the metadata through the edit dialog, the tabs and the board of a dashboard tab live on the insight page, where every change is saved as it is made (autosave). There are no versions of an insight; the audit log records each change with its before and after values.

Deleting an insight - from either state - removes it permanently and immediately: the row, its tabs (cascade), its category links and the configuration of all its boards (dashboard columns and widgets, Kanban boards) are deleted, and there is no restore. The objects it showed are not touched - they belong to their workspaces. The audit log keeps the record of the deletion.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Insight State Diagram                               ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                                 new  ╔════════╗                                      ║
║                                   ───► active ║                                      ║
║                                      ╚═╤════▲═╝                                      ║
║                                        │    │                                        ║
║                     set deleted        │    │  set active                            ║
║                     (insight_archive)  │    │  (insight_restore)                     ║
║                                     ╔══▼════╧═╗                                      ║
║                                     ║ deleted ║                                      ║
║                                     ╚════╤════╝                                      ║
║                                          │  delete (insight_delete; from either)     ║
║                                          ▼                                           ║
║                                       removed                                        ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Data Model

The **KleeneStar** Core data model forms the structural foundation of the insights. An insight is not modelled as classes and objects of its own; it is a configuration entity beside the workspaces that **selects** objects by its query rather than owning them. There is no foreign key from an insight to the objects it shows - the query is evaluated whenever a tab is read.

|Entity            |Table              |Purpose
|------------------|-------------------|--------------------------------------------------------------
|`Insight`         |`Insight`          |Name (unique, at most 64 characters), **Query**, icon, description, state, timestamps; `Type` (legacy, see below).
|`InsightView`     |`InsightView`      |A tab: name (unique per insight, at most 64 characters), view type key, configuration, colour (`#rrggbb`), order, state; cascades with the insight.
|`InsightCategory` |`InsightCategory`  |Many-to-many link between insights and categories (`Category`, shared with the workspaces).
|`DashboardColumn` |`DashboardColumn`  |A column of one dashboard tab; references the insight (`Insight`) and the tab (`View`).
|`Widget`          |`Widget`           |A widget in a column (type, name, colour, parameters, position, WQL).
|`KanbanBoard`     |`KanbanBoard`      |A tab's board configuration (Kanban and Scrum): `Workspace` = insight id, `Kind` = `insight`, `View` = tab id.

**Each board tab owns its configuration.** Dashboard columns carry `ViewId`, while `KindDashboard` and `KanbanBoard` use the owner, kind and `ViewId` as their unique scope, so two dashboard, Kanban or Scrum tabs of one insight have their own columns, widgets, lanes and filters. A board written before tabs existed is retained under the empty tab id (the *legacy* board) and is what a request without a tab addresses. `Insight.Type` is the type an insight was created as before it hosted tabs; it is kept, not dropped (dropping a column rebuilds the table in SQLite), and nothing reads it since.

The migrations behind this: `20261004214616_RenameDashboardToInsight` (since squashed into `InitialCreate`) converted the dashboards in place - tables renamed, grants `dashboard_*` → `insight_*`, scope `dashboard` → `insight`. `20261004222550_AddInsightViews` adds `Insight.Query` and the `InsightView` table and gives every existing insight one dashboard tab, using only `ADD COLUMN`, `CREATE TABLE` and an `INSERT`. `AddViewColor` adds the tab colour. `ScopeBoardsToTabs` gives every existing dashboard, Kanban and Scrum tab an independent copy of the retained legacy board with fresh column, widget and lane identifiers, preserving parameters, WQL, status assignments, ordering, colours and filters; rolling it back discards the copies. The audit log was not rewritten: `AuditTargetType.Insight` kept its number but its token is now `insight`, so events sealed about a dashboard before the rename no longer verify - accepted. Old addresses (`/dashboards`, `/dashboard/{id}`) are not redirected.

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

## Software Architecture

At the center of insight management is the `InsightManager`, reached as `CoreHub.InsightManager` through its interface `IInsightManager`. It owns the insights and their tabs and is the only component that writes them; the data itself is the `Insight` entity of `KleeneStar.Model`, persisted through `ModelHub` in the provider-agnostic `KleeneStarDbContext`. There is no separate `IInsight` interface: endpoints and fragments work with the entity and change it only through the manager's methods.

The manager reads with `GetInsight` and `GetInsights` and writes with `Add`, `Update` and `Remove`. The board of a dashboard tab is changed with `SetColumns` (column-only change, widgets kept) and `SetBoard` (full board change, widgets rebuilt), both addressed by the tab id. The tabs are handled by `GetViews`, `GetView`, `AddView` (appended; a taken name is numbered), `RemoveView`, `RenameView` (a name another tab carries is refused, not numbered), `SetViewColor`, `ReorderViews`, `AddDefaultViews` (a new insight starts with *Objects* and *Reports*) and `CopyViews` (a clone takes its original's tabs and a copy of each tab's board).

For loosely coupled communication the manager raises `InsightAdded`, `InsightUpdated` and `InsightRemoved`. The audit log subscribes to them centrally in `AuditManager.Connect()`; other components can subscribe the same way without a dependency on the manager's internals.

Which objects an insight shows is answered in one place, `WebInsight/InsightScope`: `Resolve` (the insight of the route), `MayRead` (`insight_read_content`), `MayArrange` (`insight_write_content`), `Predicate`/`Apply`/`Query`/`GetObjects` (the objects) and `Contains`. **A blank query selects everything the reader may see** (stored as `null`). **A query that does not compile is refused when it is written** (`/api/1/insights` `Validate`, with the parser's reason on the `Query` field); one that stops compiling later - an attribute renamed - selects **nothing** rather than everything, because an insight must not silently widen. The objects are read through `ObjectManager`, so security levels and the content visibility of the workspaces narrow them exactly as they narrow every other list.

The tab endpoints reuse the overview endpoints of the workspaces. Their REST bases take the set of objects from an overridable hook, and the insight endpoints under `WWW/Api/_1_/Insights/_insightid_/` override only that: `RestApiObjectKindTable` (`RetrieveScope`, `BuildCatalog` - columns from any set of classes - and `LayoutScope`), `RestApiObjectKindList` (`Scope`), `RestApiObjectKindKanban` (`ResolveScope` → `KanbanBoardScope`, `MayArrange`), `RestApiObjectKindGantt` and `RestApiObjectKindSchedule` (`ResolveScope`, `InScope`). The workspace endpoints keep the defaults. The board tabs address their own configuration through `v=<tab id>`, which the tab binding writes into the board's data service; `BoardViewScope` validates the tab's owner, kind, type and state and refuses an invalid explicit `v`.

The **reports** are computed from the history of the workflow field, replayed from the commit chain (`ModelHub.GetFieldChanges`, one query for all workflow fields of the classes involved; `WebInsight/Reports/InsightReportSource`). A recorded value resolves to its **status category** (to do, in progress, waiting, done) through the statuses of the object's class, so the reports span classes whose workflows name their statuses differently. **Resolved means entering the done category**; reopening and resolving again counts again. The calculations (`InsightReports`, `InsightSlaReport`) are pure functions over hand-buildable items, checked by `UnitTestInsightReports`.

|Component                       |Role
|--------------------------------|------------------------------------------------------------------
|`InsightManager`                |Lifecycle of insights and tabs, board persistence, events.
|`InsightViewTypeManager`        |Open registry of tab types; maps a stored tab to its template and back (`TemplateId`, `FromTemplateId`).
|`IInsightViewType`              |Descriptor of one tab type (`Key`, `Label`, `Description`, `Icon`, `Order`, `Template`); `CoreInsightViewTypes.cs` holds the core's seven, `Model.Entities.InsightViewTypes` their keys.
|`InsightScope`                  |The insight of a route, its read/arrange rights and its objects.
|`InsightQuickfilterBase`        |Base of the two quickfilter endpoints of an insight.
|`InsightReports`, `InsightReportSource`, `InsightSlaReport`|The reports and the history they are computed from.
|`ContentVisibility.Restrict(IQuery<Insight>)`|Narrows every insight list to what the caller may read.
|`ContentAuthorization.MayUseInsight`|Gate of the CRUD, permission and tab endpoints.
|`ContentAuthorization.MayCreateInsight`, `MayChangeInsightState`|Creation (installation-wide resource) and archive/restore on a change of state.

```
╔KleeneStar.Core═══════════════════════════════════════════════════════════════════════╗
║                                                                                      ║
║                              ┌────────────────────┐                                  ║
║                              │ <<Interface>>      │                                  ║
║                              │ IComponentManager  │                                  ║
║                              ├────────────────────┤                                  ║
║                              └────────Δ───────────┘                                  ║
║                                       ¦                                              ║
║                                       ¦                                              ║
║                     ┌─────────────────┴─────────────────────┐                        ║
║                     │ <<Interface>>                         │                        ║
║         ┌-----------┤ IInsightManager                       │                        ║
║         ¦           ├───────────────────────────────────────┤                        ║
║         ¦           │ InsightAdded:Event                    │                        ║
║         ¦           │ InsightUpdated:Event                  │                        ║
║         ¦           │ InsightRemoved:Event                  │                        ║
║         ¦           ├───────────────────────────────────────┤                        ║
║         ¦           │ GetInsight(Guid):Insight              │                        ║
║         ¦           │ GetInsights(IQuery):                  │                        ║
║         ¦           │   IEnumerable<Insight>                │                        ║
║         ¦           │ Add(Insight):IInsightManager          │                        ║
║         ¦           │ Update(Insight):IInsightManager       │                        ║
║         ¦           │ Remove(Guid):IInsightManager          │                        ║
║         ¦           │ SetColumns(id, columns, viewId)       │                        ║
║         ¦           │ SetBoard(id, columns, viewId)         │                        ║
║         ¦           │ GetViews(Guid):                       │                        ║
║         ¦           │   IReadOnlyList<InsightView>          │                        ║
║         ¦           │ AddView(InsightView)                  │                        ║
║         ¦           │ RemoveView, RenameView,               │                        ║
║         ¦           │   SetViewColor, ReorderViews          │                        ║
║         ¦           │ AddDefaultViews, CopyViews            │                        ║
║         ¦           └───────────────────────────────────────┘                        ║
║         ¦                                                                            ║
║         ¦                     ┌───────────────┐                                      ║
║         ¦                     │ <<Interface>> │                                      ║
║         ¦                     │ IEntity       │                                      ║
║         ¦                     ├───────────────┤                                      ║
║         ¦                     └───────Δ───────┘                                      ║
║         ¦                             ¦                                              ║
║         ¦ manage    ┌─────────────────┴──────────────────┐ 1                         ║
║         └-----------► Insight                            ├─────────────┐             ║
║                     ├────────────────────────────────────┤             │ *           ║
║                     │ Id:Guid                            │     ┌───────▼───────┐     ║
║                     │ Name:String                        │     │ InsightView   │     ║
║                     │ Query:String                       │     ├───────────────┤     ║
║                     │ Icon:ImageIcon                     │     │ Id:Guid       │     ║
║                     │ Categories:List<Category>          │     │ Name:String   │     ║
║                     │ Description:String                 │     │ ViewType:     │     ║
║                     │ State:InsightState                 │     │   String      │     ║
║                     │ Created:DateTime                   │     │ Color:String  │     ║
║                     │ Updated:DateTime                   │     │ Order:Int     │     ║
║                     │ Columns:List<DashboardColumn>      │     │ State         │     ║
║                     │ Type:String (legacy)               │     └───────┬───────┘     ║
║                     └────────────────────────────────────┘             ¦ ViewType    ║
║                                                                ┌───────▼───────┐     ║
║                                                                │ <<Interface>> │     ║
║                                                                │ IInsight-     │     ║
║                                                                │   ViewType    │     ║
║                                                                ├───────────────┤     ║
║                                                                │ Key:String    │     ║
║                                                                │ Template:Type │     ║
║                                                                └───────────────┘     ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

Responsible for all insights in the system is the `InsightManager`. It implements `IComponentManager` and is registered and constructed by the component hub of WebExpress like every other manager of the **KleeneStar**.Core module; it is resolved lazily through `CoreHub` and registered in the test fixture `CoreHubFixture`. As the central administration unit it handles the lifecycle of insights from creation and cloning to retrieval and deletion.

`Add`, `Update` and `Remove` raise a notification (`notification.insight.created|updated|deleted`), delivered as a toast to the acting session and recorded in the notification center. The live editing on the insight page - the board autosave and every tab change - raises `InsightUpdated` without a notification, so arranging an insight does not flood the reader with toasts.

Insights themselves are the `Insight` entity: id, name, query, icon, categories, description, state and timestamps. Their content is the collection of tabs (`InsightView`), each naming a tab type by its key. What a tab type draws is the business of its template - a `FragmentControlDataTabTemplate` with `[Scope<InsightTabFragment>]`, deriving from `InsightTabTemplateFragmentBase` for the catalog's icon and texts - plus content fragments scoped to that template. **What is stored is not gated:** a tab keeps the key of a type whose plugin is gone; the tab endpoint leaves it out, and it returns as soon as the type is registered again. The default type (`objects`) cannot be unregistered.

Access to insights is controlled through grants on the insight itself (scope `PermissionScope.Insight`, administered in the insight's permission dialog), evaluated by `IPermissionManager` like every other resource. Policies bundle the individual permissions and are assigned to groups; see the permissions model below.

Unlike other system components, insights are not organizationally embedded in workspaces but exist as independent entities in the system. Their query may therefore select objects of any workspace. They do not lift any boundary, though: every object an insight shows is read through `ObjectManager` with the reader's grants and clearance, and the Scrum tab, which writes into sprints that belong to a workspace, refuses cross-workspace planning.

## UI Concepts and Pages

The insight pages follow the established design principles of the **KleeneStar** WebApp: a header menu for quick access, an overview page with a table, modal dialogs for creating, editing, cloning, deleting and the permissions, and a page per insight that hosts its tabs. All of them are for signed-in callers only.

The overview and the dialogs manage the insight as a record; the insight page is where it is used and arranged. A reader without `insight_write_content` sees the tab control read-only - no adding, dragging, renaming, recolouring or closing of tabs.

### Global Insight Dropdown (Header)

The insight dropdown (`InsightDropdownFragment`, label *Insights*, pie-chart icon) sits in the primary navigation of the header beside the workspace dropdown. It opens a searchable list of the active insights the caller may read, each leading to its insight page, followed by *Add Insight* (opens the create dialog; offered only to a caller who may create insights, `ContentAuthorization.MayCreateInsight`) and *Insights* (the overview page). The entries come from `/api/1/insights/dropdown`, narrowed by `ContentVisibility.Restrict`; an insight set to deleted is found in the overview only. The dropdown is left out entirely for a caller who is not signed in (`ContentVisibility.MayUseInsights`).

The following schematic illustrates the position and function of the dropdown in the overall layout of the web application:

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insights ▼        [+ AddObject]         [Search]    │║
║└─────────────────────────────────¦──────────────────────────────────────────────────┘║
║┌Breadcrumb─────────────────────┌─┴────────────────┐─────────────────────────────────┐║
║│ / Service Desk                │┌────────────────┐│                                 │║
║└───────────────────────────────││ Search         ││─────────────────────────────────┘║
║┌Workspace ────────────┐ ┌──────│└────────────────┘│─────────────────────────────────┐║
║│[Name]                │░│      │ Insight 0        │                                 │║
║│                      │░│ Incid│ Insight 1        │                             […] │║
║│      [Icon]          │░│      │ ...              │                        [Search] │║
║│                      │░│ Summa│ Insight n        │        | Status  | Impact     + │║
║│                      │░│------├──────────────────┤--------|---------|--------------│║
║│ Issue                │░│ VPN c│ + Add Insight    │        | Open    | High     […] │║
║│ ├─ Incident          │░│ Outlo│ Insights         │        | Open    | Medium   […] │║
║│ ├─ Problem           │░│ Print└──────────────────┘e       | Assigne | Medium   […] │║
║│ └─ ServiceRequest    │░│ File share not accessible        | In Prog.| Low      […] │║
║│                      │░│ Remote desktop not reachable     | Open    | Medium   […] │║
║│                      │<│                                                           │║
║│                      │<│                                   ‹ Prev  1  2  3  Next › │║
║│                      │<│                                                           │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║├──────────────────────┤░│                                                           │║
║│ [+] | [Setting]   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Insight Management (Page)

The overview page `/insights` (`WWW/Insights/Index`) is the central point for managing the insights. In contrast to the header dropdown it shows the whole inventory the caller may read - insights set to deleted included - with search, the quickfilter *Active* and paging, and it offers the management actions per row.

The heart of the page is a table (`/api/1/insights/table`) with the columns name, query, description and state (translated), and the column chooser stores the reader's layout. A click on a row opens the insight. The row menu offers *Edit*, *Clone*, *Permissions* and, after a separator, *Delete* - each opening its modal dialog. The headline carries *Add Insight* for a caller who may create insights and, for the installation's administrators, *Permissions*, the dialog that says which groups may create insights (`WWW/Insights/Permission`, `InsightCreationPermissionFragment`, see the permissions model). Above the table stands a short description of what an insight is, and the sidebar carries the *All Insights* entry that resets the filter. The page declares the insight domain, so the table refreshes when an insight is added, changed or removed.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insights ▼        [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Insights                                                                         │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Sidebar───────────────┐ ┌Insights───────────────────── [Permissions] [+ Add Insight]┐║
║│                      │░│                                                           │║
║│ - All Insights       │░│ This overview lists all available insights. ...           │║
║│                      │░│                                       [Search] [Active]   │║
║│                      │░│ Name               | Query              | Desc… | State   │║
║│                      │░│--------------------|--------------------|-----------------│║
║│                      │░│ Operations Overvi… | Workspace.Key = "… | Dash… | Active  │║
║│                      │░│ Engineering Insig… | Workspace.Key = "… | Dash… | Active  │║
║│                      │░│ Finance Summary    | Workspace.Key = "… | Dash… | Deleted │║
║│                      │░│                                                        ¦  │║
║│                      │░│                                   ‹ Prev  ┌────────────┴┐ │║
║│                      │<│                                           │ Setting     │ │║
║│                      │<│                                           │ Edit        │ │║
║│                      │<│                                           │ Clone       │ │║
║│                      │░│                                           │ Permissions │ │║
║│                      │░│                                           ├─────────────┤ │║
║│                      │░│                                           │ Delete      │ │║
║│                      │░│                                           └─────────────┘ │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║├──────────────────────┤░│                                                           │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Insight Management - Display (Page)

The insight page `/insight/{insightid}` (`WWW/Insight/_insightid_/Index`) is where an insight is used. It is reached from the dropdown, from a row of the overview or directly by its address; the page title and headline are the insight's name. An insight set to deleted (or an id that names none) is not opened: the page redirects to the overview. The sidebar (`InsightSidebarFilterFragment`) lists the active insights the caller may read, by name, as links to their pages with the open one marked - the same narrowing as the header menu.

The content is one tab control, `InsightTabFragment` - a `FragmentControlDataTab` over `/api/1/insights/{insightid}/tab`. Every tab type contributes a tab template scoped to it, exactly like the issue overview's `IssueTabFragment` and its templates. A new tab is added from the template picker (`+`); the tabs are dragged into order (persisted), and the "…" menu of a tab renames it, colours it or closes it. A rename to a name another tab of the insight carries is refused rather than suffixed, the label holds at most 64 characters. For a caller who may not arrange the insight the control is read-only.

What the tabs show:

- **Objects** - the objects table (column layout stored per reader, insight and tab) and list, with search (text or WQL), quickfilters and paging. Quickfilters defined on an insight are stored under the view key `insight` with the insight id as context.
- **Dashboard** - the tab's board of columns and widgets, saved as it is arranged.
- **Kanban** - the tab's board; a drop is a workflow transition and is refused, with the reason, where the workflow or the caller's rights forbid it.
- **Scrum** - sprints belong to a workspace, so the backlog lists the sprints of every workspace the objects live in (named `KEY · Sprint` when there are several), refuses a move into a sprint of another workspace, and creates a sprint only when the objects live in exactly one workspace. Every change needs `workspace_write_content` on the workspace it changes. The sprint board shows the objects committed to the running sprint of their workspace.
- **Gantt** and **Calendar** - the objects on a timeline or a calendar grid; dragging writes the dates where the caller may change the object.
- **Reports** - cumulative flow, velocity, average age, created vs. resolved, resolution time and service level compliance, drawn with the framework's `ChartCtrl` (`InsightReportControl` and `Assets/js/insightreports.js`, included by `IncludeInsightReportsScript`). Range, interval and report are remembered in the browser per insight, and the tab links the current report and the objects as CSV.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insights ▼        [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Operations Overview                                                              │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Sidebar───────────────┐ ┌───────────────────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│ Operations Overview  │░│ Operations Overview                                       │║
║│ Engineering Insights │░│                                                           │║
║│ Finance Summary      │░│ [Dashboard…] [Objects…] [Kanban…] [Calendar…] [Reports…] +│║
║│                      │░│ ┌───────────────────────────────────────────────────────┐ │║
║│                      │░│ │ Report: [Cumulative flow ▼] Range: [90 days ▼]        │ │║
║│                      │░│ │ Interval: [Week ▼]                  [Report as CSV]   │ │║
║│                      │░│ │                                                       │ │║
║│                      │░│ │  60 ┤                              ▓▓▓▓▓▓▓▓▓▓▓▓▓▓     │ │║
║│                      │░│ │  45 ┤                ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒     │ │║
║│                      │░│ │  30 ┤   ▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░░░    │ │║
║│                      │░│ │  15 ┤   ▒▒▒▒▒▒▒▒▒▒▒▒▒░░░░░░░░░░░░░░░░░░░░░░░░░░░░     │ │║
║│                      │<│ │   0 ┼───────────────────────────────────────────      │ │║
║│                      │<│ │      Aug        Sep        Oct        Nov             │ │║
║│                      │<│ │                                                       │ │║
║│                      │░│ │ ░ Done   ▒ In progress   ▓ To do                      │ │║
║│                      │░│ └───────────────────────────────────────────────────────┘ │║
║│                      │░│                                                           │║
║├──────────────────────┤░│                                                           │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Insight Management - New/Edit (Modal)

A modal dialog is used to create or edit an insight, so the task is done without leaving the page it was started from. The create dialog (`WWW/Insights/Add`, `InsightAddFormFragment`) is opened by *Add Insight* in the header dropdown or in the headline of the overview - both offered only to a caller who may create insights - the edit dialog (`WWW/Insight/{insightid}/Edit`, `InsightEditFormFragment`) by *Edit* in the row menu of the overview.

Both carry the same fields: the **name** (required, unique, checked while typing against `/api/1/insights/uniquename`), the **query** in the WQL prompt of the search page (`InsightFormItems`, with completion from `/api/1/objects/wql` and a help text with an example), the **categories** as tags, the **description** in the prose editor and the **state**. A query the parser rejects is refused on save with the parser's reason under the field. A created insight starts with the tabs *Objects* and *Reports*.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insights ▼        [+ AddObject]         [Search]    │║
║└─────╔InsightAddEditModal═════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / In║│ Add Insight / Edit Insight                                           │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Side ║│                                                                      │║─────┐║
║│     ║│   Insight Name*: [ Operations Overview                             ] │║     │║
║│ - Al║│           Query: [ Workspace.Key = "SD"                            ] │║ […] │║
║│     ║│                  The WQL query that selects the objects of the       │║rch] │║
║│     ║│                  insight, across workspaces - for example ...        │║     │║
║│     ║│        Category: [ Operations x ] [ Support x ]                      │║---- │║
║│     ║│     Description: ┌───────────────────────────────────────────────┐   │║ […] │║
║│     ║│                  │ Dashboard for monitoring key operational ...  │   │║ […] │║
║│     ║│                  └───────────────────────────────────────────────┘   │║ […] │║
║│     ║│           State: [ Active ▼ ]                                        │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                              [Create/Update] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Insight Management - Clone (Modal)

Cloning an insight uses a modal dialog of its own (`WWW/Insight/{insightid}/Clone`, `InsightCloneFormFragment`), opened by *Clone* in the row menu of the overview. It is the quick way to reuse a proven arrangement of tabs and boards for another set of objects.

The dialog carries the same fields as the edit dialog, pre-filled from the original: the name with the suffix " (Copy)", the query, the categories, the description and the state. The new insight receives a new id and new timestamps, a copy of each of the original's tabs (name, type, configuration, colour) and of each tab's board - dashboard columns and widgets, Kanban and Scrum configuration - under new identifiers; should the original have no tabs, it gets the default ones. The grants of the original are **not** copied: a clone starts without grants of its own.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insights ▼        [+ AddObject]         [Search]    │║
║└─────╔InsightCloneModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / In║│ Clone Insight                                                        │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Side ║│                                                                      │║─────┐║
║│     ║│   Insight Name*: [ Operations Overview (Copy)                      ] │║     │║
║│ - Al║│           Query: [ Workspace.Key = "SD"                            ] │║ […] │║
║│     ║│        Category: [ Operations x ] [ Support x ]                      │║rch] │║
║│     ║│     Description: ┌───────────────────────────────────────────────┐   │║     │║
║│     ║│                  │ Dashboard for monitoring key operational ...  │   │║---- │║
║│     ║│                  └───────────────────────────────────────────────┘   │║ […] │║
║│     ║│           State: [ Active ▼ ]                                        │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                      [Clone] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Insight Management - Delete (Modal)

Deleting an insight is permanent and requires deliberate confirmation. It is handled by a small modal dialog (`WWW/Insight/{insightid}/Delete`, `InsightDeleteFormFragment`, the framework's `ControlDataFormDelete`), opened by *Delete* in the row menu of the overview.

The dialog asks whether the item should really be deleted and names the insight. As an additional safeguard the endpoint hands the insight's name to the dialog as the confirmation item (`RetrieveForDelete`), and the *Delete* button is enabled only once exactly that name has been typed. *Cancel* closes the dialog without a change. On deletion the insight, its tabs and all their board configurations are removed (see the lifecycle above).

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insights ▼        [+ AddObject]         [Search]    │║
║└─────╔InsightDeleteModal══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / In║│ Delete Insight                                                       │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Side ║│                                                                      │║─────┐║
║│     ║│ Are you sure you want to delete this item?                           │║     │║
║│ - Al║│                                                                      │║ […] │║
║│     ║│ To confirm, type 'Operations Overview' in the box below*:            │║rch] │║
║│     ║│ [                                                                  ] │║     │║
║│     ║│                                                                      │║---- │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                     [Delete] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Insight Management - Permissions Management (Modal)

The access rights to an insight are managed in a modal dialog (`WWW/Insight/{insightid}/Permission`, `InsightPermissionFragment`, the framework's `FragmentControlDataPermission`), opened by *Permissions* in the row menu of the overview. It assigns groups to the insight policies, and those assignments are valid for this one insight only.

Opening the dialog and every change need `insight_manage_profiles` on the insight (the page guard and the endpoint `/api/1/insight/{insightid}/permission` ask the same question). The dialog offers every group (`…/permissiongroups`) and the policies of the insight scope (`…/permissionpolicies`); an assignment is added with *+ Assign* and removed in the table of assignments, and *Done* closes the dialog.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insights ▼        [+ AddObject]         [Search]    │║
║└─────╔InsightPermissionsModal═════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / In║│  Insight Permissions                                                 │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Side ║│                                                                      │║─────┐║
║│     ║│  Assign Group*: [ Admin ▼]                                           │║     │║
║│ - Al║│        Policy*: [ insight_admin_policy ▼]                            │║ […] │║
║│     ║│                                                                      │║rch] │║
║│     ║│  [+ Assign]                                                          │║     │║
║│     ║│                                                             [Search] │║---- │║
║│     ║│                                                                      │║ […] │║
║│     ║│ Assigned Group       | Effective Policy                              │║ […] │║
║│     ║│----------------------|-----------------------------------------------│║ […] │║
║│     ║│ Admin                | insight_admin_policy                        X │║     │║
║│     ║│ Signed-in users      | insight_view_policy                         X │║xt › │║
║│     ║│                                                                      │║     │║
║│     ║│                                             ‹ Prev  1  2  3  Next ›  │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                                [Done]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Sitemap

The sitemap defines the pages of insight management and their addresses. The insights are addressed by their **id** (`{insightid}`, a guid), not by a key; the overview lives under the plural, a single insight under the singular. Every page is guarded centrally by `RouteAuthorization` (areas `insights` and `insight`): a signed-in caller, and the permission given in the table - for a page naming an insight on that insight's grants, for the create dialog on the installation-wide creation resource.

|Path                                 |Page                   |Description
|-------------------------------------|-----------------------|------------------------------------------------------------------------
|`/insight`                           |Insight                |Hidden segment; redirects to the start page.
|`/insights`                          |Insight Overview       |Table of all readable insights with search, quickfilter, paging and the row menu.
|`/insights/add`                      |Insight Creation       |Modal form for creating a new insight (`insight_create`).
|`/insights/permission`               |Creation Permissions   |Modal for the groups that may create insights (administrators only).
|`/insight/{insightid}`               |Insight Detail View    |The insight's tabs (`insight_read`); an insight not in use redirects to the overview.
|`/insight/{insightid}/edit`          |Insight Editing        |Modal form for editing the metadata of an insight (`insight_update`).
|`/insight/{insightid}/clone`         |Insight Cloning        |Modal form for cloning an insight with adjustable fields (`insight_clone`).
|`/insight/{insightid}/delete`        |Insight Deletion       |Modal for confirming and performing the permanent deletion (`insight_delete`).
|`/insight/{insightid}/permission`    |Insight Permissions    |Modal for managing the group-policy assignments of an insight (`insight_manage_profiles`).

The start page's *Choose start page* leads to the insight overview.

## API Interfaces (REST Endpoints)

For programmatic interaction, integration with third-party systems and automation, **KleeneStar** offers the insights through its REST API under `/api/1`. The interface exchanges JSON (the CRUD endpoint also accepts the form data the dialogs post), authenticates through the session or a personal access token, and authorizes per insight. An insight is addressed by its id, on the CRUD endpoint as the `id` parameter (`?id=<insightid>`), on the tab endpoints as a path segment.

|Endpoint                                            |HTTP Method       |Description
|----------------------------------------------------|------------------|------------------------------------------------------------------------
|`/api/1/insights`                                   |GET               |Lists the readable insights (`RestApiCrud<Insight>`); with `id` and `mode=edit\|delete\|clone` the data for a dialog.
|`/api/1/insights`                                   |POST              |Creates an insight (`insight_create`; name required; query validated; default tabs added) or, with `id`, clones it (`insight_clone` on the original and `insight_create`).
|`/api/1/insights?id={insightid}`                    |PUT, PATCH        |Updates the metadata (name, query, categories, description, state); a change of state also needs `insight_archive` or `insight_restore`.
|`/api/1/insights?id={insightid}`                    |DELETE            |Permanently deletes an insight.
|`/api/1/insights/table`, `/list`, `/dropdown`       |GET               |Overview table (with stored column layout), list and header menu entries (active insights only).
|`/api/1/insights/state`, `/uniquename`              |GET               |State selection and name availability of the dialogs.
|`/api/1/insights/quickfilter`, `/wql`               |GET               |Quickfilter and WQL prompt of the overview.
|`/api/1/insights/{insightid}/tab`                   |GET, POST, PUT, DELETE|The tabs: list, add (by template id), reorder, rename, colour, remove.
|`/api/1/insights/{insightid}/table`, `/list`        |GET, PUT          |Objects tab (`PUT` stores the column layout).
|`/api/1/insights/{insightid}/quickfilter`, `/boardquickfilter`|GET, POST, PUT, DELETE|Quickfilter bars (table/list with *archived*; Kanban, Scrum, Gantt, calendar).
|`/api/1/insights/{insightid}/view`                  |GET, PUT          |The board of a dashboard tab (`v=<tab id>`).
|`/api/1/insights/{insightid}/kanban`                |GET, PUT, …       |The Kanban board of a tab, its settings, lanes and card moves.
|`/api/1/insights/{insightid}/scrumboard`, `/scrumbacklog`|GET, POST, PUT, PATCH, DELETE|Scrum tab: sprint board and backlog.
|`/api/1/insights/{insightid}/gantt`, `/calendar`    |GET, PUT          |Gantt and calendar tabs.
|`/api/1/insights/{insightid}/reports`               |GET               |Report catalog and reports (`format=csv` for CSV).
|`/api/1/insights/{insightid}/export`                |GET               |The insight's objects as CSV.
|`/api/1/insight/{insightid}/permission`             |GET, POST, PUT, DELETE|Grants of the insight (`insight_manage_profiles`).
|`/api/1/insight/{insightid}/permissiongroups`, `/permissionpolicies`|GET|Groups and policies offered by the permission dialog.
|`/api/1/insights/permission`                        |GET, POST, PUT, DELETE|Grants of the creation resource (administrators only; `insight_creator_policy` alone).
|`/api/1/insights/permissiongroups`, `/permissionpolicies`|GET          |Groups and the creator policy offered by the creation dialog.

**Reports.** `/api/1/insights/{insightid}/reports` answers the catalog without `report`, and one report for `?report=cumulativeflow|velocity|averageage|createdresolved|resolutiontime|slacompliance`, over the last `range` days (default 90, 7 to 3650) cut by `interval` (`day`, `week` - default - or `month`; a range too wide for days is coarsened); velocity takes the last `sprints` sprints (default 8). A series on `y1` (the open count of *average age* and *created vs. resolved*, the resolved count of *resolution time*) is drawn on the chart's second value axis. Known limits, said in the answer: an object whose class has no workflow has no state and is left out (`notice` says how many); velocity reads today's sprint membership, because no history of it is kept; an object without a recorded change of its workflow field counts as having stood in its current category since its creation; a change dated in the future counts as made now.

**Service level compliance.** The report `slacompliance` evaluates the service level targets of the objects created in the period - now, the way the SLA card and the escalation rules see them (`SlaEvaluations.For`) - and stacks per interval how many were **met**, **breached**, are **at risk** or still **running**. The summary gives the compliance rate (met of decided), the counts and the most breached target (`WebInsight/Reports/InsightSlaReport`).

**Exports.** Every report and the objects leave as CSV (`WebReport/CsvDocument`: RFC 4180, UTF-8 with byte order mark, `delimiter=semicolon` for spreadsheets of comma-decimal locales, and a cell starting with `= + - @` defused with an apostrophe). A report with `format=csv` writes one row per interval and one column per series, then the summary; `slacompliance` writes one row per object and target. `/export` picks and orders its columns with `columns` (`key, summary, kind, workspace, class, state, assignee, creator, created, updated, sla` and `field:<name>`; default `key, summary, workspace, class, state, assignee, created, updated`), narrows with `from`/`to` (ISO, `to` exclusive) by `date=created` (default) or `updated`, and stops at 200,000 rows. Both read the objects the insight shows its reader, so an export never holds more than the screen. There is no import or export of an insight's own configuration.

Error responses: `400 Bad Request` for validation errors (a missing name, a query that does not compile - with the reason on the field -, a change of state without `insight_archive`/`insight_restore` - on the state field - or a malformed payload), `403 Forbidden` for a caller who is not signed in or lacks the permission on the insight or the creation resource, and `404 Not Found` for an id that names no insight. The tab endpoints of an insight not in use answer as for a missing insight. A successful create, update or delete answers `200 OK`. The board and Kanban endpoints refuse with a `RestApiRefusal`, whose translated message the board shows.

## Insight Events

Insight management communicates its changes through events, so other components can react without being coupled to the manager. The events are ordinary .NET events of `IInsightManager` (`EventHandler<Insight>`), subscribed to through `CoreHub.InsightManager`; they are not published on a separate event bus.

The following events are raised by the `InsightManager`:

|Event Name       |Description
|-----------------|-----------------------------------------------------------------------------
|`InsightAdded`   |Raised when an insight has been created - a clone included.
|`InsightUpdated` |Raised when the metadata of an insight changed, and on every board autosave and tab change (add, remove, rename, colour, reorder).
|`InsightRemoved` |Raised when an insight has been permanently deleted.

The payload is the insight entity itself, including:
- The unique insight id
- Its name, query, description, state and categories
- The timestamps of creation and last update

Who acted and when is not part of the payload: the audit log, which subscribes to all three events in `AuditManager.Connect()`, records the actor of the request (or the identity named by `BeginIdentity`) together with the attribute-level changes. Creating, updating and deleting additionally raise a notification to the acting session.

## Permissions Model

**KleeneStar**'s permission model is applied to each insight individually. Groups are bound to permission policies through grants, which are valid exclusively within one insight (scope `PermissionScope.Insight` = `insight`). Insights belong to no workspace, so the workspace grants that keep an anonymous visitor out say nothing about them: **insights are for signed-in callers** - an anonymous caller reads none and opens no insight page - and an insight's own grants decide beyond that.

**Who may create insights** is decided on an installation-wide resource of its own, because a new insight is on no resource yet: scope `insight`, scope id `creation` (`ContentAuthorization.InsightCreationResource`; not a guid, so the per-insight readers `GetRefusedIds`/`GetGrantedIds` pass it by). Its grants are made in the *Permissions* dialog of the overview (`/insights/permission`), which offers and accepts `insight_creator_policy` alone and is for the installation's administrators only (`MayAdministerInsightCreation`, fail-closed on `Group.AdministratorsId`). `ContentAuthorization.MayCreateInsight` asks it for the create dialog, the *Add Insight* entries and `POST /api/1/insights`.

As for every resource, an insight - or the creation resource - that carries **no grant at all is unrestricted for use**: every signed-in caller holds every insight permission on it. The first grant makes it administered, and from then on only what the grants give applies. The insight permissions are deliberately unscoped (`PermissionImplication`): no workspace grant implies them, and they are not treated as administrative, so the fail-closed rule for administration does not apply to an unadministered insight. What an insight shows is never more than its reader may see, and changing an object through an insight (moving a card, dragging a bar, planning into a sprint) needs the same right on that object or workspace it needs anywhere else.

The following table lists the individual permissions and where they are demanded:

|Permission                |Description
|--------------------------|-----------------------------------------------------------------------------------
|`insight_create`          |Create insights, and clone them (on the creation resource; create dialog, *Add Insight*, `POST`).
|`insight_read`            |Read an insight's metadata; lists, dropdown and the insight page.
|`insight_update`          |Change an insight's metadata (edit dialog, `PUT`/`PATCH`).
|`insight_delete`          |Delete an insight (delete dialog, `DELETE`).
|`insight_archive`         |Take an insight out of use - set it to *deleted* (edit dialog, `PUT`/`PATCH`).
|`insight_restore`         |Put an insight set to *deleted* back into use (edit dialog, `PUT`/`PATCH`).
|`insight_clone`           |Clone an insight (clone dialog, `POST` with `id`).
|`insight_manage_profiles` |Administer an insight's grants (permission dialog and endpoint).
|`insight_read_content`    |Read an insight's tabs and the objects they show.
|`insight_write_content`   |Arrange an insight: its tabs, its dashboards, its Kanban and Scrum configuration.

These individual permissions are bundled into policies that represent typical roles. The policies are assigned to groups in the insight's permission dialog:

|Policy                   |Description                                    |Included Permissions
|-------------------------|-----------------------------------------------|-------------------------
|`insight_admin_policy`   |Full administrative control over an insight.   |all `insight_*`
|`insight_edit_policy`    |Use and arrangement of an insight.             |`insight_read`, `insight_read_content`, `insight_write_content`
|`insight_view_policy`    |Read-only access to an insight and its tabs.   |`insight_read`, `insight_read_content`
|`insight_creator_policy` |Creating new insights (creation resource).     |`insight_create`

The gates: pages through `RouteAuthorization` (areas `insights` and `insight`), lists through `ContentVisibility.Restrict(IQuery<Insight>)`, the CRUD, permission and tab endpoints through `ContentAuthorization.MayUseInsight`, creation through `MayCreateInsight`, a change of state through `MayChangeInsightState`, the tab content through `InsightScope.MayRead` / `MayArrange` (both false for an insight not in use); the tab control is read-only for a caller who may not arrange. The tests that cover them are `UnitTestInsightAuthorization`, `UnitTestRouteAuthorization`, `UnitTestInsightViews`, `UnitTestBoardTabs` and `UnitTestTabMenu`.

## Conclusion

This concept paper describes insight management as implemented in the reference implementation: an insight is a cross-workspace selection of objects by a WQL query, presented through an extensible set of tabs, and managed through an overview, modal dialogs, a REST API and a per-insight permission model. Data model, manager, pages, endpoints, events and permissions are in place, and the reports and exports turn the history of the objects into charts and CSV.
