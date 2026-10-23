![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Template Management Concept

In **KleeneStar**, a "Template" is a predefined, reusable blueprint for creating objects of a specific class. While a **Class** defines the structural schema (the data types, validations, and allowed fields), a **Template** provides concrete preset data, default configurations, contextual help, and categorization for the actual instantiation of that class. Templates streamline the creation of common object types (e.g., "Hardware Request", "Bug Report", "New Employee Onboarding") by pre-filling fields, setting predefined statuses, or providing targeted placeholder texts. 

The goal of the template management system is to empower workspace administrators to create guided, error-resistant, and efficient data entry experiences for end-users, without needing to duplicate or fragment the underlying class structure.

The `TemplateManager` is responsible for the lifecycle of these templates. It ensures that templates remain compatible with their underlying class definitions, manages their categorization, and controls which templates are exposed to which user groups based on permissions.

## Data Model and Relationships

Templates are bound to a specific workspace and a specific class. They contain metadata for presentation (icon, title, description, category) and a payload of preset field values.

- **Key attributes**: `Id` (stable GUID), `Name` (internal/system name), `Title` (localizable display name), `Description` (localizable), `Category` (for grouping in the UI), `Icon` (visual identifier), `State` (Active, Archived), `Created`/`Updated` timestamps.
- **Class Reference**: A mandatory link (`ClassId`) to the class this template instantiates.
- **Presets**: A serialized dictionary or collection of field presets (mapping `FieldId` or `FieldName` to default values).
- **Permissions**: Like classes and fields, templates can have specific permission profiles (e.g., restricting a "VIP Support Request" template to specific user groups).

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                             KleeneStar Core Data Model                               ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║          ┌───────────┐ *           * ┌───────┐ 1          * ┌───────┐                ║
║          │ Workspace ├───────────────► Class ◄──────────────┤ Field │                ║
║          └─────┬─────┘               └───▲───┘              └───────┘                ║
║                │ 1                       │ 1                                         ║
║                │                         │                                           ║
║                │ *                     * │                                           ║
║          ┌─────▼─────┐ *           1 ┌───┴───┐                                       ║
║          │ Template  ├───────────────► Object│                                       ║
║          └───────────┘               └───────┘                                       ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Software Architecture

The `TemplateManager` follows the established pattern of component managers in KleeneStar. It handles CRUD operations for templates, enforces security policies, and integrates with the `EventManager` to broadcast lifecycle changes (`TemplateAdded`, `TemplateUpdated`, `TemplateRemoved`).

When a user initiates the object creation process, the system queries the `TemplateManager` for all `Active` templates available in the target workspace that the user has permission to see. 

If a template is modified or its underlying class changes (e.g., a required field is added to the class), the template remains valid but the object creation form will simply present the new required field as empty, prompting the user for input. Templates act as *pre-fillers*, not strict immutable contracts.

### Applying a template

A template is applied by `/api/1/objects` when a create payload names it (`TemplateId`), and the endpoint - not the wizard - is where the presets reach the object, so a script or a hand-written client gets the same result as the dialog. Three rules govern the transfer:

- **Presets are keyed by field name** (`{"Priority":"P3 - Moderate","Impact":"Medium"}`) and land as value rows of the fields the class defines. A preset on a field the class lacks is dropped; a preset on a fixed set of options (a priority, a selection) has to name a value the field offers - the create form cannot pre-select a value the input does not know, and the shipped seeds are checked for exactly this (`SeedTemplatePresetsNameFieldsAndValuesOfTheirClass`).
- **The caller's answer wins, a blank is not an answer.** The wizard pre-fills the create form from the presets and posts *every* input of it, so an input the form could not pre-fill arrives as an empty string. Such a field takes the preset; a field the caller actually answered keeps the answer. The empty document the prose editor submits for a field nobody wrote into counts as blank.
- **`Summary` and `Description` are columns of the object, not value rows.** A preset on either is written onto the row itself before the object is stored, and only where the caller left it blank. A child of a composite template has nobody answering for it: every preset applies, and a summary or description it presets wins over the child template's own name and description.

- **The template's own description is the `Description` preset**, unless its presets name one. The description is written with the same editor the object description uses, and a child of a composite has always been described by it; answering it as a preset is what makes the text a template was written with the text its objects start with. A template that wants to say one thing on its card and another in its objects writes `Description` into its presets, which wins. Preset names are matched ignoring case.

The object row, its answered values, its presets and the children of a composite template are recorded in one genesis commit per object.

### Work structures across workspaces

A composite template describes a whole work structure: several objects, who does each, what waits for what, and in which department each part is done. The onboarding of a new colleague is the typical case - filed in HR, it creates the account request at the IT desk and the desk set-up in HR, and the set-up waits for the account.

|Element of the structure |Template                                  |Created as
|-------------------------|------------------------------------------|------------------------------------------------------------
|A part of the work       |a child template (`ParentId`)             |an object of the child template's class, **in that class's workspace**
|Its place in the order   |-                                         |an `AggregatesProgress` relation from the parent object (the order reports the progress of the whole structure, `kleenestar.relation.md`); additionally `ParentId` when the child stays in the parent's workspace and the parent's class allows it
|Who does it              |`AssigneeId`                              |the object's assignee (operator-side accounts only)
|What it waits for        |`PredecessorId` (a sibling below the same parent) |a `BlocksCompletion` relation from the predecessor's object to this one - the workflow refuses to finish it first
|Classification           |-                                         |the default security level of the child's class

**Both sides consent to a cross-workspace link.** Linking a template below a template of another workspace requires administering that one too (`ValidateHierarchy`; the parent candidates offer templates of other workspaces only where the caller administers them), because the link changes what it creates. The objects of a structure are then created on the template's authority, not on the filer's: an HR clerk filing an onboarding does not need write access to the IT desk.

**Reliable and repeatable** (`WebManager/ObjectCreationRun`, entity `ObjectCreation`, since 2026-10-22). A creation writes through several managers that persist on their own, so there is no single transaction to roll back; the run compensates instead:

- **Idempotent.** The wizard renders a fresh key into every dialog (`IdempotencyKey`), a REST client may pass `?idempotencykey=`. The key is unique: a second submission under a completed key is answered with the object of the first (no duplicate structure), one under a running key is refused with a readable message, a rolled-back key may run again. A dialog opened anew creates anew - that is what repeating a template means.
- **Atomic.** Every object and relation is recorded in the run's row *as it is written*. A failure takes them back - relations first, objects in reverse order, unrestricted - records the error and rethrows. The audit log stays append-only and shows the created-and-removed objects.
- **Crash-safe.** A host that dies half-way leaves a running record naming what was written; the next start rolls back running records older than 15 minutes (`RecoverAbandoned`, from `KleeneStarApplication.Run`) and prunes completed records older than a week.

## UI Concepts and Pages

Template management is integrated into the workspace settings, typically accessible alongside classes and fields. For end-users, templates manifest as the starting point of the object creation journey.

### Object Creation (End-User View)

When a user clicks "Add Object", they are not immediately presented with a blank form. Instead, they see the **Template Catalog**. Templates are grouped by category (e.g., "IT Support", "HR", "Facilities").

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Create new object                                                 │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Sidebar───────────────┐ ┌Content────────────────────────────────────────────────────┐║
║│                      │░│                                                           │║
║│  - All Categories    │░│ Select an object template:                       [Search] │║
║│  - IT Support        │░│                                                           │║
║│  - HR & Onboarding   │░│ ┌IT Support─────────────────────────────────────────────┐ │║
║│  - Facilities        │░│ │                                                       │ │║
║│  - Operations        │░│ │ ┌──────────────────┐ ┌──────────────────┐             │ │║
║│                      │<│ │ │ [Icon]           │ │ [Icon]           │             │ │║
║│                      │<│ │ │ Software Issue   │ │ Hardware Request │             │ │║
║│                      │<│ │ │ Report a bug or  │ │ Request new IT   │             │ │║
║│                      │░│ │ │ software problem │ │ equipment        │             │ │║
║│                      │░│ │ └──────────────────┘ └──────────────────┘             │ │║
║│                      │░│ └───────────────────────────────────────────────────────┘ │║
║│                      │░│ ┌HR & Onboarding────────────────────────────────────────┐ │║
║│                      │░│ │ ┌──────────────────┐ ┌──────────────────┐             │ │║
║│                      │░│ │ │ [Icon]           │ │ [Icon]           │             │ │║
║│                      │░│ │ │ New Employee     │ │ Leave Request    │             │ │║
║│                      │░│ │ │ Start onboarding │ │ Apply for PTO or │             │ │║
║│                      │░│ │ │ process          │ │ sick leave       │             │ │║
║│                      │░│ │ └──────────────────┘ └──────────────────┘             │ │║
║│                      │░│ └───────────────────────────────────────────────────────┘ │║
║│                      │░│                                                           │║
║├──────────────────────┤░│                                                           │║
║│ [Setting]         << │░│                                                           │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
║┌Footer──────────────────────────────────────────────────────────────────────────────┐║
║│ [Documentation]        |        KleeneStar v1.2.3        |      [Report a problem] │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Template Management (Admin View)

Workspace administrators manage templates via a dedicated list view, similar to class or field administration. They can create, edit, clone, or delete templates.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Breadcrumb──────────────────────────────────────────────────────────────────────────┐║
║│ / Service Desk / Settings / Templates                                              │║
║└────────────────────────────────────────────────────────────────────────────────────┘║
║┌Sidebar───────────────┐ ┌Content────────────────────────────────────────────────────┐║
║│                      │░│ Templates                                [Search] [+ Add] │║
║│  - All               │░│                                                           │║
║│  - Active            │░│ Title              | Class          | Category  | Status  │║
║│  - Archived          │░│--------------------|----------------|-----------|---------│║
║│                      │░│ Software Issue     | Incident       | IT        | Active  │║
║│                      │░│ Hardware Request   | ServiceRequest | IT        | Active  │║
║│                      │░│ New Employee       | Request        | HR        | Active  │║
║│                      │░│ Leave Request      | Request        | HR        | Active  │║
║│                      │░│ Network Access     | ServiceRequest | IT        | Arch.   │║
║└──────────────────────┘ └───────────────────────────────────────────────────────────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

### Template Edit (Modal/Page)

Editing a template involves setting its metadata (Title, Category, Icon) and defining the **Presets**. Presets are rendered dynamically based on the selected Class. The admin fills out the form exactly as an end-user would, but saves it as a template rather than an object.

```
╔WebAppPage════════════════════════════════════════════════════════════════════════════╗
║┌Header──────────────────────────────────────────────────────────────────────────────┐║
║│ * KleeneStar     Workspace ▼   Dashboard ▼       [+ AddObject]         [Search]    │║
║└─────╔TemplateEditModal═══════════════════════════════════════════════════════╗─────┘║
║┌Bread║┌Form──────────────────────────────────────────────────────────────────┐║─────┐║
║│ / Se║│ Edit Template                                                        │║     │║
║└─────║├──────────────┬────────────────────────────┬──────────────────────────┤║─────┘║
║┌Sideb║│ General Info │ Presets (Class: Incident)  │ Permissions              │║─────┐║
║│     ║├──────────────┴────────────────────────────┴──────────────────────────┤║     │║
║│  - A║│                                                                      │║ Add]│║
║│  - A║│       Title*: [ Software Issue                                     ] │║     │║
║│  - A║│  Description: [ Report a bug or software problem                   ] │║     │║
║│     ║│     Category: [ IT Support                                         ] │║atus │║
║│     ║│         Icon: [ (Select Icon) ▼                                    ] │║-----│║
║│     ║│       Active: [x]                                                    │║ctive│║
║│     ║│                                                                      │║ctive│║
║│     ║│                                                                      │║ctive│║
║│     ║└──────────────────────────────────────────────────────────────────────┘║     │║
║├─────║                                                                        ║     │║
║│     ║                                                       [Save] [Cancel]  ║     │║
║└─────║                                                                        ║─────┘║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

## Permissions

Template permissions mirror field and class permissions:
- `template_create`, `template_read`, `template_update`, `template_delete`
- Standard policies: `template_admin_policy`, `template_edit_policy`, `template_view_policy`

By assigning `template_view_policy` to specific groups in a template's profile, administrators can hide internal or sensitive templates from regular users, even if those users have permission to create objects of the underlying class.

## API Interfaces

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/1/workspaces/{workspaceKey}/templates` | GET | List templates |
| `/api/1/workspaces/{workspaceKey}/templates` | POST | Create template |
| `/api/1/workspaces/{workspaceKey}/templates/{templateId}` | GET/PUT/DELETE | Read/Update/Delete template |
| `/api/1/workspaces/{workspaceKey}/templates/categories` | GET | List distinct categories used in templates |

## Conclusion
The Template Management system bridge the gap between abstract structural definitions (Classes) and end-user data entry by providing curated, pre-filled starting points. This concept promotes data consistency and greatly improves the usability of the KleeneStar platform for daily operational tasks.