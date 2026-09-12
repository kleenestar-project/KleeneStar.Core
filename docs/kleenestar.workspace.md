![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Workspace Management Concept

This document specifies the management of workspaces as a central organizational and encapsulation instance within the **KleeneStar** system. Workspace management creates the multi-tenant framework required for the structured collection and logical separation of all modeled content.

A workspace acts as a self-contained container that bundles all associated data, such as type definitions, concrete instances of these types, their attributes, and relationships (links). It also isolates this data from other workspaces. This segmentation forms the foundation of the system's multi-tenancy and enables different organizational units, projects, or security domains to be cleanly separated from one another.

To promote standardization and consistency across multiple workspaces, the concept of blueprints is introduced. Any workspace can serve as a blueprint for other workspaces. A workspace derived from a blueprint inherits its structural configuration, particularly the definitions of Classes and Fields.

This inheritance is dynamic: changes to the type definitions in the blueprint are automatically propagated to all derived workspaces. This mechanism ensures that an entire group of workspaces is based on a uniform, centrally managed data model. At the same time, each derived workspace maintains its independence, as metadata such as name, description, or color-coding, as well as assigned permissions, can be configured individually. They are not inherited from the blueprint.

## Lifecycle of the Workflow

The functional scope of workspace management covers the entire lifecycle of a workspace. This includes the following core operations:

- **Create**: Creating new, isolated workspaces.
- **View and Navigate**: Clear presentation and quick switching between available workspaces.
- **Edit**: Adjusting the metadata and configurations of an existing workspace.
- **Clone**: Creating a copy of a workspace as a template.
- **Archive and Restore**: Temporarily decommissioning and reactivating workspaces to preserve data without cluttering the active work environment.
- **Delete**: Secure and traceable removal of no-longer-needed workspaces, taking into account retention periods.

The following state diagram visualizes these transitions:

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                       KleeneStar Workspace State Diagram                             ║
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

The **KleeneStar** Core Data Model forms the structural foundation for managing identities, roles, and permissions. It is based on a clearly modular architecture that distinguishes between type definitions, concrete instances, and semantic extensions. The components of the permission model (e.g., Position, Role, Resource, Policy) can be defined as Classes. Their specific manifestations (e.g., "Head of Marketing", "Admin") are Objects with corresponding Values (e.g., description, assigned group). Relationships such as "Position → Role" or "Role → Resource" are modeled via Links. Comments, versioning, and file references enable additional documentation and traceability.

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

The application's architecture follows a modular, decoupled design. At its core is the `WorkspaceManager`, which is exclusively responsible for the lifecycle and access to all workspaces. Through the `IWorkspaceManager` interface, it manages a collection of `IWorkspace` instances.

The data structure of a workspace is defined by the `IWorkspace` interface and its concrete implementation in the `Workspace` class. These objects contain central attributes such as `Key`, `Name`, `Description`, and lifecycle information. New workspace instances are created exclusively by the `WorkspaceManager`. The system does not allow direct access to internal data structures. Instead, all interactions are routed through a controlled interface that serves as an intermediary, ensuring consistent enforcement of data integrity.

For reactive, loosely coupled communication, the `WorkspaceManager` provides the `WorkspaceAdded`, `WorkspaceUpdated` and `WorkspaceRemoved` events. Other components can subscribe to these events and react to changes without creating a direct dependency on the manager. This event-driven mechanism promotes high cohesion while maintaining modularity. Additionally, the events are made available system-wide via the `WebExpress-EventManager`.

The `WorkspaceManager` also handles several server-side tasks that are essential for scalability, security, and traceability. This includes the persistent storage of all workspaces in a transaction-safe, versioned repository. At system startup, all stored workspaces are loaded, and all indexes and event subscriptions are initialized.

To support high-performance full-text and metadata searches, a server-side reverse index is created for each workspace. This index includes keywords from the `Name`, `Description`, user-defined tags, and structured metadata such as creation date and status. The index is continuously updated and enables fast, context-aware searches across the entire workspace inventory.

Another central aspect is access control. The `WorkspaceManager` checks the permissions of the calling module or user with every request. Access restrictions can be defined via policies. The context itself can contain temporary rights, audit trails, or context-dependent filters. This allows for the implementation of time-limited write permissions or differentiated read permissions.

To ensure transparency and traceability, every relevant action related to workspaces is logged by an integrated audit system. It documents accesses, changes, context switches, and permission checks in a structured format. The logs contain timestamps, user identities, affected workspace keys, and the type of action (e.g., creation, modification). This data is used for analysis, error diagnosis, compliance auditing, and state restoration.

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
║         ┌-----------┤ IWorkspaceManager                     │                        ║
║         ¦           ├───────────────────────────────────────┤                        ║
║         ¦           │ WorkspaceAdded:Event                  │                        ║
║         ¦           │ WorkspaceUpdated:Event                │                        ║
║         ¦           │ WorkspaceRemoved:Event                │                        ║
║         ¦           ├───────────────────────────────────────┤ 1                      ║
║         ¦           │ Workspaces:IEnumerable<IWorkspace>    ├───────┐                ║
║         ¦           ├───────────────────────────────────────┤       │                ║
║         ¦           │ AddWorkspace(Workspace):              │       │                ║
║         ¦           │   IWorkspaceManager                   │       │                ║
║         ¦           │ GetWorkspaces(predicate):             │       │                ║
║         ¦           │   IEnumerable<IWorkspace>             │       │                ║
║         ¦           │ CloneWorkspace(IWorkspace):           │       │                ║
║         ¦           │   IWorkspaceManager                   │       │                ║
║         ¦           │ RemoveWorkspace(IWorkspace)           │       │                ║
║         ¦           │   IWorkspaceManager                   │       │                ║
║         ¦           └───────────────────────────────────────┘       │                ║
║         ¦                                                           │                ║
║         ¦             ┌───────────────┐   ┌────────────────┐        │                ║
║         ¦             │ <<Interface>> │   │ <<Interface>>  │        │                ║
║         ¦             │ IModel        │   │ IIndexItem     │        │                ║
║         ¦             ├───────────────┤   ├────────────────┤        │                ║
║         ¦             └──────Δ────────┘   │ Id: Guid       │        │                ║
║         ¦                    ¦            └──────Δ─────────┘        │                ║
║         ¦                    ¦                   ¦                  │                ║
║         ¦                    └--------┬----------┘                  │                ║
║         ¦                             ¦                             │                ║
║         ¦       ┌─────────────────────┴──────────────┐ *            │                ║
║         ¦       │ <<Interface>>                      ◄──────────────┘                ║
║         ¦       │ IWorkspace                         │     ┌────────────────────┐    ║
║         ¦       ├────────────────────────────────────┤     │ <<Enum>>           │    ║
║         ¦       │ Key:String                         │     │ WorkspaceState     │    ║
║         ¦       │ Name:String                        │     ├────────────────────┤    ║
║         ¦       │ State:WorkspaceState               │     │ Active             │    ║
║         ¦       │ Icon:IIcon                         │     │ Archived           │    ║
║         ¦       │ Description:String                 │     └────────────────────┘    ║
║         ¦       │ Created:DateTime                   │  ┌─────────────────────────┐  ║
║         ¦       │ Updated:DateTime                   │  │ <<Enum>>                │  ║
║         ¦       │ Inherited:IWorkspace               │  │ WorkspaceAccessModifier │  ║
║         ¦       │ Sealed:Bool                        │  ├─────────────────────────┤  ║
║         ¦       │ Tenant:IEnumerable<ITenant>        │  │ Private                 │  ║
║         ¦       │ Classes:                           │  │ Protected               │  ║
║         ¦       │   IEnumerable<IClass>              │  │ Public                  │  ║
║         ¦       │ Objects:                           │  │ Internal                │  ║
║         ¦       │   IEnumerable<IObject>             │  └─────────────────────────┘  ║
║         ¦       │ Categories:IEnumerable<String>     │                               ║
║         ¦       │ AccessModifier:                    │                               ║
║         ¦       │   WorkspaceAccessModifier          │                               ║
║         ¦       │ PermissionsProfiles:               │                               ║
║         ¦       │   IEnumerable<IPermissionsProfile> │                               ║
║         ¦       └─────────────────Δ──────────────────┘                               ║
║         ¦                         ¦                                                  ║
║         ¦                         ¦                                                  ║
║         ¦ create ┌────────────────┴───────────────────┐                              ║
║         └--------► Workspace                          │                              ║
║                  ├────────────────────────────────────┤                              ║
║                  │ Key:String                         │                              ║
║                  │ Name:String                        │                              ║
║                  │ State:WorkspaceState               │                              ║
║                  │ Icon:IIcon                         │                              ║
║                  │ Description:String                 │                              ║
║                  │ Created:DateTime                   │                              ║
║                  │ Updated:DateTime                   │                              ║
║                  │ Inherited:IWorkspace               │                              ║
║                  │ Sealed:Bool                        │                              ║
║                  │ Tenant:IEnumerable<ITenant>        │                              ║
║                  │ Classes:                           │                              ║
║                  │   IEnumerable<IClass>              │                              ║
║                  │ Objects:                           │                              ║
║                  │   IEnumerable<IObject>             │                              ║
║                  │ Categories:IEnumerable<String>     │                              ║
║                  │ AccessModifier:                    │                              ║
║                  │   WorkspaceAccessModifier          │                              ║
║                  │ PermissionsProfiles:               │                              ║
║                  │   IEnumerable<IPermissionsProfile> │                              ║
║                  └────────────────────────────────────┘                              ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

A workspace can inherit exactly one other workspace as its base. Inheritance is referential, meaning classes and fields from the parent workspace become visible in the child without being copied. Resolution follows a clear precedence: overrides in the child take priority over definitions in the parent. If no override exists, the parent’s values apply. Child-specific overloads can selectively hide inherited elements without modifying the parent. In cases of naming conflicts, the system deterministically favors the child. Each workspace maintains its own operational state; archiving the parent does not automatically affect its children but is displayed as a source status.

Access modifiers follow a monotonic visibility rule across the inheritance hierarchy. A child may only adopt the same or more restrictive visibility than its parent, never a broader one. The modifier private enforces complete encapsulation, disallowing any tenant-wide or hierarchical exposure. Protected grants visibility to derived workspaces. Internal allows visibility across the entire tenant. Public enables cross-tenant visibility. Inheritance across tenants is permitted only in public scenarios.

A workspace can also be assigned to multiple tenants simultaneously. This allows shared visibility and reuse across organizational boundaries, provided that access modifiers and inheritance rules are respected.

The modifier sealed introduces an inheritance boundary. A sealed workspace can be inherited, but it cannot itself be extended further. Once a workspace is marked as sealed, it becomes a terminal node in the inheritance chain. This is useful for stabilizing reference implementations, locking down shared templates, or preventing unintended specialization.

## UI Concepts and Pages

The following UI mockups translate the abstract data models and lifecycle rules of workspace management into a concrete and tangible user experience. The goal is to ensure intuitive, efficient, and secure interaction with workspaces. All UI concepts presented here are based on the established and consistent UI patterns of the **KleeneStar** WebApp to ensure high recognizability and a short learning curve.

These mockups serve as a blueprint for the final design and specify navigation, the arrangement of controls, and the display of various system states. They illustrate how users are guided through the application to perform operations such as creating, managing, or archiving workspaces.

### Global Workspace Dropdown (Header)

The global workspace dropdown is a central and permanently available component in the application header. It allows for quick and context-aware switching between different workspaces without leaving the current view. The control displays the name of the active workspace and, upon interaction, opens a dropdown menu that provides a searchable list of all available workspaces. To increase efficiency, recently used workspaces are prominently placed.

The dropdown is personalized for each user. Without a search term, it displays recently opened workspaces first, ordered by the latest visit. Favorites feature a leading star icon. Older favorites appear at the bottom of the list so they are never hidden.
The system registers a visit whenever a user opens a workspace or its subpages. These subpages include the content view, object detail pages, and class management. This mechanism ensures active workspaces remain at the very top.
Entering a search term instantly switches the dropdown to a full-text search across all workspaces. If a user has no bookmarks yet, the dropdown displays the full workspace list so the control is never empty.
Both features rely on a per-identity `WorkspaceBookmark`. This is an (`Owner`, `Workspace`) pair containing a Favorite flag and a LastVisited timestamp. Accessing a workspace or its subpages updates the `LastVisited` time. Users can toggle the favorite flag via the overflow menu in the workspace management table. This bookmark model mirrors the `Starred` and `LastUsed` logic used for saved searches.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└───────────────────¦────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb───────┌─┴────────────────┐───────────────────────────────────────────────┐║
║│ / Service Desk  │┌────────────────┐│                                               │║
║└─────────────────││ Search         ││───────────────────────────────────────────────┘║
║┌Workspace ───────│└────────────────┘│───────────────────────────────────────────────┐║
║│[Name]           │ Workspace 0      │                                               │║
║│                 │ Workspace 1      │                                           […] │║
║│      [Icon]     │ ...              │                                      [Search] │║
║│                 │ Workspace n      │                      | Status  | Impact     + │║
║│                 ├──────────────────┤----------------------|---------|--------------│║
║│ Issue           │ Manage Workspace │ion disrupted         | Open    | High     […] │║
║│ ├─ Incident     │ + Add Workspace  │'t start              | Open    | Medium   […] │║
║│ ├─ Problem      │ <section>        │floor 3 offline       | Assigne | Medium   […] │║
║│ └─ ServiceReques└──────────────────┘ fails                | In Prog.| Low      […] │║
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

### Workspace Management (Page)

A dedicated management page exists for the comprehensive control of all workspaces in the system. This page serves as a central hub, offering a complete overview and powerful tools for managing the entire workspace inventory. In contrast to context-specific components like the global dropdown, this view provides a global perspective and offers advanced search and filtering capabilities.

The main component of this page is a tabular list of all workspaces. Each row represents a workspace and displays its most important attributes, such as name, key, current status, and type (e.g., standard or blueprint). To facilitate navigation even in extensive inventories, powerful search and filter functions are integrated directly into the interface. Furthermore, the page supports bulk operations, allowing administrators to select multiple workspaces to efficiently perform actions like archiving or deleting for the entire selection at once. A clearly visible button for creating a new workspace completes the feature set and serves as the primary entry point for expanding the system.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Workspaces                                                                       │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workspace Category────┐ ┌Workspaces─────────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│ - All                │░│ My Workspaces                  [Search] [+ Add Workspace] │║
║│ - Category 0         │░│                                                           │║
║│ - Category 1         │░│ Name             | Key         | Status   | ...           │║
║│ - ...                │░│------------------|-------------|----------|---------------│║
║│ - Category n         │░│ Sales Operations | sales-ops   | active   | ...       […] │║
║│ - Archived           │░│ Engineering      | engineering | archived | ...       […] │║
║│                      │░│ Marketing        | marketing   | active   | ...       […] │║
║│                      │░│                                                        ¦  │║
║│                      │░│                                   ‹ Pre┌───────────────┴┐ │║
║│                      │<│                                        │ Edit           │ │║
║│                      │<│                                        │ Clone          │ │║
║│                      │<│                                        │ Manage Classes │ │║
║│                      │░│                                        │ Permissions    │ │║
║│                      │░│                                        │ <section>      │ │║
║│                      │░│                                        ├────────────────┤ │║
║│                      │░│                                        │ Delete         │ │║
║│                      │░│                                        └────────────────┘ │║
║│                      │░│                                                           │║
║├──────────────────────┤░│                                                           │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Workspace Management (Sidebar)

Complementing the global dropdown, the sidebar offers a detailed view and direct interaction options for the currently selected workspace. This context-sensitive component is prominently placed in the main navigation or a dedicated management view and serves as the central dashboard for the respective workspace.

The sidebar visualizes essential metadata at a glance, such as the name, an icon, and the current status (e.g., active, archived, or whether it is a blueprint). Additionally, it provides direct access to central management functions. Actions like editing settings, cloning, archiving, or deleting the workspace are immediately accessible via clearly labeled buttons.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk                                                                     │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workspace─────────────┐ ┌Workspace Content──────────────────────────────────────────┐║
║│[Name]                │░│                                                           │║
║│                      │░│ Incident                                              […] │║
║│      [Icon]          │░│                                                  [Search] │║
║│                      │░│ Title                            | Status  | Impact       │║
║│           [ Search ] │░│----------------------------------|---------|--------------│║
║│ Issue                │░│ VPN connection disrupted         | Open    | High     […] │║
║│ ├─ Incident          │░│ Outlook won't start              | Open    | Medium   […] │║
║│ ├─ Problem           │░│ Printer on floor 3 offline       | Assigned| Low      […] │║
║│ └─ ServiceRequest    │░│ File upload fails                | In Prog.| Medium   […] │║
║│                      │░│ Remote desktop not reachable     | Open    | High     […] │║
║│    ┌Workspace───────┐│<│ Password reset not possible      | Closed  | Low      […] │║
║│    │ Edit           ││<│ Wi-Fi outage in conference room  | Open    | High     […] │║
║│    │ Clone          ││<│ Teams notifications delayed      | Assigned| Medium   […] │║
║│    │ Manage Classes ││░│ Scanner not sending PDFs         | Assigned| Low      […] │║
║│    │ Permissions    ││░│ SharePoint access denied         | Open    | High     […] │║
║│    │ <section>      ││░│ Software update blocks startup   | In Prog.| High     […] │║
║│    ├────────────────┤│░│ Screen flickers intermittently   | Closed  | Medium   […] │║
║│    │ Delete         ││░│                                                           │║
║│    └───┬────────────┘│░│                                   ‹ Prev  1  2  3  Next › │║
║├────────¦─────────────┤░│                                                           │║
║│ [+] | [Setting]   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Workspace Management - New/Edit (Modal)

A modal dialog window is used for creating a new or editing an existing workspace. This approach ensures a focused and distraction-free interaction by concentrating the editing process on a single, well-defined task without leaving the context of the parent management page. The modal is opened through dedicated actions such as the "+ New Workspace" button on the management page, an "Edit" command within the workspace table, or via the "Settings" button in the workspace sidebar.

The content of the modal dynamically adapts to the respective use case. In edit mode, the form fields are pre-filled with the data of the selected workspace. Critical, immutable fields such as the unique key ("Key") are read-only in this mode to maintain system integrity and referential consistency. When creating a new workspace, users have the option to derive it from an existing blueprint, which helps standardize configurations and promote system consistency.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkspaceAddEditModal═══════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Workspace / Edit Workspace                                       │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│                                                                      │║─────┐║
║│[Name║│           Name*: [ Service Desk                                    ] │║     │║
║│     ║│            Key*: [ sd                                              ] │║ […] │║
║│     ║│        Category: [                                                 ] │║rch] │║
║│     ║│          Tenant: [ Tenant A, Tenant B                             ▼] │║     │║
║│     ║│          Active: [✓]                                                 │║---- │║
║│ Issu║│Access Modifier*: [ Private                                        ▼] │║ […] │║
║│ ├─ I║│          Sealed: [✓]                                                 │║ […] │║
║│ ├─ P║│       Inherited: [ None                                           ▼] │║ […] │║
║│ └─ S║│     Description: [                                                 ] │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│       Blueprint: [ None                                           ▼] │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║├─────║                                                                        ║     │║
║│ [+] ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Workspace Management - Icon Edit (Modal)

In addition to editing textual and structural properties of a workspace, there is also the option to customize the associated icon. This makes it easier to visually distinguish workspaces within the management interface and the sidebar. In the selected workspace, the current icon is displayed in the sidebar and can be modified via an edit button. A modal window opens, allowing the icon to be replaced and, if necessary, resized or cropped. The system supports standardized icon sets to ensure consistent representation across different tenants, but also permits individual uploads when required. Once saved, the changes are immediately visible in the workspace sidebar without the need to reload the page.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkspaceIconModal══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Edit Workspace Icon                                                  │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│                                                                      │║─────┐║
║│[Name║│             Select image by drag or double-click.                    │║     │║
║│     ║│                ┌────────────────────────────────────┐                │║ […] │║
║│     ║│                │              * * *                 │                │║rch] │║
║│     ║│                │               ***                  │                │║     │║
║│     ║│                │             *** ***                │                │║---- │║
║│ Issu║│                │               ***                  │                │║ […] │║
║│ ├─ I║│                │              * * *                 │                │║ […] │║
║│ ├─ P║│                └────────────────────────────────────┘                │║ […] │║
║│ └─ S║│                Zoom                                                  │║ […] │║
║│     ║│                  ──■───────────────────────────────                  │║ […] │║
║│     ║│                           [Select Image]                             │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│ ┌──────────────────────────────────────────────────────────────────┐ │║ […] │║
║│     ║│ │ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐│ │║ […] │║
║│     ║│ │ │ □ │ │ ◇ │ │ ○ │ │ ◎ │ │ ◍ │ │ ◉ │ │ ◔ │ │ ◕ │ │ ▣ │ │ ◪ │ │ ◫ ││ │║ […] │║
║│     ║│ │ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘ └───┘│ │║ […] │║
║│     ║│ │ Icon  Icon  Icon  Icon  Icon  Icon  Icon  Icon  Icon  Icon  Icon │ │║     │║
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

### Workspace Management - Clone (Modal)

Cloning a workspace allows for the rapid replication of existing structures and is particularly useful for reusing proven configurations. The function is provided via a separate modal dialog window, which can be called from the detail view or the management page.

When cloning, a new workspace is created whose attributes (such as name, description, tags, and metadata) are copied from the original. System-critical properties like the key, creation time, and permissions are newly generated or explicitly requested. The modal allows the user to customize the name and description of the new workspace. Optionally, tags and context-dependent settings can be adopted or modified.

After confirmation, the new workspace is created and automatically integrated into the existing workspace list.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkspaceCloneModal═════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Clone Workspace                                                      │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│                                                                      │║─────┐║
║│[Name║│ You are about to clone the workspace 'Sales Operations'.             │║     │║
║│     ║│ Please adjust the details for the new workspace below.               │║ […] │║
║│     ║│                                                                      │║rch] │║
║│     ║│ Workspace Name*: [ Sales Operations (Copy)                         ] │║     │║
║│     ║│            Key*: [ newkey                                          ] │║---- │║
║│ Issu║│     Description: [ Copy of sales-related workflows and assets.     ] │║ […] │║
║│ ├─ I║│                                                                      │║ […] │║
║│ ├─ P║│   Include structure: [✓]                                             │║ […] │║
║│ └─ S║│ Include permissions: [✓]                                             │║ […] │║
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
║│ [+] ║                                                      [Clone] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Workspace Management - Delete (Modal)

Deleting a workspace is a critical and irreversible operation that requires explicit confirmation from the user. To prevent accidental deletions, a modal dialog window is used. This modal is activated when the user initiates the delete action from the workspace detail view or the management page.

The dialog window clearly indicates which workspace is intended for deletion by explicitly stating its name and key. As an additional security measure, the user must type the key of the workspace to be deleted into a designated input field. Only when the input matches the workspace's key does the final delete button become active. This mechanism ensures that the action is performed consciously and deliberately. In addition to the confirmation button, the modal offers a clear option to cancel the process, allowing the window to be closed without making any changes.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkspaceDeleteModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Delete Workspace                                                     │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│                                                                      │║─────┐║
║│[Name║│ Are you sure you want to delete the workspace 'Sales Operations'?    │║     │║
║│     ║│ This action cannot be undone.                                        │║ […] │║
║│     ║│                                                                      │║rch] │║
║│     ║│ To confirm, please type 'sales-ops' in the box below*:               │║     │║
║│     ║│ [                                                                 ]  │║---- │║
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

### Workspace Management - Permissions Management (Modal)

To manage the access rights of a workspace, a modal dialog window is used, which serves as the central interface for assigning groups to context-specific policies. This modal allows for granular control over which groups may act with which roles and permissions within a workspace.

Access to the modal is granted via the "Permissions" button in the workspace management view. Displaying and using the dialog requires the `workspace_manage_profiles` permission. Within the modal, administrators can select groups and assign them suitable policies, for instance, for read access, editing, or administrative control.

Assignments are displayed in a tabular overview and can be adjusted or removed at any time.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkspacePermissionsModal═══════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│  Manage Permissions for 'Sales Operations'                           │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Works║│                                                                      │║─────┐║
║│[Name║│  Assign Group*: [ Admin ▼]                                           │║     │║
║│     ║│        Policy*: [ workspace_admin_policy ▼]                          │║ […] │║
║│     ║│                                                                      │║rch] │║
║│     ║│  [+ Assign]                                                          │║     │║
║│     ║│                                                             [Search] │║---- │║
║│ Issu║│                                                                      │║ […] │║
║│ ├─ I║│ Assigned Group       | Effective Policy                              │║ […] │║
║│ ├─ P║│----------------------|-----------------------------------------------│║ […] │║
║│ └─ S║│ Admin                | workspace_admin_policy                      X │║ […] │║
║│     ║│ User                 | workspace_view_policy                       X │║ […] │║
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

The sitemap defines the hierarchical structure and navigation paths of the user interface for workspace management. It ensures a clear organization of the pages, serves as the basis for routing within the web application, and is structured as follows:

|Path                                     |Page                  |Description
|-----------------------------------------|----------------------|-------------------------------------------------------------
|`/`                                      |Dashboard             |Central entry point of the application.
|`/workspaces`                            |Workspace Management  |Overview of all workspaces with search, filter, and management functions.
|`/workspaces/add`                        |Workspace Creation    |Form for creating a new workspace.
|`/workspaces/{workspaceKey}`             |Workspace Detail View |Detailed view and actions for a single workspace.
|`/workspaces/{workspaceKey}/edit`        |Workspace Editing     |Form for editing the metadata of an existing workspace.
|`/workspaces/{workspaceKey}/clone`       |Workspace Cloning     |Dialog for replicating an existing workspace with customizable fields.
|`/workspaces/{workspaceKey}/delete`      |Workspace Deletion    |Modal for confirming and executing the irreversible deletion of a workspace.
|`/workspaces/{workspaceKey}/permissions` |Workspace Permissions |Modal for managing profiles (group-policy assignments) for a specific workspace.
|`/workspaces/{workspaceKey}/import`      |Workspace Import      |Import of external workspace schemas.
|`/workspaces/{workspaceKey}/export`      |Workspace Export      |Export of the current workspace schema for reuse or transfer.


## API Interfaces (REST Endpoints)

For programmatic interaction, third-party integration, and automation purposes, **KleeneStar** provides a standardized REST API for managing class definitions within a workspace. The interface adheres to REST principles and uses JSON as the data exchange format. Authentication and authorization are handled by **KleeneStar**. Standard HTTP status codes indicate the outcome of each request, including success, validation errors, permission issues, or missing resources.

The management of workspaces is handled via the following endpoints:

|Endpoint                                                |HTTP Method |Description
|--------------------------------------------------------|------------|------------------------------------------------------------
|`/api/1/workspaces`                                     |GET         |Lists all available workspaces. The results are paginated and can be filtered by status and sorted.
|`/api/1/workspaces`                                     |POST        |Creates a new workspace. Requires at least a `name` and a system-wide unique `key` in the request body.
|`/api/1/workspaces/{workspaceKey}`                      |GET         |Retrieves the detailed information of a specific workspace by its key.
|`/api/1/workspaces/{workspaceKey}`                      |PUT         |Updates the metadata (e.g., `name`, `description`) of an existing workspace. The key is immutable.
|`/api/1/workspaces/{workspaceKey}`                      |DELETE      |Deletes a workspace.
|`/api/1/workspaces/{workspaceKey}/archive`              |POST        |Archives a workspace, placing it in a read-only state.
|`/api/1/workspaces/{workspaceKey}/restore`              |POST        |Restores an archived or temporarily deleted workspace, setting its status to `active`.
|`/api/1/workspaces/{workspaceKey}/profiles`             |GET         |Lists all profiles (group-policy assignments) for the specified workspace. Requires the workspace:manage_profiles permission.
|`/api/1/workspaces/{workspaceKey}/profiles`             |POST        |Creates a new profile, assigning a group to a policy within the workspace. The request body must contain groupId and policyId.
|`/api/1/workspaces/{workspaceKey}/profiles/{profileId}` |DELETE      |Deletes a profile for a specific group from the workspace, thereby revoking the group's permissions.
|`/api/1/workspaces/{workspaceKey}/import`               |POST        |Imports one or more workspace definitions from an external schema (e.g., JSON or YAML).
|`/api/1/workspaces/{workspaceKey}/export`               |GET         |Exports the current workspace schema for backup or reuse.

Standard error responses include `400 Bad Request` for validation errors (e.g., a key that is already taken), `401 Unauthorized` for missing authentication, `403 Forbidden` for insufficient permissions, and `404 Not Found` if the requested resource does not exist. A successful creation (POST) is acknowledged with `201 Created`, while a successful deletion (DELETE) results in a `204 No Content` response.

## Workspace Events

Workspace management utilizes an event-driven architecture model to communicate state changes transparently and reactively throughout the system. Events are published via the `WebExpress-EventManager`, which acts as the central event backbone. This allows other modules, plugins, or external systems to subscribe to relevant changes without being directly coupled to the `WorkspaceManager`.

The following events are published by the `WorkspaceManager` via the `WebExpress-EventManager`:

|Event Name          |Description
|--------------------|-----------------------------------------------------------------------------
|`WorkspaceAdded`    |Triggered when a new workspace has been successfully created.
|`WorkspaceUpdated`  |Signals changes to the metadata of an existing workspace.
|`WorkspaceRemoved`  |Indicates the permanent deletion of a workspace.
|`WorkspaceArchived` |Marks a workspace as archived, placing it in a passive state.
|`WorkspaceRestored` |Reports the restoration of a previously archived or deleted workspace.
|`WorkspaceCloned`   |Triggered when a workspace has been successfully duplicated.

The events contain a structured payload, including:
- The unique workspace key
- Timestamp of the action
- User or module context
- Type and source of the action

Through integration with the `WebExpress-EventManager`, these events are available both within the application and to connected subsystems.

## Permissions Model

The permissions model of **KleeneStar** is applied context-specifically to individual workspaces. The connection between the globally defined groups and policies is established through a Profile, which is valid exclusively within a specific workspace.

A profile defines which policy a global group receives within a specific workspace. It functions as a context-aware role assignment and enables granular and flexible rights management.

- **Principle:** A user receives the rights defined by a policy for a workspace if they are a member of a group for which a corresponding profile (Group → Policy) exists in that workspace.
- **Flexibility:** The same global group (e.g., "Marketing") can be assigned the `workspace_view_policy` (read-only access) in Workspace A and the `workspace_edit_policy` (write access) in Workspace B.
- **Management:** Users with administrative rights for a workspace (e.g., through the `workspace_admin_policy`) can create, edit, and delete profiles. This means they can manage the assignment of policies to groups for their workspace.

The following table lists the granular permissions required for comprehensive control of workspace management.

|Permission                  |Description
|--------------------------- |-----------------------------------------------------------------------------------
|`workspace_create`          |Allows the creation of new, isolated workspaces.
|`workspace_read`            |Grants read access to the metadata of a workspace (name, description, status, etc.).
|`workspace_update`          |Authorizes the modification of an existing workspace's metadata.
|`workspace_delete`          |Allows the permanent deletion of a workspace.
|`workspace_archive`         |Permits the archiving of an active workspace.
|`workspace_restore`         |Enables the restoration of an archived workspace.
|`workspace_clone`           |Authorizes the duplication of an existing workspace.
|`workspace_manage_profiles` |Allows the management of profiles (assignment of policies to groups) for a workspace.
|`workspace_read_content`    |Grants read access to the contents of a workspace (entities, attributes, etc.).
|`workspace_write_content`   |Allows the creation, editing, and deletion of content within a workspace.

These permissions are bundled into logical policies to represent typical use cases and responsibilities. The policies can be assigned to global groups within a c profile.

|Policy                     |Description                                                 |Included Permissions
|---------------------------|------------------------------------------------------------|-------------------------
|`workspace_admin_policy`   |Complete administrative control over a workspace.           |all `workspace_*`
|`workspace_edit_policy`    |Authorizes the management of a workspace's contents.        |`workspace_read`, `workspace_read_content`, `workspace_write_content`
|`workspace_view_policy`    |Grants read-only access to a workspace and its contents.    |`workspace_read`, `workspace_read_content`
|`workspace_creator_policy` |A global policy that allows the creation of new workspaces. |`workspace_create`

## Conclusion

The document "KleeneStar Workspace Management" provides the conceptual foundation for a reference implementation. It outlines core functional requirements across data modeling, architecture, and user interfaces, and defines the lifecycle of a workspace including a flexible permissions model based on context-specific profiles. As a high-level blueprint, it intentionally omits technical detail in key areas. Concurrent access handling is left open and must be defined during implementation, and so are long-running operations like cloning or archiving large workspaces, asynchronous processing, and the retention logic from the state diagram. The focus lies on core functional flows. The other gaps have since been closed by the reference implementation and are no longer open: **persistent storage** is EF Core through `KleeneStarDbContext`; **validation** is the gate of `/api/1/workspaces`, which demands key and name on a create, refuses the reserved keys and refuses duplicates of either (see *The required fields are actually required* in [kleenestar.workspacetemplate.md](kleenestar.workspacetemplate.md)); **user notifications** are raised by the `WorkspaceManager` on every change through `CoreHub.AddNotification`; and the **audit system** is the installation-wide, hash-chained log of [kleenestar.audit.md](kleenestar.audit.md), which subscribes to the workspace manager's events centrally.
