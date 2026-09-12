![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Field Management Concept

Field management in **KleeneStar** complements the class concept with an independent, fine-grained administration of all fields within a class. Fields define the data type, cardinality, default values, validations, indexes, visibility, and semantic properties of an object attribute. The goal is a consistent, versionable, and traceable schema system that combines central governance with local extensibility, ensures data quality, and enables performant, context-sensitive queries.

The `FieldManager` is responsible for the entire lifecycle of field definitions within a workspace and a class. It ensures that:
- Field definitions are consistent, valid, and compatible with existing data.
- Changes are controlled, versioned, and, if necessary, secured via declarative migration paths.
- Permissions at the field level are enforced in a differentiated way (read, write, masking, indexing, permission management).
- Auditability and traceability of all field operations are guaranteed.

The `FieldManager` is complementary to the `ClassManager` and is integrated into its lifecycle. Classes provide the structural framework. Fields refine the semantic and technical design.

## Lifecycle and States

Field management follows a default lifecycle analogous to classes, with the states active, archived, and deleted.

- **active:** The field is usable in production. Only compatible changes or versioned migrations are permitted.
- **archived:** The field is read-only. Serves for historicization and referencing in older object versions.
- **deleted:** Final removal initiated. Actual deletion occurs after retention periods and dependency checks.

State transitions occur in a controlled manner with validation, migration planning, and audit entries. Restoration (restore) is possible from archived to active, provided there are no irreconcilable conflicts.

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                         KleeneStar Field State Diagram                               ║
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

## Data Model and Relationships

Fields are uniquely assigned to a class and affect objects of that class. The definition covers technical, semantic, and UI-related aspects:

- Key attributes: id (stable), name (localizable), description (localizable), order/index, state, deprecated.
- Typing: primitive types (String, Text, Integer, Float, Boolean, DateTime), structured types (Enum, JSON), references (link to class/object), computed fields.
- Cardinality: single, list, bounded list (min/max).
- Validation: required, unique, pattern/regex, range/length, enum values, cross-field rules, custom validators.
- Defaults: static default, dynamic default (e.g., now(), currentUser()).
- Data protection and visibility: access modifier (Public, Private, Protected, Internal).
- UI hints: display name, help text, placeholder, format (e.g., date format).

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

The application follows a modular, decoupled architectural principle. At the center of field administration is the `FieldManager`, which is exclusively responsible for lifecycle, consistency, and access to all fields within a class. It manages field definitions per class, provides a controlled interface for all interactions, and integrates closely with the `ClassManager`, which provides the structural framework. New fields are created exclusively via the `FieldManager` to ensure data integrity, compatibility checks, and consistent access rules.

For a loosely coupled, reactive architecture, the `FieldManager` provides field-related events. Other components can subscribe to these and react to changes without directly depending on the manager. This event system fosters modularity and high cohesion.

The `FieldManager` handles server-side tasks such as the persistent storage of all field definitions in a transactional, versioned store. At system startup, stored fields are loaded, field- and class-specific indexes are built, and event subscriptions are initialized.

For each request, the `FieldManager` enforces authorization for the user. Access restrictions are governed by policies that can include context-dependent filters, time-limited rights, or audit requirements. This enables a flexible and fine-grained design of read, write, and administrative rights at the field level.

An integrated audit system documents all relevant actions around fields: accesses, changes, deprecations, context switches, and permission checks are logged with timestamp, user identity, class and field keys, and action type. These data serve analysis, troubleshooting, compliance checks, and the restoration of states.

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
║    ┌----------------┤ IFieldManager                         │                        ║
║    ¦                ├───────────────────────────────────────┤                        ║
║    ¦                │ FieldAdded:Event                      │                        ║
║    ¦                │ FieldUpdated:Event                    │                        ║
║    ¦                │ FieldRemoved:Event                    │                        ║
║    ¦              1 ├───────────────────────────────────────┤                        ║
║    ¦          ┌─────┤ FieldTypes:IEnumerable<IFieldTypes>   │                        ║
║    ¦          │     │ Fields:IEnumerable<IField>            ├───────┐                ║
║    ¦          │     ├───────────────────────────────────────┤ 1     │                ║
║    ¦          │     │ AddField(IClass,IField):              │       │                ║
║    ¦          │     │   IFieldManager                       │       │                ║
║    ¦          │     │ GetFields(IClass,predicate):          │       │                ║
║    ¦          │     │   IEnumerable<IField>                 │       │                ║
║    ¦          │     │ CloneField(IClass,IField):            │       │                ║
║    ¦          │     │   IFieldManager                       │       │                ║
║    ¦          │     │ RemoveField(IClass,IField):           │       │                ║
║    ¦          │     │   IFieldManager                       │       │                ║
║    ¦          │     └───────────────────────────────────────┘       │                ║
║    ¦          │ *                                                   │                ║
║    ¦ ┌────────▼───────────┐                                         │                ║
║    ¦ │ <<Interface>>      │                                         │                ║
║    ¦ │ IFieldType         │                                         │                ║
║    ¦ ├────────────────────┤        ┌───────────────┐                │                ║
║    ¦ │ Id:String          │        │ <<Interface>> │                │                ║
║    ¦ │ Name:String        │        │ IModel        │                │                ║
║    ¦ ├────────────────────┤        ├───────────────┤                │                ║
║    ¦ │ Marshal(Object):   │        └──────Δ────────┘                │                ║
║    ¦ │   String           │               ¦                         │                ║
║    ¦ │ Unmarshal(String): │               ¦                         │                ║
║    ¦ │   Object           │               ¦                         │                ║
║    ¦ └────────▲───────────┘           ┌---┘                         │                ║
║    ¦          │ 1                     ¦                             │                ║
║    ¦          │                       ¦                             │                ║
║    ¦          │     ┌─────────────────┴──────────────────┐ *        │                ║
║    ¦          │     │ <<Interface>>                      ◄──────────┘                ║
║    ¦          │     │ IField                             │       ┌────────────┐      ║
║    ¦          │     ├────────────────────────────────────┤       │ <<Enum>>   │      ║
║    ¦          │     │ Id:Guid                            │       │ FieldState │      ║
║    ¦          │     │ Name:String                        │       ├────────────┤      ║
║    ¦          │     │ Description:String                 │       │ Active     │      ║
║    ¦          │     │ HelpText:String                    │       │ Archived   │      ║
║    ¦          │     │ Placeholder:String                 │       └────────────┘      ║
║    ¦          │     │ State:FieldState                   │   ┌─────────────────────┐ ║
║    ¦          │     │ Class:IClass                       │   │ <<Enum>>            │ ║
║    ¦          │     │ Created:DateTime                   │   │ FieldAccessModifier │ ║
║    ¦          │   * │ Updated:DateTime                   │   ├─────────────────────┤ ║
║    ¦          └─────┤ Type:IFieldType                    │   │ Private             │ ║
║    ¦                │ Cardinality:FieldCardinality       │   │ Protected           │ ║
║    ¦                │ Required:Bool                      │   │ Public              │ ║
║    ¦                │ Unique:Bool                        │   │ Internal            │ ║
║    ¦                │ Deprecated:Bool                    │   └─────────────────────┘ ║
║    ¦                │ AccessModifier:TypeAccessModifier  │                           ║
║    ¦                │ ValidationRules:IEnumerable<IRule> │                           ║
║    ¦                │ Default:IDefaultSpec               │                           ║
║    ¦                │ PermissionsProfiles:               │                           ║
║    ¦                │   IEnumerable<IPermissionsProfile> │                           ║
║    ¦                └─────────────────Δ──────────────────┘                           ║
║    ¦                                  ¦                                              ║
║    ¦                                  ¦                                              ║
║    ¦ create         ┌─────────────────┴──────────────────┐                           ║
║    └----------------► Field                              │                           ║
║                     ├────────────────────────────────────┤                           ║
║                     │ Id:Guid                            │                           ║
║                     │ Name:String                        │                           ║
║                     │ Description:String                 │                           ║
║                     │ HelpText:String                    │                           ║
║                     │ Placeholder:String                 │                           ║
║                     │ State:FieldState                   │                           ║
║                     │ Class:Class                        │                           ║
║                     │ Created:DateTime                   │                           ║
║                     │ Updated:DateTime                   │                           ║
║                     │ Type:IFieldType                    │                           ║
║                     │ Cardinality:FieldCardinality       │                           ║
║                     │ Required:Bool                      │                           ║
║                     │ Unique:Bool                        │                           ║
║                     │ Deprecated:Bool                    │                           ║
║                     │ AccessModifier:TypeAccessModifier  │                           ║
║                     │ ValidationRules:IEnumerable<Rule>  │                           ║
║                     │ Default:DefaultSpec                │                           ║
║                     │ PermissionsProfiles:               │                           ║
║                     │   IEnumerable<IPermissionsProfile> │                           ║
║                     └────────────────────────────────────┘                           ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

The field model in **KleeneStar** enables the precise definition and maintenance of attributes within a class and supports both simple data types and complex structures and relationships. It covers primitive and structured types, referential links, computed fields, files and URIs, complemented by visibility and privacy aspects such as access modifiers, masking, and editorial rules. Cardinalities from single field to list and expressive validation rules with range, pattern, and cross-field checks form the basis for a modular, extensible, and semantically consistent field definition.

The `IFieldType` interface provides the central link for consistent processing of field types in the **KleeneStar** model system. It defines, in a binding way, what kind of data a field contains, thus creating a clear and extensible foundation for the entire field logic. Thanks to its modular architecture, it enables clean decoupling and high extensibility. New field types can be integrated as plugins without having to adapt existing logic or UI components. Developers can implement their own derivations of `IFieldType` and register them system-wide.

To illustrate the field types, the following table provides an overview of the basic types in **KleeneStar** and their typical use cases:

|Field type               |Description
|-------------------------|-----------------------------------------------------------
|`StringFieldType`        |Single-line text, e.g., name, title
|`TextFieldType`          |Multi-line free text, e.g., description, comment
|`IntegerFieldType`       |Integer, e.g., count, priority
|`FloatFieldType`         |Decimal number, e.g., price, rating
|`BooleanFieldType`       |True/False, e.g., active, approved
|`DateFieldType`          |Date without time, e.g., creation date, due date
|`DateTimeFieldType`      |Timestamp with date and time, e.g., creation timestamp
|`DurationFieldType`      |Time span, e.g., processing time, SLA
|`EnumFieldType`          |Pick list with fixed values, e.g., status, category
|`ObjectFieldType`        |Reference to other objects, e.g., user, issues, document
|`EmailFieldType`         |Specialized string with email validation
|`PhoneFieldType`         |Specialized string with phone number format
|`UrlFieldType`           |Specialized string with URL validation
|`IPFieldType`            |Specialized string with IP validation
|`UserReferenceFieldType` |Reference to user objects, e.g., assignee, creator
|`WorkflowFieldType`      |State field with defined lifecycle logic, e.g., Open, In Progress, Done
|`PriorityFieldType`      |Predefined priority level, e.g., Low, Medium, High, Critical

## UI Concepts and Pages

The following UI mockups show how the complex structures and rules of field management in **KleeneStar** are translated into a comprehensible and user-friendly interface. The goal is to make working with fields within a class as intuitive, efficient, and secure as possible.

The user interface consistently follows the established design patterns of the **KleeneStar** web application. This creates a familiar user experience with clear processes and recognizable elements. Onboarding time remains short, and typical tasks in the field domain (e.g., creating, validating, indexing) can be completed quickly and reliably.

The mockups serve as a visual template for the final UI design. They show how navigation is structured in the context of a class, where relevant controls for fields are located, and how different system states of fields (such as active, archived, deleted as well as deprecation status) are displayed. Using concrete workflows, they illustrate how fields are created, edited, deprecated, archived, or removed. Particular emphasis is placed on clarity and adherence to field-level permissions.

### Field Management in Class Editing (Content)

Entry into field management occurs directly from the class overview. On the `ClassManager` page, each class row has an actions dropdown that includes the "Manage Fields" option. Selecting this entry navigates to the field management page of the respective class.

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
║│  - All               │░│ My Classes                         [Search] [+ Add Class] │║
║│  - Issues            │░│                                                           │║
║│  - Sub-Issues        │░│ Class Name       | Description                 | Status   │║
║│  - Hidden            │░│------------------|-----------------------------|----------│║
║│  - Archived          │░│ Incident         | Report of a disruption      | ...  […] │║
║│                      │░│ Problem          | Analysis of recurring errors| ...   ¦  │║
║│                      │░│ ChangeRequest    | Request for change   ┌──────────────┴┐ │║
║│                      │░│ ServiceRequest   | Standard service requ│ Edit          │ │║
║│                      │░│ KnowledgeArticle | Documented knowledge │ Clone         │ │║
║│                      │<│ Approval         | Approval step        │ Manage Fields │ │║
║│                      │<│ Request          | Inquiry or sub-proces│ Permissions   │ │║
║│                      │<│ Task             | Executable activity  │ <section>     │ │║
║│                      │░│ SLA              | Service Level Agreeme├───────────────┤ │║
║│                      │░│ Comment          | Free-text note       │ Delete        │ │║
║│                      │░│ UserFeedback     | User feedback        └───────────────┘ │║
║│                      │░│ Escalation       | Escalation to higher inst...| ...  […] │║
║│                      │░│                                                           │║
║│                      │░│                                   ‹ Prev  1  2  3  Next › │║
║├──────────────────────┤░│                                                           │║
║│ [Setting]         << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Field Management (Page)

This page serves as the central administrative view for all fields of a selected class. It provides a complete overview of existing field definitions and offers functions for creation, editing, organization, and governance at the field level.

The main area contains a tabular list of all fields in the class. Each row shows key attributes such as name, type, status, and state (including deprecation). For better orientation in large schemas, search and filter functions are integrated directly into the interface. The page supports actions such as editing, cloning, reordering, deprecating, archiving, and deleting fields as well as managing field permissions. New field definitions can be created via the "Add Field" button.

The page can be accessed via the "Manage Fields" option in class administration or from the class detail view. Changes to fields take immediate effect on the objects of the respective class, including validation and visibility rules, and are enforced according to the stored policies and audit guidelines.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Incident / Fields                                                 │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Fields────────────────┐ ┌Fields Content─────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All               │░│ My Fields                          [Search] [+ Add Field] │║
║│  - Required          │░│                                                           │║
║│  - Deprecated        │░│ Field Name   | Type      | Req. | Status                  │║
║│  - Archived          │░│--------------|-----------|------|-------------------------│║
║│                      │░│ Title        | String    | ✓    | Active              […] │║
║│                      │░│ Status       | Enum      | ✓    | Active               ¦  │║
║│                      │░│ Priority     | Enum      |      | Active  ┌────────────┴┐ │║
║│                      │░│ Assignee     | Link      |      | Active  │ Edit        │ │║
║│                      │░│ Tags         | Tags      |      | Active  │ Configure   │ │║
║│                      │<│ Description  | Text      |      | Active  │ Clone       │ │║
║│                      │<│ ReportedAt   | DateTime  | ✓    | Active  │ Permissions │ │║
║│                      │<│ Affected CI  | Link      |      | Active  │ <section>   │ │║
║│                      │░│                                           ├─────────────┤ │║
║│                      │░│                                   ‹ Prev  │ Delete      │ │║
║│                      │░│                                           └─────────────┘ │║
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

### Field Management - New/Edit (Modal)

The "Add Field" and "Edit Field" modals in the **KleeneStar** web application provide a central interface for creating and maintaining fields within a selected class.

When adding a field, basic properties such as name and description are specified and the field type is selected (e.g., String, Text, Integer, Float, Boolean, DateTime, Enum, Object). UI hints such as placeholder and help text support consistent capture and display scenarios.

When editing a field, all existing properties are prefilled. Adjustable items include, among others, name, description, type-specific options, cardinality, and validations. Changes are applied via "Save", while "Cancel" discards the process without applying changes. For potentially incompatible adjustments, the form points out required migrations or reindexing.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldAddEditModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Add Field / Edit Field                                               │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Field║│            Name*: [ Title                                          ] │║─────┐║
║│     ║│            Type*: [ String                                        ▼] │║     │║
║│  - A║│      Placeholder: [                                                ] │║ild] │║
║│  - R║│           Active: [ ]                                                │║     │║
║│  - D║│       Deprecated: [ ]                                                │║     │║
║│  - A║│ Access Modifier*: [ Private                                       ▼] │║-----│║
║│     ║│      Description: [ Human-readable help text …                     ] │║ […] │║
║│     ║│         HelpText: [                                                ] │║ […] │║
║│     ║│           Unique: [ ]                                                │║ […] │║
║│     ║│           Hidden: [ ]                                                │║ […] │║
║│     ║│          Indexed: [ ]                                                │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Field Management - Configure (Modal)

The "Configure" modal in the **KleeneStar** web application provides a central interface for context-sensitive configuration of existing fields within a selected class. In the configuration, neither identity characteristics of a field (e.g., name) nor the base type are defined. These belong to the schema or creation phase. The modal focuses exclusively on the four configuration categories Cardinality, Validation, Options, and Filter objects. Which categories are visible and editable depends on the field type. Non-applicable areas are disabled or hidden.

Configuration serves the precise control of value multiplicity, validation logic, selectable values, and referential constraints. Changes take effect in a controlled manner and, if necessary, are accompanied by reindexing or transformation rules. Not all settings are meaningful or available for every field type. The form therefore displays only the sections that are compatible in each case.

#### Cardinality

The "Cardinality" category controls how many values a field may accept, thereby defining the allowed multiplicity between single-field and multivalued forms.

Using minimum, maximum, and the "Unlimited" option, it is defined whether a field can contain exactly one, optionally one, or multiple values. The minimum count takes effect in combination with "Required" for valid entries. The maximum count limits lists to a defined upper bound. "Unlimited" removes the upper bound and is only meaningful for field types that support multiple values (e.g., lists, sets). For strictly scalar field types (e.g., Boolean), the category is not available. Changes to cardinality may require migration and backfill steps.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldConfigureModal═════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Configure                                                            │║     │║
║└─────║├─────────────┬────────────┬─────────┬────────────────┬──────────┬─────┤║─────┘║
║┌Field║│ Cardinality │ Validation │ Options │ Filter objects │ Workflow │ Prio│║─────┐║
║│     ║│             └────────────┴─────────┴────────────────┴──────────┴─────┤║     │║
║│  - A║│                                                                      │║ild] │║
║│  - R║│       Minimum: [   0]                                                │║     │║
║│  - D║│     Unlimited: [ ]                                                   │║     │║
║│  - A║│       Maximum: [   1]                                                │║-----│║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Validation

The "Validation" category defines the rules according to which field values are considered valid, thus preventing inadmissible entries already at capture time.

For text and string fields, regex patterns can be stored. Validations are only displayed if they match the type. More complex, dependent validations (e.g., conditional rules) can be configured as advanced policies and may require reindexing.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldAConfigureModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Configure                                                            │║     │║
║└─────║├─────────────┬────────────┬─────────┬────────────────┬──────────┬─────┤║─────┘║
║┌Field║│ Cardinality │ Validation │ Options │ Filter objects │ Workflow │ Prio│║─────┐║
║│     ║├─────────────┘            └─────────┴────────────────┴──────────┴─────┤║     │║
║│  - A║│                                                                      │║ild] │║
║│  - R║│ Regular expression: [                                              ] │║     │║
║│  - D║│                                                                      │║     │║
║│  - A║│                                                                      │║-----│║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Options

The "Options" category is used to manage permissible selection values and is available for enumerable field types such as Enum, Select, or Flags.

Value-label pairs can be maintained, optionally with stable keys, ordering, active/inactive status, and localizations. The list of options determines both capture and presentation (e.g., dropdown, checkbox group). Changes to options can trigger transformation steps (e.g., mapping old to new values) and require reindexing if necessary. For free text or numeric fields, this category is not relevant and is hidden.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldAConfigureModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Configure                                                            │║     │║
║└─────║├─────────────┬────────────┬─────────┬────────────────┬──────────┬─────┤║─────┘║
║┌Field║│ Cardinality │ Validation │ Options │ Filter objects │ Workflow │ Prio│║─────┐║
║│     ║├─────────────┴────────────┘         └────────────────┴──────────┴─────┤║     │║
║│  - A║│                                                                      │║ild] │║
║│  - R║│ Options: [                                                         ] │║     │║
║│  - D║│                                                                      │║     │║
║│  - A║│  [+ AddOption]                                                       │║-----│║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│   Option 1                                                         x │║ […] │║
║│     ║│   Option 2                                                         x │║ […] │║
║│     ║│   ...                                                              x │║ […] │║
║│     ║│   Option n                                                         x │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Filter objects

The "Filter objects" category restricts the permissible target objects for referential fields and thus ensures that only suitable references are set.

A WQL-based filter can be used to define target classes, status, metadata, tenant context, or other criteria. The filter is enforced during selection, validation, and optionally on the server. This prevents invalid links and improves data quality. The category is relevant only for fields that reference other objects. For non-referential types it is hidden. Adjustments to filters may require reindexing of the referenced fields.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldAConfigureModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Configure                                                            │║     │║
║└─────║├─────────────┬────────────┬─────────┬────────────────┬──────────┬─────┤║─────┘║
║┌Field║│ Cardinality │ Validation │ Options │ Filter objects │ Workflow │ Prio│║─────┐║
║│     ║├─────────────┴────────────┴─────────┘                └──────────┴─────┤║     │║
║│  - A║│                                                                      │║ild] │║
║│  - R║│ Filter by WQL: [                                                   ] │║     │║
║│  - D║│                                                                      │║     │║
║│  - A║│                                                                      │║-----│║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Workflow

The Workflow category is used to assign an active workflow to a field of type `WorkflowFieldType`. It enables the direct linkage of a field to a specific process model that governs or visualizes the lifecycle of an object.

Selection is limited to published (Active) workflows that are compatible with the class of the field. The assignment is binding and directly influences the field’s behavior, such as displaying status information, executing transitions, or integrating with UI components.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldAConfigureModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Configure                                                            │║     │║
║└─────║├─────────────┬────────────┬─────────┬────────────────┬──────────┬─────┤║─────┘║
║┌Field║│ Cardinality │ Validation │ Options │ Filter objects │ Workflow │ Prio│║─────┐║
║│     ║├─────────────┴────────────┴─────────┴────────────────┘          └─────┤║     │║
║│  - A║│                                                                      │║ild] │║
║│  - R║│ Workflow*: [                                                      ▼] │║     │║
║│  - D║│                                                                      │║     │║
║│  - A║│                                                                      │║-----│║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

#### Priority

The Priority category is used to assign a predefined priority level to a field of type `PriorityFieldType`. It enables the direct association of the field with a specific priority definition that reflects the urgency, importance, or processing order of an object.

Selection is limited to published priority values that are defined and released within the current workspace. The assignment is binding and directly affects the field’s behavior (for example, in object sorting, automatic triggering of time-sensitive processes (such as SLA tracking), or visual highlighting of urgency and escalation levels in the user interface).

Automatic migration of existing values does not take place. Fields retain their previous value in objects until those objects are actively edited. If the existing value is no longer valid or available in the new configuration, the field will be reset to the defined default value during the next edit. The user is transparently informed about this change to prevent unexpected effects and ensure traceability.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldAConfigureModal════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Configure                                                            │║     │║
║└─────║├────────┬────────────┬─────────┬────────────────┬──────────┬──────────┤║─────┘║
║┌Field║│ nality │ Validation │ Options │ Filter objects │ Workflow │ Priority │║─────┐║
║│     ║├────────┴────────────┴─────────┴────────────────┴──────────┘          │║     │║
║│  - A║│                                                                      │║ild] │║
║│  - R║│  Default priority*: [ Medium                                      ▼] │║     │║
║│  - D║│                                                                      │║     │║
║│  - A║│ ┌────────────────────────────┬────┬────────────────────────────────┐ │║-----│║
║│     ║│ │    Selected priorities     │    │     Available priorities       │ │║ […] │║
║│     ║│ ├────────────────────────────┼────┼────────────────────────────────┤ │║ […] │║
║│     ║│ │ ⠿ High                     │ << │ Low                            │ │║ […] │║
║│     ║│ │ ⠿ Medium                   │ <  │ Critical                       │ │║ […] │║
║│     ║│ │                            │ >  │                                │ │║ […] │║
║│     ║│ │                            │ >> │                                │ │║ […] │║
║│     ║│ │                            │    │                                │ │║ […] │║
║│     ║│ │                            │    │                                │ │║ […] │║
║│     ║│ └────────────────────────────┴────┴────────────────────────────────┘ │║     │║
║│     ║│                                                                      │║xt › │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Field Management - Clone (Modal)

Cloning a field enables the quick reuse of proven field definitions within the same class. The function is provided in a separate modal dialog, which can be opened from the class detail view (actions dropdown "Manage Fields") as well as from the class’s field overview. When cloning, a new field is created whose central properties can be adopted from the original. System-critical characteristics such as the unique field key and, if applicable, permission-related assignments are newly generated or explicitly requested.

In the modal, only the new name of the field can be adjusted. All other properties, such as validation rules, indexing options, default values, UI hints, and permissions, are fully inherited from the original field and must be modified manually afterward if needed.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldCloneModal═════════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Clone Field                                                          │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Field║│ The field 'Priority' from class 'Incident' will be cloned.           │║─────┐║
║│     ║│ Please adjust the details for the new field.                         │║     │║
║│  - A║│                                                                      │║ild] │║
║│  - R║│       New Name*: [ Priority (Copy)                                 ] │║     │║
║│  - D║│                                                                      │║     │║
║│  - A║│                                                                      │║-----│║
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
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                      [Clone] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Field Management - Delete (Modal)

Deleting a field is a critical and irreversible operation that requires explicit confirmation. To prevent unintentional deletions, a modal dialog is used, which can be opened from the class’s field overview or via the actions dropdown ("Manage Fields"). The dialog clearly states the field to be deleted and its class as well as the field key. As an additional safety measure, the exact field key must be entered. Only when it matches does the "Delete" button become active. The dialog points out the consequences (e.g., removal of existing values, update/removal of indexes, possible impacts on validation or cross-field rules). A "Cancel" option aborts the process without changes.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔═FieldDeleteModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Delete Field                                                         │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Field║│                                                                      │║─────┐║
║│     ║│ Are you sure you want to delete the field 'Tags' in class 'Incident'?│║     │║
║│  - A║│ This action cannot be undone. Field values and related indexes will  │║ild] │║
║│  - R║│ be removed. Cross-field rules may be affected.                       │║     │║
║│  - D║│                                                                      │║     │║
║│  - A║│ To confirm, please type the field name 'Tags' in the box below:      │║---- │║
║│     ║│ [                                                                  ] │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║│     ║                                                                        ║     │║
║├─────║                                                                        ║     │║
║│     ║                                                     [Delete] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Field Management - Permissions Management (Modal)

A modal dialog serves as the central interface for granting field-specific access rights by assigning appropriate permission policies to groups. This enables fine-grained control over which groups may read, write, or administratively manage a particular field (e.g., schema changes, reindexing, or permission assignment itself). It is invoked via the "Permissions" button within field management, either as a row action in the field list or from the field detail view. Display and use require the permission `field_manage_permissions`. In the dialog, groups can be selected and suitable field policies assigned. Existing assignments can be adjusted or removed at any time.

Assignments are displayed in tabular form and can be managed efficiently via search and pagination. Changes take effect immediately and follow the stored guidelines and auditing requirements.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔FieldPermissionsModal═══════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│  Manage Permissions for Field 'Assignee'                             │║     │║
║└─────║├──────────────────────────────────────────────────────────────────────┤║─────┘║
║┌Field║│                                                                      │║─────┐║
║│     ║│  Assign Group*: [ IT Support ▼]                                      │║     │║
║│  - A║│        Policy*: [ field_edit_policy ▼]                               │║ild] │║
║│  - R║│                                                                      │║     │║
║│  - D║│  [+ Assign]                                                          │║     │║
║│  - A║│                                                             [Search] │║---- │║
║│     ║│ Assigned Group       | Effective Policy                              │║ […] │║
║│     ║│----------------------|-----------------------------------------------│║ […] │║
║│     ║│ IT Support           | field_edit_policy                           X │║ […] │║
║│     ║│ Service Desk         | field_view_policy                           X │║ […] │║
║│     ║│ Incident Managers    | field_admin_policy                          X │║ […] │║
║│     ║│ Privacy Officers     | field_privacy_policy                        X │║ […] │║
║│     ║│                                                                      │║ […] │║
║│     ║│                                             ‹ Prev  1  2  3  Next ›  │║ […] │║
║│     ║│                                                                      │║     │║
║│     ║└──────────────────────────────────────────────────────────────────────┘║xt › │║
║│     ║                                                                        ║     │║
║├─────║                                                                        ║     │║
║│     ║                                                                [Done]  ║     │║
║└─────║                                                                        ║─────┘║
║┌Foote╚════════════════════════════════════════════════════════════════════════╝─────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Sitemap Field Management

The sitemap for field management describes the hierarchical structure and navigation paths within the user interface for managing fields in **KleeneStar**. It ensures a clear organization of the pages, forms the basis for routing in the web application, and is aligned with the system’s modular structure.

|Path                                                                          |Page                |Description
|------------------------------------------------------------------------------|--------------------|-------------------------------------------------------------
|`/workspaces/{workspaceKey}/classes/{classKey}/fields`                        |Field management    |Central overview and administration of all fields of a class.
|`/workspaces/{workspaceKey}/classes/{classKey}/fields/add`                    |Field creation      |Form for creating a new field within the class.
|`/workspaces/{workspaceKey}/classes/{classKey}/fields/{fieldKey}`             |Field detail view   |Detail view of a single field with attributes and status.
|`/workspaces/{workspaceKey}/classes/{classKey}/fields/{fieldKey}/edit`        |Field editing       |Form for changing the field definition and properties.
|`/workspaces/{workspaceKey}/classes/{classKey}/fields/{fieldKey}/configure`   |Field configuration |Modal for context-sensitive adjustment of cardinality, validation, options, and filter objects.
|`/workspaces/{workspaceKey}/classes/{classKey}/fields/{fieldKey}/clone`       |Field cloning       |Dialog for reusing an existing field definition.
|`/workspaces/{workspaceKey}/classes/{classKey}/fields/{fieldKey}/delete`      |Field deletion      |Modal for confirming and performing the final removal of a field.
|`/workspaces/{workspaceKey}/classes/{classKey}/fields/{fieldKey}/permissions` |Field permissions   |Modal for managing group and policy assignments for a field.

## API Interfaces (REST Endpoints) - Field Management

For programmatic interaction, third-party integration, and automation, **KleeneStar** provides a standardized REST API for managing field definitions within a class. The interface follows REST principles and uses JSON as the data format. Authentication and authorization are ensured by **KleeneStar**. Standard HTTP status codes indicate the result of each request, including success, validation errors, permission issues, or missing resources.

Field administration is performed via the following endpoints:

|Endpoint                                                              |HTTP Method |Description
|----------------------------------------------------------------------|------------|------------------------------------------------------------
|`/api/1/classes/{classKey}/fields`                                    |GET         |Lists all fields of a class. Results are paginated and can be filtered by status, type, and name.
|`/api/1/classes/{classKey}/fields`                                    |POST        |Creates a new field within the class. Requires at least a unique `key`, `name`, and `type` in the request body.
|`/api/1/classes/{classKey}/fields/{fieldKey}`                         |GET         |Returns detailed information about a specific field by its key.
|`/api/1/classes/{classKey}/fields/{fieldKey}`                         |PUT         |Updates the field definition and properties (e.g., name, type, cardinality, validations, indexes, privacy). The field key is immutable.
|`/api/1/classes/{classKey}/fields/{fieldKey}`                         |DELETE      |Permanently deletes a field. Values and indexes are removed. Confirmation is required.
|`/api/1/classes/{classKey}/fields/{fieldKey}/archive`                 |POST        |Archives a field, setting it to a read-only state.
|`/api/1/classes/{classKey}/fields/{fieldKey}/restore`                 |POST        |Restores an archived or deleted field, setting the status to `active`.
|`/api/1/classes/{classKey}/fields/{fieldKey}/clone`                   |POST        |Creates a new field by cloning an existing field definition. The request body can contain optional settings for validation, indexing, permissions, etc.
|`/api/1/classes/{classKey}/fields/{fieldKey}/configure`               |PUT         |Configures field-specific settings such as cardinality, validation rules, options, and filter objects.
|`/api/1/classes/{classKey}/fields/{fieldKey}/permissions`             |GET         |Lists all permission assignments (group-policy) for a field. Requires the permission field_manage_permissions.
|`/api/1/classes/{classKey}/fields/{fieldKey}/permissions`             |POST        |Creates a new permission assignment by assigning a policy to a group for the field. The request body requires groupId and policyId.
|`/api/1/classes/{classKey}/fields/{fieldKey}/permissions/{profileId}` |DELETE      |Removes a permission assignment from a field, thereby revoking the group’s rights.
|`/api/1/classes/{classKey}/fields/{fieldKey}/audit`                   |GET         |Returns the audit history for all actions around the field.
|`/api/1/classes/{classKey}/fields/{fieldKey}/migrate`                 |POST        |Initiates a migration of the field schema, e.g., type change, backfill, transformation.

Standard error responses include `400 Bad Request` for validation errors (e.g., key already taken), `401 Unauthorized` for missing authentication, `403 Forbidden` for insufficient permissions, and `404 Not Found` if the resource does not exist. A successful creation (POST) is confirmed with `201 Created`, a successful deletion (DELETE) with `204 No Content`.

## Field Events

Field management in **KleeneStar** relies on an event-driven architectural model to communicate state changes transparently and reactively throughout the system. Events are published via the **WebExpress** `EventManager`, which serves as the central backbone for events. This allows other modules, plugins, or external systems to subscribe to relevant changes without being directly coupled to the `FieldManager`.

The following events are published by the `FieldManager` via the **WebExpress** `EventManager`:

|Event Name                |Description
|--------------------------|-----------------------------------------------------------------------------
|`FieldAdded`              |Triggered when a new field has been successfully integrated into a class.
|`FieldUpdated`            |Signals changes to the metadata or properties of an existing field.
|`FieldRemoved`            |Indicates the permanent deletion of a field from a class.
|`FieldArchived`           |Marks a field as archived and sets it to a read-only state.
|`FieldRestored`           |Reports the restoration of a previously archived or deleted field.
|`FieldCloned`             |Triggered when a field is successfully duplicated and inserted into the class.
|`FieldConfigured`         |Indicates that a field’s configuration (e.g., validations, cardinality, options) has changed.
|`FieldPermissionsUpdated` |Reports changes to the permission assignments of a field.
|`FieldMigrated`           |Signals the execution of a field migration, e.g., for schema changes or backfill.

Each field-related event includes a well-defined payload that provides all necessary context for system-wide processing. This payload contains the unique identifier of the field and its associated class, a timestamp marking when the event occurred, information about the user or system component that triggered the event, and metadata describing the nature and origin of the action. This structure ensures that events are traceable, actionable, and seamlessly integrated into reactive workflows and audit mechanisms across the platform.

Through integration with the WebExpress EventManager, these events are available both within the application and to connected subsystems.

## Field Management Permission Model

The permission model for field management in **KleeneStar** is context-based and enables fine-grained control of access and actions at the field level within a class. The link between global groups and policies is established via a field profile that is exclusive to a class and a field.

A field profile defines which policy a global group receives for a particular field within a class. This creates a context-sensitive role assignment that enables flexible and differentiated rights management down to the single attribute.

- **Principle:** A user receives the rights of a policy for a field in a class if they are a member of a group for which a corresponding field profile (group → policy) exists in this context.
- **Flexibility:** The same global group (e.g., "Data Stewards") can receive the policy `field_view_policy` (read-only) for the "email" field in Class A and the policy `field_edit_policy` (edit) for the "phone" field in Class B.
- **Administration:** Users with administrative rights for a class (e.g., via the policy `field_admin_policy`) can create, edit, and remove field profiles and thus control the assignment of policies to groups for fields.

The following table lists the granular permissions for comprehensive control of field management:

|Permission                 |Description
|---------------------------|-----------------------------------------------------------------------------------
|`field_create`             |Allows the creation of new fields within a class.
|`field_read`               |Permits reading the metadata and properties of a field.
|`field_update`             |Authorizes changing the definition and configuration of a field.
|`field_delete`             |Allows the permanent removal of a field.
|`field_archive`            |Enables archiving an active field.
|`field_restore`            |Permits restoring an archived field.
|`field_clone`              |Authorizes cloning an existing field.
|`field_manage_permissions` |Allows managing field profiles (assigning policies to groups) for a field.
|`field_read_values`        |Permits reading the actual values of a field for the associated objects.
|`field_write_values`       |Allows capturing, editing, and deleting field values within objects.

These permissions are bundled into logical policies that represent typical use cases and responsibilities. Policies can be assigned to global groups in the field profile of a class.

|Policy                 |Description                                            |Included permissions
|-----------------------|-------------------------------------------------------|-------------------------
|`field_admin_policy`   |Full administrative control over a field.              |all `field_*`
|`field_edit_policy`    |Authorizes the management of field values.             |`field_read`, `field_read_values`, `field_write_values`
|`field_view_policy`    |Grants read access to the field definition and values. |`field_read`, `field_read_values`
|`field_creator_policy` |Global policy for creating new fields.                 |`field_create`

Assignment and management of field policies are performed via the field administration interfaces and are transparently documented in auditing and governance.

## Conclusion

The "KleeneStar Field Management" document forms the conceptual foundation for implementing field management in the **KleeneStar** system. It describes the essential functional requirements from the areas of data modeling, system architecture, and user interface and presents the lifecycle of a field as well as a differentiated permission model for attributes. The presentation deliberately follows a high-level concept and leaves technical details such as handling of parallel access open. The focus is on the central processes and governance of fields. More complex processes such as migration of large data volumes and asynchronous processing are not covered, and neither are the regulations for data retention. The reference implementation has since answered the other questions this document left open, and they are no longer open: **persistence** is EF Core through `KleeneStarDbContext` and a provider assembly per database; **validation** is the framework's `Validate…` attributes plus the gate of `/api/1/fields`, which names the class a field cannot be stored without instead of leaving it to the foreign key; **user notification** is raised by the `FieldManager` on every change through `CoreHub.AddNotification`; and the **audit system** is the installation-wide, hash-chained log of [kleenestar.audit.md](kleenestar.audit.md), which subscribes to the field manager's events centrally. Overall, the document provides a solid conceptual basis; the questions still unanswered are the ones named above.
