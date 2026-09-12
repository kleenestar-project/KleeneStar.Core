![KleeneStar](https://raw.githubusercontent.com/kleene-star/.github/main/docs/assets/img/banner.png)

# KleeneStar Managed Workflow Concept

The Managed Workflow concept in **KleeneStar** describes the modeled, versioned, and server-side controlled management of object life cycles within a class. A workflow defines which states, transitions, conditions, validations, and follow-up actions are permitted for objects. The goal is to ensure consistent, safe, and traceable process execution, from capture through processing to closure. Workflows are auditable, multi-tenant capable, and closely tied to fields (e.g., status). Workflows are versionable, strictly enforced server-side (including validations and post functions), can be integrated on the UI side, and extended via plugins. This creates a robust, rule-based control system for object-related processes.

A workflow’s authorization is directly coupled to the authorization structure of the associated class. This eliminates a separate authorization management on the workflow level. Access and execution rights automatically follow from the existing class permissions and remain consistent across all states and transitions.

Transitions connect two states and contain metadata (e.g., keys). They can be supplemented by additional elements such as guards, validators, and post functions. A guard is a condition that must be met for a transition to be executed (for example, certain roles, field values, or time windows). A validator checks before execution whether all required rules are met, for instance whether required fields are filled or field values match certain patterns. After a successful transition, post functions can be executed to trigger side effects such as setting field values, creating links, or sending notifications.

Each workflow exists in a versioned form and can move between the states "Draft", "Active", "Archived", and "Deleted". Versioning enables traceable evolution of workflows and comparison of changes over time. When switching to a new version that impacts existing objects, the previous workflow version is retained for each object until it is actively processed. A special identifier marks whether an object still relies on an outdated workflow version. If an object is in a state that no longer exists in the new version, it is automatically reset to the defined initial state of the new version. The user is transparently informed about this process.

## Lifecycle of the Workflow

A workflow follows a clearly defined, versioned life cycle enabling controlled and traceable evolution. Every change starts in the draft state, in which states, transitions, validators, and post functions can be freely modeled. This draft has no effect and does not impact running objects. During this phase, validation and simulation functions are available that check structural consistency and compatibility, such as reachability of end states or uniqueness of keys. Every save operation creates a new revision with timestamp and author, facilitating collaboration.

When a draft is published, it transitions to the active state and becomes the binding basis for all new object transactions of the affected class. In the active state, the workflow is enforced server-side. Object transitions validate against the published state machine, and post functions are executed as configured. Changes never occur directly but always through new drafts, ensuring the active version remains stable until a new version is deliberately activated.

Non-active versions are archived. They are read-only, referenceable, and can be reactivated if needed. Archived versions remain available as long as operational or regulatory requirements demand and serve traceability and historical analysis.

Versions that should be permanently removed are deleted immediately and irrevocably. There is no retention period or downstream review. Removal is final. Before deletion, it is ensured that no active objects still reference the respective version. At the same time, all relevant audit information remains to ensure transparency and traceability. This mechanism selectively relieves the configuration and prevents outdated or superseded drafts from hindering further model maintenance.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                           Workflow Definition Lifecycle                              ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                      new  ╔═══════╗                                                  ║
║                        ───► draft ║                                                  ║
║                           ╚═══════╝                                                  ║
║                               │ publish                                              ║
║                               │                                                      ║
║                           ┌───▼────┐         ┌──────────┐                            ║
║                           │ active │         │ archived │                            ║
║                           └─┬──┬──▲┘         └─┬─▲────┬─┘                            ║
║                             │  │  │  restore   │ │    │                              ║
║                             │  │  └────────────┘ │    │                              ║
║                             │  └─────────────────┘    │                              ║
║                             │        archive          │                              ║
║                             │      ╔═════════╗        │                              ║
║                             └──────► deleted ◄────────┘                              ║
║                                    ╚═════════╝                                       ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Data Model

The data model of Managed Workflows in **KleeneStar** is anchored locally at the class level and forms the basis for structured control of object life cycles. Each workflow is assigned to a specific class and references relevant fields.

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

The application follows a modular, decoupled architectural principle. At its center is the `WorkflowManager`, exclusively responsible for the life cycle and management of all workflows. It manages a collection of versioned workflow instances and provides a controlled interface for all interactions.

Each workflow instance contains central characteristics such as name, associated class, versioning status and access modifiers. New workflows are created exclusively through the `WorkflowManager` to ensure data integrity and consistent access rules.

The `WorkflowManager` performs server-side tasks such as persistent storage of all workflows. At system startup, stored workflows are loaded, state machines initialized, and event subscriptions activated. For every request, the `WorkflowManager` enforces an authorization check for the user. Access is governed by policies that may include context-sensitive filters, time-limited permissions, or audit requirements. This creates a flexible and fine-grained implementation of differentiated read and write rights.

An integrated audit system logs all relevant actions around workflows: accesses, changes, context switches, and permission checks are recorded with timestamp, user ID, workflow ID, and action type. These data support analysis, troubleshooting, compliance review, and state restoration.

```
╔KleeneStar.Core═══════════════════════════════════════════════════════════════════════╗
║                                                                                      ║
║                                ┌────────────────────┐                                ║
║                                │ <<Interface>>      │                                ║
║                                │ IComponentManager  │                                ║
║                                ├────────────────────┤                                ║
║                                └────────Δ───────────┘                                ║
║                                         ¦                                            ║
║                    ┌--------------------┴------------------┐                         ║
║                    ¦                                       ¦                         ║
║     ┌──────────────┴──────────────┐      ┌─────────────────┴────────────────┐        ║
║     │ <<Interface>>               │      │ <<Interface>>                    │        ║
║     │ IStatusManager              │      │ IWorkflowManager                 ├-----┐  ║
║     ├─────────────────────────────┤      ├──────────────────────────────────┤     ¦  ║
║     │ StatusAdded:Event           │      │ WorkflowAdded:Event              │     ¦  ║
║     │ StatusUpdated:Event         │      │ WorkflowUpdated:Event            │     ¦  ║
║     │ StatusRemove:Event          │      │ WorkflowRemoved:Event            │     ¦  ║
║   1 ├─────────────────────────────┤    1 ├──────────────────────────────────┤     ¦  ║
║   ┌─┤ Status:IEnumerable<IStatus> │   ┌──┤ Workflows:IEnumerable<IWorkflow> │     ¦  ║
║   │ ├─────────────────────────────┤   │  ├──────────────────────────────────┤     ¦  ║
║   │ │ Add(IClass, IWorkflow):     │   │  │ Add(IClass, IWorkflow):          │     ¦  ║
║   │ │   IWorkflowSateManager      │   │  │   IWorkflowManager               │     ¦  ║
║   │ │ Get(IClass,filter):         │   │  │ Get(IClass,filter):              │     ¦  ║
║   │ │   IEnumerable<IClass>       │   │  │   IEnumerable<IClass>            │     ¦  ║
║   │ │ Clone(IClass,IWorkflow):    │   │  │ Clone(IClass,IWorkflow):         │     ¦  ║
║   │ │   IWorkflowSateManager      │   │  │   IWorkflowManager               │     ¦  ║
║   │ │ Remove(IClass,IWorkflow):   │   │  │ Remove(IClass,IWorkflow):        │     ¦  ║
║   │ │   IWorkflowSateManager      │   │  │   IWorkflowManager               │     ¦  ║
║   │ └─────────────────────────────┘   │  └──────────────────────────────────┘     ¦  ║
║   │                                   │                                           ¦  ║
║   └─────────────────────┐             └─────────────────────────────┐             ¦  ║
║                       * │                                           │             ¦  ║
║           ┌─────────────▼───────────────┐     ┌────────────────┐    │             ¦  ║
║           │ <<Interface>>               │     │ <<Enum>>       │    │             ¦  ║
║           │ IStatus                     │     | StatusCategory |    │             ¦  ║
║           ├─────────────────────────────┤     ├────────────────┤    │             ¦  ║
║           │ Id:Guid                     │     │ ToDo           │    │             ¦  ║
║           │ Name:string                 │     │ InProgress     │    │             ¦  ║
║           | Category:TypeStatusCategory |     │ Wating         │    │             ¦  ║
║           └──────▲──────────────────────┘     │ Done           │    │             ¦  ║
║                  │ *                          └────────────────┘    │             ¦  ║
║                  │                                                  │             ¦  ║
║                  │              ┌───────────────┐                   │             ¦  ║
║                  │              │ <<Interface>> │                   │             ¦  ║
║                  │              │ IModel        │                   │             ¦  ║
║                  │              ├───────────────┤                   │             ¦  ║
║                  │              └──────Δ────────┘                   │             ¦  ║
║                  │                     ¦                            │             ¦  ║
║                  │                     ¦                            │             ¦  ║
║                  │    ┌────────────────┴──────────────┐ *           │             ¦  ║
║                  │    │ <<Interface>>                 ◄─────────────┘             ¦  ║
║                  │    │ IWorkflow                     │     ┌────────────────┐    ¦  ║
║                  │    ├───────────────────────────────┤     │ <<Enum>>       │    ¦  ║
║                  │    │ Id:Guid                       │     │ WorkflowStatus │    ¦  ║
║                  │    │ Name:String                   │     ├────────────────┤    ¦  ║
║                  │    │ Status:WorkflowStatus         │     │ Draft          │    ¦  ║
║                  │    │ Class:IClass                  │     │ Active         │    ¦  ║
║                  │    │ Created:DateTime              │     │ Archived       │    ¦  ║
║                  │  1 │ Updated:DateTime              │     └────────────────┘    ¦  ║
║                  └────┤ Statuses:IEnumerable<IStatus> │                           ¦  ║
║                  ┌────┤ Transitions:                  │                           ¦  ║
║                  │  1 │   IEnumerable<ITransition>    │                           ¦  ║
║                  │    └──────────────────────────Δ────┘                           ¦  ║
║                  │                               ¦                                ¦  ║
║                  │                               ¦                                ¦  ║
║                  │ *                             ¦                                ¦  ║
║            ┌─────▼────────────────────────┐      ¦                                ¦  ║
║            │ <<Interface>>                │      ¦                                ¦  ║
║            │ ITransition                  │      ¦                                ¦  ║
║            ├──────────────────────────────┤      ¦                                ¦  ║
║            │ Id:Guid                      │      ¦                                ¦  ║
║            │ Source:IStatus               │      ¦                                ¦  ║
║            │ Target:IStatus               │      ¦                                ¦  ║
║            │ Guards:                      │      ¦                                ¦  ║
║            │   IEnumerable<IGuard>        │      ¦                                ¦  ║
║            │ Validators:                  │      ¦                                ¦  ║
║            │   IEnumerable<IValidator>    │      ¦                                ¦  ║
║            │ PostFunctions:               │      ¦                                ¦  ║
║            │   IEnumerable<IPostFunction> │      ¦                                ¦  ║
║            │ Screen:IScreen               │      ¦                                ¦  ║
║            └──────────────────────────────┘      ¦                                ¦  ║
║                                                  ¦                                ¦  ║
║                                  ┌───────────────┴───────────────┐   create       ¦  ║
║                                  │ Workflow                      ◄----------------┘  ║
║                                  ├───────────────────────────────┤                   ║
║                                  │ Id:Guid                       │                   ║
║                                  │ Name:String                   │                   ║
║                                  │ State:WorkflowStatus          │                   ║
║                                  │ Class:IClass                  │                   ║
║                                  │ Created:DateTime              │                   ║
║                                  │ Updated:DateTime              │                   ║
║                                  │ Statuses:IEnumerable<IStatus> │                   ║
║                                  │ Transitions:                  │                   ║
║                                  │   IEnumerable<ITransition>    │                   ║
║                                  └───────────────────────────────┘                   ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The workflow management architecture in **KleeneStar.Core** is based on a component-oriented model that enables a clear separation of responsibilities and high extensibility. At its center is the `IWorkflowManager`, which as part of the `IComponentManager` forms the central interface for managing workflows. It controls the creation, update, removal, and querying of workflows and provides events such as `StatusAdded`, `StatusUpdated`, `StatusRemoved`, `WorkflowAdded`, `WorkflowUpdated`, and `WorkflowRemoved` to enable reactive and loosely coupled system integration.

A workflow is modeled through the IWorkflow interface and contains key properties such as a unique ID, name, status (`TypeWorkflowState`), associated class (`IClass`), timestamps for creation and update, and access control via AccessModifier. Access levels such as Private, Protected, Internal, or Public govern visibility and usability of the workflow within and across tenants.

Each workflow consists of a set of states (`IStatus`) and transitions (`ITransition`). States are defined by an ID, a name, and a category (`TypeStatusCategory`) such as "ToDo", "InProgress", "Waiting", or "Done". Transitions connect two states and include, besides source and target, a collection of conditions (Guards), validation rules (Validators), follow-up actions (PostFunctions), and optional UI elements (Form). This structure enables precise control of the object life cycle within a workflow.

Workflows are versionable and follow defined states such as "Draft", "Active", or "Archived".

## UI Concepts and Pages

The following UI mockups show how the complex structures and rules of workflow management in **KleeneStar** are translated into a comprehensible and user-friendly interface. The goal is to make working with workflows as intuitive, efficient, and safe as possible.

The user interface consistently follows the established design principles of the **KleeneStar** web application. This creates a familiar user experience with clear procedures and recognizable elements. Onboarding remains short, and typical tasks such as creating, editing, or activating workflows can be performed quickly and reliably.

The mockups serve as visual templates for the final UI design. They show how navigation is structured, where central controls are placed, and how different workflow states (e.g., "Draft") are represented. Concrete procedures illustrate how workflows are created, versioned, published, archived, or deleted. Particular emphasis is placed on clarity and visual feedback for actions, e.g., when accessing transitions, states, or configuration functions.

### Class Management (Page)

The Class Management page forms the central administrative interface for all class types within a workspace and is closely integrated with **KleeneStar**'s Workflow Manager. In addition to structured class maintenance, including functions such as create, edit, clone, archive and delete, it serves as the starting point for assigning and controlling versioned workflows.

The tabular overview displays important attributes per class such as name, description and status. Additionally, there is direct access to each class’s associated workflows: via the options menu, the Manage Workflows function is available to create, version, activate or archive workflows.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Classes                                                           │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Classes───────────────┐ ┌Classes Content────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│                                    [Search] [+ Add Class] │║
║│  - Issues            │░│                                                           │║
║│  - Sub-Issues        │░│ Class Name       | Description                 | Status   │║
║│  - Hidden            │░│------------------|-----------------------------|----------│║
║│  - Archived          │░│ Incident         | Report of a disruption      | ...  […] │║
║│                      │░│ Problem          | Analysis of recurring errors| ...   ¦  │║
║│                      │░│ ChangeRequest    | Request for chang┌──────────────────┴┐ │║
║│                      │░│ ServiceRequest   | Standard service │ Edit              │ │║
║│                      │░│ KnowledgeArticle | Documented knowle│ Clone             │ │║
║│                      │<│ Approval         | Approval step    │ Manage Fields     │ │║
║│                      │<│ Request          | Inquiry or sub-pr│ Manage Statuses   │ │║
║│                      │<│ Task             | Executable activi│ Manage Workflows  │ │║
║│                      │░│ SLA              | Service Level Agr│ Manage Priorities │ │║
║│                      │░│ Comment          | Free-text note   │ Manage Forms      │ │║
║│                      │░│ UserFeedback     | User feedback    │ Permissions       │ │║
║│                      │░│ Escalation       | Escalation to hig│ <section>         │ │║
║│                      │░│                                     ├───────────────────┤ │║
║│                      │░│                                   ‹ │ Delete            │ │║
║├──────────────────────┤░│                                     └───────────────────┘ │║
║│ [Setting]         << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Status Management (Page)

Status Management forms the central interface for maintaining all states used within workflows of a class. Each status describes a phase in an object’s life cycle and serves as the target or starting point for transitions. The page allows the creation of new status values, the editing of existing entries, and the safe deletion of no longer needed states.

For each status, properties such as name, description, and category can be defined. Changes directly affect workflow modeling and are versionable. The page provides a tabular overview of all status values in a class, including usage proof in active workflows.

Status maintenance is accessed directly from Class Management via the "Manage Status" menu item. New statuses can be added using the "Add Status" button. Existing entries can be edited or deleted via action menus, provided they are no longer referenced. This achieves a consistent, traceable, and flexibly expandable state logic for all object-related processes.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / Status                                                 │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Status────────────────┐ ┌Status Content─────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│ My Statuses                        [Search] [+ Add Status]│║
║│  - ToDo              │░│                                                           │║
║│  - InProgress        │░│ Status Name  | Category   | Description                   │║
║│  - Waiting           │░│--------------|------------|-------------------------------│║
║│  - Done              │░│ ToDo         | ToDo       | Newly created             […] │║
║│                      │░│ InProgress   | InProgress | In progress                ¦  │║
║│                      │░│ Review       | InProgress | Under review  ┌────────────┴┐ │║
║│                      │░│ Done         | Done       | Completed     │ Edit        │ │║
║│                      │░│                                           │ Clone       │ │║
║│                      │░│                                   ‹ Prev  │ <section>   │ │║
║│                      │<│                                           ├─────────────┤ │║
║│                      │<│                                           │ Delete      │ │║
║│                      │<│                                           └─────────────┘ │║
║│                      │░│                                                           │║
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

### Workflow Management - New/Edit Status (Modal)

The modal for creating and editing status values in Status Management is used to maintain the basic properties of a status used within a workflow. It opens when a new status is to be added or an existing one edited. Central details such as name, description, and category can be captured or adjusted.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔StatusAddEditModal══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Status / Edit Status                                             │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Statu║│                                                                      │║─────┐║
║│     ║│                                                                      │║     │║
║│  - A║│           Name*: [                                                 ] │║─────┤║
║│  - T║│        Category: [ ToDo                                           ▼] │║     │║
║│  - I║│     Description: [                                                 ] │║atus]│║
║│  - W║│                                                                      │║     │║
║│  - D║│                                                                      │║     │║
║│     ║│                                                                      │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
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

### Workflow Management - Clone Status (Modal)

The clone modal exclusively serves to duplicate an existing status value within Status Management. It opens when an existing status is to be used as a template for a new entry. The aim is to quickly adopt the properties of an existing status and adjust them, without changing the original.

In the modal, the fields name, category, and description are automatically prefilled with the source status values. The user can adjust these to uniquely name and describe the new status in context. Cloning facilitates reuse of proven status configurations and accelerates model maintenance, especially for similar process phases or parallel workflows.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔StatusCloneModal════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Clone Status                                                         │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Statu║│                                                                      │║─────┐║
║│     ║│ You are about to clone the status 'Review'.                          │║     │║
║│  - A║│ Please adjust the details for the new status below.                  │║─────┤║
║│  - T║│                                                                      │║     │║
║│  - I║│     Neuer Name*: [ Review Copy                                     ] │║atus]│║
║│  - W║│        Category: [ ToDo                                           ▼] │║     │║
║│  - D║│     Description: [ Copy of Status 'Review'                         ] │║     │║
║│     ║│                                                                      │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
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

### Workflow Management - Delete Status (Modal)

The delete modal serves the final removal of an existing status value from Status Management. It opens when a status is no longer needed or should be removed for reasons of consistency and clarity.

However, a status can only be deleted if it is not used in any workflow. This applies to active, archived, and draft workflows alike. This check happens automatically before the deletion is permitted. If the status is still in use, the user is informed accordingly and the deletion is blocked.

When the modal opens, the name of the status to be deleted is displayed clearly to avoid accidental deletions. The user receives a safety prompt stating that this operation cannot be undone. Only after explicit confirmation and successful availability check is the status permanently removed from the system.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔StatusDeleteModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Delete Status                                                        │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Statu║│                                                                      │║─────┐║
║│     ║│ Are you sure you want to delete the status 'Review'?                 │║     │║
║│  - A║│ This action cannot be undone. Active status cannot be deleted.       │║─────┤║
║│  - T║│                                                                      │║     │║
║│  - I║│ To confirm, please type 'Review' in the box below:                   │║atus]│║
║│  - W║│ [                                                                  ] │║     │║
║│  - D║│                                                                      │║     │║
║│     ║│                                                                      │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
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

### Workflow Management (Page)

This page provides the central administration view for all workflows assigned to a class. It offers a comprehensive overview of all workflow definitions of a class, including version, status (e.g. "Draft), and relevant metadata. For efficient model maintenance, targeted functions are available: creating new drafts, editing existing drafts in the graphical designer, simulating transitions, publishing reviewed versions, and archiving obsolete workflows.

Search and filter functions facilitate navigation, especially for classes with multiple workflow variants or extensive version histories. New workflows (e.g., for alternative process paths or special cases) can be conveniently created via the "Add Workflow" button. Access to this page occurs directly from Class Management via the "Manage Workflows" menu.

Published workflows directly affect the process control of the associated objects. Drafts, on the other hand, remain without consequence for ongoing operations until explicitly published and enable risk-free evolution. The page thus supports both operational control and the controlled evolution of workflows within the class context.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / Workflows                                              │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workflows─────────────┐ ┌Workflows Content──────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│                                  [Search] [+ Add Workflow]│║
║│  - Drafts            │░│                                                           │║
║│  - Active            │░│ Workflow Name     | Version | Status                      │║
║│  - Archived          │░│-------------------|---------|-----------------------------│║
║│                      │░│ Incident Default  | v2      | Active                  […] │║
║│                      │░│ Incident Draft    | v3      | Draft                    ¦  │║
║│                      │░│ Escalation Flow   | v1      | Archived    ┌────────────┴┐ │║
║│                      │░│                                           │ Edit        │ │║
║│                      │░│                                   ‹ Prev  │ Clone       │ │║
║│                      │░│                                           │ Publish     │ │║
║│                      │<│                                           │ Versions    │ │║
║│                      │<│                                           │ Permissions │ │║
║│                      │<│                                           │ <section>   │ │║
║│                      │░│                                           ├─────────────┤ │║
║│                      │░│                                           │ Delete      │ │║
║│                      │░│                                           └─────────────┘ │║
║├──────────────────────┤░│                                                           │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Workflow Designer

The Workflow Designer is a central tool for modeling and maintaining state logic. It supports both visual and tabular editing modes, providing maximum flexibility for different user needs.

In the visual view, workflows can be designed intuitively via drag-and-drop, while the tabular view enables structured maintenance of complex models in list form. Both variants offer comprehensive functions for configuring states, transitions, validation rules, and follow-up actions.

#### Workflow Designer - Visual View (Page)

The Workflow Designer enables visual modeling of a class’s state machine with drag-and-drop support, direct editing of state and transition properties, and embedded validation and publication control. States are arranged as nodes on a canvas, and transitions are represented as directed edges. A properties panel provides context-sensitive settings, including labels, guards, validators, and post functions. Changes to the draft are revision-safe. Before publication, the designer consistently checks the reachability of all end states and reference integrity to forms.

The designer **is** the page rather than a block on it, and takes the height the content region offers (`ControlDataWorkflow.Fill`, set by `WorkflowDetailViewFragment`): the canvas and the properties pane end where the pane does and scroll on their own, instead of a fixed frame leaving the rest of the page empty below it. The viewport arithmetic this used to be (`100vh` minus a guessed chrome height) is gone — it was never applied anyway, because the rule it fed was keyed on the controller class the framework strips at mount, and the designer sat at the graph editor's 400px floor.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│  / Service Desk / Incident / Incident Draft / Designer                             │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workflow─┬──────┬───────────────────────────────────────────────────────────────────┐║
║│ Visual  │ Text │                                                                   │║
║│         └──────┴──────────────────────────────────────────────┬────────────────────┤║
║│                                                               │ Transition         │║
║│  new ┌──────┐                                                 │                    │║
║│  ---►│ Open ├─────┐                                           │  Edit              │║
║│      └──────┘     │                                           │  Guards (0         │║
║│                   ▼                                           │  Validators (0)    │║
║│            ┌─────────────┐                                    │  PostFunctions (5) │║
║│            │ In Progress ├─────┐                              │  <section>         │║
║│            └─────────────┘     │                              │  Delete            │║
║│                   ▲            ▼                              │                    │║
║│                   │       ┌────────┐                          │                    │║
║│                   └───────┤ Review ├─────┐                    │                    │║
║│                           └────────┘     │                    │                    │║
║│                                          ▼                    │                    │║
║│                                      ┌──────┐                 │                    │║
║│                                      │ Done │                 │                    │║
║│                                      └──────┘                 │                    │║
║│                                                               │                    │║
║└───────────────────────────────────────────────────────────────┴────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Workflow Designer - Tabular View (Page)

The tabular workflow management allows faithful maintenance of states and transitions without a graphical canvas. States and transitions are displayed in separate tables with clear columns for keys, labels, typings, as well as rules and actions. Inline actions allow quick editing, configuration of guards, validators, and post functions. Drafts are stored revision-safely. Before publication, the system checks the reachability of all end states and reference integrity to forms.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│  / Service Desk / Incident / Incident Draft / Designer                             │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Workflow─┬──────┬───────────────────────────────────────────────────────────────────┐║
║│ Visual  │ Text │                                                                   │║
║├─────────┘      └───────────────────────────────────────────────────────────────────┤║
║│  Source*: [ Open      ▼]                                                           │║
║│  Target*: [ Done      ▼]                                                           │║
║│                                                                                    │║
║│  [+ AddTransition]                                                                 │║
║│                                                                                    │║
║│ Label           | Source      | Target                                             │║
║│-----------------|-------------|----------------------------------------------------│║
║│ Submit          | Open        | In Progress                                    […] │║
║│ Request review  | In Progress | Review                                         […] │║
║│ Approve         | Review      | Done                                            ¦  │║
║│ Reject          | Review      | In Progress                      ┌──────────────┴┐ │║
║│                                                                  │ Edit          │ │║
║│                                                                  │ Guards        │ │║
║│                                                                  │ Validators    │ │║
║│                                                                  │ PostFunctions │ │║
║│                                                                  │ <section>     │ │║
║│                                                                  ├───────────────┤ │║
║└──────────────────────────────────────────────────────────────────│ Delete        │─┘║
║┌Footer────────────────────────────────────────────────────────────└───────────────┘─┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Workflow Management - New/Edit Transition (Modal)

The modal for creating and editing transitions in workflow management is used to maintain the basic information of a transition. It opens when a new transition is to be added or an existing one edited. Central properties such as label and description can be configured. More complex settings such as guards, validators, post functions, or UI options are not part of the modal and, if necessary, must be performed via advanced editing functions or the tabular view. The modal aims at a quick, focused capture of the core information required for the workflow’s structure and functionality.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkflowAddEditModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Workflow / Edit Workflow                                         │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Workf║│                                                                      │║─────┐║
║│ Visu║│                                                                      │║     │║
║│     ║│          Label*: [                                                 ] │║─────┤║
║│     ║│            Form: [                                                ▼] │║     │║
║│  new║│     Description: [                                                 ] │║     │║
║│  ---║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║)    │║
║│     ║│                                                                      │║ (5) │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║│     ║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Workflow Management - Guard Management (Modal)

In the Workflow Designer, the Guard Management modal provides a targeted way to add conditions to transitions that control their executability. Guards define under which prerequisites a transition may be triggered within a workflow, e.g., based on user roles, group memberships, field values, or custom attributes. The modal opens when an existing transition is to be supplemented with a guard condition or an existing one adjusted.

Configuration is carried out via a structured interface that supports both simple conditions and complex logical combinations. AND and OR branches can be used to combine multiple criteria. Integrated validation checks the syntax and consistency of inputs, while a description documents the condition’s logic. The modal aims at precise, traceable, and safe control of transition logic that meets the requirements of complex workflows.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkflowGuardModal══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Guard / Edit Guard                                               │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Workf║│                                                                      │║─────┐║
║│ Visu║│  Guard*: [                                                        ▼] │║     │║
║│     ║│                                                                      │║─────┤║
║│     ║│  [+ AddGuard]                                                        │║     │║
║│  new║│                                                                      │║     │║
║│  ---║│ Guard condition:                                                     │║     │║
║│     ║│    AND                                                             x │║     │║
║│     ║│    ├─ user role == "Reviewer"                                      x │║)    │║
║│     ║│    ├─ field "Priority" == "High"                                   x │║ (5) │║
║│     ║│    └─ OR                                                           x │║     │║
║│     ║│        ├─ group == "IncidentManager"                               x │║     │║
║│     ║│        └─ user attribute "Region" == "EMEA"                        x │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║│     ║                                                                        ║     │║
║│     ║                                                                [Done]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Workflow Management - Validator Management (Modal)

In the Workflow Designer, the Validator Management modal enables targeted configuration of validation rules evaluated before a transition is executed. Validators ensure that specific prerequisites are met before a state change may occur, for example completeness of required fields, compliance with specified formats, or fulfillment of business conditions. The modal is invoked when an existing transition is to be supplemented with a new validation rule or an existing rule adjusted.

Within the modal, simple validation rules can be defined, such as whether a particular field is filled or a date is set correctly. For more complex requirements, multiple conditions can be logically combined, either requiring that all are fulfilled (AND) or at least one of several suffices (OR).

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkflowValidatorModal══════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Validator / Edit Validator                                       │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Workf║│                                                                      │║─────┐║
║│ Visu║│  Validator*: [                                                    ▼] │║     │║
║│     ║│                                                                      │║─────┤║
║│     ║│  [+ AddValidator]                                                    │║     │║
║│  new║│                                                                      │║     │║
║│  ---║│ Validator rules:                                                     │║     │║
║│     ║│    AND                                                             x │║     │║
║│     ║│    ├─ field "Summary" != empty                                     x │║)    │║
║│     ║│    ├─ field "DueDate" >= today                                     x │║ (5) │║
║│     ║│    └─ OR                                                           x │║     │║
║│     ║│        ├─ field "Category" == "Urgent"                             x │║     │║
║│     ║│        └─ user attribute "ClearanceLevel" >= 3                     x │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║│     ║                                                                        ║     │║
║│     ║                                                                [Done]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Workflow Management - Postfunction Management (Modal)

In the Workflow Designer, the Postfunction Management modal enables targeted configuration of actions that run automatically after a successful transition. Post functions are used to trigger follow-up processes, change data, or initiate system interactions, e.g., setting a field value, sending a notification, or creating a linked object. The modal opens when an existing transition is to be supplemented with a new post function or an existing one adjusted.

Within the modal, simple actions can be selected and configured directly, for example automatically setting a status or adding a comment. For more complex sequences, multiple post functions can be combined and ordered. The interface supports both standard functions and custom extensions, with a description to facilitate traceability of the configuration.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkflowPostfunctionModal═══════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Postfunction / Edit Postfunction                                 │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Workf║│                                                                      │║─────┐║
║│ Visu║│  Postfunction*: [                                                 ▼] │║     │║
║│     ║│                                                                      │║─────┤║
║│     ║│  [+ AddValidator]                                                    │║     │║
║│  new║│                                                                      │║     │║
║│  ---║│ Postfunction actions:                                                │║     │║
║│     ║│    1. Set field "Status" = "In Progress"                           x │║     │║
║│     ║│    2. Add comment: "Processing has started."                       x │║)    │║
║│     ║│    3. Send notification to group "SupportTeam"                     x │║ (5) │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║│     ║                                                                        ║     │║
║│     ║                                                                [Done]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Workflow Management - Cloning (Modal)

Cloning a workflow enables quick reuse of proven process models within the same class. Cloning always creates a new workflow definition in the "Draft" state. Published versions remain unaffected. States and transitions, including guards, validators, and post functions, are taken over. System-critical identity characteristics such as the internal version identifier and publication metadata are newly generated. The wizard supports renaming and optional target class selection. Once confirmed, the new draft is integrated into the class’s workflow list. The cloned definition only becomes effective after successful validation and publication.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔WorkflowCloneModa═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Clone Workflow                                                       │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Workf║│                                                                      │║─────┐║
║│     ║│ You are about to clone the workflow 'Incident Default (v2)'.         │║     │║
║│  - A║│ Please adjust the details for the new workflow below.                │║flow]│║
║│  - D║│                                                                      │║     │║
║│  - A║│ Workflow Name*: [ Incident Default (Copy)                          ] │║     │║
║│  - A║│    Description: [ Draft cloned from v2.                            ] │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│   Note: Cloned workflows start as Draft and must be published to     │║     │║
║│     ║│         become effective.                                            │║     │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Clone] [Cancel] ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Workflow Management - Deletion (Modal)

Deleting a workflow definition is a critical and irreversible operation and requires explicit confirmation. The workflow is started via its own modal dialog, either from the workflow detail view or via the workflow list. To prevent unintentional deletions, the dialog clearly identifies which workflow and, if applicable, which version ("Draft" or "Archived") is affected. For security reasons, the workflow definition key must be typed into an input field. For version-specific deletion, input in the format key@version is required (e.g., incident-default@v3). Only when the input exactly matches the expected key will the "Delete" button be enabled. The action can be canceled at any time via "Cancel" without making changes.

Active workflows (Active) cannot be deleted directly. They must first be archived or replaced by another published version to ensure no running objects depend on the definition. For drafts and archived versions, deletion leads to permanent removal of the draft. Audit data and publication histories remain according to governance requirements.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]                     │║
║└─────╔═WorkflowDeleteModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Wo║│ Delete Workflow                                                      │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Workf║│                                                                      │║─────┐║
║│     ║│ Are you sure you want to delete the workflow                         │║     │║
║│  - A║│ 'Incident Default' (version 'v3', Draft)?                            │║flow]│║
║│  - D║│ This action cannot be undone. Active workflows cannot be deleted.    │║     │║
║│  - A║│                                                                      │║     │║
║│  - A║│ To confirm, please type 'incident-default@v3' in the box below:      │║-----│║
║│     ║│ [                                                                  ] │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│ Note: Published (active) versions must be archived before deletion.  │║     │║
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

## Sitemap

The sitemap defines navigation paths and visibility logic for workflow management within the application. It forms the basis for routing in the context of workspace and class and allows deep-linkable addresses for list views, detail/edit views (designer or table view), publication, versioning, as well as archiving and deletion. Additionally, status maintenance, transition detail routes, permission and trigger management, and validation/simulation are addressable.

|Path                                                                                                          |Page/View                       |Description
|--------------------------------------------------------------------------------------------------------------|--------------------------------|---------------------------------------------
|`/workspaces/{workspaceKey}/classes`                                                                          |Class Management                |Overview of all classes in a workspace.
|`/workspaces/{workspaceKey}/classes/{classKey}`                                                               |Class Detail                    |Detail/entry page of a class incl. actions.
|`/workspaces/{workspaceKey}/classes/{classKey}/status`                                                        |Status Management               |List and administration of all statuses of a class.
|`/workspaces/{workspaceKey}/classes/{classKey}/status/new`                                                    |Status New (Modal)              |Create a new status value.
|`/workspaces/{workspaceKey}/classes/{classKey}/status/{statusId}/edit`                                        |Status Edit (Modal)             |Edit an existing status value.
|`/workspaces/{workspaceKey}/classes/{classKey}/status/{statusId}/clone`                                       |Status Clone (Modal)            |Duplicate a status as a template.
|`/workspaces/{workspaceKey}/classes/{classKey}/status/{statusId}/delete`                                      |Status Delete (Modal)           |Final removal of a non-referenced status.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow`                                                      |Workflow Management             |Designer, lists, matrix, versions; filter/searchable.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow?filter={draft|active|archived}`                       |Workflow Management             |Filtered list view by status.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}`                                         |Workflow Designer               |Detail view of a workflow with graphical modeling.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/designer`                                |Workflow Designer               |Explicit designer route (default).
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/designer?mode=visual`                    |Workflow Designer (Visual)      |Visual canvas mode.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/designer?mode=table`                     |Workflow Designer (Tabular)     |Table/list mode for states/transitions.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/edit`                                    |Workflow Edit                   |Edit workflow metadata and settings.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/transitions`                             |Transitions List                |Overview of all transitions of a workflow.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/transitions/{transitionId}`              |Transition Edit                 |Edit label/description/form.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/transitions/{transitionId}/guards`       |Guard Management (Modal)        |Add/edit guard conditions.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/transitions/{transitionId}/validators`   |Validator Management (Modal)    |Add/edit validation rules.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/transitions/{transitionId}/postfunctions`|Postfunction Management (Modal) |Configure follow-up actions.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/permissions`                             |Permissions Management          |Workflow-wide/transition-specific permissions.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/simulate`                                |Workflow Simulation             |Simulation/what-if view, optionally with object context.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/validate`                                |Workflow Validate               |Validation/output of report (synchronous).
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/publish`                                 |Workflow Publish                |Publication, mappings, report; sync/async.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/versions`                                |Workflow Versions               |List versions (Active, Archive, Draft history).
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/versions/{version}`                      |Workflow Version Detail         |Show a specific version incl. states/transitions.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/archive`                                 |Workflow Archive                |Archive an active version incl. checks.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/restore`                                 |Workflow Restore                |Restore an archived version (compatible).
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/copy`                                    |Workflow Clone                  |Duplicate an existing workflow as draft.
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/{workflowId}/delete`                                  |Workflow Delete                 |Remove a workflow (after archiving).
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/import`                                               |Class Import                    |Import external workflow definitions (dry run, conflict strategy).
|`/workspaces/{workspaceKey}/classes/{classKey}/workflow/export`                                               |Class Export                    |Export definitions/versions (selection via parameters).

## API Interfaces (REST Endpoints)

For programmatic access, integration of external systems, and process automation, **KleeneStar** provides a standardized REST API. This interface enables workflow management at the class level and adheres to REST principles. JSON is used as the data exchange format. Authentication and authorization are handled by **KleeneStar**. The status of each request is communicated using standardized HTTP status codes, including successful execution, validation errors, access issues, or missing resources. In addition to direct, synchronous operations such as reading or saving drafts, the API also supports asynchronous processes.

Workflow management is performed via the following endpoints:

|Endpoint                                                                                                                              |HTTP Method |Description
|--------------------------------------------------------------------------------------------------------------------------------------|------------|----------------------------------------------------------------------
|`/api/workspaces/{workspaceKey}/classes/{classKey}/statuses`                                                                          |GET         |Lists all statuses of a class; filter by category/usage.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/statuses`                                                                          |POST        |Creates a status (name, category, description); checks uniqueness.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/statuses/{statusKey}`                                                              |GET         |Details for a status.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/statuses/{statusKey}`                                                              |PUT         |Updates name/category/description; denies incompatible changes when in use.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/statuses/{statusKey}`                                                              |DELETE      |Deletes a non-referenced status.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/statuses/{statusKey}/usage`                                                        |GET         |Usage proof (referencing workflows/versions/transitions).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows`                                                                         |GET         |Lists all workflows of a class in the workspace. Supports filters (status, version) and pagination.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows`                                                                         |POST        |Creates a new workflow definition in Draft state. Requires at least `name` and a class-unique `name`.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}`                                                           |GET         |Returns metadata and state (Draft/Active/Archived) of a workflow definition; optionally with embedded states/transitions via `?include=states,transitions`.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}`                                                           |PUT         |Updates the draft definition (states, transitions, guards, validators, post functions). The workflow key is immutable. Uses ETag for concurrent changes.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}`                                                           |DELETE      |Deletes a workflow definition in Draft or Archived state. Active workflows must be archived or replaced first.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/clone`                                                     |POST        |Creates a new workflow draft by cloning an existing definition. Optional scope: rules, permissions, triggers, screen overrides.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/validate`                                                  |POST        |Validates the current draft definition (graph consistency, key uniqueness, reference integrity, screen/field compatibility) and returns a report.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/publish`                                                   |POST        |Publishes the reviewed draft to the active version. Supports optional status mappings in the request body for incompatible changes. May respond synchronously (200) or asynchronously (202).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/versions`                                                  |GET         |Lists versions (Active, Archive, Draft history) with diff metadata.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/versions/{version}`                                        |GET         |Retrieves a specific version of the workflow definition with associated states/transitions.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/archive`                                                   |POST        |Archives an active workflow version. Requires security-relevant checks regarding running dependencies.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/restore`                                                   |POST        |Restores an archived workflow version as active (if compatible).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/permissions`                                               |GET         |Lists all permission assignments (group-policy) workflow-wide and optionally per transition (`?scope=workflow|transition`).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/import`                                                                  |POST        |Imports one or more workflow definitions (e.g., JSON/YAML). Supports dry run (`?dryRun=true`) and conflict strategy.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/export`                                                                  |GET         |Exports workflow definition(s) for backup or reuse; supports selection via `workflowKey`/`version`.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions`                                               |GET         |Lists all transitions of a workflow (draft context).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions`                                               |POST        |Creates a transition (label, source, target, optional form/description).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}`                                |GET         |Details for a transition; `include=guards,validators,postfunctions` optional.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}`                                |PUT         |Updates label, description, form, and references (draft).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}`                                |DELETE      |Removes a transition (draft).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/guards`                         |GET         |Lists all guards of a transition.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/guards`                         |POST        |Adds a guard (type, expression/parameters, description).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/guards/{guardId}`               |PUT         |Updates a guard.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/guards/{guardId}`               |DELETE      |Deletes a guard.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/validators`                     |GET         |Lists all validators of a transition.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/validators`                     |POST        |Adds a validator (type, rules, logical combination).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/validators/{validatorId}`       |PUT         |Updates a validator.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/validators/{validatorId}`       |DELETE      |Deletes a validator.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/postfunctions`                  |GET         |Lists all post functions of a transition.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/postfunctions`                  |POST        |Adds a post function (type, parameters, order).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/postfunctions/{postFunctionId}` |PUT         |Updates a post function including order.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/{workflowKey}/transitions/{transitionId}/postfunctions/{postFunctionId}` |DELETE      |Deletes a post function.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/import?dryRun=true|false`                                                |POST        |Imports one or more workflows; dry run returns a report without persistence.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/workflows/export?keys=a,b&versions=v2`                                             |GET         |Exports selected workflows/versions as a bundle (JSON/YAML).

The API communicates the outcome of each request using standardized HTTP status codes. In case of errors, common responses include **400 Bad Request**, which indicates issues such as invalid definitions, incorrect mappings, or missing required attributes. A **401 Unauthorized** response signals missing or invalid authentication credentials, while **403 Forbidden** denotes insufficient permissions, for example when required roles like `workflow_update` or `workflow_publish` are not granted. If a requested workflow, class, or workspace cannot be found, the API returns **404 Not Found**. A **409 Conflict** may occur due to version mismatches (e.g., ETag conflicts), attempts to delete active workflows, or publication actions that interfere with the current state. Additionally, **422 Unprocessable Entity** is used when rule violations, guard conditions, validator failures, or unresolvable status mappings prevent successful processing.

For successful operations, the API returns **201 Created** when new resources are generated via POST requests, such as drafts, clones, or triggers. A **200 OK** response confirms successful execution of GET, PUT, or POST requests, including validation or simulation reports. When an operation is initiated for asynchronous processing, such as publishing workflows in large environments, the API responds with **202 Accepted**. Finally, **204 No Content** is returned when DELETE operations complete successfully or when no response body is required.

## Workflow Events

Workflow management follows an event-driven architectural principle to communicate state changes transparently and reactively within the system. Events are published via the central **WebExpress** `EventManager` and allow modules, plugins, and external systems to react to changes without being tightly coupled to the `WorkflowManager`. This deterministically and traceably triggers UI updates, auditing, integrations, and synchronizations.

|Event                        |Description
|-----------------------------|--------------------------------------------------------------------------------------------------------------
|`StatusCreated`              |Emitted when a new status is created within a class.
|`StatusUpdated`              |Signals changes to an existing status (name, category, description).
|`StatusCloned`               |Emitted when a status is duplicated from an existing status.
|`StatusDeleted`              |Signals the final removal of a status, provided it is no longer referenced.
|`WorkflowCreated`            |Emitted when a new workflow definition (Draft) is created within a class.
|`WorkflowUpdated`            |Signals changes to an existing draft definition (states, transitions, guards, validators, post functions).
|`WorkflowValidated`          |Emitted after a draft definition has been successfully reviewed; contains the validation report.
|`WorkflowValidationFailed`   |Emitted when validation fails; contains validation errors and affected elements.
|`WorkflowPublished`          |Emitted when a reviewed draft is published as the active version; includes version information and status mappings.
|`WorkflowArchived`           |Marks a previously active workflow version as archived; subsequently read-only.
|`WorkflowRestored`           |Reports restoration of an archived version as active, if compatible.
|`WorkflowDeleted`            |Signals final removal of a draft or archived workflow definition after retention and dependency checks.
|`WorkflowCloned`             |Emitted when a workflow definition is successfully cloned and created as a draft.
|`WorkflowPermissionsChanged` |Indicates that permission assignments have changed workflow-wide or transition-specific.
|`WorkflowTriggerAdded`       |Emitted when a time/event-based trigger (e.g., SLA) is created.
|`WorkflowTriggerRemoved`     |Emitted when a trigger is deleted.
|`WorkflowTriggerFired`       |Reports execution of a trigger (including referenced transition/action).
|`WorkflowPostFunctionFailed` |Signals an error during execution of a post function; contains context for retry/error analysis.

Each event contains a structured payload with at least the following information:
- Workflow key, associated class key, and workspace key
- Version information (e.g., `v3`) and state of the definition (Draft, Active, Archived)
- Timestamp of the event
- Triggering user or module context
- Action type and source (e.g., API, UI, Scheduler)
- Optional delta summary (e.g., number of changed states/transitions)

Events are available both within the application and to connected subsystems and enable reactive UI updates, revision-safe audit logs, plugin hooks, as well as external synchronization with downstream systems.

## Permissions Model

**KleeneStar** manages workflow-related permissions context-specifically at the class level. Global groups are bound via profiles to workflow-wide policies or transition-specific rights. This model enables precise control over who may read, design, validate, simulate, publish, archive, or delete workflow definitions and who may execute specific transitions on objects. Assignments can apply workflow-wide or be explicitly stored for individual transitions, optionally with conditions (e.g., segregation of duties, role- or field-value-based expressions). Enforcement is performed server-side in the `WorkflowManager` and consolidated with class, field, and object permissions. All changes are auditable.

- **Assignment logic:** A user inherits the rights of a policy if they belong to a group for which an active profile exists in the context (Workspace → Class → Workflow or Transition). Transition rights can additionally be conditional and are evaluated in the guard check.
- **Granularity:** Policies distinguish reading/editing workflow definitions, validation/simulation, publication/archive/restore, cloning/import/export, trigger management, as well as execution of individual transitions.
- **Delegation:** Workflow administrators (e.g., via `workflow_admin_policy`) manage profiles and assignments for workflow-wide and transition-specific rights.

The following table lists fine-grained permissions for workflow management:

|Permission                    |Description
|------------------------------|----------------------------------------------------------------------------------
|`workflow_read`               |Read workflow metadata, states/transitions, and version history.
|`workflow_update`             |Edit drafts (states, transitions, guards, validators, post functions).
|`workflow_validate`           |Run consistency check for a draft; access validation reports.
|`workflow_publish`            |Publish a reviewed draft as the active version (incl. mappings).
|`workflow_archive`            |Archive an active workflow version.
|`workflow_restore`            |Restore an archived workflow version.
|`workflow_clone`              |Clone an existing workflow definition as a draft.
|`workflow_delete`             |Permanently delete draft or archived definitions.
|`workflow_versions_read`      |Read version list, diffs, and history.
|`workflow_import`             |Import external workflow definitions (incl. dry run).
|`workflow_export`             |Export definitions/versions.
|`workflow_manage_permissions` |Assign/update/remove permissions (workflow/transition-specific).
|`transition_execute`          |Execute a transition on objects (workflow-wide or transition-specific).
|`status_read`                 |Read the status catalog of a class.
|`status_create`               |Create a status.
|`status_update`               |Edit a status (name, category, description).
|`status_clone`                |Clone a status.
|`status_delete`               |Delete a non-referenced status.
|`status_usage_read`           |Read the usage proof of a status.

These permissions are bundled into typical roles in policies:

|Policy                      |Description                        |Included Permissions
|----------------------------|-----------------------------------|------------------------------
|`workflow_admin_policy`     |Full administration.               |all `workflow_*`, `transition_execute`, `status_*`
|`workflow_publisher_policy` |Release/lifecycle control.         |`workflow_read`, `workflow_validate`, `workflow_publish`, `workflow_archive`, `workflow_restore`, `workflow_versions_read`
|`workflow_edit_policy`      |Model maintenance without release. |`workflow_read`, `workflow_update`, `workflow_validate`, `workflow_clone`, `workflow_versions_read`
|`workflow_view_policy`      |Read-only permissions.             |`workflow_read`, `workflow_versions_read`
|`workflow_importer_policy`  |Import external definitions.       |`workflow_import`
|`workflow_exporter_policy`  |Export definitions/versions.       |`workflow_export`
|`status_admin_policy`       |Maintain status catalog.           |`status_*`

## Conclusion

The Managed Workflow concept in **KleeneStar** describes a server-side controlled, versioned state model for object-related processes at the class level. States, transitions, and associated control mechanisms such as guards, validators, and post functions form the business logic and are directly linked to fields via a local data model. The life cycle of a workflow definition goes through the phases "Draft", "Active", "Archived", and "Deleted". Versions are traceable, continue per object, and can be reset to defined initial states in case of conflicts.

At its center is the `WorkflowManager`, which acts as the orchestrating and validating instance and enables loose coupling to other components via events. Modeling is supported by visual and tabular UI tools, while a comprehensive REST API provides functions such as administration, versioning, validation, publication, archiving, cloning, as well as import and export.

Important technical foundations are still needed for a stable and scalable implementation: these include a consistent model for transactions and concurrency (incl. idempotency and conflict resolution), a well-thought-out strategy for persistence and migration, clear performance and scaling goals. Further necessary building blocks are rules for API and event versioning, security and compliance requirements (e.g., tenant isolation, encryption, audit integrity), a complete OpenAPI specification, defined trigger and scheduler semantics, extensibility via SDKs with sandbox boundaries, UI/UX guidelines for accessibility and collaboration, as well as operational standards for logging, monitoring, and recovery.

The concept should be rounded off with example workflows, documentation of edge cases, and migration scenarios to reliably and traceably support implementation, operations, and further development.