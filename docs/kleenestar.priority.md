![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Priority Management Concept

Priority management in **KleeneStar** enables model-based and controlled administration of priorities, for example in terms of urgency or impact, within the context of a class. A priority describes attributes such as name, description, and weighting. The objective is a consistent and safe classification that is used for object processing, sorting, SLA control, and decision-making in workflows.

Priorities are anchored server-side, multi-tenant capable, and tightly bound to classes. They are managed via their state (active or archived). Deleted priorities are permanently removed and cannot be restored. Permissions are governed exclusively by the permission model of the associated class. There are no separate permissions at the priority level.

Each priority is assigned to one or more priority fields. Such a field can contain a subset or the entirety of all available priorities, thus enabling context-specific selection within objects.

In the object context, priorities serve to display and rate individual objects. In the SLA context, they are linked to response and resolution times. Extension points also allow the integration of plugins for rule-based derivation or dynamic calculation of context-based priorities.

## Lifecycle and States

Priority management follows a structured lifecycle analogous to the class model. States: active, archived, deleted. Active means productive use. Changes are possible at any time. Archived preserves an immutable version and serves reference purposes (e.g., for older object states). Deleted removes a priority irrevocably. State transitions are logged in an audit-proof manner. Restoration from archived back to active is possible.

If a priority is removed, references stored on objects or historical evaluations remain (historization). The priority continues to exist technically for historical reference but is no longer assignable if it was deleted.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                          KleeneStar Priority State Diagram                           ║
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

The **KleeneStar** data model forms the structural foundation for priority management and firmly anchors priorities within the context of a class. Priorities define the classification logic for objects and are closely linked to fields. They are locally bound to classes and can be used within them.

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

Priority management in **KleeneStar** is modular and deliberately decoupled from other components. The central element is the `PriorityManager`, which controls the entire lifecycle of priorities, ensures their consistency, and regulates access within a class. It manages all priority definitions server-side and provides a controlled interface for creation, modification, archiving, and deletion. Tight integration with the `ClassManager` ensures that priorities are always used in a context-appropriate manner.

New priorities are created exclusively through the `PriorityManager`. Changes are versioned and persisted transactionally, ensuring a consistent and traceable state at all times. On system startup, the manager loads all existing priorities, builds indexes (for example by score or category), and initializes relevant events.

An integrated event system enables reactive coupling with other components. These can react to events such as `PriorityCreated` or `PriorityUpdated` and trigger corresponding follow-up actions, such as recalculating SLAs or adjusting escalation logics.

The audit system records every relevant action in the lifecycle of a priority. This includes creation, modification, archiving, restoration, and deletion, as well as adjustments to rule definitions, escalation configurations, or permission assignments. In this way, complete traceability and compliance-compliant management are ensured.

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
║                      ┌────────────────┴──────────────────┐                           ║
║                      │ <<Interface>>                     │                           ║
║   ┌------------------┤ IPriorityManager                  │                           ║
║   ¦                  ├───────────────────────────────────┤                           ║
║   ¦                  │ PriorityAdded:Event               │                           ║
║   ¦                  │ PriorityUpdated:Event             │                           ║
║   ¦                  │ PriorityRemoved:Event             │                           ║
║   ¦                  ├───────────────────────────────────┤                           ║
║   ¦                  │ Priorities:IEnumerable<IPriority> ├────────┐                  ║
║   ¦                  ├───────────────────────────────────┤ 1      │                  ║
║   ¦                  │ AddPriority(IClass,IPriority):    │        │                  ║
║   ¦                  │   IPriorityManager                │        │                  ║
║   ¦                  │ GetPriorities(IClass,predicate):  │        │                  ║
║   ¦                  │   IEnumerable<IPriority>          │        │                  ║
║   ¦                  │ ClonePriority(IClass,IPriority):  │        │                  ║
║   ¦                  │   IPriorityManager                │        │                  ║
║   ¦                  │ UpdatePriority(IPriority):        │        │                  ║
║   ¦                  │   IPriorityManager                │        │                  ║
║   ¦                  │ DeletePriority(IPriority):        │        │                  ║
║   ¦                  │   IPriorityManager                │        │                  ║
║   ¦                  └───────────────────────────────────┘        │                  ║
║   ¦                                                               │                  ║
║   ¦                           ┌───────────────┐                   │                  ║
║   ¦                           │ <<Interface>> │                   │                  ║
║   ¦                           │ IModel        │                   │                  ║
║   ¦                           ├───────────────┤                   │                  ║
║   ¦                           └───────Δ───────┘                   │                  ║
║   ¦                                   ¦                           │                  ║
║   ¦                                   ¦                           │                  ║
║   ¦                        ┌──────────┴──────────┐ *              │                  ║
║   ¦                        │ <<Interface>>       ◄────────────────┘                  ║
║   ¦                        │ IPriority           │                                   ║
║   ¦                        ├─────────────────────┤               ┌───────────────┐   ║
║   ¦                        │ Id:Guid             │               │ <<Enum>>      │   ║
║   ¦                        │ Name:String         │               │ PriorityState │   ║
║   ¦                        │ Description:String  │               ├───────────────┤   ║
║   ¦                        │ Score:int           │               │ Active        │   ║
║   ¦                        │ Category:String     │               │ Archived      │   ║
║   ¦                        │ State:PriorityState │               └───────────────┘   ║
║   ¦                        │ Class:IClass        │                                   ║
║   ¦                        │ Created:DateTime    │                                   ║
║   ¦                        │ Updated:DateTime    │                                   ║
║   ¦                        └──────────Δ──────────┘                                   ║
║   ¦                                   ¦                                              ║
║   ¦                                   ¦                                              ║
║   ¦ create                 ┌──────────┴──────────┐                                   ║
║   └------------------------► Priority            │                                   ║
║                            ├─────────────────────┤                                   ║
║                            │ Id:Guid             │                                   ║
║                            │ Name:String         │                                   ║
║                            │ Description:String  │                                   ║
║                            │ Score:int           │                                   ║
║                            │ Category:String     │                                   ║
║                            │ State:PriorityState │                                   ║
║                            │ Class:IClass        │                                   ║
║                            │ Created:DateTime    │                                   ║
║                            │ Updated:DateTime    │                                   ║
║                            └─────────────────────┘                                   ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The model shown represents the core functions of priority management in **KleeneStar**. The starting point is the generic `IComponentManager` interface, which serves as a base for manager components. Building on this, `IPriorityManager` specifies the administration of priorities and provides methods for their lifecycle. These include adding, updating, cloning, and deleting priorities, as well as the ability to query priorities of a class in a targeted manner. This is complemented by events such as `PriorityAdded`, `PriorityUpdated`, or `PriorityRemoved`, which enable reactive coupling with other system parts and can, for example, trigger SLA recalculations or escalation logic.

The actual priority is described via the `IPriority` interface, which contains central attributes such as Id, name, and description. The state is formalized by the `PriorityState` enumeration and distinguishes between active and archived priorities. Other properties such as the reference to the associated class and timestamps for creation and update ensure context binding and traceability. The concrete implementation is carried out by the priority class, which adopts all defined properties and is used in the system as a persistent entity.

## UI Concepts and Pages

The user interface for priority management in **KleeneStar** is designed to translate complex classification logic and rules into an intuitive and comprehensible interaction model. The aim is to make working with priorities within a class efficient, safe, and user-friendly.

The design is based on the established UI patterns of the **KleeneStar** web application. Users benefit from a consistent user experience with clear navigation paths, familiar controls, and recurring interaction principles. This reduces onboarding time and enables rapid execution of typical tasks in the context of priorities. Mockups serve as a visual reference for the final UI design and illustrate how priorities are managed within the class context, from navigation and selection to the display of relevant metadata and states such as active or archived.

Using concrete use cases, the interface shows how priorities are created, adjusted, archived, or deleted. Interaction is supported by targeted action areas, context-dependent controls, and clear feedback. This ensures that users always keep an overview and that changes to priorities are comprehensible and consistent.

### Class Management (Page)

The class management page forms the central administration interface for all class types within a workspace and also serves as an entry point for priority management in **KleeneStar**. In addition to the structured maintenance of classes, it enables the targeted assignment and management of priority-related configurations in the respective class context.

The tabular overview shows central attributes per class such as name, description, and status. Various management functions are available via the options menu, including Manage Priorities. This menu item leads to the priority-related configuration interface where priorities can be created, adjusted, archived, or deleted. Priority management is closely linked to the respective class and takes into account its semantic and procedural context.

The user interface for priority management follows the established UI patterns of the **KleeneStar** web application. Users benefit from a consistent operating logic with clear navigation paths, familiar controls, and context-sensitive actions. By embedding it in class management, priorities are ensured to be defined and used in a context-related and domain-consistent manner, for example to control object behavior, process control in workflows, or the implementation of SLA requirements.

The Manage Priorities function thus complements the existing management options such as Manage Fields, Manage Status, Manage Workflows, or Manage Forms and extends a class’s configuration options with a central element for classification and escalation logic. This ensures that priorities are not managed in isolation, but always in conjunction with the other class elements.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Classes                                                           │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Classes───────────────┐ ┌Classes Content────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│ My Classes                         [Search] [+ Add Class] │║
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

### Priority Management (Page)

The priority management page is the central administration view for all priority definitions of a selected class. It provides a comprehensive overview of existing priorities and offers functions for creating, editing, organizing, and controlling at the priority level.

The main area contains a tabular list of all priorities assigned to a class. Each row displays key properties such as name and description. Search and filter functions are integrated for extensive priority inventories. The page supports actions such as edit, clone, archive, and delete. New priorities can be created via the "Add Priority" button.

Access to the priority management page is via Manage Priorities in class administration or directly from a class’s detail view. Changes to priorities have an immediate impact on the processing, evaluation, and control of objects, particularly in the context of workflows, escalation logic, and SLA rules.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / Forms                                                  │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Priorities────────────┐ ┌Priority Content───────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│ My Priorities                   [Search] [+ Add Priority] │║
║│  - Active            │░│                                                           │║
║│  - Archived          │░│ Priority Name | Category      | Score | Status            │║
║│                      │░│---------------|---------------|-------|-------------------│║
║│                      │░│ ⠿ Critical    | Impact        | 100   | Active        […] │║
║│                      │░│ ⠿ High        | Impact        | 80    | Active         ¦  │║
║│                      │░│ ⠿ Medium      | Impact        | 50    | Acti┌──────────┴┐ │║
║│                      │░│ ⠿ Low         | Impact        | 20    | Arch│ Edit      │ │║
║│                      │░│ ⠿ Info        | Informational | 5     | Acti│ Clone     │ │║
║│                      │<│                                             │ <section> │ │║
║│                      │<│                                   ‹ Prev  1 ├───────────┤ │║
║│                      │<│                                             │ Delete    │ │║
║│                      │░│                                             └───────────┘ │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
║│                      │░│                                                           │║
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

### Priority Management - New/Edit (Modal)

The "Add Priority" and "Edit Priority" modals in the **KleeneStar** web application provide the central user interface for creating and maintaining priorities within a selected class. They offer a structured and user-friendly surface focused on managing priority-specific properties.

When creating a new priority, basic metadata is defined first, including the name, a description, the numeric weighting (score), and an optional category such as urgency or impact. In addition, the state (active or archived) can be set. The priority is directly linked to the associated class, ensuring its context-related use. Rule definitions or escalation paths can be added via separate configuration areas.

When editing an existing priority, all properties are prefilled and can be adjusted. Changes are saved via the "Save" button, while "Cancel" discards all modifications. The interface provides clear feedback and supports consistent, auditable administration of priorities.

The modals for creating and editing priorities are directly accessible from the priority management page. Changes immediately influence the classification logic and object processing, particularly in relation to workflows, SLA rules, and escalation mechanisms.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔PriorityAddEditModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Priority / Edit Priority                                         │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Prior║│                                                                      │║─────┐║
║│     ║│            Name*: [ Critical                                       ] │║     │║
║│  - A║│      Description: [ Priority for particularly critical processes.  ] │║ity] │║
║│  - A║│                                                                      │║     │║
║│  - A║│           Active: [✓]                                                │║     │║
║│     ║│                                                                      │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Priority Management - Clone (Modal)

Cloning priorities enables the quick reuse of proven priority definitions within the same class. This function is available as a dedicated modal and can be invoked directly from a class’s priorities overview.

When cloning, a new priority is created that adopts central properties of the original priority, including name and description, score, and category. System-critical features such as the unique ID or internal references are regenerated. In the modal, the primary adjustment is the name of the new priority. All other properties are adopted and can be subsequently edited via priority management if required.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔PriorityCloneModal══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Clone Priority                                                       │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Prior║│ The priority 'Critical' from class 'Incident' will be cloned.        │║─────┐║
║│     ║│ Please adjust the details for the new priority.                      │║     │║
║│  - A║│                                                                      │║ity] │║
║│  - A║│        New Name*: [ Critical (Copy)                                ] │║     │║
║│  - A║│                                                                      │║     │║
║│     ║│                                                                      │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                      [Clone] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Priority Management - Delete (Modal)

Deleting a priority in **KleeneStar**’s priority management is a security-critical process carried out via a dedicated modal. The aim is to prevent accidental deletions and clearly inform the user about the consequences.

The dialog identifies the affected priority unambiguously, including its name and associated class. To confirm deletion, the exact name of the priority must be manually entered into an input field. Only when this input is correct does the "Delete" button become enabled.

The dialog clearly points out that this action cannot be undone. Deleting the priority removes all associated references, which may impact dependent structures such as workflows, SLA rules, or object fields.

The process can be canceled at any time via the "Cancel" button without applying changes. This modal thus provides a controlled, traceable, and safe way to permanently remove priority definitions from the system.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔PriorityDeleteModal═════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Delete Priority                                                      │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Prior║│ Are you sure you want to delete the priority 'Critical' in           │║─────┐║
║│     ║│ class 'Incident'?                                                    │║     │║
║│  - A║│ This action cannot be undone.                                        │║ity] │║
║│  - A║│                                                                      │║     │║
║│  - A║│ To confirm, please type the priority name 'Critical' in the          │║     │║
║│     ║│ box below:                                                           │║-----│║
║│     ║│ [                                                                  ] │║ […] │║
║│     ║│                                                                      │║ […] │║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                     [Delete] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Sitemap Priority Management

This sitemap provides the technical and functional foundation for managing priorities within a class in the context of a workspace. It defines clearly structured routes that map the entire lifecycle of a priority—from creation and editing to cloning and archiving through to permanent deletion.

Each route is designed to specifically support a given phase in a priority’s lifecycle. The separation of paths ensures clear responsibilities, a consistent user experience, and a traceable technical implementation. The result is a robust, modular system for controlling classification logic in the class context.

|Path                                                                     |Page/View         |Description
|-------------------------------------------------------------------------|------------------|-----------
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities`               |Priority overview |Central overview (search/filter/pagination).
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities/add`           |Create priority   |Creates a new active priority.
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}`         |Priority detail   |Metadata, state, rules, references.
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/edit`    |Edit priority     |Change metadata / score / category.
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/clone`   |Clone priority    |Copies an existing priority.
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/archive` |Archive priority  |Archives the active version.
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/restore` |Restore priority  |Restores an archived version.
|`/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/delete`  |Delete priority   |Final removal.

## API Interfaces (REST) - Priority Management

For programmatic interaction, external integrations, and automation, **KleeneStar** provides a standardized REST API for managing priority definitions within a class. The interface follows REST principles and uses JSON as the data format. Authentication and authorization are ensured by **KleeneStar**. Standardized HTTP status codes indicate the outcome of each request, such as success, validation errors, permission issues, or missing resources.

Priority management is performed via the following endpoints:

|Endpoint                                                                     |HTTP Method |Description
|-----------------------------------------------------------------------------|------------|-----------
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities`               |GET         |Lists all priorities of a class.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities`               |POST        |Creates a new priority (active).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}`         |GET         |Reads metadata / rules.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}`         |PUT         |Updates a priority.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}`         |DELETE      |Permanently deletes (archived only).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/archive` |POST        |Archives the active version.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/restore` |POST        |Sets archived version active.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/priorities/{key}/clone`   |POST        |Clones a priority.

In the context of form management in **KleeneStar**, different HTTP status codes clearly indicate the outcome of operations. Successful interactions are confirmed as follows: creating or cloning a form returns `201 Created`, signaling a new resource was created successfully. General read and write operations—such as retrieving form information, editing metadata, or running preview and validation—return `200 OK`, indicating successful processing without resource creation. Deleting a form returns `204 No Content`, indicating the deletion succeeded and no further data is returned.

## Priority Events

Priority management in **KleeneStar** uses the central **WebExpress** `EventManager` to publish all relevant system events. Each event represents a clearly defined action or state change in the lifecycle of a priority. These events serve as system-wide signals that enable connected components to react immediately and contextually.

Through event processing, user interfaces can be updated dynamically, audit logs can be expanded automatically, and external integrations or subsequent processes can be triggered. This creates a transparent, reactive, and extensible infrastructure for consistent control of priority states throughout the system.

The following events are published by the `PriorityManager` via the **WebExpress** `EventManager`:

|Event Name            |Description
|----------------------|-----------
|`PriorityCreated`     |New priority created (active).
|`PriorityUpdated`     |Changes saved (new version active).
|`PriorityArchived`    |Priority archived (read-only).
|`PriorityRestored`    |Archived version active again.
|`PriorityDeleted`     |Priority removed.
|`PriorityCloned`      |Priority cloned (active).
|`PriorityRuleChanged` |Rules modified.

Each event triggered within priority management includes a structured payload that delivers all relevant context for downstream processing. This payload contains the unique identifier of the priority and its related class, a timestamp marking the moment of the event, details about the initiating user or system module, and metadata describing the nature and origin of the action. This structure ensures that priority events are clearly traceable, context-aware, and seamlessly usable across reactive components, audit trails, and external integrations.

## Permission Model

The permission model for priority management in **KleeneStar** is context-sensitive and enables fine-grained access control at the class level. Permissions are not assigned directly to individual priorities but are managed via so-called class profiles. These profiles link global user groups with specific policies within a class, creating a flexible, consistent rights concept aligned with organizational and domain requirements.

A user receives the rights defined in a policy for all priorities of a class if they belong to a global group linked to this policy in the class profile. The permissions thus apply to all priorities of the class, regardless of whether they are active, archived, or newly created.

Administrators with the `priority_admin_policy` can manage class profiles and thereby control the assignment of policies to groups. They determine which actions are permitted for a class within the scope of priority management.

The following individual permissions form the basis for comprehensive and controlled management of priorities:

|Permission         |Description
|-------------------|-----------------------------------------------
|`priority_create`  |Create new priorities.
|`priority_read`    |Read metadata, rules, states.
|`priority_update`  |Modify (score, category, rules, description).
|`priority_delete`  |Permanent deletion (archived only).
|`priority_archive` |Archive active priorities.
|`priority_restore` |Restore archived versions.
|`priority_clone`   |Clone existing priorities.
|`priority_import`  |Import external priority definitions.
|`priority_export`  |Export (incl. rules).

These permissions are grouped into policies that reflect typical roles and responsibilities. Policies can be assigned to global groups via the class profile.

|Policy                      |Description                        |Included Permissions
|----------------------------|-----------------------------------|------------------------------------------------------------
|`priority_admin_policy`     |Full access                        |all `priority_*`
|`priority_publisher_policy` |Lifecycle control without deletion |`priority_read`, `priority_archive`, `priority_restore`, `priority_export`
|`priority_edit_policy`      |Model/rule maintenance             |`priority_read`, `priority_update`, `priority_clone`
|`priority_view_policy`      |View only                          |`priority_read`
|`priority_importer_policy`  |Import                             |`priority_import`
|`priority_exporter_policy`  |Export                             |`priority_export`

All actions including creation, modification, archiving, restoration, deletion, and permission changes are logged in a manner that ensures audit security. Each entry records the timestamp, user information, and the origin of the action to maintain transparency and enable full traceability across the system.

## Conclusion

Server-side priority management in **KleeneStar** provides a structured and powerful foundation for creating and maintaining priorities in the class context. By directly linking to class attributes and hierarchical organization, a flexible and extensible model emerges that can be efficiently integrated into existing processes.

The central `PriorityManager` takes on all tasks related to storage, event processing, and lifecycle control. The user interface and REST API enable comprehensive operation and easy connection of external systems.

However, further technical and conceptual details remain to be clarified for complete implementation. These include structural changes, performance aspects, accessibility, import and export functions, and consistent rights management across all system levels.
