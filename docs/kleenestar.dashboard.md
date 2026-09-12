![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Dashboard Management Concept

Dashboards form the central interface in the **KleeneStar** system for organizing, visualizing, and controlling modeled content. They create a structured, multi-tenant environment in which data can be logically separated and displayed clearly.

A dashboard consists of modular widgets that can be flexibly combined and freely arranged. These components represent different functional units and can be tailored to the requirements of various user groups or use cases. The user interface thus becomes context-aware and personalized, enabling both operational and analytical tasks to be efficiently supported.

Typical widgets include:

- Charts (e.g., bar, line, pie)
- Tables for displaying structured data
- KPI tiles/cards with metrics and target values
- Filters for dynamically narrowing data
- Interactive controls such as toggles, dropdowns, or timelines
- Text and note fields for commenting or documentation
- Map views for geospatial data
- Status indicators and progress bars

The widget library is extensible. Add-ons make it possible to integrate additional visualization types, data sources, or interaction modules to meet specific requirements or to connect external systems.

Each dashboard acts as a self-contained unit that aggregates all relevant information. This includes type definitions, concrete instances, attributes, and relationships. At the same time, the architecture ensures a clear separation from other dashboards, enabling a clean separation between organizational units, projects, or security domains.

## Lifecycle of the Dashboard

Dashboards in the **KleeneStar** go through a clearly defined lifecycle that enables controlled and traceable development. Dashboards exist only in two states: "active" or "deleted." This clear separation ensures simple, transparent administration and prevents unnecessary complexity from intermediate or archival states.

An active dashboard is fully configured, visible, and interactively usable. It contains all relevant components, such as charts, tables, filters, or key figures, and forms the central interface for data visualization and control. Changes to an active dashboard are made through targeted revision and subsequent publication of a new version, whereby the existing version is completely replaced.

Dashboards that are no longer needed can be permanently and irreversibly deleted. Deletion occurs immediately, without intermediate storage or restore options. Before removal, it is ensured that no active processes or users are still accessing the dashboard. All relevant audit information remains preserved to ensure transparency and traceability.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Dashboard State Diagram                             ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                                 new  ╔════════╗                                      ║
║                                   ───► active ║                                      ║
║                                      ╚════════╝                                      ║
║                                          │                                           ║
║                                          │  delete                                   ║
║                                          │                                           ║
║                                     ╔════▼════╗                                      ║
║                                     ║ deleted ║                                      ║
║                                     ╚═════════╝                                      ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Data Model

The **KleeneStar** Core data model forms the structural foundation for managing and configuring dashboards. It is based on a modular architecture that distinguishes between type definitions, concrete instances, and semantic extensions. This structure enables flexible, reusable, and traceable design of dashboards.

In the context of dashboard management, key elements such as components, data sources, visualization types, or interaction logics are defined as classes. Their concrete manifestations, such as a "Revenue Chart," a "Region Filter," or a "KPI Tile," are modeled as objects with associated values, e.g., title, data binding, or formatting.

Relationships between these objects are represented via links. These semantic connections enable dynamic and context-dependent presentation of content. Additional metadata such as comments, versions, and file references support documentation, traceability, and collaboration in the development and maintenance of dashboards.

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

## Software Architecture

The application architecture follows a modular, decoupled design. At its center is the `DashboardManager`, which is exclusively responsible for the lifecycle and access to all dashboards. Via the `IDashboardManager` interface, it manages a collection of `Dashboard` instances.

The data structure of a dashboard is defined by the IDashboard interface and its concrete implementation in the `Dashboard` class. These objects contain key attributes such as key, name, description, and status information. New dashboard instances are created exclusively by the `DashboardManager`. Direct access to internal data structures is not possible. All interactions take place via a controlled interface that acts as a mediator and consistently enforces data integrity.

For reactive, loosely coupled communication, the `DashboardManager` provides the events `DashboardAdded`, `DashboardUpdated`, and `DashboardRemoved`. Other components can subscribe to these events and react to changes without building a direct dependency on the manager. This event-driven model promotes high cohesion with simultaneous modularity. The events are available system-wide via the **WebExpress** `EventManager`.

The `DashboardManager` also takes on several server-side tasks that are essential for scalability, security, and traceability. This includes persistent storage of all dashboards in a transactional, versioned repository. At system startup, all stored dashboards are loaded, indexes initialized, and event subscriptions activated.

To support powerful full-text and metadata search, a server-side reverse index is created for each dashboard. This index includes keywords from the name, description, user-defined tags as well as structured metadata such as creation date and status. The index is continuously updated and enables fast, context-based searches across the entire inventory of dashboards.

Another central aspect is access control. The `DashboardManager` checks the permissions of the calling module or user for each request. Access restrictions can be defined via policies. The context itself can contain temporary rights, audit trails, or context-dependent filters, for example to implement time-limited write permissions or differentiated read permissions.

To ensure transparency and traceability, every relevant action related to dashboards is logged by an integrated audit system. It documents access, changes, context switches, and permission checks in a structured form. The logs contain timestamps, user identities, affected dashboard keys, and the type of action (e.g., creation, modification). This data serves analysis, error diagnosis, compliance auditing, and state restoration.

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
║         ┌-----------┤ IDashboardManager                     │                        ║
║         ¦           ├───────────────────────────────────────┤                        ║
║         ¦           │ DashboardAdded:Event                  │                        ║
║         ¦           │ DashboardUpdated:Event                │                        ║
║         ¦           │ DashboardRemoved:Event                │                        ║
║         ¦           ├───────────────────────────────────────┤ 1                      ║
║         ¦           │ Workspaces:IEnumerable<IWorkspace>    ├───────┐                ║
║         ¦           │ Widgets:IEnumerable<IWidget>          │       │                ║
║         ¦           ├───────────────────────────────────────┤       │                ║
║         ¦           │ AddDashboard(IDashboard):             │       │                ║
║         ¦           │   IDashboardManager                   │       │                ║
║         ¦           │ GetDashboards(predicate):             │       │                ║
║         ¦           │   IEnumerable<IDashboard>             │       │                ║
║         ¦           │ CloneDashboard(IDashboard):           │       │                ║
║         ¦           │   IDashboardManager                   │       │                ║
║         ¦           │ RemoveDashboard(IDashboard)           │       │                ║
║         ¦           │   IDashboardManager                   │       │                ║
║         ¦           └───────────────────────────────────────┘       │                ║
║         ¦                                                           │                ║
║         ¦                     ┌───────────────┐                     │                ║
║         ¦                     │ <<Interface>> │                     │                ║
║         ¦                     │ IModel        │                     │                ║
║         ¦                     ├───────────────┤                     │                ║
║         ¦                     └───────Δ───────┘                     │                ║
║         ¦                             ¦                             │                ║
║         ¦                             ¦                             │                ║
║         ¦           ┌─────────────────┴──────────────────┐ *        │                ║
║         ¦           │ <<Interface>>                      ◄──────────┘                ║
║         ¦           │ IDashboard                         │                           ║
║         ¦           ├────────────────────────────────────┤                           ║
║         ¦           │ Id: Guid                           │                           ║
║         ¦           │ Name:String                        │                           ║
║         ¦           │ Icon:IIcon                         │                           ║
║         ¦           │ Categories:IEnumerable<String>     │                           ║
║         ¦           │ Description:String                 │                           ║
║         ¦           │ Created:DateTime                   │                           ║
║         ¦           │ Updated:DateTime                   │                           ║
║         ¦           │ Widget                             ├─────────────┐             ║
║         ¦           │   IEnumerable<IWidget>             │             │             ║
║         ¦           │ PermissionsProfiles:               │             │             ║
║         ¦           │   IEnumerable<IPermissionsProfile> │             │             ║
║         ¦           └─────────────────Δ──────────────────┘             │             ║
║         ¦                             ¦                        ┌───────▼───────┐     ║
║         ¦                             ¦                        │ <<Interface>> │     ║
║         ¦                             ¦                        │ IWidget       │     ║
║         ¦                             ¦                        ├───────────────┤     ║
║         ¦                             ¦                        │ Id: Guid      │     ║
║         ¦                             ¦                        │ Name:String   │     ║
║         ¦                             ¦                        │ WQL:String    │     ║
║         ¦                             ¦                        └───────────────┘     ║
║         ¦ create    ┌─────────────────┴──────────────────┐                           ║
║         └-----------► Dashboard                          │                           ║
║                     ├────────────────────────────────────┤                           ║
║                     │ Id: Guid                           │                           ║
║                     │ Name:String                        │                           ║
║                     │ Icon:IIcon                         │                           ║
║                     │ Categories:IEnumerable<String>     │                           ║
║                     │ Description:String                 │                           ║
║                     │ Created:DateTime                   │                           ║
║                     │ Updated:DateTime                   │                           ║
║                     │ Widget                             │                           ║
║                     │   IEnumerable<IWidget>             │                           ║
║                     │ PermissionsProfiles:               │                           ║
║                     │   IEnumerable<IPermissionsProfile> │                           ║
║                     └────────────────────────────────────┘                           ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

Responsible for controlling all dashboards in the system is the `DashboardManager`. It implements the `IComponentManager` interface and is thus fully integrated into the component-based architecture of the **KleeneStar**.Core module. As the central administration unit, it handles all tasks related to the lifecycle of dashboards, from creation and cloning to retrieval and final deletion.

For this purpose, the `DashboardManager` provides a set of clearly defined methods. This functionality is complemented by an event-driven notification system with events. These enable other system components to react to changes without being directly coupled to the manager.

Dashboards themselves are described by the `IDashboard` interface and concretely implemented in the `Dashboard` class. They contain key metadata such as a unique ID, name, description, icon, and timestamps for creation and update. In addition, they consist of a collection of widgets defined via the `IWidget` interface. Each widget has its own ID, a name, and a WQL query (Widget Query Language), which allows it to dynamically bind data sources. Concrete implementations of `IWidget`, for example for charts, tables, Kanban boards, or Scrum views, can be added modularly via extensions. This makes it possible to flexibly adapt dashboard functionality to different use cases and user needs.

To control access to dashboards, permission profiles are provided, integrated via the `IPermissionsProfile` interface. These enable differentiated assignment of rights and can be flexibly linked to roles or user groups.

Unlike other system components, dashboards are not organizationally embedded in workspaces but exist as independent entities in the system. This independence allows them to access all accessible objects in the system via WQL, regardless of their workspace affiliation. As a result, dashboards can represent cross-domain evaluations, aggregated visualizations, and cross-context control mechanisms without being restricted by organizational boundaries.

## UI Concepts and Pages

To make dashboard management clear and efficient for users, concrete UI concepts were developed that translate complex data structures and process logic into an intuitive user interface. The focus is on user-friendliness as well as security and efficiency.

The proposed designs are based on the established design principles of the **KleeneStar** WebApp and thus ensure a familiar operation and quick orientation. They serve as the basis for the final design and define navigation, controls, and the visual representation of various system states. Users are guided through the application to create, edit, or archive dashboards.

### Global Dashboard Dropdown (Header)

The global dashboard dropdown is a central, permanently visible element in the header area of the application. It enables a fast, context-sensitive switch between different dashboards without having to leave the current view. The control shows the name of the active dashboard and opens a dropdown menu with a searchable list of all available dashboards upon interaction. To increase productivity, recently used dashboards are highlighted.

The following schematic illustrates the position and function of the dropdown in the overall layout of the web application:

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────────────────────────────────¦──────────────────────────────────────────────────┘║
║┌Breadcrumb─────────────────────┌─┴────────────────┐─────────────────────────────────┐║
║│ / Service Desk                │┌────────────────┐│                                 │║
║└───────────────────────────────││ Search         ││─────────────────────────────────┘║
║┌Workspace ────────────┐ ┌──────│└────────────────┘│─────────────────────────────────┐║
║│[Name]                │░│      │ Dashboard 0      │                                 │║
║│                      │░│ Incid│ Dashboard 1      │                             […] │║
║│      [Icon]          │░│      │ ...              │                        [Search] │║
║│                      │░│ Summa│ Dashboard n      │        | Status  | Impact     + │║
║│                      │░│------├──────────────────┤--------|---------|--------------│║
║│ Issue                │░│ VPN c│ Manage Dashboard │        | Open    | High     […] │║
║│ ├─ Incident          │░│ Outlo│ + Add Dashboard  │        | Open    | Medium   […] │║
║│ ├─ Problem           │░│ Print│ <section>        │e       | Assigne | Medium   […] │║
║│ └─ ServiceRequest    │░│ File └──────────────────┘        | In Prog.| Low      […] │║
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
║├──────────────────────┤░│                                                           │║
║│ [+] | [Setting]   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Dashboard Management (Page)

A dedicated management page is available for the central control of all dashboards in the system. It serves as a central point of contact and offers a complete overview as well as powerful tools for managing the entire inventory of dashboards. In contrast to context-related elements such as the global dropdown navigation, this page enables a holistic view and provides advanced search and filter functions.

The heart of the page is a tabular overview of all dashboards. Each row represents an individual dashboard and shows key attributes such as name and key. To ensure easy navigation even with extensive inventories, powerful search and filter options are directly integrated into the interface.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Dashboards                                                                       │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Dashboard Category────┐ ┌Dashboards─────────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│ - All                │░│                                [Search] [+ Add Dashboard] │║
║│ - Category 0         │░│                                                           │║
║│ - Category 1         │░│ Name               | Description                          │║
║│ - ...                │░│--------------------|--------------------------------------│║
║│ - Category n         │░│ Sales Performance  | Shows revenue trends, targets... […] │║
║│                      │░│ Incident Overview  | Visualizes open IT incidents...  […] │║
║│                      │░│ Strategy Dashboard | Executive KPIs such as EBIT, ... […] │║
║│                      │░│                                                        ¦  │║
║│                      │░│                                   ‹ Prev  ┌────────────┴┐ │║
║│                      │<│                                           │ Edit        │ │║
║│                      │<│                                           │ Clone       │ │║
║│                      │<│                                           │ Permissions │ │║
║│                      │░│                                           │ <section>   │ │║
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

### Dashboard Management - Display (Page)

Displaying a dashboard in **KleeneStar** provides the central access point for visualizing and interacting with the data it contains. After selecting a dashboard from the overview or accessing it directly via the URL, the detail view is loaded, which displays all relevant information and functions in context.

The header displays the name of the dashboard, its status (e.g., active, archived), and related metadata such as description, category, and tenant. Depending on the user's permission profile, additional actions are available, such as editing content, managing permissions, or cloning the dashboard.

The main view is divided into various sections that typically include widgets, data sources, filter elements, and interactive components. These are dynamically configurable and reflect the structure defined when creating or editing the dashboard.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Dashboard / Sales Performance                                                    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌────────────────────────────────────────────────────────────────────────────────────┐║
║│                                                                                    │║
║│ Sales Performance                                               [+ Add Widget] […] │║
║│                                                                                    │║
║│ [⠿ Widget: Revenue by Region x]  [⠿ Widget: Target Atta... x]  [⠿ Widget: KPI x]   │║
║│ ┌─────────────────────────────┐  ┌──────────────────────────┐  ┌───────────────┐   │║
║│ │ Region | Revenue            │  │ Month | Target | Actual  │  │ KPI Value     │   │║
║│ │--------|--------------------│  │--------------------------│  │---------------│   │║
║│ │ North  | €120,000           │  │ Oct   | 95%    | 92%     │  │ 87%           │   │║
║│ │ South  | €98,000            │  │ Nov   | 90%    | 88%     │  │               │   │║
║│ └─────────────────────────────┘  └──────────────────────────┘  └───────────────┘   │║
║│                                                                                    │║
║│ [⠿ Widget: Revenue Trend (current year vs. previous year)                      x]  │║
║│ ┌───────────────────────────────────────────────────────────────────────────────┐  │║
║│ │ Month │ 2024       │ 2025       │ Growth                                      │  │║
║│ │-------|------------|------------|---------------------------------------------|  │║
║│ │ Jan   | ████████   | ██████████ | ▲ +12%                                      │  │║
║│ │ Feb   | ███████    | ████████   | ▲ +8%                                       │  │║
║│ │ Mar   | ████████   | ███████    | ▼ -4%                                       │  │║
║│ │ Apr   | ███████    | █████████  | ▲ +10%                                      │  │║
║│ │ May   | ████████   | ████████   | ▬  0%                                       │  │║
║│ └───────────────────────────────────────────────────────────────────────────────┘  │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Dashboard Management - New/Edit (Modal)

A modal dialog is used to create or edit a dashboard. This approach enables focused, distraction-free interaction because the editing process is concentrated on a clearly defined task without leaving the overarching management page. The modal is opened through targeted actions, e.g., by the "+ New Dashboard" button on the management page, the "Edit" command in the dashboard table, or via the "Settings" button in the dashboard sidebar.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔DashboardAddEditModal═══════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Da║│ Add Dashboard / Edit Dashboard                                       │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Dashb║│                                                                      │║─────┐║
║│     ║│           Name*: [ Service Desk                                    ] │║     │║
║│ - Al║│        Category: [                                                 ] │║ […] │║
║│ - Ca║│     Description: [                                                 ] │║rch] │║
║│ - Ca║│                                                                      │║     │║
║│ - ..║│                                                                      │║---- │║
║│ - Ca║│                                                                      │║ […] │║
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
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Dashboard Management - Clone (Modal)

When cloning a dashboard, a separate modal dialog is used that enables quick and targeted replication of existing structures. This function can be accessed both from the detail view of a dashboard and directly from the management page. It is particularly suitable for efficiently reusing proven configurations.

In the modal, the user is first informed that the dashboard is being cloned. The most important properties of the new dashboard can then be adjusted. These include, in particular, the new name, which is given the suffix "(Copy)" by default, as well as a description based on the original but editable. There is also an option to include existing permissions by activating the "Include permissions" option.

System-critical properties such as the creation time or internal references are automatically regenerated or deliberately excluded during cloning to preserve system integrity. After confirmation via the "Clone" button, the new dashboard is created and integrated directly into the existing dashboard list. The design of the modal follows the consistent UI style of the **KleeneStar** WebApp and ensures a clear, user-friendly cloning experience.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔DashboardCloneModal═════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Da║│ Clone Dashboard                                                      │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Dashb║│                                                                      │║─────┐║
║│     ║│ You are about to clone the dashboard 'Sales Performance'.            │║     │║
║│ - Al║│ Please adjust the details for the new workspace below.               │║ […] │║
║│ - Ca║│                                                                      │║rch] │║
║│ - Ca║│ Dashboard Name*: [ Sales Performance (Copy)                        ] │║     │║
║│ - ..║│     Description: [ Copy of Shows revenue trends, targets...        ] │║---- │║
║│ - Ca║│                                                                      │║ […] │║
║│     ║│ Include permissions: [✓]                                             │║ […] │║
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

### Dashboard Management - Delete (Modal)

Deleting a dashboard is a critical and non-reversible operation that requires deliberate user confirmation. To prevent accidental deletions, this process is handled via a modal dialog. The modal is activated as soon as the user initiates the delete action either from the dashboard detail view or directly from the management page.

Within the dialog, the dashboard to be deleted is clearly identified by explicitly naming it. As an additional safety measure, the user must enter the exact name of the dashboard into an input field. Only when this entry is correct is the "Delete" button enabled. This procedure ensures that the deletion is performed with full intent and attention.

In addition to the confirmation button, the modal also offers a clearly visible option to cancel the operation. This allows the dialog to be closed at any time without making changes to the system. The design of the modal follows the consistent UI style of **KleeneStar** and supports safe and traceable user guidance for sensitive operations.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔DashboardDeleteModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Da║│ Delete Dashboard                                                     │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Dashb║│                                                                      │║─────┐║
║│     ║│ Are you sure you want to delete the dashboard 'Sales Performance'?   │║     │║
║│ - Al║│ This action cannot be undone.                                        │║ […] │║
║│ - Ca║│                                                                      │║rch] │║
║│ - Ca║│ To confirm, please type 'Sales Performance' in the box below*:       │║     │║
║│ - ..║│ [                                                                 ]  │║---- │║
║│ - Ca║│                                                                      │║ […] │║
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

### Dashboard Management - Permissions Management (Modal)

Managing access rights to a dashboard is handled via a modal dialog that serves as the central interface for assigning groups to context-specific permission policies. This modal enables fine-grained control over which groups may act with which roles and rights within a dashboard.

The modal can be accessed via the "Permissions" button in the dashboard management view. The `dashboard_manage_profiles` permission is required to display and use the dialog. Within the modal, administrators can select groups from a dropdown list and assign appropriate policies to them, for example for read access, editing rights, or administrative functions.

Assignments are displayed in a tabular overview that can be changed or deleted at any time. New assignments are made via the "+ Assign" button, and changes are confirmed with "Done."

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔DashboardPermissionsModal═══════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Da║│  Manage Permissions for 'Sales Performance'                          │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Dashb║│                                                                      │║─────┐║
║│     ║│  Assign Group*: [ Admin ▼]                                           │║     │║
║│ - Al║│        Policy*: [ dashboard_admin_policy ▼]                          │║ […] │║
║│ - Ca║│                                                                      │║rch] │║
║│ - Ca║│  [+ Assign]                                                          │║     │║
║│ - ..║│                                                             [Search] │║---- │║
║│ - Ca║│                                                                      │║ […] │║
║│     ║│ Assigned Group       | Effective Policy                              │║ […] │║
║│     ║│----------------------|-----------------------------------------------│║ […] │║
║│     ║│ Admin                | dashboard_admin_policy                      X │║     │║
║│     ║│ User                 | dashboard_view_policy                       X │║xt › │║
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

The sitemap defines the hierarchical structure and navigation paths of the user interface for managing dashboards. It ensures a clear organization of pages, forms the basis for routing within the web application, and is structured as follows:

|Path                                     |Page                     |Description
|-----------------------------------------|-------------------------|------------------------------------------------------------------------
|`/`                                      |Dashboard                |Central entry page of the application.
|`/dashboards`                            |Dashboard Overview       |Overview of all dashboards with search, filter, and management functions.
|`/dashboards/add`                        |Dashboard Creation       |Form for creating a new dashboard.
|`/dashboards/{dashboardKey}`             |Dashboard Detail View    |Detail view of a single dashboard with associated actions.
|`/dashboards/{dashboardKey}/edit`        |Dashboard Editing        |Form for editing the metadata of an existing dashboard.
|`/dashboards/{dashboardKey}/clone`       |Dashboard Cloning        |Dialog for replicating an existing dashboard with adjustable fields.
|`/dashboards/{dashboardKey}/delete`      |Dashboard Deletion       |Modal for confirming and performing the permanent deletion of a dashboard.
|`/dashboards/{dashboardKey}/permissions` |Dashboard Permissions    |Modal for managing profiles (group-policy assignments) for a dashboard.

## API Interfaces (REST Endpoints)

For programmatic interaction, integration with third-party systems, and automation, **KleeneStar** provides a standardized REST API for managing dashboards. The interface follows REST principles and uses JSON as the exchange format. Authentication and authorization are handled by the central security system. Standardized HTTP status codes provide information about the success or failure of a request (e.g., validation errors, permission issues, or resources not found).

Managing dashboards is performed via the following endpoints:

|Endpoint                                                |HTTP Method |Description
|--------------------------------------------------------|------------|------------------------------------------------------------------------
|`/api/1/dashboards`                                     |GET         |Lists all available dashboards. Results are paginated, and can be filtered and sorted.
|´/api/1/dashboards`                                     |POST        |Creates a new dashboard. Requires at least name and a unique key.
|`/api/1/dashboards/{dashboardKey}`                      |GET         |Retrieves the details of a specific dashboard by its key.
|`/api/1/dashboards/{dashboardKey}`                      |PUT         |Updates the metadata (e.g., name, description) of an existing dashboard.
|`/api/1/dashboards/{dashboardKey}`                      |DELETE      |Permanently deletes a dashboard.
|`/api/1/dashboards/{dashboardKey}/profiles`             |GET         |Lists all profiles (group-policy assignments) for the specified dashboard. Requires workspace_manage_profiles.
|`/api/1/dashboards/{dashboardKey}/profiles`             |POST        |Creates a new profile to assign a group to a policy within the dashboard.
|`/api/1/dashboards/{dashboardKey}/profiles/{profileId}` |DELETE      |Removes a profile, thereby revoking the rights assigned to the group.
|`/api/1/dashboards/{dashboardKey}/import`               |POST        |Imports one or more dashboards from an external schema (e.g., JSON/YAML).
|`/api/1/dashboards/{dashboardKey}/export`               |GET         |Exports the current dashboard schema for reuse or backup.

Standard error responses include 400 Bad Request for validation errors (e.g., a key that is already taken), 401 Unauthorized for missing authentication, 403 Forbidden for insufficient permissions, and 404 Not Found if the requested resource does not exist. A successful creation (POST) is acknowledged with 201 Created, while a successful deletion (DELETE) results in a 204 No Content response.

## Dashboard Events

Dashboard management is based on an event-driven architectural model that transparently and reactively communicates system state changes. Events are published via the **WebExpress** `EventManager`, which acts as the central event backbone. This allows other modules, plugins, or external systems to subscribe to relevant changes without being directly coupled to the DashboardManager.

The following events are published by the `DashboardManager`:

|Event Name         |Description
|-------------------|-----------------------------------------------------------------------------
|`DashboardAdded`   |Triggered when a new dashboard has been successfully created.
|`DashboardUpdated` |Signals changes to the metadata of an existing dashboard.
|`DashboardRemoved` |Marks the permanent deletion of a dashboard.
|`DashboardCloned`  |Triggered when a dashboard has been successfully duplicated.

The events contain a structured payload, including:
- The unique dashboard id
- Timestamp of the action
- User or module context
- Type and source of the action

Through integration with the **WebExpress** `EventManager`, these events are available both within the application and to connected subsystems.

## Permissions Model

**KleeneStar**’s permission model is context-sensitive and applied individually to individual dashboards. The connection between globally defined groups and permission policies is made via profiles, which are valid exclusively within a specific dashboard.

A profile defines which policy a global group receives in a particular dashboard. It thus acts as a context-bound role binding and enables fine-grained, flexible assignment of rights.

The following table lists the available individual permissions required for comprehensive control of dashboard management:

|Permission                  | Description
|----------------------------|-----------------------------------------------------------------------------------
|`dashboard_create`          |Allows the creation of new, isolated dashboards.
|`dashboard_read`            |Grants read access to a dashboard’s metadata (name, description, status).
|`dashboard_update`          |Authorizes changes to the metadata of an existing dashboard.
|`dashboard_delete`          |Allows the permanent deletion of a dashboard.
|`dashboard_archive`         |Enables the archiving of an active dashboard.
|`dashboard_restore`         |Allows the restoration of an archived dashboard.
|`dashboard_clone`           |Authorizes the duplication of an existing dashboard.
|`dashboard_manage_profiles` |Allows the management of profiles (assignment of policies to groups).
|`dashboard_read_content`    |Grants read access to a dashboard’s content (e.g., entities, attributes).
|`dashboard_write_content`   |Allows creating, editing, and deleting content within a dashboard.

These individual permissions are bundled into logical policies that represent typical roles and use cases. Policies can be assigned to global groups via profiles:

|Policy                     |Description                                               |Included Permissions
|---------------------------|----------------------------------------------------------|-------------------------
|`dashboard_admin_policy`   |Full administrative control over a dashboard.             |all `dashboard_*`
|`dashboard_edit_policy`    |Management of a dashboard’s content.                      |`dashboard_read`, `dashboard_read_content`, `dashboard_write_content`
|`dashboard_view_policy`    |Read-only access to a dashboard and its content.          |`dashboard_read`, `dashboard_read_content`
|`dashboard_creator_policy` |Global policy for creating new dashboards.                |`dashboard_create`

## Conclusion

This concept paper defines the architectural and functional foundations for a reference implementation for managing dashboards. It describes key aspects such as data modeling, system architecture, and user interaction, thus setting the framework for a scalable and multi-tenant solution. The focus is on the complete lifecycle of a dashboard, from creation to use to archiving, complemented by a context-sensitive permission model that enables fine-grained control of access rights.

As a deliberately abstract design template, the document refrains from technical depth in certain key areas. Aspects such as handling concurrent access, validation logic beyond the framework's `Validate…` attributes, and processing long-running operations, for example when cloning or archiving large dashboards, are not specified conclusively. The cross-system functions it left open have since been provided and are no longer open: **persistent storage** is EF Core through `KleeneStarDbContext`; **notifications** are raised by the `DashboardManager` on every create, update and delete through `CoreHub.AddNotification`, reaching the notification center and the toast; and the **audit system** is the installation-wide, hash-chained log of [kleenestar.audit.md](kleenestar.audit.md), which subscribes to the dashboard manager's events centrally.

The aim of the reference implementation is to close these conceptual gaps with concrete technical solutions and to validate the practicality of the proposed models. It is intended to serve as a proof of concept and form the basis for productive further development. The focus here is on implementing the functional core flows, while technical details are deliberately elaborated within the scope of implementation.
