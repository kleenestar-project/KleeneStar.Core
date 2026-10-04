![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Insight Management Concept

An **insight** is a user-defined view on the data of a **KleeneStar** installation. The term is the
umbrella for every kind of custom presentation a user creates and manages for themselves or their
team: a dashboard of widgets, and - built on the same foundation - calendars, Gantt charts, lists,
tables or Kanban boards. What kind of view an insight is, is its **type**.

Insights replaced the *dashboards* (2026-10-04). The header menu that was called *Dashboards* is
called *Insights*, and every dashboard that existed carries on as an insight of the type
*Dashboard*, with its id, name, description, categories, state, permissions, columns and widgets.
The dashboard is the one type the core ships today; the others are what the concept is the
foundation for.

Insights are not embedded in a workspace. They exist on their own in the installation, so a view
can aggregate objects across workspaces - restricted, as every read is, to what the reader may see.

## Insight Types

There is deliberately **no enum of insight types**. `InsightTypeCatalog`
(`KleeneStar.Core/WebInsight/`) is the open registry beside the object kind, renderer and
authentication source catalogs: the core registers `dashboard` (`DashboardInsightType`), and a
plugin contributes a type by registering an `IInsightType`:

|Member        |Meaning
|--------------|----------------------------------------------------------------------------------
|`Key`         |The key the type is persisted under in `Insight.Type`, compared without case.
|`Label`       |The i18n key of the name the type is offered under.
|`Description` |The i18n key of the sentence that explains the type.
|`Icon`        |The icon the type is listed under (the type picker).
|`Order`       |The position among the other types.

Rules the catalog and the endpoint keep:

- **The type is chosen once.** The create dialog offers the registered types
  (`InsightTypeSelectionControl`, projected from the catalog whenever its `Version` changes, so a
  type a plugin registers later still appears). The content of one type means nothing to another -
  a dashboard's columns are no calendar - so the edit dialog does not offer the type, a clone takes
  the type of its original, and `/api/1/insights` refuses an update that names a different one.
- **A create that names no type is a dashboard** (`InsightTypeCatalog.Default`); a create naming a
  type nobody registered is refused, naming the types that are available.
- **The dashboard cannot be unregistered**, only replaced - it is the fallback.
- **What is stored is not gated.** An insight keeps the key of a type whose plugin is gone; its page
  shows a notice (`InsightTypeUnavailableFragment`) instead of standing empty, and the insight
  returns as soon as the type is registered again.

**No page knows about insight types.** The insight page `WWW/Insight/_insightid_/Index` carries one
fragment per type in `SectionContentPrimary`, each gated on a condition derived from
`InsightTypeCondition`. The dashboard's is `InsightDashboardFragment` with
`InsightDashboardCondition`. A new type is a descriptor, a condition and a fragment; nothing
already written is edited.

### The Dashboard Type

A dashboard consists of columns of freely arranged widgets - key figures, charts, lists, progress
bars, info cards and notes. The columns (`DashboardColumn`) and widgets (`Widget`) are the content of
the dashboard type and hang off the insight (`DashboardColumn.InsightId`); an insight of another type
has none, and `ModelHub.SetDashboardColumns` / `SetDashboardBoard` write nothing into an insight
that is not a dashboard. The board is the framework's `ControlDataDashboard`, backed by
`/api/1/insights/{insightid}/view` (`RestApiDashboard`), which persists every column and widget
change. The KleeneStar widget types and their texts are inlined into the page
(`DashboardWidgetScript`).

The per-kind *Dashboard* tab of the issue and asset overviews (`KindDashboard`,
`ObjectViewType.Dashboard`) is a different thing - a view of a workspace overview, not an insight -
and keeps its name.

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
|`Insight`         |`Insight`          |Name (unique), **Type**, icon, description, state, timestamps.
|`InsightCategory` |`InsightCategory`  |Many-to-many link between insights and categories.
|`DashboardColumn` |`DashboardColumn`  |A column of a dashboard; references the insight (`Insight`).
|`Widget`          |`Widget`           |A widget in a column (type id, name, colour, params, WQL).

`Insight.Type` is a string of at most 64 characters, required, with the database default
`dashboard`.

### Migration from the Dashboards

`20261004214616_RenameDashboardToInsight` (`KleeneStar.Model.Sqlite`) converts an existing store in
place and loses nothing:

- the tables are **renamed**, not recreated (`Dashboard` → `Insight`, `DashboardCategory` →
  `InsightCategory` with `DashboardId` → `InsightId`, `DashboardColumn.Dashboard` → `Insight`), and
  `Insight.Type` is added with the default `dashboard`. The migration uses nothing but
  `ALTER TABLE … RENAME` and `ADD COLUMN` on purpose: SQLite carries the foreign keys along, while a
  table rebuild - which EF Core defers to the end of a migration - would have run after a dropped
  table had cascaded into every column and widget. The constraint names inside the table
  definitions keep their old spelling until the next rebuild; SQLite does not address them by name;
- the grants follow: `PermissionAssignment.Policy` `dashboard_*` → `insight_*` and
  `Scope` `dashboard` → `insight`;
- the stored layout of the overview table follows its endpoint type name
  (`…Api._1_.Dashboards.Table` → `…Api._1_.Insights.Table`);
- stored notifications follow: message key `notification.dashboard.*` → `notification.insight.*`,
  link `/dashboard/{id}` → `/insight/{id}`.

The audit log is not touched - it is append-only. Its target type member is now
`AuditTargetType.Insight` (same stored number), and its token - part of the sealed canonical form -
is now `insight`: events sealed about a dashboard before the rename carry `dashboard` in their form
and no longer verify. This was accepted with the rename.

Old addresses (`/dashboards`, `/dashboard/{id}`) are not redirected.

## Software Architecture

`InsightManager` (`IInsightManager`, `CoreHub.InsightManager`) owns the lifecycle of the insights:
`GetInsight`, `GetInsights`, `Add`, `Update`, `Remove`, and for the dashboard type `SetColumns`
(column-only change) and `SetBoard` (full board change, widgets rebuilt). It raises `InsightAdded`,
`InsightUpdated` and `InsightRemoved`; the audit log subscribes to them centrally in
`AuditManager.Connect()`, and `Add`/`Update`/`Remove` raise a notification
(`notification.insight.*`). The board autosave (`SetColumns`, `SetBoard`) raises `InsightUpdated`
without a notification.

|Component                       |Role
|--------------------------------|------------------------------------------------------------------
|`InsightTypeCatalog`            |Open registry of insight types; the authority on create.
|`IInsightType`                  |Descriptor of one type; `DashboardInsightType` is the core's.
|`InsightTypeCondition`          |Base of the per-type fragment conditions on the insight page.
|`InsightUnavailableTypeCondition`|Fulfilled for an insight whose type is not registered.
|`InsightTypeSelectionControl`   |Type picker of the create dialog.
|`ContentVisibility.Restrict(IQuery<Insight>)`|Narrows every insight list to what the caller may read.
|`ContentAuthorization.MayUseInsight`|Gate of the CRUD and permission endpoints.

## UI

- **Header menu *Insights*** (`InsightDropdownFragment`, pie-chart icon): the readable insights,
  *Add Insight* and *Insights* (the overview). Shown to signed-in callers only.
- **Overview** `WWW/Insights/Index` (`/insights`): table (name, type, description, state) with
  search, quickfilter and paging, plus *Add Insight*. Row menu: edit, clone, permissions, delete.
- **Create** `WWW/Insights/Add`: name, **type**, category, description, state.
- **Edit / Clone / Delete / Permissions** `WWW/Insight/{insightid}/Edit|Clone|Delete|Permission`:
  modals; edit and clone carry no type.
- **Insight page** `WWW/Insight/{insightid}` (`/insight/{insightid}`): the view of the insight's
  type - the editable board for a dashboard.

The start page's *Choose start page* leads to the insight overview.

## REST Endpoints

|Endpoint                                      |Method          |Purpose
|----------------------------------------------|----------------|------------------------------------------
|`/api/1/insights`                             |GET/POST/PUT/DELETE|CRUD (`RestApiCrud<Insight>`); validates the type.
|`/api/1/insights/table`                       |GET             |Overview table.
|`/api/1/insights/list`                        |GET             |List of insights.
|`/api/1/insights/dropdown`                    |GET             |Entries of the header menu.
|`/api/1/insights/state`                       |GET             |State selection.
|`/api/1/insights/uniquename`                  |GET             |Name availability.
|`/api/1/insights/quickfilter`, `/wql`         |GET             |Quickfilter and WQL prompt of the overview.
|`/api/1/insights/{insightid}/view`            |GET/PUT         |Board of a dashboard insight.
|`/api/1/insight/{insightid}/permission…`      |GET/POST/DELETE |Permission dialog (grants, groups, policies).

## Permissions Model

Insights belong to no workspace, so the workspace grants that keep an anonymous visitor out say
nothing about them: **insights are for signed-in callers**, and an insight's own grants (scope
`PermissionScope.Insight` = `insight`, administered in its permission dialog) decide beyond that.
Route guard: the `insight(s)` areas of `RouteAuthorization`.

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

**Known gap:** `/api/1/insights/{insightid}/view` checks no permission - neither the read of the
board nor its autosave asks `ContentAuthorization.MayUseInsight` (it did not as the dashboard
endpoint either).
