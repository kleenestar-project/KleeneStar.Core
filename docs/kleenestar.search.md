![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Search and Saved Searches

Search in **KleeneStar** is a single, cross-cutting surface that queries objects across *every* workspace at once — independent of the per-workspace object views. Where the workspace content pages scope their tables to one workspace, the global search page runs an unscoped query over the whole object index and returns matches from all workspaces side by side. The goal is one fast, consistent "find anything" entry point that is always reachable from the application header.

Search is driven by **WQL** (the WebExpress query language) over the server-side reverse index. A query such as `Summary ~ "incident"` or `Key ~ "SD-9000"` is parsed and evaluated against the indexed object fields (key, summary, description, metadata) and returns a paginated result set. The search bar offers WQL autocomplete and a short history of recent expressions.

On top of the ad-hoc search sits the **saved search** concept: a named, reusable query that a single identity can star, run, edit, and delete. Saved searches are personal — each identity only ever sees its own. They back two surfaces: the search-page sidebar (all of the owner's saved searches, starred first) and the "recently used" navigation list. A saved search records the moment it was last run so the recency ordering stays current.

The `SavedSearchManager` is responsible for the lifecycle of saved searches. It ensures that:
- Saved searches are personal, valid, and attributable (every saved search has an owner and a WQL query).
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

- Key attributes: id (stable Guid), name, description (optional), query (WQL string), starred (bool), lastUsed (DateTime), state, created, updated.
- Required reference: owner (the identity the saved search belongs to). Saved searches are personal; there is no sharing model.

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
- The results table is a REST table fed by the unscoped object table endpoint (`/api/1/objects`). It returns objects from **every** workspace, with the columns **Key**, **Summary**, **Workspace**, and **Description**. Pagination is bound to the table so paging re-queries server-side.
- The sidebar lists a `+ New search` entry (clears any applied query), the owner's saved searches (starred ones first, each prefixed with a ★), and a `+ New saved search` action. Clicking a saved search runs it; double-clicking opens its edit modal.
- Running a saved search navigates to the search page with the saved query applied and the saved-search id flagged for recency tracking (`/search?wql=<query>&use=<id>`). The `use` flag makes the page stamp the saved search as just used so the recently-used ordering stays current.

### Saved Search — New / Edit (Modal)

Creating or editing a saved search is done in a focused modal opened from the sidebar (`+ New saved search`) or by double-clicking an existing entry. The form captures the name, the WQL query, an optional description, and the starred flag. In edit mode the fields are pre-filled with the saved search's current values.

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

Deleting a saved search is confirmed in a small modal opened from the entry's options. The dialog names the saved search and removes it on confirmation; because saved searches are personal, the action only ever affects the calling identity's own data.

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
|`/api/1/savedsearches`                |GET         |Lists / retrieves the calling identity's saved searches (backs the add / edit / delete modal forms).
|`/api/1/savedsearches`                |POST        |Creates a new saved search owned by the calling identity. Requires at least a `name` and a `query` (WQL).
|`/api/1/savedsearches/{savedSearchId}`|PUT         |Updates the metadata of a saved search (`name`, `query`, `description`, `starred`).
|`/api/1/savedsearches/{savedSearchId}`|DELETE      |Deletes a saved search owned by the calling identity.
|`/api/1/savedsearches/dropdown`       |GET         |Returns the calling identity's most recently used saved searches as dropdown items (newest first).
|`/api/1/savedsearches/table`          |GET         |REST table over the owner's saved searches; supports a `q` substring search by name and a `qf_starred` quickfilter.

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
- **Saved-search ownership.** Saved searches are strictly personal. Every saved search carries an `OwnerId`, and ownership is enforced on every access path. The sidebar and "recently used" surfaces are fed by the manager's owner-filtered `GetForOwner` / `GetRecent` retrievals, and the by-id CRUD endpoint (`/api/1/savedsearches`) scopes every item lookup — list, retrieve, edit-form, update, delete, and clone-source — to the calling identity's `OwnerId`, so a request naming another identity's saved search resolves to nothing and returns `404 Not Found` instead of acting on foreign data. A user can therefore only list, run, edit, star, and delete their own saved searches. There is no cross-user sharing model.

|Permission                |Description
|--------------------------|-----------------------------------------------------------------------------------
|`object_read`             |Required for an object to appear in the search results. Search never bypasses per-object/workspace read grants.
|`savedsearch_manage_own`  |Create, edit, star, run, and delete the calling identity's own saved searches. Implicit for every authenticated identity.

## Conclusion

This document describes search in **KleeneStar** as a single cross-workspace surface built on WQL over the object index, plus a thin per-identity saved-search layer on top. The reference implementation comprises the global search page (`/search`) composed from page-scoped fragments, the unscoped object table and WQL prompt endpoints that feed it, the `SavedSearch` entity and `SavedSearchManager` component, the saved-search REST endpoints and modal pages, and a seeded set of example saved searches. The model is intentionally narrow — global read-scoped results, personal (un-shared) saved searches, a two-state lifecycle — so future additions (shared/team saved searches, faceted filters, search analytics) can be layered on without revisiting the storage schema.
