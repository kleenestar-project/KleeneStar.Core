![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Insight Management Concept

An **insight** is a user-defined view on the data of a **KleeneStar** installation: a set of
objects, chosen by a WQL **query** across workspaces, shown through **tabs** the way a workspace
overview (for example `/issues/SD`) shows its issues - tables and lists, a dashboard, Kanban,
Scrum, Gantt, a calendar and **reports** with charts. A dashboard is one possible tab, not what an
insight is.

Insights replaced the *dashboards* (2026-10-04) and became tab hosts on 2026-10-05. Every dashboard
that existed carries on as an insight with one dashboard tab, with its id, name, description,
categories, state, permissions, columns and widgets.

Insights are not embedded in a workspace. They exist on their own in the installation, so a view
can aggregate objects across workspaces - restricted, as every read is, to what the reader may see.

## The Query

`Insight.Query` is a WQL expression over the object, written in the add, edit and clone dialogs
with the WQL prompt of the search page (`InsightFormItems`):

```
Workspace.Key = "SD"
Workspace.Key = "DEV" and Kind = "issue"
Summary ~ "change"
```

- **Blank selects everything the reader may see.** It is stored as `null`.
- **A query that does not compile is refused when it is written** (`/api/1/insights` `Validate`,
  with the parser's reason on the `Query` field). One that stops compiling later (an attribute
  renamed) selects **nothing** rather than everything - an insight must not silently widen.
- **The objects are read through `ObjectManager`**, so security levels and the content visibility
  of the workspaces narrow them exactly as they narrow every other list.

`WebInsight/InsightScope` is the one place that answers "which insight, may the caller read or
arrange it, which objects": `Resolve`, `MayRead` (`insight_read_content`), `MayArrange`
(`insight_write_content`), `Predicate`/`Apply`/`Query`/`GetObjects`, `Contains`. Every endpoint
behind the tabs asks there.

## Tabs

A tab is an `InsightView` row: name, **view type** (a string key), configuration, order, state,
insight. The insight page `WWW/Insight/_insightid_/Index` carries one fragment,
`InsightTabFragment` - a `FragmentControlDataTab` over `/api/1/insights/{insightid}/tab` - and
every tab type contributes a tab template scoped to it, exactly like `IssueTabFragment` and its
templates. Tabs are added from the template picker, dragged into order (persisted) and closed.

There is deliberately **no enum of tab types**. `WebInsight/InsightViewTypeCatalog` is the open
registry beside the kind, renderer and authentication source catalogs; an `IInsightViewType`
names `Key`, `Label`, `Description`, `Icon`, `Order` and its **`Template`** (the tab template
fragment type). The tab endpoint maps a stored tab to its template (`TemplateId`, derived from the
type like `ObjectViewTemplate`) and a picked template back to its type (`FromTemplateId`). The
keys the core ships are spelled out in `Model.Entities.InsightViewTypes`, because the seeder and
the migration name them too:

|Key        |Template                              |Shows
|-----------|--------------------------------------|----------------------------------------------
|`objects`  |`InsightTabObjectsTemplateFragment`   |Table and list (toggle), search, quickfilters, paging. The default.
|`dashboard`|`InsightTabDashboardTemplateFragment` |The insight's board of widgets.
|`kanban`   |`InsightTabKanbanTemplateFragment`    |The insight's Kanban board; a drop is a workflow transition.
|`scrum`    |`InsightTabScrumTemplateFragment`     |Sprint board and backlog of the workspaces the objects live in.
|`gantt`    |`InsightTabGanttTemplateFragment`     |Timeline from the classes' date fields; dragging writes the dates.
|`calendar` |`InsightTabCalendarTemplateFragment`  |Month/week/agenda grid; moving writes the dates.
|`reports`  |`InsightTabReportsTemplateFragment`   |Charts from the objects' history (below).

Rules:

- **A new insight starts with *Objects* and *Reports*** (`InsightManager.AddDefaultViews`); a
  clone takes its original's tabs and a copy of its board (`CopyViews`).
- **What is stored is not gated.** A tab keeps the key of a type whose plugin is gone; the tab
  endpoint leaves it out and it returns as soon as the type is registered again. The default type
  cannot be unregistered.
- **Each board tab owns its configuration.** Dashboard columns carry `ViewId`, while
  `KindDashboard` and `KanbanBoard` use the owner, kind and `ViewId` as their unique scope.
  Two dashboard, Kanban or Scrum tabs can therefore have different columns, widgets, lanes
  and filters. The tab binding `boardservice` writes `v=<tab id>` into the board service
  before the client initializes it. The server validates the tab's owner, kind, type and state.
  Requests without `v` address the retained legacy configuration; an invalid explicit `v`
  is refused. New tabs start with the default layout and never inherit a sibling's changes.
- **The objects table stores its column layout per reader, insight and tab** (`v` parameter,
  written into the table's data service through the tab binding `insighttable`).
- **Quickfilters users define on an insight** are stored under the view key `insight` with the
  insight id as context; `Quickfilter` (table/list, with *archived*) and `BoardQuickfilter`
  (Kanban, Scrum, Gantt, calendar) serve them.

A plugin adds a tab type by registering an `IInsightViewType` and declaring a
`FragmentControlDataTabTemplate` with `[Scope<InsightTabFragment>]` (deriving from
`InsightTabTemplateFragmentBase` gives it the catalog's icon and texts), plus its content
fragments scoped to that template. Nothing already written is edited.

### How the shared REST bases serve an insight

The overview endpoints were written for one workspace and one kind. Their bases now take the set
of objects from an overridable hook, and the insight endpoints override only that:

|Base                      |Hook(s)                                                       
|--------------------------|--------------------------------------------------------------
|`RestApiObjectKindTable`  |`RetrieveScope`, `BuildCatalog` (columns from any set of classes: `ObjectTableColumnCatalog.Build(classIds, …)`), `LayoutScope`
|`RestApiObjectKindList`   |`Scope`
|`RestApiObjectKindKanban` |`ResolveScope` → `KanbanBoardScope` (board owner and kind, query narrowing, classes, status catalog via `KanbanStatusCatalog.Build(classes)`), `MayArrange`
|`RestApiObjectKindGantt`, `RestApiObjectKindSchedule`|`ResolveScope`, `InScope`

The workspace endpoints keep the defaults and behave as before.

### Scrum across workspaces

Sprints belong to a workspace and an object can only be planned into a sprint of its own
workspace. The insight's backlog (`ScrumBacklog`) therefore lists the sprints of every workspace
its objects live in - named `KEY · Sprint` when there are several - refuses a move into a sprint of
another workspace, and creates a new sprint only when the objects live in exactly one workspace.
Every change needs `workspace_write_content` on the workspace it changes: an insight grants no
rights on what it shows. The sprint board (`ScrumBoard`) shows the objects committed to the
running sprint of their workspace. The issue overview's team workload and burn-down headers are
not part of the insight's Scrum tab.

## Reports

`/api/1/insights/{insightid}/reports` answers the report catalog without `report`, and one report
for `?report=cumulativeflow|velocity|averageage|createdresolved|resolutiontime`, over the last
`range` days (default 90) cut by `interval` (`day`, `week` - default - or `month`; a range too wide
for days is coarsened); velocity takes the last `sprints` sprints (default 8). The tab
(`InsightReportControl` + `Assets/js/insightreports.js`, included on the insight page by
`IncludeInsightReportsScript`) renders the translated toolbar on the server and draws the answer
with the Chart.js the framework ships; the reader's choice is remembered in the browser per insight.

The reports are computed from the **history of the workflow field**, replayed from the commit
chain (`ModelHub.GetFieldChanges`, one query for all workflow fields of the classes involved;
`WebInsight/Reports/InsightReportSource`). A recorded value resolves to its **status category**
(to do, in progress, waiting, done) through the statuses of the object's class - the reports span
classes whose workflows name their statuses differently, but every status has a category.
**Resolved means entering the done category**; reopening and resolving again counts again.

|Report               |What it shows
|---------------------|------------------------------------------------------------------------
|Cumulative flow      |Per bucket, how many objects stood in each category at its end (stacked area; done at the bottom).
|Velocity             |Per running or completed sprint, committed vs. completed work in story points - in objects when nothing is estimated. Completed = in done at the sprint's end.
|Average age          |Per bucket, the average age of the objects open at its end, beside their number.
|Created vs. resolved |Per bucket, created and resolved, beside the open objects at its end.
|Resolution time      |Per bucket of resolution, the average time from creation, beside the number resolved; median as a figure.

Known limits, said in the answer or here:

- **An object whose class has no workflow has no state** and is left out of every report; the
  answer's `notice` says how many.
- **Velocity reads today's sprint membership** - the store keeps no history of it.
- **An object with no recorded change** of its workflow field (created before the history existed)
  is taken to have stood in its current category since its creation.
- **A change dated in the future counts as made now.** The seeder lays out the transition of a
  seeded object an hour after its creation, which for an object seeded "now" is in the future.
- Durations are drawn in hours when a whole series stays below a day.

The calculations (`InsightReports`) are pure functions over `InsightReportItem` (creation, states,
points, sprint) and `InsightReportPeriod`, so `UnitTestInsightReports` checks them on hand-built
histories.

## Lifecycle

Insights exist in two states, **active** and **deleted** (`InsightState`). An active insight is
visible and usable; a deleted one is removed. The audit log keeps every change.

```
                     new  ╔════════╗
                       ───► active ║
                          ╚════════╝
                              │
                              │  delete
                              │
                         ╔════▼════╗
                         ║ deleted ║
                         ╚═════════╝
```

## Data Model

|Entity            |Table              |Purpose
|------------------|-------------------|--------------------------------------------------------------
|`Insight`         |`Insight`          |Name (unique), **Query**, icon, description, state, timestamps; `Type` (legacy, see below).
|`InsightView`     |`InsightView`      |A tab: name (unique per insight), view type key, configuration, order, state; cascades with the insight.
|`InsightCategory` |`InsightCategory`  |Many-to-many link between insights and categories.
|`DashboardColumn` |`DashboardColumn`  |A column of one dashboard tab; references the insight (`Insight`) and tab (`View`).
|`Widget`          |`Widget`           |A widget in a column (type id, name, colour, params, WQL).
|`KanbanBoard`     |`KanbanBoard`      |A tab's board configuration: `Workspace` = insight id, `Kind` = `insight`, `View` = tab id.

`Insight.Type` is the type an insight was created as before it hosted tabs. It is kept, not
dropped (dropping a column rebuilds the table in SQLite), and nothing reads it since.

### Migrations

The `ScopeBoardsToTabs` migration retains the legacy boards and gives every existing dashboard,
Kanban and Scrum tab an independent copy with fresh column, widget and lane identifiers. It
preserves widget parameters, legacy WQL, status assignments, ordering, colors and filters.
This also applies to issue and asset overviews. Rolling back discards the tab-specific copies
and restores the retained legacy configurations. Fresh installations seed dashboard columns
with their owning tab identifier.

The lifecycle follows the tab. Removing a tab deletes its configuration and children, while
removing an insight or workspace deletes all its board configurations. Cloning an insight copies
each tab's dashboard and Kanban configuration under new identifiers, including Scrum boards.

`20261004222550_AddInsightViews` adds `Insight.Query` and the `InsightView` table, and gives every
existing insight one tab of its type (`Dashboard`), so it opens on what it showed before; the board
stays on the insight. Only `ADD COLUMN` and `CREATE TABLE` plus an `INSERT`, no table rebuild.

`20261004214616_RenameDashboardToInsight` (since squashed into `InitialCreate`) had converted the
dashboards in place: tables renamed, not recreated; grants `dashboard_*` → `insight_*`, scope
`dashboard` → `insight`; the overview table layout key and stored notifications followed. The audit
log was not touched; `AuditTargetType.Insight` keeps the number but its token is now `insight`, so
events sealed about a dashboard before the rename no longer verify - accepted. Old addresses
(`/dashboards`, `/dashboard/{id}`) are not redirected.

## Software Architecture

`InsightManager` (`IInsightManager`, `CoreHub.InsightManager`) owns the insights and their tabs:
`GetInsight`, `GetInsights`, `Add`, `Update`, `Remove`; the board `SetColumns` (column-only
change) and `SetBoard` (full board change, widgets rebuilt); the tabs `GetViews`, `GetView`,
`AddView` (appended, a taken name numbered), `RemoveView`, `ReorderViews`, `AddDefaultViews`,
`CopyViews`. It raises `InsightAdded`, `InsightUpdated` and `InsightRemoved`; the audit log
subscribes centrally in `AuditManager.Connect()`. `Add`/`Update`/`Remove` raise a notification
(`notification.insight.*`); the board autosave and tab changes raise `InsightUpdated` without one.

|Component                       |Role
|--------------------------------|------------------------------------------------------------------
|`InsightViewTypeCatalog`        |Open registry of tab types; maps stored tabs to templates and back.
|`IInsightViewType`              |Descriptor of one tab type; `CoreInsightViewTypes.cs` holds the core's seven.
|`InsightScope`                  |The insight of a route, its read/arrange rights and its objects.
|`InsightQuickfilterBase`        |Base of the two quickfilter endpoints.
|`InsightReports`, `InsightReportSource`|The five reports and the history they are computed from.
|`ContentVisibility.Restrict(IQuery<Insight>)`|Narrows every insight list to what the caller may read.
|`ContentAuthorization.MayUseInsight`|Gate of the CRUD, permission and tab endpoints.

## UI

- **Header menu *Insights*** (`InsightDropdownFragment`, pie-chart icon): the readable insights,
  *Add Insight* and *Insights* (the overview). Shown to signed-in callers only.
- **Overview** `WWW/Insights/Index` (`/insights`): table (name, query, description, state) with
  search, quickfilter and paging, plus *Add Insight*. Row menu: edit, clone, permissions, delete.
- **Create / Edit / Clone** `WWW/Insights/Add`, `WWW/Insight/{insightid}/Edit|Clone`: name,
  **query**, category, description, state. **Delete / Permissions** as modals.
- **Insight page** `WWW/Insight/{insightid}` (`/insight/{insightid}`): the tab control.

The start page's *Choose start page* leads to the insight overview.

## REST Endpoints

|Endpoint                                      |Method          |Purpose
|----------------------------------------------|----------------|------------------------------------------
|`/api/1/insights`                             |GET/POST/PUT/DELETE|CRUD (`RestApiCrud<Insight>`); validates the query, adds the default tabs.
|`/api/1/insights/table`                       |GET             |Overview table.
|`/api/1/insights/list`                        |GET             |List of insights.
|`/api/1/insights/dropdown`                    |GET             |Entries of the header menu.
|`/api/1/insights/state`                       |GET             |State selection.
|`/api/1/insights/uniquename`                  |GET             |Name availability.
|`/api/1/insights/quickfilter`, `/wql`         |GET             |Quickfilter and WQL prompt of the overview.
|`/api/1/insights/{insightid}/tab`             |GET/POST/PUT/DELETE|The tabs: list, add (by template id), reorder, remove.
|`/api/1/insights/{insightid}/table`, `/list`  |GET/PUT         |Objects tab (PUT stores the column layout).
|`/api/1/insights/{insightid}/quickfilter`, `/boardquickfilter`|GET/POST/PUT/DELETE|Quickfilter bars.
|`/api/1/insights/{insightid}/view`            |GET/PUT         |The insight's board (dashboard tabs).
|`/api/1/insights/{insightid}/kanban`          |GET/PUT/…       |The insight's Kanban board.
|`/api/1/insights/{insightid}/scrumboard`, `/scrumbacklog`|GET/…|Scrum tab.
|`/api/1/insights/{insightid}/gantt`, `/calendar`|GET/PUT       |Gantt and calendar tabs.
|`/api/1/insights/{insightid}/reports`         |GET             |Report catalog and reports.
|`/api/1/insight/{insightid}/permission…`      |GET/POST/DELETE |Permission dialog (grants, groups, policies).

## Permissions Model

Insights belong to no workspace, so the workspace grants that keep an anonymous visitor out say
nothing about them: **insights are for signed-in callers**, and an insight's own grants (scope
`PermissionScope.Insight` = `insight`, administered in its permission dialog) decide beyond that.
Route guard: the `insight(s)` areas of `RouteAuthorization`.

Reading any tab needs `insight_read_content`; arranging the insight - its tabs, its board, its
Kanban configuration - needs `insight_write_content` (the board refuses with a `RestApiRefusal`).
What an insight shows is never more than its reader may see, and changing an object through an
insight (moving a card, dragging a bar, planning into a sprint) needs the same right on that object
or workspace it needs anywhere else.

|Permission                |Description
|--------------------------|-------------------------------------------------------------------
|`insight_create`          |Create insights.
|`insight_read`            |Read an insight's metadata.
|`insight_update`          |Change an insight's metadata.
|`insight_delete`          |Delete an insight.
|`insight_archive`         |Archive an insight.
|`insight_restore`         |Restore an insight.
|`insight_clone`           |Clone an insight.
|`insight_manage_profiles` |Administer an insight's grants.
|`insight_read_content`    |Read an insight's content.
|`insight_write_content`   |Change an insight's content.

|Policy                   |Included permissions
|-------------------------|---------------------------------------------------------------
|`insight_admin_policy`   |all `insight_*`
|`insight_edit_policy`    |`insight_read`, `insight_read_content`, `insight_write_content`
|`insight_view_policy`    |`insight_read`, `insight_read_content`
|`insight_creator_policy` |`insight_create`

The former gap - `/api/1/insights/{insightid}/view` checked no permission - is closed: the board
is read with `insight_read_content` and saved with `insight_write_content`.
