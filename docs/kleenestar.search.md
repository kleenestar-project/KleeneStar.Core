![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Search and Saved Searches

Search in **KleeneStar** is a single, cross-cutting surface that queries objects across *every* workspace at once — independent of the per-workspace object views. Where the workspace content pages scope their tables to one workspace, the global search page runs an unscoped query over the whole object index and returns matches from all workspaces side by side. The goal is one fast, consistent "find anything" entry point that is always reachable from the application header.

Search is driven by **WQL** (the WebExpress query language) over the server-side reverse index. A query such as `Summary ~ "incident"` or `Key ~ "SD-9000"` is parsed and evaluated against the indexed object fields (key, summary, description, metadata) and returns a paginated result set. The search bar offers WQL autocomplete and a short history of recent expressions.

On top of the ad-hoc search sits the **saved search** concept: a named, reusable WQL query that its owner can star, run, edit, delete — and **share**. A saved search is private until its owner grants a group a `savedsearch_…` policy in its permission dialog; the members then see it under *Shared with me* and may do what the policy carries. Saved searches back two surfaces: the search-page sidebar (the owner's saved searches, starred first, then those shared with the caller) and the "recently used" navigation list. A saved search records the moment its owner last ran it so the recency ordering stays current.

The `SavedSearchManager` is responsible for the lifecycle of saved searches. It ensures that:
- Saved searches are valid and attributable (every saved search has an owner and a WQL query the search page can run), and private until shared (`IsGranted`, `GetSharedWith`).
- Running a saved search quietly bumps its `LastUsed` timestamp so the "recently used" ordering reflects real usage.
- Starring is a quiet, per-owner pin that floats a saved search to the top of the sidebar.
- A reactive event surface (`SavedSearchAdded`, `SavedSearchUpdated`, `SavedSearchRemoved`) is published so subscribers can react without depending on the manager.

The `SavedSearchManager` is complementary to the `ObjectManager`: objects (across workspaces) provide the search corpus, while saved searches add a personal, reusable layer of named queries on top.

## Lifecycle and States

The ad-hoc search itself is stateless — it is just a query against the index. A **saved search** follows a minimal lifecycle with the states active and deleted.

- **active:** The saved search is visible in the sidebar and the recently-used list, and can be run, edited, and starred.
- **deleted:** The saved search has been removed by its owner and is no longer listed or runnable. Deletion is performed through the manager's `Remove` path.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                      KleeneStar Saved Search State Diagram                           ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                       new   ╔════════╗      delete      ╔═════════╗                  ║
║                        ───► ║ active ├──────────────────► deleted ║                  ║
║                             ╚════╤═══╝                  ╚═════════╝                  ║
║                              ▲   │                                                   ║
║                       star / │   │ run (bumps LastUsed)                              ║
║                       edit   └───┘                                                   ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

`RecordUse` (run) and `SetStarred` (pin) are *quiet* mutations: they update `LastUsed` / `Starred` without raising a user-facing notification, because they fire on every run / toggle. `Add`, `Update`, and `Remove` are the loud mutations that own the create / update / delete toasts.

## Data Model and Relationships

A saved search is bound to exactly one identity (its owner) and carries a WQL query string that is evaluated against the shared object index when the saved search is run. The search has no foreign key to a workspace — its query deliberately spans every workspace.

- Key attributes: id (stable Guid), name, description (optional; the prose editor's document, printed through `ProseText`), query (WQL string), starred (bool, the owner's pin), lastUsed (DateTime, the owner's), state, created, updated.
- Required reference: owner (the identity the saved search belongs to). Sharing is not a column: it is the grants of scope `savedsearch` on the saved search (`PermissionAssignment`).

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Search Data Model                                   ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║     ┌──────────┐ 1                                  * ┌─────────────┐                ║
║     │ Identity ├──────────────────────────────────────► SavedSearch │                ║
║     └──────────┘                  owner               └──────┬──────┘                ║
║                                                              │ Query (WQL)           ║
║                                                              │ evaluated over        ║
║                                                              ▼                       ║
║     ┌───────────┐ 1                * ┌────────┐    reverse-index   ┌───────────────┐ ║
║     │ Workspace ├─────────────────────► Object ◄───────────────────┤ WebIndex /WQL │ ║
║     └───────────┘   (every one)      └────────┘                    └───────────────┘ ║
║                                                                                      ║
║   The global search runs one WQL query over the Object index spanning ALL            ║
║   workspaces; a SavedSearch simply persists that query for one identity.             ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The `SavedSearch` entity lives in the `SavedSearches` table and references the owning `Identity`. Because objects are indexed by the WebExpress reverse index (`KleeneStarDbContext` implements `IQueryContext`), the same WQL expression that a saved search stores is exactly what the ad-hoc search bar accepts.

## Software Architecture

The `SavedSearchManager` mirrors the architecture of the other domain managers. It is a WebExpress component registered via reflection from `KleeneStar.Core.WebManager.SavedSearchManager`, exposed via the `ISavedSearchManager` interface, and resolved by callers through `CoreHub.SavedSearchManager`. Persistence is delegated to `ModelHub.SavedSearch` (the partial class that holds the query, insert, update, and delete code), which talks to the `KleeneStarDbContext`.

The ad-hoc search has no manager of its own: the results table is fed by the unscoped object table endpoint (`/api/1/objects`), and the WQL autocomplete is served by the WQL prompt endpoint (`/api/1/objects/wql`). The HTML surface is composed entirely from page-scoped fragments on the search page.

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
║    ┌----------------┤ ISavedSearchManager                   │                        ║
║    ¦                ├───────────────────────────────────────┤                        ║
║    ¦                │ SavedSearchAdded:Event                │                        ║
║    ¦                │ SavedSearchUpdated:Event              │                        ║
║    ¦                │ SavedSearchRemoved:Event              │                        ║
║    ¦              1 ├───────────────────────────────────────┤                        ║
║    ¦                │ GetSavedSearch(Guid):SavedSearch      │                        ║
║    ¦                │ GetForOwner(Guid):                    │                        ║
║    ¦                │   IReadOnlyList<SavedSearch>          │                        ║
║    ¦                │ GetRecent(Guid,int):                  │                        ║
║    ¦                │   IReadOnlyList<SavedSearch>          │                        ║
║    ¦                │ Add(SavedSearch):ISavedSearchManager  │                        ║
║    ¦                │ Update(SavedSearch):                  │                        ║
║    ¦                │   ISavedSearchManager                 │                        ║
║    ¦                │ Remove(Guid):ISavedSearchManager      │                        ║
║    ¦                │ RecordUse(Guid):SavedSearch           │                        ║
║    ¦                │ SetStarred(Guid,bool):SavedSearch     │                        ║
║    ¦                └────────────────Δ──────────────────────┘                        ║
║    ¦                                 ¦                                               ║
║    ¦ create         ┌────────────────┴──────────────────────┐                        ║
║    └----------------► SavedSearch                           │                        ║
║                     ├───────────────────────────────────────┤                        ║
║                     │ Id:Guid                               │                        ║
║                     │ Name:String                           │                        ║
║                     │ Description:String                    │                        ║
║                     │ Query:String (WQL)                    │                        ║
║                     │ OwnerId:Guid                          │  ┌─────────────────┐   ║
║                     │ Owner:Identity                        │  │ <<Enum>>        │   ║
║                     │ Starred:Bool                          │  │ SavedSearchState│   ║
║                     │ LastUsed:DateTime                     │  ├─────────────────┤   ║
║                     │ State:SavedSearchState                │  │ Active          │   ║
║                     │ Created:DateTime                      │  │ Deleted         │   ║
║                     │ Updated:DateTime                      │  └─────────────────┘   ║
║                     └───────────────────────────────────────┘                        ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Persistence

The `SavedSearch` entity is persisted in the `SavedSearches` table and references the owning `Identity`. The `GetForOwner` retrieval returns the owner's active saved searches ordered starred-first and then by name (the sidebar order); `GetRecent` returns them ordered by `LastUsed` descending and capped (the "recently used" order). A default set of example saved searches is seeded for the bootstrap admin identity so the surfaces are populated on first run.

## UI Concepts and Pages

Search is reached from the application header. The header object dropdown lists the calling identity's recently opened objects and, below a divider, a titled **Search** section whose entry opens the global search page (this replaced the former standalone header search field and the separate saved-search dropdown). The global search page itself hosts the search bar, the cross-workspace results table, the pagination, and the saved-search sidebar.

### Global Search Page

The search page renders the WQL search bar in the view header, the cross-workspace results table in the primary section, and the saved searches in the sidebar. Typing in the search bar (or running a saved search) re-queries the unscoped object table and repaginates the results.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar   Workspace ▼   Object ▼   Dashboard ▼          [+ AddObject]         │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Search                                                                           │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Saved searches────────┐ ┌Search across all workspaces───────────────────────────────┐║
║│                      │░│                                                           │║
║│  + New search        │░│ [ Summary ~ "incident"                          🔍 ]      │║
║│                      │░│                                                           │║
║│  SAVED SEARCHES      │░│ Key      | Summary                  | Workspace  | …      │║
║│  ★ My open incidents│░│----------|-------------------------|------------|---------│║
║│  ★ High prio · week │░│ SD-1042  | VPN drop                | ServiceDesk| …       │║
║│  Login flow tickets  │░│ INC-204  | Login flow broken       | ServiceDesk| …       │║
║│  Service desk backlog│░│ DEV-88   | Incident postmortem     | Software   | …       │║
║│                      │░│ CMDB-12  | Incident impact mapping | CMDB       | …       │║
║│  + New saved search  │<│                                                           │║
║│                      │<│                                   ‹ Prev  1  2  3  Next › │║
║├──────────────────────┤░│                                                           │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Behaviour

- The search bar is an advanced-search control bound to the WQL prompt endpoint (`/api/1/objects/wql`), which offers WQL autocomplete and a short history of recent expressions. Its content id drives the results table's `BindSearch`, so the table re-queries as the user types.
- **The history is the caller's own, and it is recorded rather than illustrated.** Every prompt derives from `KleeneStarRestApiWqlPrompt<T>`, which answers `GetHistory` from `WqlHistory` — one row per query an identity has run, persisted, capped at 20 and cascaded off the account. It is written where a query is *executed*, by `KleeneStarRestApiTable<T>.Filter(IWqlStatement…)`, which the framework reaches only after the parser accepted the expression; the prompt's own `analyze` endpoint cannot stand in for that, because it fires on every keystroke and again for the pre-submit validation and so cannot tell a finished query from a half-typed one. Both ends name the queried type as the *subject*, which is what lets the prompt read back what the table it feeds has run — so the global object search, an issue list and an asset inventory share one history (same attributes, same expressions) while unrelated entities keep theirs apart.
- The results are a view with two presentations, switched in the view header: **Table** (`SearchViewTableFragment`, `/api/1/objects/table`) and **List** (`SearchViewListFragment`, `/api/1/objects/list`) - a master-detail view whose pane shows the reduced reading view of the selected object (`ObjectKindCatalog.ResolvePreviewUri`). Both are bound to the same search field, quickfilter bar and pager and seeded with the same query; both return objects from **every** workspace. The table has the columns **Key**, **Summary**, **Workspace** and **Description**, offers its column chooser always (`WebControl/ColumnChoosingDataTable` writes `data-allow-column-remove`, because the chooser otherwise lives in the header of the options column, which exists only while rows carry a menu) and gives each row an *Open* entry. Both endpoints count the whole result (`RetrieveTotal`), so the pager offers every page - the table used to report the size of the page it returned.
- The sidebar lists a `+ New search` entry (clears any applied query), the owner's saved searches (starred ones first, each prefixed with a ★), and — under *Shared with me* — the saved searches others shared with the caller. There is no *new saved search* entry: a saved search is made from the search on screen, with the save button. Clicking a saved search runs it (the running one is marked active); double-clicking opens its edit modal where the caller may change it. The tooltip is the description as words, else the query. The entries are `WebControl/UnboundSidebarItemLink`s: the framework's sidebar link binds its address against the request, which replaces query values too, so on a page running a saved search every entry pointed at the running one.
- Running a saved search navigates to `/search?use=<id>` (`WebFragment/Search/SavedSearchRun`). The page resolves the query from the **stored** record — only when the caller may see it — titles itself by the saved search, seeds the results table with the query (state key `wql`) and opens the advanced search in WQL mode on it (`Assets/js/savedsearch.js`, which switches the mode quietly: the framework's own switch would re-query the table with the empty `value` of the prompt's editable div). For the owner it stamps the saved search as just used. An ad-hoc expression can be handed over as `/search?wql=<query>`; `use` wins.
- Beside the search field stand the controls of `WebControl/SavedSearchAdvancedSearch`: **Save search** (a new saved search; while a saved search runs and the caller may change it, that one) and a **"…" menu** (`ControlDropdown`, left out when empty) with **Save as new** (only beside a saved search the caller may change), **Permissions** (owner or admin policy) and **Delete** (owner or admin policy; after the delete the script opens the plain search page, since the deleted search cannot run any more). The delete dialog binds the saved search through `ItemId` - it used to address no record. The menu's entries keep their `data-ks-savedsearch-carry` mark, because the dropdown script rebuilds its entries with their data attributes. The dialogs they open receive the expression *on screen* — typed but not submitted included — in their `wql` parameter, which the script appends on click; a basic term is saved as `Summary ~ "<term>"`, which is what the basic search matches. The buttons stand next to the control rather than inside it, because the framework's `SearchCtrl` clears its host before collecting the children it means to keep, so `ControlAdvancedSearch.Add(...)` content never survives.
- **Columns are part of the saved search** (`SavedSearch.Columns`, the JSON of `WebRestApi/TableLayout` - id, visibility, width per column; migration `SavedSearchColumns`). The results table's address carries `?use=<id>` while a saved search runs (`SearchViewTableFragment`, set through `WebEx.CurrentRequest` in the data-service preset), and `Objects/Table` then shows that layout and stores a change of it into the saved search (`ISavedSearchManager.SetColumns`, quiet) when the caller may change it - otherwise into the caller's own layout **for that saved search** (session key `…Objects.Table@savedsearch:<id>`), so a reader can rearrange without rearranging it for everybody. A saved search that never had columns shows the caller's own layout of the table. A new saved search takes over the layout on screen (`Objects.Table.CurrentLayout`: the running saved search's - the add dialog names it with `use` - or the caller's own); a clone copies the original's. The rows emit their cells in the effective column order, since the client pairs them by position (the table ignored its `columns` argument before, so a reordered layout mislabelled the cells).
- **Quickfilters per saved search.** While a saved search runs, `SearchViewQuickfilterFragment` shows its bar, served by `/api/1/savedsearch/{id}/quickfilter` (view key `savedsearch`, context = the saved search's id; `CustomQuickfilter` rows, the dialogs `Quickfilters/Add|Edit` routed by `QuickfilterService`). A chip is its author's; switched to shared it is offered to everybody who may see the saved search. Only a caller who may read the saved search gets chips or may define one; a chip is changed or removed by its author or by whoever may change the saved search. `Objects/Table` applies the active chips (`CustomQuickfilterSupport.Apply`).
- **Header menu *Search*** (`SearchDropdownFragment`, signed-in callers, after *Dashboards*): the caller's saved searches (starred first), *Shared with me*, and *Recent searches* - the WQL history of the object search (`IWqlHistoryManager`, subject `Object`), opening `/search?wql=…` - each group under a heading and left out when empty, served by `/api/1/savedsearches/dropdown`; below, *Search all workspaces*. The workspace and dashboard menus are labelled *Workspaces* / *Dashboards* and carry a glyph like the per-kind menus.
- After a create, `objectcreated.js` follows the answer (`{ created, id, uri }`) to the new saved search; after an edit or delete the script reloads the page, since sidebar and headline are rendered once.
- WQL's text operators compile to `string.Contains(value, StringComparison.OrdinalIgnoreCase)`, which EF Core does not translate - every `~` query against a database-backed table used to fail with `400`. `KleeneStar.Model/Interceptors/StringComparisonQueryInterceptor` (registered in `KleeneStarDbContext.OnConfiguring`) rewrites such calls into `a.ToLower().Contains(b.ToLower())` before compilation.

### Saved Search — New / Edit (Modal)

Creating or editing a saved search is done in a focused modal opened from the search field's buttons, from the sidebar (`+ New saved search`) or by double-clicking an existing entry. The form captures the name, the query in the **WQL prompt** of the search page (highlighting, completion and syntax check against the object model; a named `ControlDataWqlPrompt`, built for both dialogs by `SavedSearchFormItems`), a description in the **prose editor** (`TypeEditTextFormat.Wysiwyg`), and the starred flag. In edit mode the fields are pre-filled with the saved search's current values — the query replaced by the expression on screen when the dialog was opened from the search page running it. The endpoint refuses a query that does not compile against `Object` (`WqlFilter.TryValidate`). It answers an unwritten description as `""`: the prose editor throws on `null`, and the form fills its fields in the order of the answer, so a `null` description stopped the query after it from ever being filled.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar   Workspace ▼   Object ▼   Dashboard ▼          [+ AddObject]         │║
║└─────╔SavedSearchAddEditModal═════════════════════════════════════════════════╗─────┘║
║┌Saved║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│  + N║│ New saved search / Edit saved search                                 │║rch] │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Searc║│                                                                      │║─────┐║
║│  + N║│          Name*: [ My open incidents                                ] │║     │║
║│     ║│         Query*: [ Summary ~ "incident"                             ] │║──── │║
║│  SAV║│   Description: [ All open incidents across every workspace.        ] │║     │║
║│  ★ ║│       Starred: [✓]                                                   │║     │║
║│  ★ ║│                                                                      │║     │║
║│  Log║│                                                                      │║     │║
║│  Ser║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║│  + N║                                                       [Save] [Cancel]  ║     │║
║└─────╚════════════════════════════════════════════════════════════════════════╝─────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Saved Search — Delete (Modal)

Deleting a saved search is confirmed in a small modal opened from the entry's options. The dialog names the saved search and removes it on confirmation; it is offered to the owner and to members of a group granted the admin policy.

### Saved Search — Permissions (Modal)

`/savedsearch/{id}/permission` is the framework's permission surface (`SavedSearchPermissionFragment`, endpoints under `/api/1/savedsearch/{id}/permission|permissiongroups|permissionpolicies`, scope `savedsearch`). It offers the three policies of the scope — *View* (run), *Edit* (run and change) and *Admin* (also delete and share further) — to any group, the built-in *Signed-in users* included. `PermissionSearchFragment` names the fragment among its scopes, so the dialog has its search box.

## Sitemap

The search and saved-search surfaces are wired into the following routes. The saved-search create / edit / delete pages are opened as modals over the search page.

|Path                                    |Page                   |Description
|----------------------------------------|-----------------------|------------------------------------------------------------
|`/search`                               |Global Search          |Cross-workspace object search with WQL bar, results table, and saved-search sidebar.
|`/search?wql={query}&use={savedSearchId}`|Run saved search      |The search page with a saved query applied; `use` stamps the saved search as just used.
|`/savedsearches/add`                    |New Saved Search       |Modal form for creating a saved search owned by the calling identity.
|`/savedsearch/{savedSearchId}`          |Saved Search (canonical)|Declares the `{savedSearchId}` segment; titles the page with the saved search's name.
|`/savedsearch/{savedSearchId}/edit`     |Edit Saved Search      |Modal form for editing an existing saved search.
|`/savedsearch/{savedSearchId}/delete`   |Delete Saved Search    |Modal confirming removal of a saved search.

## API Interfaces (REST Endpoints)

Search and saved searches are exposed via versioned REST endpoints rooted at `/api/1`. They follow REST conventions, use JSON, and are protected by the standard **KleeneStar** authentication and authorization stack.

|Endpoint                              |HTTP Method |Description
|--------------------------------------|------------|------------------------------------------------------------
|`/api/1/objects`                      |GET         |Cross-workspace object search. Backs the results table; accepts the WQL/substring filter and paging and returns matching objects from every workspace.
|`/api/1/objects/wql`                  |GET         |WQL prompt endpoint. Provides WQL autocomplete and a short history of recent expressions for the search bar.
|`/api/1/savedsearches`                |GET         |Lists / retrieves the saved searches the caller may see — own and shared (backs the add / edit / delete modal forms). `mode=new` answers the `wql` parameter as the query to start with; `mode=edit` needs `savedsearch_update` and answers `wql` in place of the stored query.
|`/api/1/savedsearches`                |POST        |Creates a saved search owned by the signed-in caller (`403` anonymously). Requires a `name` and a `query` that compiles; answers `{ created, id, uri }` with the run address.
|`/api/1/savedsearches?id={id}`        |PUT         |Updates a saved search (`savedsearch_update`, else `403`). Owner, state and — for anybody but the owner — the star are kept.
|`/api/1/savedsearches?id={id}`        |DELETE      |Deletes a saved search (`savedsearch_delete`, else `403`).
|`/api/1/savedsearch/{id}/permission`  |GET/POST/PUT/DELETE|The grants on a saved search; owner or admin policy only (else `403`).
|`/api/1/savedsearches/dropdown`       |GET         |The header search menu: own saved searches, shared ones and recent searches, grouped under headings; nothing for an anonymous caller.
|`/api/1/savedsearch/{id}/quickfilter` |GET/POST/PUT/DELETE|The quickfilter bar of a saved search (readers only; change/remove: author or who may change the saved search).
|`/api/1/objects/table?use={id}`       |GET/PUT     |The results of the page with the columns (and their storage) and quickfilters of that saved search.
|`/api/1/savedsearches/table`          |GET         |REST table over the saved searches the caller may see; the row options follow the caller's permissions; supports a `q` substring search by name and a `qf_starred` quickfilter.

Standard error responses include `400 Bad Request` for validation errors (e.g. a missing name or query), `401 Unauthorized` for missing authentication, `403 Forbidden` for insufficient permissions, and `404 Not Found` for an unknown saved search. A successful creation (POST) is acknowledged with `201 Created`; a successful deletion (DELETE) results in `204 No Content`.

## Search / Saved Search Events

The `SavedSearchManager` publishes the following events via the **WebExpress** `EventManager`. UI components, analytics pipelines, and other subsystems can subscribe to react to changes without depending on the manager directly:

|Event Name             |Description
|-----------------------|----------------------------------------------------------------
|`SavedSearchAdded`     |Triggered when a new saved search has been persisted.
|`SavedSearchUpdated`   |Signals an edit, a recorded run (`LastUsed`), or a starred-flag change.
|`SavedSearchRemoved`   |Indicates the removal of a saved search.

Each event payload carries the affected `SavedSearch` entity, allowing subscribers to invalidate caches or refresh the navigation and sidebar surfaces without re-querying.

## Permission Model

Search has no permission table of its own. Two existing layers govern what a search can reach and who owns a saved search:

- **Result visibility.** The cross-workspace results are produced by the object search endpoint and are subject to the existing object / workspace read permissions. A user only ever sees objects in workspaces they may read (`workspace_read` / `workspace_read_content` / `object_read`); the global scope of the search does not widen those grants.
- **Saved-search ownership and sharing.** Every saved search carries an `OwnerId`; the owner may do everything with it. Anybody else needs a **grant on the saved search** (scope `PermissionScope.SavedSearch`) whose policy carries the permission — and, unlike every other scope, **a saved search nobody shared is private**, not open: `ISavedSearchManager.IsGranted` answers the owner with yes and everybody else through `IPermissionManager.GetGrantedIds`, which only counts resources that carry a grant. A deleted saved search and nobody (`Guid.Empty`) are refused. `WebRestApi/SavedSearchAuthorization` asks the same question for the endpoints (`403`), `RouteAuthorization` for the dialogs (area `savedsearch(es)`: signed in, plus the permission the dialog needs), and the search page runs a saved search only for a caller who may read it. Lists read "own or shared"; a request naming a saved search the caller may not see resolves to nothing (`404`). The star and the recently-used stamp stay the owner's.

|Permission                |Description
|--------------------------|-----------------------------------------------------------------------------------
|`object_read`             |Required for an object to appear in the search results. Search never bypasses per-object/workspace read grants.
|`savedsearch_read`        |See and run a saved search somebody else owns (policies *view*, *edit*, *admin*).
|`savedsearch_update`      |Change its name, query and description (*edit*, *admin*).
|`savedsearch_delete`      |Delete it (*admin*).
|`savedsearch_manage_profiles`|Open its permission dialog and share it further (*admin*).

Creating a saved search needs a signed-in caller, who becomes its owner; the owner holds all four without a grant.

## Conclusion

This document describes search in **KleeneStar** as a single cross-workspace surface built on WQL over the object index, plus a thin per-identity saved-search layer on top. The reference implementation comprises the global search page (`/search`) composed from page-scoped fragments, the unscoped object table and WQL prompt endpoints that feed it, the `SavedSearch` entity and `SavedSearchManager` component, the saved-search REST endpoints and modal pages, and a seeded set of example saved searches. The model is intentionally narrow — global read-scoped results, private-until-shared saved searches, a two-state lifecycle — so future additions (faceted filters, search analytics) can be layered on without revisiting the storage schema.
