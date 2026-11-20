![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Template Management Concept

In **KleeneStar**, a "Template" is a predefined, reusable blueprint for creating objects of a specific class. While a **Class** defines the structural schema (the data types, validations, and allowed fields), a **Template** provides concrete preset data, default configurations, contextual help, and categorization for the actual instantiation of that class. Templates streamline the creation of common object types (e.g., "Hardware Request", "Bug Report", "New Employee Onboarding") by pre-filling fields, setting predefined statuses, or providing targeted placeholder texts. 

The goal of the template management system is to empower workspace administrators to create guided, error-resistant, and efficient data entry experiences for end-users, without needing to duplicate or fragment the underlying class structure.

The `TemplateManager` is responsible for the lifecycle of these templates. It ensures that templates remain compatible with their underlying class definitions, manages their categorization, and controls which templates are exposed to which user groups based on permissions.

## Data Model and Relationships

Templates are bound to a workspace and serve one or several classes of it. They contain metadata for presentation (icon, title, description, category) and a payload of preset field values.

- **Key attributes**: `Id` (stable GUID), `Name` (internal/system name), `Title` (localizable display name), `Description` (localizable), `Category` (the categories that group the template in the UI, separated by `;`, see below), `Icon` (visual identifier), `State` (Active, Archived), `Created`/`Updated` timestamps.
- **Class Reference**: A mandatory link (`ClassId`) to the owning class. It decides the workspace, the permission chain (`ClassOf` of `/api/1/templates`, `RouteAuthorization`) and the class a child of a composite is created in.
- **Further classes**: `TemplateClass` rows (`TemplateId`, `ClassId`, unique pair, cascading from both sides) name the other classes of the same workspace the template serves. The owning class is never stored as a further class. `ITemplateManager.GetClassIds` answers the owning class first, then the further classes; `GetFurtherTemplateIds(classId)` answers the templates serving a class as a further class; `SetFurtherClasses` replaces them.
- **Presets**: A JSON object mapping field names to values in storage form (the form a value row holds: dates round trip, booleans `true`, tags comma separated), plus `Summary` for the title. One preset reaches every served class that has a field of that name.
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

### Templates serving several classes

The object creation wizard offers a template under each class it serves (`ObjectAddFormFragment`, `Catalog.TemplatesOf`). The tile selection needs a distinct value per card, so the card under the owning class carries the template id and a card under a further class carries `{templateId}@{classId}` (`WebControl/TemplateChoice`). `/api/1/objects` and `/api/1/objects/form` read the template id through `TemplateChoice.TryParse`; the class travels as `ClassId`. A payload naming only a template creates the object in the owning class.

The template overview lists every served class in the class column (owning class first); its class quick filter and the template count of the class overview include the templates serving the class as a further class.

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
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
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
║│ * KleeneStar     Workspace ▼   Insight ▼         [+ AddObject]         [Search]    │║
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

### Template wizard (add and edit)

Templates are created and edited in a wizard laid out like the object creation wizard (`WebFragment/Template/TemplateWizardFragmentBase`, sealed `TemplateAddFormFragment` on `/templates/{workspacekey}/add` and `TemplateEditFormFragment` on `/template/{templateid}/edit`). The steps are built per request.

|Step       |Content
|-----------|---------------------------------------------------------------------------------------------------------------------
|`classes`  |Tile selection `Classes` with several selectable tiles (Ctrl+click adds, Shift+click selects a range), semicolon separated. Offered: the active concrete classes of the workspace the caller administers, plus the classes the edited template serves. Each card shows the object type, the description, the number of fields that take a preset and the number of templates. The owning class carries the chip *Owner*.
|`template` |`Name` (unique among all templates, `/api/1/templates/uniquename?exclude={templateid}` on an edit), `Category` (tag input, several categories; a comma, semicolon or space ends one), `Description` (prose editor; the description the objects start with).
|`structure`|`ParentId`, on an edit `AssigneeId` and `PredecessorId`, `State`.
|`presets`  |Rendered by `POST /api/1/templates/form?part=presets[&id={templateid}]` from the wizard payload (`WWW/Api/_1_/Templates/Form`, built by `WebControl/TemplatePresetForm`).

**Presets step.** The step shows a title input (`Summary`) and the dialog inputs of the object mask (`ObjectFieldSheet.CreateDialogInput`) for the fields of the selected classes, once per field name: a name several classes share appears under the first class that has it, laid out after that class's create form (else its edit form); fields on neither form follow below it. Several contributing classes become one tab per class, with a note that fields of the same name share one preset. Workflow, attachment, picture and password fields take no preset, and no input is required. The inputs are named `Preset_{field name}`, because the template's own attributes (`Name`, `Category`, `State`, ...) travel in the same payload. An input starts from the answer the payload carries for it, else from the stored preset.

**Saving** (`/api/1/templates`): the classes come from `Classes` (an API client may send `ClassId`); a create needs at least one, all of them in one workspace (the template's on an edit) and administered by the caller (`MayCreate`, `ValidateClasses`). The first selected class owns a new template; an edit keeps the owning class while it is selected. When the payload carries preset keys, `TemplatePresetForm.Collect` writes the presets: every offered field takes its answer in storage form; a blank answer, an unticked checkbox, an empty editor document and the starting value of a control that always submits one (colour `#000000`, rating `0`, slider `0;100`, range `5`) preset nothing, unless the field carried a stored preset. A stored `Description` or `SecurityLevelId` preset is kept, a preset naming no field of the served classes is dropped. A payload without preset keys leaves the presets unchanged. A clone copies the classes of its source.

**Categories** (`WebControl/TemplateCategories`): `Template.Category` holds the categories separated by `;`, the value format of the tag input (wizard and clone dialog). `/api/1/templates` stores them trimmed, each once ignoring case, `null` for none, and refuses a text longer than the column (128 characters). A single category without separator, as seeds and workspace templates write it, is one category. Readers split the text: the sidebar lists every category once, its filter (`TemplateCategoryFilter`) matches a whole category (`Category = c`, starting with `c;`, ending with `;c` or containing `;c;`), the table shows the column as tags, the template cards of the object wizard name the categories joined by ` · `.

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