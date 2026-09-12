![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Object Management Concept

Object management in **KleeneStar** forms the central foundation for structured, traceable, and scalable data management. An object represents a concrete instance of a class and corresponds to an individual record, such as an incident, a document, or any business entity. Objects are always assigned to a workspace and inherit its structural, security-related, and organizational constraints.

An object aggregates business data values, relationships to other objects, and accompanying metadata such as comments, versioning, file attachments, or audit information. This combination enables a holistic view of the data context and supports both operational and analytical requirements.

The object management is fully implemented server-side, multi-tenant, and audit-proof. The lifecycle of an object in **KleeneStar** essentially consists of the states "active" and "archived". Within the active state, however, an object can traverse various status values controlled by the workflow of the associated class. This workflow defines states, transitions, validation rules, and the business logic linked to them. This makes it possible to model and automate complex processes.

Interaction with objects takes place via dynamically generated forms that ensure consistent, validated, and role-based data entry and presentation. The form logic takes into account both the structure of the class and the current object configuration and workflow state. In addition, REST APIs and event-based interfaces are available to integrate external systems and enable automated processes.

The object model in **KleeneStar** combines semantic modeling at the class level with operational data management at the instance level. It thereby creates a robust, rule-based system that ensures both business consistency and technical extensibility.

## Lifecycle and States

Object management in **KleeneStar** is based on a dual lifecycle model that considers both administrative and business aspects. The administrative lifecycle of an object comprises the states "active" and "archived", while the business progression is controlled by the workflow associated with the class.

An object in the "active" state is productively usable and passes through the status values defined in the workflow within this framework, such as "Open", "In Progress", or "Closed". These status values reflect the business dynamics of the object and enable regular operations as well as defined state transitions. The workflow allows flexible modeling of processes without altering the overarching lifecycle.

If an object is no longer needed actively, it can be transferred to the "archived" state. In this mode, the object is write-protected and serves for historization. All data and relationships are retained and remain available for reporting or traceability. If required, an archived object can be restored and returned to the active state.

Additionally, an object can be permanently deleted. This step can be performed from either the active or archived state and is irreversible. The deletion is executed immediately.

The following state diagram illustrates this model and shows the possible transitions between lifecycle states as well as the embedding of the business status logic within the active area:

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                            KleeneStar Object State Diagram                           ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                               ┌───────────────────┐                                  ║
║                               │     archive       │                                  ║
║                      new  ╔════════╗         ┌────▼─────┐                            ║
║                        ───► active ║         │ archived │                            ║
║                           ╚══════▲═╝         └─┬──────┬─┘                            ║
║                             │    │   restore   │      │                              ║
║                             │    └─────────────┘      │                              ║
║                             │                         │                              ║
║                             │      ╔═════════╗        │                              ║
║                             └──────► deleted ◄────────┘                              ║
║                                    ╚═════════╝                                       ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Data Model

The object data model is a central component of the **KleeneStar** Core Data Models and forms the basis for structured management of business content. Each object is an instance of exactly one class and inherits its field definitions, ensuring a consistent and typed data structure.

The concrete values of an object are stored in so-called `Value` instances. These provide the link between an object and a specific field of its class and enable the storage and validation of data content. Relationships between individual objects (such as links, references, or hierarchies) are represented via `Link` instances, which define both the direction and the type of relationship.

To improve traceability and documentation, objects can be enriched with additional metadata. These include comments (`Comment`) for content explanations, versions (`Version`) for change history, and file references (`FileReference`) to attach external or internal documents. These modular extensions make the data model not only transparent and auditable, but also flexible and extensible for complex application scenarios.

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
║    ┌───────────┐  ┌──────┐ *    2 ┌─▼────┴─┐ 1    * ┌───┴───┐        ┌───┴────┐      ║
║    │ Dashboard │  │ Link ├────────► Object ◄────────┤ Value │        │ Change │      ║
║    └─────┬─────┘  └──────┘        └─▲──▲──▲┘        └───────┘        └───┬────┘      ║
║          │ 1                      1 │  │ 1│ 1                            │ *         ║
║          │             ┌────────────┘  │  └──────────────┐    ┌──────────┘           ║
║          │ *           │ *             │ *               │ *  │ 1                    ║
║     ┌────▼───┐    ┌────┴────┐    ┌─────┴─────────┐     ┌─┴────▼─┐                    ║
║     │ Widget │    │ Comment │    │ FileReference │     │ Commit │                    ║
║     └────────┘    └─────────┘    └───────────────┘     └────────┘                    ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Persistence

The **class is the single source of the kind**: `Class.Kind` is chosen in the class add/edit/clone forms ("Object type"), and every object of the class carries that kind. The `ObjectManager` stamps the class kind onto `Object.Kind` on every add and update, and changing a class's kind re-stamps the existing objects of the class, so the kind overviews immediately reflect the change. The object-side copy exists purely for efficient querying (the kind overviews filter on the indexed `Object.Kind` column).

Both kinds are persisted as plain, lower-case string keys (defaulting to `issue`) rather than enums. This keeps the set of kinds **open**: add-ons may introduce further keys without touching the data layer, without a schema change, and without the ordinal-shift hazards of persisted enums. Unknown keys survive in the database, so content of an add-on kind outlives the add-on itself. The `ObjectKind` class in the model defines the well-known keys and the normalization rule (trim, lower-case, empty → `issue`).

### Extensibility (add-ons)

The UI-side counterpart of the persisted key is the `IObjectKind` descriptor (key, i18n label, icon, order, overview route, and the per-kind **detail route** `DetailUri(objectKey)` plus an optional **edit route** `EditUri(objectKey)`) registered in the `ObjectKindCatalog`. Because each kind now owns its own detail view (see *Object Detail View* below), `ObjectKindCatalog.ResolveDetailUri(object)` and `ResolveEditUri(object)` are the **single dispatch points** every object link uses to reach the view matching an object's kind; an unknown key (e.g. of an uninstalled add-on) falls back to the issue route. The core registers its three built-in kinds; an add-on introduces a new kind by:

1. implementing `IObjectKind` — including `DetailUri`/`EditUri` — and calling `ObjectKindCatalog.Register(...)` from its plugin initialization,
2. contributing an overview page for the kind (a workspace-scoped page like `WWW/Documents/{workspacekey}`) and a detail page (an object-scoped page like `WWW/Document/{objectkey}`) that `DetailUri` returns,
3. deriving a sidebar link from `ObjectKindSidebarLinkFragment` and scoping it to the kind overviews and the per-kind detail pages, and
4. contributing the kind's view fragments scoped to its overview and detail pages.

Because the sidebar links and views compose declaratively via Section/Scope attributes, no core code changes are needed. The catalog does not gate persistence — it is the lookup UI components use to resolve a key to its presentation. Registered kinds automatically appear as options in the "Object type" selection of the class forms.

### Navigation and starring

Every per-kind detail page carries the **same sidebar as its kind overview** — the workspace header and icon, the kind links (Documents, Blogs, Issues), the workspace settings toolbar, and, for documents and blogs, the document tree resp. blog timeline — so navigation stays continuous when moving from the overview into an object. In that tree/timeline the entry of the currently displayed document (or post) is highlighted as **active** and the path down to it is expanded so it is on screen. The kinds thus stay switchable from everywhere. The issue overview is the workspace's content landing page; the former objects overview has been removed entirely (its `/objects/{workspacekey}` route no longer exists), and the organize dialog now lives under `/issues/{workspacekey}/organize`. Only the add-object wizard keeps its `/objects/add` route. Every identity can additionally **star** objects: the star toggle lives in the object headline's more menu and in the issue overview's row menu, and starred issues surface under the issue overview's "Starred" quickfilter. The star is stored per identity on the object visit row (`ObjectVisit.Favorite`), mirroring the combined favorite/recency design of the workspace bookmark.

### Known limitations

- The kind is class-wide by design: an object cannot override the kind of its class. To move a single object into another kind overview, move it to a class of that kind.
- The document and blog detail views show the title and the body of the record (plus a date/author line for posts), without the comment thread or the attachment area of the issue view. To capture a discussion on such an object, model it with a class of the issue kind. *Resolved since:* which surface the body is read and written through is no longer fixed per kind — the class's [renderer](kleenestar.renderer.md) decides between the prose editor and the structured mask of the class's forms, so a document with structured fields is a document class rendered as a form. *Resolved since:* the reading view behind an editing dialog is brought up to date — every object detail page reloads after a dialog has changed the record it shows (the structured edit mask, the prose editor's publish, the security-level and organize dialogs), the same way a workflow transition on the page already ends in a reload; `IncludeObjectRefreshScript` and `ObjectRefreshFragment` are the two halves, and a dialog that writes something else on the page is left alone.
- The document tree and blog timeline in the sidebars render the first 200 objects of their kind ("top 200") to keep the pages responsive; there is no paging within the trees yet.
- The home page a workspace's document overview opens on **is** designated: `Workspace.HomeId`, answered by `IWorkspaceManager.GetHome`. Unset — and for a choice whose document has since been deleted — it falls back to the first root of the tree by summary, which is what the overview did before the setting existed. The choice belongs to the workspace, not to the person who made it: everybody opening the overview lands on the same page.
- It is chosen from the **overview's** more menu (*choose home page*), which opens a modal listing every document of the workspace, not from a menu on a document. Standing on one page and being told "make this the home page" means changing the whole workspace from inside one of its documents, and the page one is reading is the last place from which the alternatives are visible. Beside that menu sits the button that opens the home document itself.
- The issue overview filters and pages in memory (the starred scope is a per-identity projection a WebIndex query cannot express); the advanced-search box only feeds the plain-text `q` filter — full WQL is not wired to this projection.

## Software Architecture

The architecture of object management follows a modular and decoupled principle. The central control element is the `ObjectManager`, which is responsible for the entire lifecycle and access to all objects within a workspace. It manages object instances, provides a controlled interface for all interactions, and is closely integrated with the `ClassManager`, `FieldManager`, and `WorkflowManager`.

New objects are created exclusively through the `ObjectManager`. This process ensures that the object is correctly initialized, assigned to a class, and given an initial workflow status. For a reactive and loosely coupled architecture, the `ObjectManager` emits events such as `ObjectCreated`, `ObjectUpdated`, and `ObjectDeleted`. Other system components can subscribe to these events to react to changes without being directly dependent on the manager.

The `ObjectManager` handles server-side tasks such as persistent, transactional, and versioned storage of all objects and their values. On every access, it checks the permissions of the calling user or module. Access control is governed by the consolidated permissions derived from workspace, class, and field.

An integrated audit system logs all relevant actions around objects: accesses, value changes, status transitions, and permission checks are recorded with timestamp, user identity, object ID, and action type. This data supports analytics, troubleshooting, compliance checks, and state restoration.

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
║         ┌-----------┤ IObjectManager                        │                        ║
║         ¦           ├───────────────────────────────────────┤                        ║
║         ¦           │ ObjectCreated:Event                   │                        ║
║         ¦           │ ObjectUpdated:Event                   │                        ║
║         ¦           │ ObjectDeleted:Event                   │                        ║
║         ¦           │ ObjectTransitioned:Event              │                        ║
║         ¦           ├───────────────────────────────────────┤ 1                      ║
║         ¦           │ GetObject(key):IObject                ├───────┐                ║
║         ¦           │ GetObjects(query):IEnumerable<IObject>│       │                ║
║         ¦           │ CreateObject(class, values):IObject   │       │                ║
║         ¦           │ UpdateObject(key, values):IObject     │       │                ║
║         ¦           │ DeleteObject(key):void                │       │                ║
║         ¦           │ TransitionObject(key, transition):    │       │                ║
║         ¦           │   IObject                             │       │                ║
║         ¦           └───────────────────────────────────────┘       │                ║
║         ¦                                                           │                ║
║         ¦                                                           │                ║
║         ¦                            ┌───────────────┐              │                ║
║         ¦                            │ <<Interface>> │              │                ║
║         ¦                            │ IModel        │              │                ║
║         ¦                            ├───────────────┤              │                ║
║         ¦                            └───────Δ───────┘              │                ║
║         ¦                                    ¦                      │                ║
║         ¦                                    ¦                      │                ║
║         ¦               ┌────────────────────┴───────────────┐ *    │                ║
║         ¦               │ <<Interface>>                      ◄──────┘                ║
║         ¦               │ IObject                            │                       ║
║         ¦               ├────────────────────────────────────┤                       ║
║         ¦               │ Id:Guid                            │                       ║
║         ¦               │ Key:String                         │                       ║
║         ¦               │ Summary:String                     │                       ║
║         ¦               │ Description:String                 │                       ║
║         ¦               │ Class:IClass                       │                       ║
║         ¦               │ Workspace:IWorkspace               │                       ║
║         ¦               │ State:IWorkflowState               │ 1                     ║
║         ¦               │ Values:IEnumerable<IValue>         ├─────────┐             ║
║         ¦               │ Created:DateTime                   │         │             ║
║         ¦               │ Updated:DateTime                   │         │             ║
║         ¦               │ PermissionsProfiles:               │         │             ║
║         ¦               │   IEnumerable<IPermissionsProfile> │         │             ║
║         ¦               └──────────────────Δ─────────────────┘         │             ║
║         ¦                                  ¦                           │ *           ║
║         ¦                                  ¦                   ┌───────▼───────┐     ║
║         ¦                                  ¦                   │ <<Interface>> │     ║
║         ¦                                  ¦                   │ IValue        │     ║
║         ¦                                  ¦                   ├───────────────┤     ║
║         ¦                                  ¦                   │ Id:Guid       │     ║
║         ¦                                  ¦                   │ Field:IField  │     ║
║         ¦                                  ¦                   │ Value:Object  │     ║
║         ¦                                  ¦                   └───────────────┘     ║
║         ¦ create        ┌──────────────────┴─────────────────┐                       ║
║         └---------------► Object                             │                       ║
║                         ├────────────────────────────────────┤                       ║
║                         │ Id:Guid                            │                       ║
║                         │ Key:String                         │                       ║
║                         │ Summary:String                     │                       ║
║                         │ Description:String                 │                       ║
║                         │ Class:IClass                       │                       ║
║                         │ Workspace:IWorkspace               │                       ║
║                         │ State:IWorkflowState               │                       ║
║                         │ Values:IEnumerable<Value>          │                       ║
║                         │ Created:DateTime                   │                       ║
║                         │ Updated:DateTime                   │                       ║
║                         │ PermissionsProfiles:               │                       ║
║                         │   IEnumerable<IPermissionsProfile> │                       ║
║                         └────────────────────────────────────┘                       ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The **KleeneStar**.Core model describes a modular, object-oriented architecture for managing structured data objects within a systematic and extensible framework. It is based on the principle of separation of concerns and clear definition of interfaces for key functions such as object management, data modeling, and access control.

At the center is the `IComponentManager` interface, which serves as the overarching management unit. Derived from it is `IObjectManager`, a specialized interface that provides all essential operations for object lifecycle management. These include creating, fetching, updating, deleting, and transitioning objects. These operations are event-driven, meaning that corresponding events are triggered on each change, such as `ObjectCreated` or `ObjectUpdated`.

Objects themselves are described by the `IObject` interface. In addition to a unique key, they contain metadata such as class, workspace, and current state. The content data of an object is organized as `IValue` instances, each representing a field and its value. Additionally, objects can be equipped with permission profiles (`IPermissionsProfile`) to define granular access rights.

The architecture also foresees a clear separation between interfaces and implementations. `IObject` is implemented by the concrete object class, which provides all defined properties and methods. This separation enables high flexibility and extensibility of the system.

Beyond its class, every object carries a kind, a coarse subtype that decides how the object is presented and navigated. The kind partitions the objects of a workspace into content families with distinct overview experiences:

- **Document:** This kind is designed for structured knowledge organization and permanent documentation. Content is arranged in a hierarchical parent-child page tree, which is mirrored in the sidebar navigation, while the main panel opens with the designated home document. Typical use cases include internal wikis, standard operating procedures, manuals, and meeting notes.
- **Blog:** This kind handles time-sensitive updates, corporate communication, and chronological announcements. The sidebar displays a timeline sorted by year and month with the newest entries listed first, while the main panel serves a stream of the latest articles. Common examples are company news, product launch logs, and team updates.
- **Issue:** This kind manages active work items, task lifecycles, and operational workflows. It serves as the default behavior for unclassified objects and features dynamic, tabbed views including Kanban boards, Scrum backlogs, and lists that users can customize via templates. It is heavily used for tracking software bugs, customer incidents, and project tasks.
- **Asset:** This category represents digital files and binary resources uploaded from external sources, strictly distinguishing them from physical capital assets or financial asset management. Digital assets do not possess their own navigation structure but are instead attached to or embedded within documents, blogs, or issues to support them as reference materials. Examples include images, PDF documents, spreadsheets, and video clips.


## UI Concepts and Pages

The **KleeneStar** WebApp user interface translates the abstract data models and lifecycle rules of object management into a concrete and user-friendly application. The goal is to enable intuitive, efficient, and secure interaction with structured objects and their metadata.

The design follows the established UI patterns of the **KleeneStar** WebApp. This creates strong familiarity, easing onboarding and significantly shortening the learning curve. The planned UI mockups serve as visual drafts for later implementation. They define:

- navigation within the application,
- the arrangement of controls,
- the presentation of different system states.

They illustrate how users are guided through the application to perform common tasks, such as creating new objects, viewing and editing existing entries, performing workflow transitions, or deleting objects.

### Object Management - Object Search (Page)

The global search feature, accessible via the main navigation, is a central tool for fast and precise information retrieval. It enables full-text search across all objects and fields that are visible to the user according to their permissions. The search is workspace-wide, meaning it spans all workspaces to which the user has access, and delivers consolidated results independent of the currently open context. In addition, precise filter criteria such as class, status, priority, or assignee can be combined to further narrow down the results.

The search page offers an interactive interface where queries can be formulated directly and results are displayed immediately. The search syntax is based on WQL (WebExpress Query Language) and can be saved and reused if needed. The result list shows a compact overview of matches, including status and relevance, and allows direct navigation to the respective object detail.

The search supports both temporary filters (e.g., "Recently updated") and user-defined favorites, making frequently used queries quickly accessible. Actions such as "Edit", "Clone", or "Delete" are available directly from the result list, depending on the user’s permissions.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Search                                                                           │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Filter────────────────┐ ┌Search Content─────────────────────────────────────────────┐║
║│             [Search] │░│                                                           │║
║│ Filters:             │░│ WQL: [ Text~"vpn-gateway" and Assignee="Max Power"][Save] │║
║│ - All                │░│                                                           │║
║│ - Active             │░│ Summary                           | Status | Impact     + │║
║│ - Archived           │░│-----------------------------------|--------|--------------│║
║│ - My Objects         │░│ VPN connection disrupted          | Open   | High     […] │║
║│ - Created recently   │░│ ▼ VPN Access for new employee     | Done   | Medium    ¦  │║
║│ - Updated recently   │░│  - AD account created for new empl| Done  ┌────────────┴┐ │║
║│ - Viewed recently    │░│  - VPN credentials sent via email | Done  │ Edit        │ │║
║│                      │░│                                           │ Clone       │ │║
║│ Favorites:           │<│                                   ‹ Prev  │ Permissions │ │║
║│ - My Tasks           │<│                                           │ <section>   │ │║
║│ - New                │<│                                           ├─────────────┤ │║
║│ - In Progress        │░│                                           │ Delete      │ │║
║│ - Resolved           │░│                                           └─────────────┘ │║
║│                      │░│                                                           │║
║│ My Filters:          │░│                                                           │║
║│ - All Tasks          │░│                                                           │║
║│ - My Tasks           │░│                                                           │║
║├──────────────────────┤░│                                                           │║
║│ [+] | [Settings]  << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Object Overview (Page)

The object overview page is the central view for displaying objects of a specific class within a workspace. It provides a tabular or card-based list that can be searched, filtered, and sorted. Each object is displayed with its most important attributes. New objects can be created via the "+ Add Object" button. Context menus on each object provide direct access to actions such as editing, deleting, or performing workflow transitions.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk                                                                     │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workspace─────────────┐ ┌Object Content─────────────────────────────────────────────┐║
║│[Name]                │░│ Incident                                              […] │║
║│                      │░│ View Dashboard Scrum +                                    │║
║│      [Icon]          │░│ ────                                   [Table|Tile|Split] │║
║│                      │░│                                                  [Search] │║
║│           [ Search ] │░│ Summary                          | Status  | Impact     + │║
║│ Issue                │░│----------------------------------|---------|--------------│║
║│ ├─ Incident          │░│ VPN connection disrupted         | Open    | High     […] │║
║│ ├─ Problem           │░│ Outlook won't start              | Open    | Medium    ¦  │║
║│ └─ ServiceRequest    │░│ Printer on floor 3 offline       | Assigne┌────────────┴┐ │║
║│                      │░│ File upload fails                | In Prog│ Edit        │ │║
║│                      │░│ Remote desktop not reachable     | Open   │ Clone       │ │║
║│                      │<│ Password reset not possible      | Closed │ Permissions │ │║
║│                      │<│ Wi-Fi outage in conference room  | Open   │ <section>   │ │║
║│                      │<│ Teams notifications delayed      | Assigne├─────────────┤ │║
║│                      │░│ Scanner not sending PDFs         | Assigne│ Delete      │ │║
║│                      │░│ SharePoint access denied         | Open   └─────────────┘ │║
║│                      │░│ Software update blocks startup   | In Prog.| High     […] │║
║│                      │░│                                                           │║
║│                      │░│                                   ‹ Prev  1  2  3  Next › │║
║├──────────────────────┤░│                                                           │║
║│ [+] | [Settings]  << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Object Detail View (Page)

Each **kind** owns its own detail view, reached at a kind-specific route resolved through `ObjectKindCatalog.ResolveDetailUri` (see *Object Kinds*):

- **Issue** — `/issue/{objectKey}`: the full, form-driven work-item view described in the rest of this section and shown below.
- **Document** — `/document/{objectKey}` (reading) and `/document/{objectKey}/edit` (editing).
- **Blog** — `/blog/{objectKey}` (reading) and `/blog/{objectKey}/edit` (editing).

Opening any object link routes to the view matching that object's kind; requesting the wrong route for an object (e.g. a document under `/issue/…`) transparently redirects to the correct one.

The **issue** detail view is presented via a form defined in the form management for the respective object class. This view displays all fields and metadata of the object in a structured form and provides a comprehensive overview of the current state and context. The page is typically divided into sections to present different information areas such as attributes, linked objects, comments, attachments, and version history clearly.

Workflow transitions are prominently offered as actions, enabling the user to advance the process directly from the detail view. These actions are context-sensitive and may offer different options depending on the status and role.

A central component of the detail view is the comment area. Users can add new comments, reply to existing posts, and build a chronological communication history for the object. This supports transparent documentation of decisions, inquiries, and processing steps.

Handling attachments is also user-friendly: files can be dragged and dropped directly into the window, where they are automatically associated with the object and displayed in the "Attachments" section. This makes it easy to quickly add screenshots, documents, or other relevant files without needing separate upload steps.

For the **document** and **blog** kinds the detail view is deliberately reduced to prose. The **reading view** presents the object's title (in the headline) and its rich-text body — a blog post additionally shows a date/author line — without the form-driven field structure, comment thread, or attachment area of the issue view. An **Edit** button in the headline opens the **editing view as a fullscreen modal** (hosted by the object's `/…/edit` route): a focused form for just the title and body that persists through the same object REST endpoint and, like the issue edit modal, closes automatically on a successful save, returning to the reading view. The document reading page keeps the workspace's document tree in the sidebar (blogs keep their timeline), so navigation within the kind is preserved while reading.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / INC-00123                                                         │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workspace─────────────┐ ┌Object Content────────────────────────────┬────────────────┐║
║│[Name]                │░│                                          │                │║
║│                      │░│ INC-00123: VPN connection disrupted  […] │   Status: Open▼│║
║│      [Icon]          │░│                                       ¦  │ Priority: High │║
║│                      │░│ Description: Users cann┌──────────────┴┐.│ Assignee: Max  │║
║│           [ Search ] │░│ Affected CI: vpn-gatewa│ Edit          │ │           Power│║
║│ Issue                │░│ ...                    │ Clone         │ │  Created: 2025 │║
║│ ├─ Incident          │░│                        │ Add Link      │ │           -01  │║
║│ ├─ Problem           │░│ ⠿ Attachments:         │ Add Subobject │ │           -16  │║
║│ └─ ServiceRequest    │░│   - Screenshot.png     │ Show as...    │ │                │║
║│                      │░│                        │ Move          │ │  Watchers:  [+]│║
║│                      │<│                        │ Export        │ │   - Erika Mus x│║
║│                      │<│ ⠿ Comments:            │ Permissions   │ │                │║
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

### Object Management - Add/Edit Object

The Add Object workflow provides a guided, multi‑step process for creating new objects within **KleeneStar**. It begins with selecting the appropriate workspace, continues with choosing an object template, and concludes with entering the object’s data in a dynamically generated form.

When editing an existing object, this wizard is not used. Instead, the system opens the Add/Edit modal directly and loads the corresponding form for the selected object. This ensures that editing remains fast, focused, and context‑preserving.

#### Object Management - Select Workspace (Modal)

Clicking the "+ Add Object" button opens a central modal that allows the user to choose a workspace in which the new object will be created. Each workspace is presented as a card containing its name, category, and icon for quick visual recognition. Selecting a workspace determines the context for the new object and filters the available templates accordingly.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectBlueprintModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Create new object                                                    │║     │║
║└─────║├──────────────┬───────────────────────────────────────────────────────┤║─────┘║
║┌Works║│              │ Select an workspace:                         [Search] │║─────┐║
║│[Name║│ - All        │                                                       │║     │║
║│     ║│ - Category 0 │ ┌Workspaces─────────────────────────────────────────┐ │║ […] │║
║│     ║│ - Category 1 │ │                                                  ▲│ │║rch] │║
║│     ║│ - ...        │ │ ┌───────────────────┐  ┌────────────────────┐    ▒│ │║     │║
║│     ║│ - Category n │ │ │ [Icon] SALE       │  │ [Icon] SD          │    ░│ │║-----│║
║│ Issu║│              │ │ │ Sales             │  │ IT Service Desk    │    ░│ │║ […] │║
║│ ├─ I║│              │ │ │ Operations        │  │                    │    ░│ │║ […] │║
║│ ├─ P║│              │ │ └───────────────────┘  └────────────────────┘    ░│ │║ […] │║
║│ └─ S║│              │ │                                                  ░│ │║ […] │║
║│     ║│              │ │ ┌───────────────────┐  ┌────────────────────┐    ░│ │║ […] │║
║│     ║│              │ │ │ [Icon] CMDB       │  │ [Icon] DEV         │    ░│ │║ […] │║
║│     ║│              │ │ │ Configuration     │  │ Software           │    ░│ │║ […] │║
║│     ║│              │ │ │ Database          │  │ Development        │    ░│ │║ […] │║
║│     ║│              │ │ └───────────────────┘  └────────────────────┘    ░│ │║ […] │║
║│     ║│              │ │                                                  ░│ │║ […] │║
║│     ║│              │ │ ┌───────────────────┐  ┌────────────────────┐    ░│ │║ […] │║
║│     ║│              │ │ │ [Icon] FIN        │  │ [Icon] HR          │    ▼│ │║ […] │║
║│     ║│              │ └───────────────────────────────────────────────────┘ │║     │║
║│     ║└──────────────┴───────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                       [Next] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```


#### Object Management - Templates (Modal)

After selecting a workspace, the user is presented with a modal displaying all available object templates. Each template is shown as a card that includes the class name, a short description, and an icon to improve recognizability. Choosing a template initializes a new object with predefined fields, default values, and configuration settings defined in the class blueprint.

The step always offers a way on: beside the templates of the chosen class stands a *no template* card, which is what a class nobody has written a template for is left with. When it is the only card left — the class has no templates — the step **answers itself**: the picker selects the sole remaining choice, so the user is not made to click the one card that can be clicked. The choice is taken back the moment a class with templates is picked instead, and a card the user chose is never overridden. This is the tile picker's general rule for a required single choice with one answer (`InputTileCtrl`, WebExpress.WebUI), not something the wizard does on its own.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectBlueprintModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Create new object                                                    │║     │║
║└─────║├──────────────┬───────────────────────────────────────────────────────┤║─────┘║
║┌Works║│              │ Select an object template:                   [Search] │║─────┐║
║│[Name║│ - All        │                                                       │║     │║
║│     ║│ - Category 0 │ ┌BlueprintGrid──────────────────────────────────────┐ │║ […] │║
║│     ║│ - Category 1 │ │                                                  ▲│ │║rch] │║
║│     ║│ - ...        │ │ ┌───────────────────┐  ┌────────────────────┐    ▒│ │║     │║
║│     ║│ - Category n │ │ │ [Icon] Incident   │  │ [Icon] Problem     │    ░│ │║-----│║
║│ Issu║│              │ │ │        Service    │  │        Technical   │    ░│ │║ […] │║
║│ ├─ I║│              │ │ │        disruption │  │        defect      │    ░│ │║ […] │║
║│ ├─ P║│              │ │ └───────────────────┘  └────────────────────┘    ░│ │║ […] │║
║│ └─ S║│              │ │                                                  ░│ │║ […] │║
║│     ║│              │ │ ┌───────────────────┐  ┌────────────────────┐    ░│ │║ […] │║
║│     ║│              │ │ │ [Icon] ServiceReq │  │ [Icon] Change      │    ░│ │║ […] │║
║│     ║│              │ │ │        Request    │  │        Change      │    ░│ │║ […] │║
║│     ║│              │ │ │        by user    │  │        request     │    ░│ │║ […] │║
║│     ║│              │ │ └───────────────────┘  └────────────────────┘    ░│ │║ […] │║
║│     ║│              │ │                                                  ░│ │║ […] │║
║│     ║│              │ │ ┌───────────────────┐  ┌────────────────────┐    ░│ │║ […] │║
║│     ║│              │ │ │ [Icon] Release    │  │ [Icon] Task        │    ▼│ │║ […] │║
║│     ║│              │ └───────────────────────────────────────────────────┘ │║     │║
║│     ║└──────────────┴───────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                       [Next] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Object Management - New/Edit (Modal)

Creating and editing objects is handled through a modal form. For new objects, this form is reached as the final step of the creation wizard. For existing objects, the wizard is skipped entirely, and the modal opens directly in Edit mode.

The form layout is dynamically loaded from the form manager, based on the selected class and the current action (create, edit, or workflow transition). The modal mirrors the structure defined in the form designer, including all fields, layout groups, tabs, and input arrangements. All user input is validated server‑side according to field rules, workflow constraints, and class definitions before the object is saved.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectAddEditModal══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Incident / Edit Incident                                         │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│ ┌───────┬───────┬─────┬───────┬────────────────────────────────────┐ │║─────┐║
║│[Name║│ │ Tab 1 │ Tab 2 │ ... | Tab n |                                    │ │║     │║
║│     ║│ │       └───────┴─────┴───────┴────────────────────────────────────┤ │║ […] │║
║│     ║│ │                                                                  │ │║rch] │║
║│     ║│ │     Summary*: [                                                ] │ │║     │║
║│     ║│ │       Status: [ Open                                          ▼] │ │║-----│║
║│ Issu║│ │     Priority: [ Medium                                        ▼] │ │║ […] │║
║│ ├─ I║│ │     Assignee: [                                               ▼] │ │║ […] │║
║│ ├─ P║│ │         Tags: [                                                ] │ │║ […] │║
║│ └─ S║│ │  Description: [                                                ] │ │║ […] │║
║│     ║│ │ Reported At*: [ 2025-11-07 15:00]     Affected CI: [          ▼] │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ └──────────────────────────────────────────────────────────────────┘ │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Add Link (Modal)

The "Add Link" function enables linking external or internal references directly to an object. It is available within the object view and serves to present relevant information, resources, or relationships clearly and traceably.

Via the "Add Link" button, a user can enter a URL or a system-internal reference. This facilitates orientation and supports collaboration by linking to related issues, documentation, knowledge articles, or external tools, for example. All added links are displayed in the object context and are directly accessible to authorized users. Link management is dynamic. Links can be added, edited, or removed at any time. The function thus contributes significantly to transparency and structured information networking within KleeneStar.

#### Recently viewed

This area displays recently viewed objects that are particularly suitable for quick linking. The list provides a compact overview of titles and statuses of recently viewed entries. This way, frequently used or currently relevant content can be linked directly to the object without lengthy searches.

The search function also enables targeted finding of a specific entry within the recently viewed objects.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectLinkModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add link                                                             │║     │║
║└─────║├────────────────────┬─────────────────────────────────────────────────┤║─────┘║
║┌Works║│                    │ Recently viewed                        [Search] │║─────┐║
║│[Name║│ - Recently viewed  │                                                 │║     │║
║│     ║│ - Recently created │ Summary                        | Status       + │║ […] │║
║│     ║│ - Object Link      │--------------------------------|----------------│║rch] │║
║│     ║│ - Web Link         │ Scanner not sending PDFs       | Assigne        │║     │║
║│ Issu║│                    │ Screen flickers intermittently | Closed         │║-----│║
║│ ├─ I║│                    │ File upload fails              | In Prog.       │║ […] │║
║│ ├─ P║│                    │                                                 │║ […] │║
║│ └─ S║│                    │                         ‹ Prev  1  2  3  Next › │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║     │║
║│     ║└────────────────────┴─────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                        [Add] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Recently created

This area displays recently created objects, for example new incidents, change requests, or service requests. It is especially useful when linking to a newly created item, for documentation or tracking purposes.

Titles and status are provided for quick orientation. The integrated search function helps filter for a specific object and link it directly.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectLinkModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add link                                                             │║     │║
║└─────║├────────────────────┬─────────────────────────────────────────────────┤║─────┘║
║┌Works║│                    │ Recently created                       [Search] │║─────┐║
║│[Name║│ - Recently viewed  │                                                 │║     │║
║│     ║│ - Recently created │ Summary                         | Status      + │║ […] │║
║│     ║│ - Object Link      │---------------------------------|---------------│║rch] │║
║│     ║│ - Web Link         │ SharePoint access denied        | Open          │║     │║
║│     ║│                    │ VPN connection disrupted        | Open          │║-----│║
║│ Issu║│                    │ Outlook won't start             | Open          │║ […] │║
║│ ├─ I║│                    │ Remote desktop not reachable    | Open          │║ […] │║
║│ ├─ P║│                    │ Wi-Fi outage in conference room | Open          │║ […] │║
║│ └─ S║│                    │                                                 │║ […] │║
║│     ║│                    │                         ‹ Prev  1  2  3  Next › │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║     │║
║│     ║└────────────────────┴─────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                        [Add] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Object Link

In this area, internal system objects can be linked directly with each other. Selection is made via a query or filtering, for example by class types or responsibilities. This allows related tickets, tasks, or configuration items to be purposefully connected to transparently represent dependencies or relationships.

The displayed list is based on the defined search query (WQL) and shows relevant objects with title and status. The linking facilitates navigation and improves traceability of complex processes within KleeneStar.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectLinkModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add link                                                             │║     │║
║└─────║├────────────────────┬─────────────────────────────────────────────────┤║─────┘║
║┌Works║│                    │ Object Link                                     │║─────┐║
║│[Name║│ - Recently viewed  │                                                 │║     │║
║│     ║│ - Recently created │ WQL: [class=incident and assignee='Max Power']  │║ […] │║
║│     ║│ - Object Link      │                                                 │║rch] │║
║│     ║│ - Web Link         │ Summary                     | Status          + │║     │║
║│     ║│                    │-----------------------------|-------------------│║-----│║
║│ Issu║│                    │ Printer on floor 3 offline  | Assigne           │║ […] │║
║│ ├─ I║│                    │ Teams notifications delayed | Assigne           │║ […] │║
║│ ├─ P║│                    │ Scanner not sending PDFs    | Assigne           │║ […] │║
║│ └─ S║│                    │                                                 │║ […] │║
║│     ║│                    │                         ‹ Prev  1  2  3  Next › │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║     │║
║│     ║└────────────────────┴─────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                        [Add] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Web Link

External resources such as websites, documentation, or tools can be linked to the object here. The user enters a URL and can optionally add a title to clarify the context.

This function is particularly suitable for integrating knowledge articles, support portals, or external systems. Links are directly accessible to authorized users and can be edited or removed at any time. This creates structured information networking across system boundaries.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectLinkModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add link                                                             │║     │║
║└─────║├────────────────────┬─────────────────────────────────────────────────┤║─────┘║
║┌Works║│                    │ Web Link                                        │║─────┐║
║│[Name║│ - Recently viewed  │                                                 │║     │║
║│     ║│ - Recently created │  Title: [ API documentation                   ] │║ […] │║
║│     ║│ - Object           │   URL*: [ https://example.com/api-docs        ] │║rch] │║
║│     ║│ - Web Link         │                                                 │║     │║
║│     ║│                    │                                                 │║-----│║
║│ Issu║│                    │                                                 │║ […] │║
║│ ├─ I║│                    │                                                 │║ […] │║
║│ ├─ P║│                    │                                                 │║ […] │║
║│ └─ S║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║ […] │║
║│     ║│                    │                                                 │║     │║
║│     ║└────────────────────┴─────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                        [Add] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Object Transition with Form (Modal)

A modal dialog is used to perform a workflow transition. It serves the structured collection of information required for the respective transition (for example a justification, result, or additional details).

The form displayed within the modal is based on the transition form assigned to the transition. This defines which fields are displayed, whether input is mandatory, and which validations apply. This functionality is only available if a form is assigned to the transition. If no form is assigned, no dialog is shown, and the transition is performed directly.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectTransitionModal═══════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Execute Transition: Resolve                                          │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│ The object will be moved to status 'Done'.                           │║─────┐║
║│[Name║│ ┌───────┬──────────────────────────────────────────────────────────┐ │║     │║
║│     ║│ │ Tab 1 │                                                          │ │║ […] │║
║│     ║│ │       └──────────────────────────────────────────────────────────┤ │║rch] │║
║│     ║│ │                                                                  │ │║     │║
║│     ║│ │  Resolution*: [ Done                                          ▼] │ │║-----│║
║│ Issu║│ │     Assignee: [                                               ▼] │ │║ […] │║
║│ ├─ I║│ │  Description: [ Issue resolved by restarting the service.      ] │ │║ […] │║
║│ ├─ P║│ │                                                                  │ │║ […] │║
║│ └─ S║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ │                                                                  │ │║ […] │║
║│     ║│ └──────────────────────────────────────────────────────────────────┘ │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                    [Resolve] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Add Subobject (Modal)

The "Add Subobject" function enables creating a new subobject that is directly linked to the currently opened object. This connection is established via a parent link, where the main object is referenced as the parent element in the newly created subobject.

The subobject is created in a defined target class, typically intended for detailed information, subprocesses, or dependent tasks. The parent link ensures a clear relationship between main and subobject (both technically and visually). In the interface, this relationship is represented through appropriate navigation paths, links, or embedded views.

"Add Subobject" is especially helpful when complex situations need to be structured, for example incidents with associated measures, projects with subtasks, or requests with follow-up processes.

When triggering the function, the template page is displayed first, analogous to the regular object creation process. This serves to select a suitable object class for the new subobject. The selection is filtered so that only classes allowed by the `AllowedChildren` property of the current class and permitted for the user are shown. Only after selecting an allowed class is the input form opened.

The input form for the new subobject corresponds to regular object creation. However, some fields are already inherited and prefilled from the parent object to ensure consistency and efficiency in data capture.

### Object Management - Show as (Modal)

The "Show as…" function makes it possible to view the user interface from the perspective of another person. It is used to check the effect of the permission system and to understand which information is visible to a particular role or group and which is hidden.

When "Show as…" is activated, the view is adjusted as if logged in with the selected user profile. This allows differences in object display, field visibility, and action availability to be seen directly. This function is particularly helpful for administrators to test permissions, validate roles, or analyze support requests.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectShowAsModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Show as...                                                           │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│ Select a person or role whose perspective you want to adopt.         │║─────┐║
║│[Name║│ The view will be adjusted according to that person's permissions.    │║     │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                             [Search] │║     │║
║│     ║│  ▢ Max Mustermann                                                    │║-----│║
║│ Issu║│  ▢ Anna Becker                                                       │║ […] │║
║│ ├─ I║│  ▢ Jonas Richter                                                     │║ […] │║
║│ ├─ P║│  ▢ Lisa Sommer                                                       │║ […] │║
║│ └─ S║│  ▢ Tom Neumann                                                       │║ […] │║
║│     ║│  ▢ Guest user                                                        │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                 [Apply] [Cancel]       ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

It is important to note that the "Show as…" function enables a read-only perspective switch. Write actions such as editing objects, performing workflow transitions, or deleting are fundamentally disabled in this view. The purpose of the function is to check information visibility, not to act on behalf of another person.

When viewing as another person is active, this is clearly indicated at the top edge of the application window. A notice appears with the name of the assumed person and a button to return to one’s own view.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║ Viewing as: Max Mustermann [Return to own view]                                    x ║
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
```

### Object Management - Move Function (Modal)

The "Move" function makes it possible to move an existing object to another class or another workspace. This is particularly helpful when the business assignment of a process changes, an object needs to be reclassified, or organizational restructuring takes place.

When moving, all existing values and fields of the object are retained, even if they are no longer valid or intended in the new configuration. Data integrity is deliberately preserved to avoid loss of information and to enable manual post-editing.

After the move, the object is displayed in the new class and/or workspace. Only on the next edit operation is it checked whether the existing values are compatible with the new class schema. Invalid or no longer supported fields must then be adjusted or removed for the object to be saved successfully.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectMoveModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Move Object                                                          │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│ You can assign this object to another class or another workspace.    │║─────┐║
║│[Name║│                                                                      │║     │║
║│     ║│ New Workspace: [ Service Desk                                     ▼] │║rch] │║
║│     ║│     New Class: [ Incident                                         ▼] │║     │║
║│     ║│                                                                      │║-----│║
║│ Issu║│                                                                      │║ […] │║
║│ ├─ I║│                                                                      │║ […] │║
║│ ├─ P║│                                                                      │║ […] │║
║│ └─ S║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│ Note: All existing values are retained, even if they are not valid   │║ […] │║
║│     ║│       in the new configuration. On the next edit, invalid fields     │║ […] │║
║│     ║│       must be adjusted.                                              │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                       [Move] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Export (Modal)

The "Export" function enables exporting objects from the system to an external format, for example for further processing, documentation, or sharing with third parties. The function is available in the object overview and may support different formats such as CSV, Excel, or PDF, depending on context.

The export includes all fields and content that are visible to the user according to their permissions. The structure of the export follows the current view. That is, filters, column selection, and sorting directly affect the exported data. Linked objects such as subobjects or parent references can optionally be included, if configured.

The export function is read-only and does not alter any data in the system. It is especially suitable for handing off information to external parties, for archiving completed processes, or for analyzing object data outside the application. For complex objects with nested relationships, the export can be rendered as a structured report, for example with section logic or a hierarchy tree in PDF format.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectExportModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Export Object                                                        │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│ Select the desired export format and the content to be exported.     │║─────┐║
║│[Name║│                                                                      │║     │║
║│     ║│ Export format:                                                       │║rch] │║
║│     ║│   ● Excel (.xlsx)                                                    │║     │║
║│     ║│   ○ CSV (.csv)                                                       │║-----│║
║│ Issu║│   ○ PDF report                                                       │║ […] │║
║│ ├─ I║│                                                                      │║ […] │║
║│ ├─ P║│                                                                      │║ […] │║
║│ └─ S║│ Content:                                                             │║ […] │║
║│     ║│   [ ] Linked subobjects                                              │║ […] │║
║│     ║│   [ ] Comments                                                       │║ […] │║
║│     ║│   [ ] History / change log                                           │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│    Note: Exported data only includes fields you are allowed to see.  │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                     [Export] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Delete Object (Modal)

Deleting an object is a critical action that must be performed with particular care. To ensure this measure is not carried out accidentally, a modal dialog is displayed that prompts the user for explicit confirmation. In this dialog, the object to be deleted is clearly identified, for example by its key such as "INC-00123", and the user must manually enter this key into an input field. Only after correct entry is the delete function enabled. This safety measure protects against unintended data loss, since deletion cannot be undone.

As a rule, deleting objects is only recommended in exceptional cases. A better approach is to keep the object active and instead mark it as "Withdrawn", "Inactive", or "Obsolete" via a status field. This method offers several advantages: traceability is preserved, since historical data remains available, accidental deletions are avoided and there remains the option to reactivate objects or include them in reports if necessary. Using a status field enables transparent, audit-proof, and flexible management of objects without having to carry out irreversible deletion actions.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔ObjectDeleteModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Delete object                                                        │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│ Are you sure you want to delete object 'INC-00123'?                  │║─────┐║
║│[Name║│ This action cannot be undone.                                        │║     │║
║│     ║│                                                                      │║ […] │║
║│     ║│ To confirm, please type 'INC-00123' into the field below*:           │║rch] │║
║│     ║│ [                                                                  ] │║     │║
║│     ║│                                                                      │║-----│║
║│ Issu║│                                                                      │║ […] │║
║│ ├─ I║│                                                                      │║ […] │║
║│ ├─ P║│                                                                      │║ […] │║
║│ └─ S║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                     [Delete] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Object Management - Permissions Management (Modal)

Access rights to individual objects are managed via a special dialog that can be opened directly from an object’s detail view. This permissions modal serves as a central interface to assign policies to groups and thus control access to objects in a granular way.

To use the modal, users need the `object_manage_profiles` permission. After opening, they can select a group and assign an appropriate access policy (for example, view-only, edit, or full administrative rights). The available policies are divided into three levels:

- `object_view_policy` for read access
- `object_edit_policy` for editing rights
- `object_admin_policy` for full control, including rights management

All existing assignments are displayed in a table. They can be adjusted or extended at any time. To maintain an overview even with extensive permission structures, the modal offers a search function and pagination. Changes to permissions take effect immediately and are logged for traceability.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkspacePermissionsModal═══════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│  Manage Permissions for 'INC-00123'                                  │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│                                                                      │║─────┐║
║│[Name║│  Assign Group*: [ Admin ▼]                                           │║     │║
║│     ║│        Policy*: [ object_admin_policy ▼]                             │║ […] │║
║│     ║│                                                                      │║rch] │║
║│     ║│  [+ Assign]                                                          │║     │║
║│     ║│                                                             [Search] │║---- │║
║│ Issu║│                                                                      │║ […] │║
║│ ├─ I║│ Assigned Group       | Effective Policy                            + │║ […] │║
║│ ├─ P║│----------------------|-----------------------------------------------│║ […] │║
║│ └─ S║│ Any                  | object_view_policy                          X │║ […] │║
║│     ║│ IT                   | object_edit_policy                          X │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                             ‹ Prev  1  2  3  Next ›  │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                                [Done]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Sitemap

The sitemap describes the structured navigation paths for accessing objects within a workspace and forms the basis for a consistent and intuitive user experience in the application. All routes are organized hierarchically and follow the logical structure by workspaces and object classes.

Each route fulfills a specific function, from the overview of all objects of a class to detail view, editing, or deletion of individual entries. This clear structure ensures that users can always navigate between different views and actions in a traceable and efficient manner.

The sitemap supports both technical integration and conceptual design of the user interface and significantly contributes to the system’s usability and maintainability.

|Path                              |Page/View               |Description
|----------------------------------|------------------------|--------------------------------------------------------------
|`/objects`                        |Object overview         |Global, workspace-wide search for objects.
|`/objects/{workspaceKey}`         |Object overview         |Workspace‑specific search for objects.
|`/objects/{workspaceKey}/add`     |Object creation         |Opens the modal for selecting an object template to create a new object.
|`/issue/{objectKey}`              |Issue detail view       |Full, form-driven detail view of an issue (fields, workflow, comments, attachments).
|`/issue/{objectKey}/edit`         |Issue editing           |Modal form for editing an issue's metadata.
|`/issue/{objectKey}/clone`        |Issue clone             |Clone an issue.
|`/issue/{objectKey}/delete`       |Issue deletion          |Confirmation and execution of the deletion process.
|`/issue/{objectKey}/export`       |Issue export            |Export an issue in different formats.
|`/issue/{objectKey}/permission`   |Permissions management  |Manage access rights for an issue.
|`/issue/{objectKey}/print`        |Issue print             |Printable rendering of an issue.
|`/document/{objectKey}`           |Document reading view   |Prose reading view of a document (title + rich-text body).
|`/document/{objectKey}/edit`      |Document editing view   |Fullscreen modal form (opened from the reading view) for a document's title and body.
|`/blog/{objectKey}`               |Blog reading view       |Prose reading view of a blog post (date/author + body).
|`/blog/{objectKey}/edit`          |Blog editing view       |Fullscreen modal form (opened from the reading view) for a blog post's title and body.

## API Interfaces (REST Endpoints)

For programmatic interaction with object management, **KleeneStar** provides a REST-compliant API. This interface uses JSON as the data format and is fully integrated into **KleeneStar**’s authentication and authorization model. The API enables access to objects within workspaces and supports all relevant CRUD operations.

|Endpoint                                                                |HTTP Method |Description
|------------------------------------------------------------------------|------------|-----------
|`/api/1/objects/`                                                       |GET         |Performs a cross-workspace search for objects using WQL.
|`/api/1/objects/{workspaceKey}`                                         |GET         |Lists all objects in the workspace. Supports filtering (by class, fields) and pagination.
|`/api/1/objects/{workspaceKey}`                                         |POST        |Creates a new object. Requires `classKey` and initial field values in the request body.
|`/api/1/objects/{workspaceKey}?id={objectKey}`                          |GET         |Retrieves detailed information for a specific object.
|`/api/1/objects/{workspaceKey}?id={objectKey}`                          |PUT         |Updates the field values of an existing object.
|`/api/1/objects/{workspaceKey}?id={objectKey}`                          |DELETE      |Deletes an object. Requires appropriate permissions.
|`/api/1/objects/{workspaceKey}?id={objectKey}`                          |POST        |Creates a duplicate of the specified object, including all fields and settings.
|`/api/1/objects/{workspaceKey}/{objectKey}/archive`                     |POST        |Archives an object and puts it into a write-protected state.
|`/api/1/objects/{workspaceKey}/{objectKey}/restore`                     |POST        |Restores a previously archived object.
|`/api/1/objects/{workspaceKey}/{objectKey}/transitions`                 |GET         |Lists all available workflow transitions for the object.
|`/api/1/objects/{workspaceKey}/{objectKey}/transitions/{transitionKey}` |POST        |Executes a workflow transition for the object. May include field values in the body.
|`/api/1/hierarchy/{objectKey}`                                          |GET         |Returns the hierarchy of the object: the ancestor chain (nearest first), the parent, the object itself, the immediate children (with their resolved workflow status), and the siblings.
|`/api/1/hierarchy/{objectKey}`                                          |PUT         |Sets or clears the parent. Body: `{ "parent": "KEY" }` (object key or id) or `{ "parent": null }`. Validates against self-parenting, cycles, cross-workspace links, and the allowed-children declaration of the parent's class.

Standard error responses include `400 Bad Request` for validation errors (e.g., a key that is already taken), `401 Unauthorized` for missing authentication, `403 Forbidden` for insufficient permissions, and `404 Not Found` if the requested resource does not exist. A successful creation (POST) is acknowledged with `201 Created`, while a successful deletion (DELETE) results in a `204 No Content` response.

## Events

Object management is based on an event-driven architecture that communicates state changes system-wide transparently, traceably, and in real time. Every relevant action on an object, such as creation, update, deletion, or a workflow transition, triggers a specific event that is published via the central **WebExpress** `EventManager`.

These events can be subscribed to and processed by other system components, integrations, or external services. Typical use cases include logging changes, triggering automated follow-up processes, synchronization with third-party systems, or sending notifications to users or systems.

The architecture enables loose coupling between components, making the system flexibly extensible.

|Event name          |Description
|--------------------|--------------------------------------------------------------------
|`ObjectCreated`     |Triggered when a new object has been created successfully.
|`ObjectUpdated`     |Signals changes to field values of an existing object.
|`ObjectDeleted`     |Indicates the permanent deletion of an object.
|`ObjectArchived`    |Marks an object as archived and write-protected.
|`ObjectRestored`    |Reports the restoration of a previously archived object.
|`ObjectMoved`       |Triggered when an object is moved to another workspace or class.
|`ObjectTransitioned`|Triggered when an object successfully undergoes a workflow transition.
|`ObjectLinked`      |Signals that an object was linked to another object or an external resource.
|`ObjectUnlinked`    |Signals that a link was removed from an object.
|`ObjectCloned`      |Triggered when an object has been cloned, and includes references to source and target objects.

## Permissions Model

The permissions model for object management in **KleeneStar** is based on a context-sensitive and derivative approach. Access rights to individual objects are not granted directly at the object level. Instead, they derive from permissions in the higher-level contexts, specifically the workspace, the object class, and the associated fields. This consolidated rights assignment ensures consistent, maintainable, and traceable access control throughout the system.

In principle, a user only gains access to an object if they have the corresponding rights across all relevant levels. Read rights require that the user has read access to the workspace and the class, and can read at least one visible field of the object. Write rights additionally require permission to edit the affected fields. Creating new objects is only possible if the `class_create_objects` permission is present for the respective class. Similarly, object deletion is tied to the `class_delete_objects` permission. For workflow transitions, the `class_transition_execute` permission must be present, either globally for the workflow or specifically for individual transitions.

In addition to these derived rights, object-specific restrictions can be defined at the object level, but exclusively for read and write access. These object-related restrictions are restrictive and allow fine-grained control, such as blocking access for certain groups or roles. They can restrict the rights derived from the contexts but never extend them.

Object-related permissions control which actions a user may perform on individual objects within a workspace. They are derived from the higher-level contexts (workspace, class, field) but can be further reduced by object-specific restrictions on read and write access.

|Permission            |Description
|----------------------|-------------------------------------------------------------------
|`object_read`         |Grants read access to an object’s fields and metadata.
|`object_update`       |Authorizes editing of existing objects (depending on field rights).
|`object_comment`      |Allows adding and editing comments on an object.
|`object_attach`       |Enables uploading and managing attachments for an object.
|`object_linking`      |Allows managing object links (e.g., to issues or external resources).
|`object_manage_profiles`|Allows management of object policies and their assignment to groups.

Policies bundle individual permissions into meaningful role profiles and enable context-based assignment via workspace profiles. They define which actions users are allowed to perform on objects, depending on their group membership and the profile assigned in the respective workspace.

|Policy                 |Description                                                       |Included permissions
|-----------------------|------------------------------------------------------------------|-------------------------------
|`object_admin_policy`  |Full administrative control over all objects in the workspace.    |All `object_*` permissions
|`object_edit_policy`   |Entitled to actively edit and comment on objects.                 |`object_read`, `object_update`, `object_comment`, `object_attach`, `object_linking`
|`object_view_policy`   |Grants read-only access to objects and their content.             |`object_read`

## Conclusion

The object management concept defines the fundamental handling of data instances in **KleeneStar**. It integrates seamlessly with the existing concepts of workspaces, classes, fields, workflows, and forms, creating a coherent system as a whole. The `ObjectManager` serves as the central instance that governs CRUD operations, lifecycle management, and enforcement of business rules and permissions. The UI concepts and the REST API provide flexible and intuitive interfaces for users and external systems. For a complete implementation, technical details such as concurrent access, performance optimization for large data volumes, and caching strategies must be elaborated further.
