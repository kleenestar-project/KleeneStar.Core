![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Template Management Concept

In **KleeneStar**, a "Template" is a predefined, reusable blueprint for creating objects of a specific class. While a **Class** defines the structural schema (the data types, validations, and allowed fields), a **Template** provides concrete preset data, default configurations, contextual help, and categorization for the actual instantiation of that class. Templates streamline the creation of common object types (e.g., "Hardware Request", "Bug Report", "New Employee Onboarding") by pre-filling fields, setting predefined statuses, or providing targeted placeholder texts. 

The goal of the template management system is to empower workspace administrators to create guided, error-resistant, and efficient data entry experiences for end-users, without needing to duplicate or fragment the underlying class structure.

The `TemplateManager` is responsible for the lifecycle of these templates. It ensures that templates remain compatible with their underlying class definitions, manages their categorization, and controls which templates are exposed to which user groups based on permissions.

## Data Model and Relationships

Templates are bound to a workspace and serve one or several classes of it. They contain metadata for presentation (icon, title, description, category) and a payload of preset field values.

- **Key attributes**: `Id` (stable GUID), `Name` (internal/system name), `Title` (localizable display name), `Description` (localizable), `Category` (the categories that group the template in the UI, separated by `;`, see below), `Icon` (visual identifier), `State` (Active, Archived), `Created`/`Updated` timestamps.
- **Class Reference**: A mandatory link (`ClassId`) to the owning class. It decides the workspace and the permission chain (`ClassOf` of `/api/1/templates`, `RouteAuthorization`).
- **Further classes**: `TemplateClass` rows (`TemplateId`, `ClassId`, unique pair, cascading from both sides) name the other classes of the same workspace the template serves. The owning class is never stored as a further class. `ITemplateManager.GetClassIds` answers the owning class first, then the further classes; `GetFurtherTemplateIds(classId)` answers the templates serving a class as a further class; `SetFurtherClasses` replaces them.
- **Structure**: a template describes one object. Work structures of several objects are template hierarchies (see *Template hierarchies*), which reference templates.
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

The template overview lists the templates flat. It lists every served class in the class column (owning class first); its class quick filter and the template count of the class overview include the templates serving the class as a further class.

### Template hierarchies

A template hierarchy (`TemplateHierarchy`, managed by `ITemplateHierarchyManager`) describes a whole work structure: several objects, who does each, what waits for what, and in which department each part is done. The onboarding of a new colleague is the typical case: filed in HR, it creates the account request at the IT desk and the desk set-up in HR, and the set-up waits for the account. A hierarchy is its own record beside the templates; templates carry no structure, and one template may stand in several hierarchies.

- **Attributes**: `Name` (unique within the workspace, at most 64 characters), `Description` (prose editor), `Category` (as for templates), `Icon`, `State` (only an active hierarchy is offered), `WorkspaceId` (the workspace of the root template's class), `Created`/`Updated`.
- **Root**: `RootTemplateId` names the template the object the user creates starts from, `RootClassId` the class of that object where the root template serves several (`ITemplateHierarchyManager.ResolveClassId`: a class the template no longer serves counts as no choice). The hierarchy is administered with the class of its root template (`ClassOf` of `/api/1/templatehierarchies`, `RouteAuthorization` area `templatehierarchy`).
- **Nodes**: `TemplateHierarchyNode` rows form a tree (`ParentNodeId`, `null` below the root), stored depth first (`Order`). `GetNodes` answers them in that order, `SetNodes` replaces them as a whole; a parent that does not come earlier is dropped (the node moves below the root), as is a predecessor that is no sibling. A template referenced by a hierarchy, as root or node, cannot be deleted (`template.delete.hierarchy`); removing a hierarchy removes its nodes and leaves the templates.

|Element of the structure |Node                                      |Created as
|-------------------------|------------------------------------------|------------------------------------------------------------
|A part of the work       |`TemplateId`                              |an object of the referenced template, **in the workspace of its class**
|Its class                |`ClassId`, required where the referenced template serves several classes |the class of the object; otherwise the class the template is bound to
|Its place in the tree    |`ParentNodeId`, `Order`                   |created with the object of the parent node (the root object for a node below the root), siblings in their order
|Sub-object               |`AsChild`                                 |an `AggregatesProgress` relation from the parent object (the parent reports the progress of the whole structure, `kleenestar.relation.md`); additionally `ParentId` when the object stays in the parent's workspace and the parent's class allows it
|Link                     |`RelationTypeKey`, `RelationInverse`     |a relation of that type from the parent object to the created one, or the other way round; offered are the active object relation types without the aggregating ones (`TemplateHierarchyNodeList.LinkTypes`), each in both directions unless it is symmetric
|Who does it              |`AssigneeId`                              |the object's assignee (operator-side accounts only)
|What it waits for        |`PredecessorId` (a sibling)               |a `BlocksCompletion` relation from that sibling's object to this one; the workflow refuses to finish it first
|Classification           |the security level the referenced template chose for the class (`TemplateSecurityLevels`) |else the default security level of the class

A node is a sub-object, a link or both; a node that is neither counts as a sub-object. A sub-object the parent's class does not allow in the same workspace is created as a link only where the node names one, and skipped otherwise. A node referencing an archived template creates nothing, nor do the nodes below it. Every created object starts in the entry state of its workflows.

**Creating from a hierarchy.** The object creation wizard offers every active hierarchy whose root template is active under the class of its root object, after the templates of that class (`ObjectAddFormFragment.CreateHierarchyCard`): the card names the root template and the number of nodes and carries the chip *Hierarchy*; its value is `hierarchy:{hierarchyId}` (`TemplateChoice.HierarchyValue`). `/api/1/objects/form` fills the later steps from the presets of the root template; `/api/1/objects` creates the root object from the root template and then the nodes depth first (`CreateNodes`).

**Both sides consent to a cross-workspace link.** Taking a template of another workspace into a hierarchy requires administering the class its object is created in (`ValidateNodes` of `/api/1/templatehierarchies`; the picker offers templates of other workspaces only where the caller administers one of their classes), because the node changes what is created there. The objects of a structure are then created on the hierarchy's authority, not on the filer's: an HR clerk filing an onboarding does not need write access to the IT desk.

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
|`template` |`Name` (unique among all templates, `/api/1/templates/uniquename?exclude={templateid}` on an edit), `Category` (tag input, several categories; a comma, semicolon or space ends one), `Description` (prose editor; the description the objects start with), `State`.
|`presets`  |Rendered by `POST /api/1/templates/form?part=presets[&id={templateid}]` from the wizard payload (`WWW/Api/_1_/Templates/Form`, built by `WebControl/TemplatePresetForm`).
|`securitylevel`|Rendered by `POST /api/1/templates/form?part=securitylevel[&id={templateid}]`: one tile selection per served class (`SecurityLevelTiles.CreateForTemplate`), named `SecurityLevel_{classId}`, with the tiles *Default of the class*, *Unclassified* and the levels of the class the caller may assign. A class without such levels has no selection; a step without any answers 204 and the wizard skips it.

**Presets step.** The step shows a title input (`Summary`) and the dialog inputs of the object mask (`ObjectFieldSheet.CreateDialogInput`) for the fields of the selected classes, once per field name: a name several classes share appears under the first class that has it, laid out after that class's create form (else its edit form); fields on neither form follow below it. Several contributing classes become one tab per class, with a note that fields of the same name share one preset. Workflow, attachment, picture and password fields take no preset, and no input is required. The inputs are named `Preset_{field name}`, because the template's own attributes (`Name`, `Category`, `State`, ...) travel in the same payload. An input starts from the answer the payload carries for it, else from the stored preset.

**Security level step.** The choices are stored as the preset `SecurityLevelId` (`WebControl/TemplateSecurityLevels`): the level ids, separated by semicolons (a level names its class), and `none@{classId}` for an unclassified object of a class; a class without a choice takes its default level. The object creation wizard preselects the choice for the class of the object (`TemplateSecurityLevels.ForClass`), `/api/1/objects` applies it to a create that names no level where the caller is cleared for it, and the nodes of a hierarchy take the choice of their template for their class.

**Saving** (`/api/1/templates`): the classes come from `Classes` (an API client may send `ClassId`); a create needs at least one, all of them in one workspace (the template's on an edit) and administered by the caller (`MayCreate`, `ValidateClasses`). The first selected class owns a new template; an edit keeps the owning class while it is selected. When the payload carries preset keys, `TemplatePresetForm.Collect` writes the presets: every offered field takes its answer in storage form; a blank answer, an unticked checkbox, an empty editor document and the starting value of a control that always submits one (colour `#000000`, rating `0`, slider `0;100`, range `5`) preset nothing, unless the field carried a stored preset. A stored `Description` or `SecurityLevelId` preset is kept, a preset naming no field of the served classes is dropped. A payload without preset keys leaves the presets unchanged. The fields `SecurityLevel_{classId}` replace the security level choices of the classes they name (`ValidateSecurityLevels`: a level of that class, active, one the caller may assign or the template keeps). A clone copies the classes and the presets of its source.

### Hierarchy wizard (add and edit)

The templates overview (`/templates/{workspacekey}`) shows the templates and, as a second view, the hierarchies of the workspace (`TemplateHierarchyViewTableFragment`, `/api/1/templatehierarchies/{workspacekey}/table`: name, description, root template, number of nodes, categories, state); the headline carries *Add template* and *Add hierarchy*. The quick filter of the overview narrows both views: *Class* keeps the templates serving the class and the hierarchies creating an object of it (root or node), *Active* keeps the active records. Hierarchies are created and edited in a wizard (`WebFragment/TemplateHierarchy/TemplateHierarchyWizardFragmentBase`, sealed `TemplateHierarchyAddFormFragment` on `/templates/{workspacekey}/addhierarchy` and `TemplateHierarchyEditFormFragment` on `/templatehierarchy/{hierarchyid}/edit`; `TemplateHierarchyDeleteFormFragment` on `/templatehierarchy/{hierarchyid}/delete`).

|Step       |Content
|-----------|---------------------------------------------------------------------------------------------------------------------
|`hierarchy`|`Name`, `Category`, `Description`, `State`.
|`root`     |Tile selection `Root`: every active template of the workspace the caller administers, once per class it serves (`TemplateChoice.Value`), so one click chooses the root template and the class of the root object.
|`nodes`    |`Nodes`: the node tree (`WebControl/TemplateHierarchyInput`, client control `kleenestar.core.TemplateHierarchyCtrl` in `Assets/js/templatehierarchy.js`).

**Nodes step.** The root template chosen on the previous step heads the tree. One card per node, indented by its depth: drag handle, position (`1.2`), icon, name, a badge naming the workspace of a template outside the hierarchy's workspace or marking an archived one, a badge naming the connection (*Sub-object*, the link, or *Not connected*), the parent node, and the buttons *Outdent*, *Indent*, *Move up*, *Move down* and *Remove*. A node moves with its subtree (buttons, drag and drop, Alt+Arrow keys on the handle); indenting makes it a child of the sibling above; a removed node hands its children to its parent. Below the head: *Class* (a selection where the referenced template serves several classes, else the class itself), *Connection* (the switch *Sub-object* and the selection of the link), *Assignee* and *Waits for* (the siblings; a sibling waiting for this one, directly or through others, is not offered). The picker below the tree (*Add a template*) offers the active templates of the workspace and those of other workspaces where the caller administers one of their classes, grouped by workspace; it adds the template at the end, below the root, and indenting places it below the node above. A new node is a sub-object; a template serving one class takes that class, one serving several asks for it. The step does not advance while a class is missing or a node is neither a sub-object nor linked. The nodes are submitted through one hidden input `Nodes` as JSON (`WebControl/TemplateHierarchyNodeList`): `[{ id, parent, template, class, child, link, assignee, waitsFor }]` depth first, where `id` is the id of a stored node or a key of a new row, and `link` is a relation type key, with `:inverse` for the direction from the created object to its parent.

**Saving** (`/api/1/templatehierarchies`): `Root` names the root template (`{templateId}` or `{templateId}@{classId}`), required on a create, a template the caller administers, in the hierarchy's workspace on an edit. `ValidateNodes` checks that every row references an existing template, names an earlier parent, names a class the template serves and names one where it serves several, creates in another workspace only where the caller administers the class, is a sub-object or names a linkable relation type, names an active assignee and waits for a sibling without a circle. A row naming a stored node keeps its id. A payload without `Nodes` leaves the nodes unchanged.

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