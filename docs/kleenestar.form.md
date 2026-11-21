![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Form Management Concept

Form management in **KleeneStar** enables the modeling, versioning, and control of UI schemas that cover both presentation and data capture at the class level. A form defines the layout, the structure of sections, bindings to fields, visibility rules, and validations for user input. The goal is to ensure consistent, secure, and auditable creation, display, and editing of objects.

Forms are server-anchored, versionable, and multi-tenant. They have a close relationship with classes, fields, and workflows, for example as transition forms. Extension points via plugins are envisaged, creating a robust, rule-based presentation system that connects the semantic modeling of domain objects (classes and fields) with the interaction logic of forms and workflows.

Authorization for forms follows the permission model of the associated class and is supplemented with form-specific rights. Visibility and usage rights are consolidated with class-, field-, and workflow-level permissions.

Forms are used in different variants depending on the usage context. In the object context, they serve to capture, display, and edit individual objects of a class (Create, Edit, View). In the transition context, they are used as screens within workflow transitions. There are also compact partial views that can be embedded as subforms in pages, widgets, or compositions (e.g., for composite objects or subclasses).

## Lifecycle and States

Form management in **KleeneStar** follows a clearly defined lifecycle aligned with that of classes. Forms go through the states active, archived, and deleted, with each state providing specific functions and constraints. In the active state a form is in productive use. Changes are possible at any time. The archived state makes a form read-only and serves for historization as well as referencing in older object versions. Once a form is deleted, it is removed immediately and permanently. State transitions are controlled and accompanied by tamper-proof logging. Restoration from archived back to active is possible.

When fields are removed from a form, previously assigned values on affected objects remain intact. These fields still exist technically but may no longer be editable through the UI because they are not part of the active UI configuration. This ensures that existing data is not lost even if it is no longer directly accessible.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                          KleeneStar Form State Diagram                               ║
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

The **KleeneStar** data model forms the structural foundation for form management and its embedding into the application’s overall architecture. Forms are always locally bound to a class and define the visual and functional capture logic for objects of that class. They reference the associated fields and can optionally be assigned to specific workflow transitions to provide transition forms for particular process steps.

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

Form management in **KleeneStar** is designed as a modular and decoupled component of the overall architecture. At its center is the `FormManager`, responsible for the full lifecycle, consistency, and access to forms within a class. It manages all form definitions on the server and provides a controlled interface for creating, editing, and deleting them. Tight integration with the `ClassManager` ensures forms always operate within the structural context of their associated class.

New forms are created exclusively via the `FormManager`, ensuring consistent administration and secure anchoring within the data model. Changes to existing forms are possible at any time and are safeguarded by a versioned, transactional storage system. At system startup, the `FormManager` loads all stored forms, builds form-specific indexes, and initializes subscribable events.

For a reactive and loosely coupled architecture, the `FormManager` exposes an event system through which other components can react to form-related changes without being directly dependent on the manager. This promotes modularity and facilitates extensibility.

Access to forms is governed by a fine-grained permission model aligned with class permissions and augmented with form-specific rules. Context-dependent filters, time-limited rights, and audit requirements enable flexible control of read, write, and administrative access.

An integrated audit system records all relevant actions related to forms, including accesses, changes, archiving, deletions, and permission checks. Each action is logged with a timestamp, the user identity, the class and form reference, and the type of action performed. This data supports traceability, facilitates error analysis, ensures compliance, and enables restoration of previous states.

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
║    ┌----------------┤ IFormManager                          │                        ║
║    ¦                ├───────────────────────────────────────┤                        ║
║    ¦                │ FormAdded:Event                       │                        ║
║    ¦                │ FormUpdated:Event                     │                        ║
║    ¦                │ FormRemoved:Event                     │                        ║
║    ¦                ├───────────────────────────────────────┤                        ║
║    ¦                │ Forms:IEnumerable<IForm>              ├───────┐                ║
║    ¦                ├───────────────────────────────────────┤ 1     │                ║
║    ¦                │ AddField(IClass,IField):              │       │                ║
║    ¦                │   IFormManager                        │       │                ║
║    ¦                │ GetForms(IClass,predicate):           │       │                ║
║    ¦                │   IEnumerable<IForm>                  │       │                ║
║    ¦                │ CloneForm(IClass,IForm):              │       │                ║
║    ¦                │   IFormManager                        │       │                ║
║    ¦                │ RemoveForm(IClass,IForm):             │       │                ║
║    ¦                │   IFormManager                        │       │                ║
║    ¦                └───────────────────────────────────────┘       │                ║
║    ¦                                                                │                ║
║    ¦                            ┌───────────────┐                   │                ║
║    ¦                            │ <<Interface>> │                   │                ║
║    ¦                            │ IModel        │                   │                ║
║    ¦                            ├───────────────┤                   │                ║
║    ¦                            └──────Δ────────┘                   │                ║
║    ¦                                   ¦                            │                ║
║    ¦                                   ¦                            │                ║
║    ¦                     ┌─────────────┴──────────────┐ *           │                ║
║    ¦                     │ <<Interface>>              ◄─────────────┘                ║
║    ¦                     │ IForm                      │             ┌───────────┐    ║
║    ¦                     ├────────────────────────────┤             │ <<Enum>>  │    ║
║    ¦                     │ Id:Guid                    │             │ FormState │    ║
║    ¦                     │ Name:String                │             ├───────────┤    ║
║    ¦                     │ Description:String         │             │ Active    │    ║
║    ¦                     │ HelpText:String            │             │ Archived  │    ║
║    ¦                     │ State:FormState            │             └───────────┘    ║
║    ¦                     │ Class:IClass               │                              ║
║    ¦                     │ Created:DateTime           │                              ║
║    ¦                   1 │ Updated:DateTime           │                              ║
║    ¦          ┌──────────┤ Tabs:IEnumerable<IFormTab> │                              ║
║    ¦          │          └───────────────────────Δ────┘                              ║
║    ¦          │                                  ¦                                   ║
║    ¦          │ *                                ¦                                   ║
║    ¦  ┌───────▼──────────┐     ┌───────────────┐ ¦                                   ║
║    ¦  │ <<Interface>>    │     │ <<Interface>> │ ¦                                   ║
║    ¦  │ IFormTab         │     │ IField        │ ¦                                   ║
║    ¦  ├──────────────────┤     ├───────────────┤ ¦                                   ║
║    ¦  │ Id:Guid          │     └──────Δ────────┘ ¦                                   ║
║    ¦  │ Name:String      │ 1          ¦          ¦                                   ║
║    ¦  │ Group:IFormGroup ├─────┐      ¦          ¦                                   ║
║    ¦  └──────────────────┘     │      ¦          ¦                                   ║
║    ¦                           │      ¦          ¦                                   ║
║    ¦                           │      ¦          ¦                                   ║
║    ¦                           │ *    ¦          ¦                                   ║
║    ¦                ┌──────────▼──────┴─────┐    ¦                                   ║
║    ¦                │ <<Interface>>         │    ¦                                   ║
║    ¦                │ IFormGroup            │    ¦                                   ║
║    ¦                ├───────────────────────┤    ¦                                   ║
║    ¦                │ Id:Guid               │    ¦                                   ║
║    ¦                │ Fields:               │    ¦                                   ║
║    ¦                │   IEnumerable<IField> │    ¦                                   ║
║    ¦                └───────────────────────┘    ¦                                   ║
║    ¦                                             ¦                                   ║
║    ¦ create                  ┌───────────────────┴────────┐                          ║
║    └-------------------------► Form                       │                          ║
║                              ├────────────────────────────┤                          ║
║                              │ Id:Guid                    │                          ║
║                              │ Name:String                │                          ║
║                              │ Description:String         │                          ║
║                              │ HelpText:String            │                          ║
║                              │ State:FormState            │                          ║
║                              │ Class:IClass               │                          ║
║                              │ Created:DateTime           │                          ║
║                              │ Updated:DateTime           │                          ║
║                              │ Tabs:IEnumerable<IFormTab> │                          ║
║                              └────────────────────────────┘                          ║
║                                                                                      ║
║                Special groups (implementations of IFormGroup):                       ║
║           ┌──────────────────────────────────────────────────────────────┐           ║
║           │ FormGroupVertical         - vertical stacking                │           ║
║           │ FormGroupHorizontal       - horizontal stacking              │           ║
║           │ FormGroupMix              - vertical item, help below        │           ║
║           │ FormGroupColumnVertical   - two columns, each vertical       │           ║
║           │ FormGroupColumnHorizontal - two columns, each row horizontal │           ║
║           │ FormGroupColumnMix        - two columns, help below          │           ║
║           └──────────────────────────────────────────────────────────────┘           ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The architecture model extends **KleeneStar** form management with a hierarchical structure that enables flexible, modular, and visually differentiated form design. At its core is the IForm interface describing a form as a standalone unit. Each form consists of a set of tabs (`IFormTab`), each containing one group (`IFormGroup`). These groups form the structural basis for arranging form elements and can contain both concrete fields as leaves and further groups as inner nodes. This creates a nested tree that can represent complex layouts and logical groupings within a form.

The group elements themselves are polymorphic and realized through specialized `IFormGroup` implementations. These include `FormGroupVertical`, `FormGroupHorizontal`, `FormGroupMix`, `FormGroupColumnVertical`, `FormGroupColumnHorizontal`, and `FormGroupColumnMix`. Each variant defines a specific layout behavior: `FormGroupVertical` enables vertical stacking of fields with labels and help texts. `FormGroupHorizontal` provides a horizontal arrangement. The mix variants combine these approaches, for example via vertical fields with help text below or column-based layouts with flexible alignment. This variety allows precise control over the visual presentation and supports both simple and complex UIs.

The `IFormManager` acts as the central control unit for form management. It provides methods for creating, cloning, querying, and removing forms and ensures that all changes are communicated system-wide via events such as `FormAdded`, `FormUpdated`, and `FormRemoved`. This enables other components to react to form changes without being directly coupled to the manager, supporting loose coupling and architectural extensibility.

All forms also have a lifecycle state (`FormState`) distinguishing between active, archived, and deleted.

## UI Concepts and Pages

The UI for form management in **KleeneStar** is designed to translate complex structures and rules into an intuitive and comprehensible interaction model. The goal is efficient, safe, and user-friendly work with forms within a class.

The design is based on the established UI patterns of the **KleeneStar** web application. Users benefit from a consistent experience that includes clear navigation paths, familiar controls, and recurring interaction principles. This consistency helps reduce onboarding time and supports the efficient execution of typical tasks within the form context. The provided mockups serve as visual references for the final UI design. They demonstrate how forms are managed within the class context, including navigation, form selection, and the display of relevant metadata and states.

Using concrete use cases, the UI demonstrates how forms are created, edited, or removed. The interface supports these workflows with targeted action areas, context-dependent controls, and clear feedback.

### Class Management (Page)

The class management page forms the central administration UI for all class types within a workspace and is closely linked to **KleeneStar**’s form management. In addition to structured maintenance of classes, it serves as the entry point for assigning and managing form-based UIs.

The tabular overview shows key attributes per class such as name, description, and status. The options menu provides various administration functions, including Manage Forms for creating, structuring, versioning, or archiving forms. This enables targeted design of input and display UIs for class objects and ensures forms always fit the semantic and process context of the respective class.

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

### Form Management (Page)

The form management page is the central administration view for all forms of a selected class. It provides a comprehensive overview of existing form definitions and offers functions for creation, editing, organization, and control at the form level.

The main area contains a tabular list of all forms assigned to a class. Each row shows key properties such as name, description, and lifecycle state (e.g., active, archived). Search and filtering are integrated for large form inventories. The page supports actions such as Edit, Clone, Restructure, Archive, and Delete. New forms can be created via the "Add Form" button.

Access to the form management page is via Manage Forms in class administration or directly from the class detail view. Changes to forms have an immediate impact on the UI and interaction logic of associated objects.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / Forms                                                  │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Forms─────────────────┐ ┌Form Content───────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│ My Forms                            [Search] [+ Add Form] │║
║│  - Active            │░│                                                           │║
║│  - Archived          │░│ Form Name      | Description            | Status          │║
║│                      │░│----------------|------------------------|-----------------│║
║│                      │░│ IncidentForm   | Incident entry form    | Active      […] │║
║│                      │░│ ApprovalForm   | Approval workflow form | Active       ¦  │║
║│                      │░│ FeedbackForm   | User feedback form     | Ar┌──────────┴┐ │║
║│                      │░│ SLAForm        | SLA configuration form | Ac│ Edit      │ │║
║│                      │░│ ChangeForm     | Change request form    | Ac│ Clone     │ │║
║│                      │<│ TaskForm       | Task execution form    | Ac│ <section> │ │║
║│                      │<│ EscalationForm | Escalation input form  | Ac├───────────┤ │║
║│                      │<│ CommentForm    | Comment entry form     | Ac│ Delete    │ │║
║│                      │░│                                             └───────────┘ │║
║│                      │░│                                                           │║
║│                      │░│                                   ‹ Prev  1  2  3  Next › │║
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

### Form Management - Designer (Page)

The Form Designer is the central tool for designing and maintaining forms within a class in **KleeneStar**. It provides a structured tree view in which all form elements are displayed hierarchically per tab. Each form is divided into tabs that serve as logical sections. Within a tab, fields and grouping elements are arranged in a tree structure. Leaves in the tree are concrete fields. Inner nodes are special layout groups such as vertical or horizontal arrangements and column-based combinations.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / IncidentForm                                           │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Forms─────────────────┐ ┌Form Designer──────────────────────────────────────────────┐║
║│IncidentForm          │░│ ┌───────────┬─────────┬─────┬─────────┬─────────────────┐ │║
║│                      │░│ │ ⠿ Tab 1 x │ ⠿ Tab 2 │ ... | ⠿ Tab n |      [+ AddTab] │ │║
║│                      │░│ │           └─────────┴─────┴─────────┴─────────────────┤ │║
║│                      │░│ │                                              [Search] │ │║
║│                      │░│ │                                                       │ │║
║│                      │░│ │ Field Name      | Type                                │ │║
║│                      │░│ │-----------------|-------------------------------------│ │║
║│                      │░│ │ ⠿ Title         | String                              │ │║
║│                      │░│ │ ⠿ Status        | Enum                                │ │║
║│                      │░│ │ ⠿ Priority      | Enum                                │ │║
║│                      │<│ │ ⠿ Assignee      | Link                                │ │║
║│                      │<│ │ ⠿ Tags          | Tags                                │ │║
║│                      │<│ │ ⠿ Description   | Text                                │ │║
║│                      │░│ │ ▼ ⠿ Horizontal  | FormGroupHorizonl                   │ │║
║│                      │░│ │   ⠿ ReportedAt  | DateTime                            │ │║
║│                      │░│ │   ⠿ Affected CI | Link                                │ │║
║│                      │░│ │                                                       │ │║
║│                      │░│ │                                                       │ │║
║│                      │░│ │ Field Name*: [                        ▼] [+ AddField] │ │║
║│                      │░│ │                                                       │ │║
║├──────────────────────┤░│ └───────────────────────────────────────────────────────┘ │║
║│                   << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Form Management - New/Edit (Modal)

The "Add Form" and "Edit Form" modals in the **KleeneStar** web application provide the central interface for creating and maintaining forms within a selected class. They offer a structured and user-friendly UI focused on managing form-specific properties.

When creating a new form, basic metadata is defined first, including the form name and a description. Placeholders and help texts can be added to support consistent, context-sensitive interaction. The form’s content structure (i.e., the arrangement of fields and layout groups) is maintained separately via the `FormManager`.

When editing an existing form, all properties are prefilled and can be adjusted. Changes are saved via the "Save" button, "Cancel" discards modifications.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔FormAddEditModal════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Form / Edit Form                                                 │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Forms║│                                                                      │║─────┐║
║│     ║│            Name*: [ IncidentForm                                   ] │║     │║
║│  - A║│      Description: [ Form for reporting incidents                   ] │║orm] │║
║│  - A║│                                                                      │║     │║
║│  - A║│           Active: [✓]                                                │║     │║
║│     ║│                                                                      │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Form Management - Clone (Modal)

Cloning allows quick reuse of proven form definitions within the same class. It is provided as a dedicated modal and can be opened from the class detail view (Manage Forms) or directly from the class’s form overview.

Cloning creates a new form with key properties inherited from the original, including tab structure and field arrangement. System-critical aspects such as the unique form key or permission-related settings are newly generated. In the modal, only the new form’s name is adjusted. All other properties are copied and can be edited later in the `FormManager` if needed.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔FormCloneModal══════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Clone Form                                                           │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Forms║│ The form 'IncidentForm' from class 'Incident' will be cloned.        │║─────┐║
║│     ║│ Please adjust the details for the new form.                          │║     │║
║│  - A║│                                                                      │║orm] │║
║│  - A║│        New Name*: [ IncidentForm (Copy)                            ] │║     │║
║│  - A║│                                                                      │║     │║
║│     ║│                                                                      │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                      [Clone] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Form Management - Delete (Modal)

Deleting a form is a critical operation handled via a dedicated modal to avoid accidental removals. The dialog clearly identifies the affected form including name and class. As an additional safeguard, the exact form name must be typed manually. Only then is the "Delete" action enabled. The dialog explains consequences such as complete removal of the form and assigned fields. Dependent structures and UI logic may be affected. The operation can be canceled at any time via "Cancel" without applying changes.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
║└─────╔FormDeleteModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Delete Form                                                          │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Forms║│ Are you sure you want to delete the form 'IncidentForm' in           │║─────┐║
║│     ║│ class 'Incident'?                                                    │║     │║
║│  - A║│ This action cannot be undone.                                        │║orm] │║
║│  - A║│                                                                      │║     │║
║│  - A║│ To confirm, please type the form name 'IncidentForm' in the          │║     │║
║│     ║│ box below:                                                           │║-----│║
║│     ║│ [                                                                  ] │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                     [Delete] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Sitemap Form Management

This sitemap provides the technical and functional foundation for managing forms within a class in the context of a workspace. It defines clearly structured routes that map the entire lifecycle of a form—from creation and editing to archiving or final deletion.

Each route is designed to precisely support a specific phase in the form lifecycle. Separation of paths ensures clear responsibilities, consistent UX, and traceable implementation.

|Path                                                                    |Page/View       |Description
|------------------------------------------------------------------------|----------------|-------------
|`/workspaces/{workspaceKey}/classes/{classKey}/forms`                   |Form management |Central overview and maintenance of all forms of a class (search/filter/pagination).
|`/workspaces/{workspaceKey}/classes/{classKey}/forms/add`               |Create form     |Create a new form (becomes active immediately).
|`/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}`         |Form detail     |Metadata, status (Active/Archived), field assignments.
|`/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/edit`    |Edit form       |Change metadata (e.g., name).
|`/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/clone`   |Clone form      |Create a copy of a form.
|`/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/archive` |Archive form    |Moves the current active version to "Archived".
|`/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/restore` |Restore form    |Restores an archived version as the new active version.
|`/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/delete`  |Delete form     |Confirmed, final removal (after dependency checks).

## API Interfaces (REST) - Form Management

The interfaces are REST-oriented, use JSON, enforce server-side validation, and apply changes immediately after successful checks. Status codes indicate results.

|Endpoint                                                                    |HTTP Method |Description
|----------------------------------------------------------------------------|------------|------------
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms`                   |GET         |Lists all forms of a class.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms`                   |POST        |Creates a new form and activates it immediately.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}`         |GET         |Returns form metadata.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}`         |PUT         |Updates the form.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}`         |DELETE      |Permanently removes a form. Active forms must be archived first.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/archive` |POST        |Archives the current active version (read-only).
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/restore` |POST        |Restores an archived version as the new active version.
|`/api/workspaces/{workspaceKey}/classes/{classKey}/forms/{formKey}/clone`   |POST        |Clones a form.

In the context of form management in **KleeneStar**, different HTTP status codes clearly indicate the outcome of operations. Successful interactions are confirmed as follows: creating or cloning a form returns `201 Created`, signaling a new resource was created successfully. General read and write operations—such as retrieving form information, editing metadata, or running preview and validation—return `200 OK`, indicating successful processing without resource creation. Deleting a form returns `204 No Content`, indicating the deletion succeeded and no further data is returned.

## Form Events

Form management in **KleeneStar** uses the central **WebExpress** `EventManager` to publish all relevant system events. Each event represents a clearly defined action or state change within the form lifecycle. These events serve as system-wide signals enabling connected components to react immediately and in context. UIs can update dynamically, audit logs can be extended automatically, and external integrations or follow-up processes can be triggered. This event processing provides a transparent, reactive, and extensible infrastructure for consistent control of form states across the system.

The following events are published by the `FormManager` via the **WebExpress** `EventManager`:

|Event Name       |Description
|-----------------|-------------------------------------------------------------------------
|`FormCreated`    |New form created and activated (initial version).
|`FormUpdated`    |Form changed. New active version created.
|`FormActivated`  |Activation of a new version with simultaneous archiving of the predecessor.
|`FormArchived`   |Form version set to "Archived" (read-only).
|`FormRestored`   |Archived version restored as new active version.
|`FormDeleted`    |Form permanently removed (after retention/dependency checks).
|`FormCloned`     |Form successfully cloned (new active version).
|`FormAssigned`   |Form assigned to a workflow transition.
|`FormUnassigned` |Form unassigned from a workflow transition.

Each event in priority management carries a structured payload containing essential metadata for processing and traceability. This includes the unique form key and its associated class key, a timestamp marking when the action occurred, the context of the triggering user or module, and the type and source of the action. These details ensure that events are clearly identifiable, auditable, and actionable across the system.

## Permission Model - Form Management

The permission model for form management in **KleeneStar** is context-sensitive and enables fine-grained access control at the class level. Rights are not assigned directly at the form level but via class profiles that link global groups with specific policies within a class. This yields a flexible and consistent rights concept aligned with organizational and business requirements.

A user receives the permissions defined by a given policy for all forms of a class if they are a member of a global group associated with that policy in the class profile. These permissions apply uniformly to all forms within the class, whether the forms are active, archived, or newly created.

Administrators with the `form_admin_policy` may manage class profiles and thus control the assignment of policies to groups, defining which actions are allowed in form management for a class.

The following fine-grained permissions form the basis for comprehensive and controlled form management:

|Permission               |Description
|-------------------------|------------------------------------------------------
|`form_create`            |Create forms (immediately active after validation).
|`form_read`              |Read metadata, structure (layout/tree), and assignments.
|`form_update`            |Edit forms (layout, rules, bindings, metadata).
|`form_delete`            |Permanently delete forms.
|`form_archive`           |Archive active forms.
|`form_restore`           |Restore archived forms (as new active versions).
|`form_clone`             |Clone forms (new active version).
|`form_assign_transition` |Maintain assignment of a form as a transition screen.
|`form_import`            |Import external form definitions.
|`form_export`            |Export forms (including structure).

These permissions are bundled into logical policies representing common use cases and responsibilities. Policies can be assigned to global groups in the class profile.

|Policy                  |Description                         |Included permissions
|------------------------|------------------------------------|------------------------------------------------------------------------
|`form_admin_policy`     |Full access to form management      |includes all `form_*` permissions
|`form_publisher_policy` |Lifecycle control without deletion  |`form_read`, `form_archive`, `form_restore`, `form_export`
|`form_edit_policy`      |Model maintenance                   |`form_read`, `form_update`, `form_validate`, `form_preview`, `form_clone`
|`form_view_policy`      |Read-only access                    |`form_read`
|`form_importer_policy`  |Import                              |`form_import`
|`form_exporter_policy`  |Export                              |`form_export`

Creation, changes, archiving/restoration, deletion, assignments, and permission changes are logged in an audit-proof manner with context (time, user, source).

## Conclusion

Server-side form management in **KleeneStar** provides a structured and powerful foundation for creating and managing forms in the class context. The tree-like organization of content and the direct binding to class attributes yield a flexible and extensible model. The central `FormManager` handles all key tasks from storage to event handling. The UI and REST API enable comprehensive operation and integration. For a complete implementation, technical and conceptual details still need to be clarified, particularly around structural changes, performance, accessibility, import/export, and consistent rights management across all layers.
