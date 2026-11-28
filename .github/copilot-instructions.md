# Copilot Instructions — KleeneStar.Core

## Project Role

`KleeneStar.Core` is the **main WebExpress plugin** for the KleeneStar platform. It provides:
- The plugin entry point (`KleeneStarPlugin`)
- All web pages (WWW layer)
- REST API selection endpoints
- UI form fragments
- Permissions and policies
- Business logic managers
- Internationalization (i18n) strings for `en` and `de`

It depends on `KleeneStar.Model` for all data entities and converters.

## Target Framework

- .NET 11
- Class library loaded as a WebExpress plugin

---

## Project Structure

```
KleeneStar.Core/
└── src/
    └── KleeneStar.Core/
        ├── Assets/                   ← Static files (icons, CSS, JS)
        ├── Internationalization/
        │   ├── en                    ← English translations (key=value, no extension)
        │   └── de                    ← German translations (key=value, no extension)
        ├── WebAttribute/             ← Custom WebExpress attributes
        ├── WebControl/               ← Custom UI controls
        ├── WebFragment/              ← Form and view fragments
        ├── WebIcon/                  ← Custom icon definitions
        ├── WebIdentity/              ← Identity provider
        ├── WebImport/                ← Import interfaces and implementations
        ├── WebManager/               ← Business logic managers (interface + implementation)
        ├── WebParameter/             ← URL parameter classes
        ├── WebPermissions/           ← IIdentityPermission implementations
        ├── WebPolicies/              ← IIdentityPolicy implementations
        ├── WebSettingPage/           ← Setting page metadata
        ├── WebUri/                   ← URI path segment variables
        ├── WWW/                      ← Pages and REST API endpoints
        │   ├── Api/_1_/<Entity>/     ← REST API selection endpoints per entity
        │   ├── <Entity>/             ← Single-entity pages (Index, Edit, Clone, ...)
        │   └── <Entities>/           ← Collection pages (Index, Add, ...)
        ├── CoreHub.cs                ← Static hub for managers and URI resolution
        ├── KleeneStarApplication.cs
        └── KleeneStarPlugin.cs       ← Plugin entry point
```

---

## Documentation Guidelines

All documentation must be placed inside the dedicated docs directory to maintain a consistent and discoverable structure across the project. Each document must be written in continuous English prose without the use of bullet points, dash based lists, or similar list formatting. Every section must begin with a short introductory paragraph that explains the purpose and context of the section before presenting any technical details or implementation specific information. Where illustrations or diagrams are required, they must be provided as ASCII based figures embedded directly into the text so that they remain readable in plain text environments and version control systems. This ensures that the documentation remains readable, coherent, and accessible for contributors, while also enforcing a uniform style that aligns with the overall conventions of the KleeneStar platform.

---

## Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Plugin class | `KleeneStarPlugin` | — |
| Application class | `KleeneStarApplication` | — |
| Hub | `CoreHub` | — |
| Fragment | `<Entity><Action>FormFragment` | `ClassAddFormFragment` |
| Page class | `Add`, `Edit`, `Index`, `Clone`, `Delete`, `Avatar` | `WWW/Classes/_workspacekey_/Add.cs` |
| REST API endpoint | Named after the data it returns | `State`, `AccessModifier`, `Index`, `Table`, `Quickfilter`, `Wql`, `UniqueName`, `Tile`, `Dropdown` |
| Permission | `<Entity><Action>Permission` | `ClassReadPermission` |
| Policy | `<Entity><Type>Policy` | `ClassViewPolicy`, `ClassEditPolicy`, `ClassAdminPolicy` |
| Manager interface | `I<Entity>Manager` | `IClassManager` |
| Manager class | `<Entity>Manager` | `ClassManager` (sealed) |
| URI variable | `<Entity>IdUriPathSegmentVariable` or `<Entity>KeyUriPathSegmentVariable` | `ClassIdUriPathSegmentVariable` |
| URL segment folders | lowercase with underscores, wrapped in `_` | `_classid_`, `_workspacekey_` |

---

## Plugin Entry Point

```csharp
[Name("kleenestar.core:plugin.name")]
[Description("kleenestar.core:plugin.description")]
[Icon("/assets/img/kleenestar.svg")]
[Application<KleeneStarApplication>()]
[Dependency("webexpress.webapp")]
public sealed class KleeneStarPlugin : IPlugin
{
    public KleeneStarPlugin()
    {
        WebEx.Favicon = "/assets/img/kleenestar.ico";
    }

    public void Run() { }
}
```

- Class is `sealed`.
- `Run()` is empty unless background work is required.

---

## CoreHub Pattern

`CoreHub` is a `public static class` that provides lazy-initialized manager access and URI resolution:

```csharp
public static IWorkspaceManager WorkspaceManager =>
    _workspaceManager ??= ComponentHub.GetComponentManager<WorkspaceManager>();

public static Uri GetUri<T>() { ... }
```

- Manager backing fields are `private static`.
- All managers are accessed through `CoreHub` in fragments and endpoints.
- `CoreHub.GetUri<T>()` is used in form controls to set `RestUri`.

---

## WWW Page Pattern

Page classes live under `WWW/` and implement `IPage<VisualTreeWebApp>` and `IScope`:

```csharp
[WebIcon<IconPlus>]
[Title("kleenestar.core:class.add.label")]
[Scope<IScopeGeneral>]
public sealed class Add : IPage<VisualTreeWebApp>, IScope
{
    public Add() { }

    public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree) { }
}
```

- `Process()` is always empty — rendering is handled entirely by fragments.
- Classes are always `sealed`.
- `[Title]` always uses an i18n key.

### WWW Folder Conventions

| Folder pattern | Purpose |
|---|---|
| `WWW/<Entities>/` | Collection pages (list, add button) |
| `WWW/<Entity>/<_id_>/` | Single-entity pages scoped to an id/key |
| `WWW/Api/_1_/<Entities>/` | REST API selection endpoints |
| `WWW/Api/_1_/<Entities>/<_id_>/` | REST API entity-scoped endpoints |
| `WWW/Settings/<Domain>/` | Admin/settings pages |

---

## REST API Selection Endpoint Pattern

Selection endpoints supply dropdown/selection options in forms.

```csharp
[Title("Class state")]
public sealed class State : RestApiSelection<Model.Entities.Class>
{
    public State() { }

    protected override IQueryable<RestApiSelectionItem> RetrieveItems(
        IQuery<Model.Entities.Class> query, IQueryContext context, IRequest request)
    {
        var list = new List<RestApiSelectionItem>()
        {
            new()
            {
                Id = ClassState.Active.Id(),
                Text = I18N.Translate(request, ClassState.Active.Text()),
                Color = ClassState.Active.Color()
            },
            new()
            {
                Id = ClassState.Archived.Id(),
                Text = I18N.Translate(request, ClassState.Archived.Text()),
                Color = ClassState.Archived.Color()
            }
        };

        return list.AsQueryable();
    }

    protected override IQuery<Model.Entities.Class> Filter(
        string filter, IQuery<Model.Entities.Class> query, IRequest request)
    {
        if (filter is null || filter == "null") return query;
        return query.WhereContainsIgnoreCase(x => x.Name, filter);
    }
}
```

### Rules

- Always `sealed`.
- Use `using KleeneStar.Model.Entities;` and `using WebExpress.WebCore.Internationalization;` — remove `using System;`.
- Use `XxxState.Value.Id()`, `I18N.Translate(request, XxxState.Value.Text())`, `XxxState.Value.Color()`.
- When the endpoint class name conflicts with an entity type name (e.g., `FieldType` endpoint vs `FieldType` enum), use the fully qualified form: `Model.Entities.FieldType.Text.Id()`.
- `Filter()` returns `query` unchanged for static lists; uses `WhereContainsIgnoreCase(x => x.Name, filter)` when filtering makes sense.
- `[Title]` uses a plain string (not an i18n key).

---

## Fragment Pattern

Fragments are form sections rendered inside pages:

```csharp
[Title("kleenestar.core:class.add.title")]
[Section<SectionContentPreferences>]
[Scope<global::KleeneStar.Core.WWW.Classes._workspacekey_.Add>]
[Cache]
public sealed class ClassAddFormFragment : FragmentControlRestFormAdd
{
    public ControlRestFormItemInputUnique ClassName { get; } = new()
    {
        Name = nameof(Model.Entities.Class.Name),
        Label = "kleenestar.core:class.name.label",
        Placeholder = "kleenestar.core:class.name.placeholder",
        Help = "kleenestar.core:class.name.help",
        Required = true,
        RestUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName>()
    };

    public ControlRestFormItemInputSelection ClassState { get; } = new()
    {
        Name = nameof(Model.Entities.Class.State),
        Label = "kleenestar.core:class.state.label",
        Placeholder = "kleenestar.core:class.state.placeholder",
        Help = "kleenestar.core:class.state.help",
        StickySelection = true,
        RestUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes.State>()
    };

    public ControlFormItemInputCheck ClassSealed { get; } = new()
    {
        Name = nameof(Model.Entities.Class.Sealed),
        Label = "kleenestar.core:class.sealed.label",
        Help = "kleenestar.core:class.sealed.help",
        Layout = TypeLayoutCheck.Switch
    };

    public ClassAddFormFragment(IFragmentContext fragmentContext)
        : base(fragmentContext)
    {
        Add(ClassName);
        Add(ClassState);
        Add(ClassSealed);
        // ...
        Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Index>();
    }
}
```

### Fragment Rules

- Always `sealed`.
- All properties are `public`, read-only (`{ get; }`), initialized inline.
- `Name` always uses `nameof(Model.Entities.<Entity>.<Property>)`.
- `Label`, `Placeholder`, `Help` always use i18n keys.
- Controls are registered via `Add(property)` in the constructor.
- `Uri` is set in the constructor to the corresponding REST `Index` endpoint.
- Fragment naming: `<Entity>AddFormFragment`, `<Entity>EditFormFragment`, `<Entity>CloneFormFragment`, `<Entity>DeleteFormFragment`.

### Form Control Rules

| Control type | Mandatory additional property |
|---|---|
| `ControlFormItemInputCheck` | `Layout = TypeLayoutCheck.Switch` |
| `ControlRestFormItemInputSelection` (for enum/state/modifier) | `StickySelection = true` |
| `ControlRestFormItemInputUnique` | `Required = true`, `RestUri = ...UniqueName endpoint` |
| `ControlFormItemInputText` (description) | `Format = TypeEditTextFormat.Wysiwyg`, `Required = false` |

---

## Permissions and Policies Pattern

```csharp
// Permission
[Name("class_read")]
[Policy<ClassViewPolicy>()]
[Policy<ClassEditPolicy>()]
[Policy<ClassAdminPolicy>()]
public sealed class ClassReadPermission : IIdentityPermission { }

// Policy
[Name("class_view_policy")]
[Permission<ClassReadPermission>()]
public sealed class ClassViewPolicy : IIdentityPolicy { }
```

- Name strings use `snake_case`.
- Policy names are suffixed with `_policy`.
- Permissions list all policies that grant them.
- Policies list the permissions they grant access to.
- Both are `sealed` classes with empty bodies.

---

## Manager Pattern

```csharp
public sealed class ClassManager : IClassManager
{
    private readonly IComponentHub _componentHub;
    private readonly IHttpServerContext _httpServerContext;

    public event EventHandler<Class> ClassAdded;
    public event EventHandler<Class> ClassUpdated;
    public event EventHandler<Class> ClassRemoved;

    public ClassManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
    {
        _componentHub = componentHub;
        _httpServerContext = httpServerContext;
    }
}
```

- Manager classes are `sealed`.
- Private fields are `readonly` with `_camelCase` prefix.
- Three standard events per entity: `Added`, `Updated`, `Removed`.
- Interface `I<Entity>Manager` lives in the same folder.

---

## Internationalization (i18n)

### File Format

Files `Internationalization/en` and `Internationalization/de` use simple `key=value` lines (no file extension):

```
class.state.label=State
class.state.placeholder=Select the state of the class.
class.state.help=The state determines the availability of the class.
```

### Key Naming

```
<entity>.<property>.<type>
```

| `<type>` suffix | Purpose |
|---|---|
| `.label` | Display label |
| `.placeholder` | Input placeholder text |
| `.help` | Help/tooltip text |
| `.title` | Page/dialog title |
| `.header` | Section header |
| `.description` | Descriptive text |
| `.conformation` | Success confirmation message |

### Shared Keys

These keys are reused across all entities:

```
state.active.label
state.archived.label
state.locked.label
state.disabled.label
state.deleted.label

accessmodifier.public.label
accessmodifier.protected.label
accessmodifier.private.label
accessmodifier.internal.label

fieldtype.text.label
fieldtype.number.label
fieldtype.date.label
fieldtype.boolean.label
fieldtype.selection.label
fieldtype.reference.label
fieldtype.workflow.label
fieldtype.attachment.label
fieldtype.user.label
fieldtype.tag.label

fieldcardinality.single.label
fieldcardinality.multiple.label
```

### i18n Key Prefix in Code

All references in `Text()` extension methods and `[Name]` / `[Description]` attributes use the plugin id prefix:

```
kleenestar.core:<key>
```

---

## Commenting Style

All `public` and `protected` members have XML doc comments. Standard patterns:

```csharp
/// <summary>
/// Represents a add form fragment for a class.
/// </summary>
public sealed class ClassAddFormFragment : FragmentControlRestFormAdd
```

```csharp
/// <summary>
/// Gets the input selection control for the state.
/// </summary>
public ControlRestFormItemInputSelection ClassState { get; } = new() { ... };
```

```csharp
/// <summary>
/// Retrieves a queryable collection of index items that match the specified query criteria.
/// </summary>
/// <param name="query">
/// An object containing the query parameters used to filter and select index items. Cannot be null.
/// </param>
/// <param name="context">
/// The context in which the query is executed. Provides additional information or constraints
/// for the retrieval operation. Cannot be null.
/// </param>
/// <param name="request">
/// The request that provides the operational context.
/// </param>
/// <returns>
/// An enumerable collection of selection items that satisfy the query criteria.
/// The collection is empty if no items match.
/// </returns>
protected override IQueryable<RestApiSelectionItem> RetrieveItems(...) { ... }
```

---

## Dependencies

| Package | Role |
|---|---|
| `KleeneStar.Model` | Entities, converters, enums |
| `WebExpress.WebCore` | Plugin system, attributes, i18n, HTTP |
| `WebExpress.WebApp` | REST API base classes, form fragments, sections |
| `WebExpress.WebUI` | UI controls (`ControlFormItemInputCheck`, etc.) |
| `WebExpress.WebIndex` | WQL query support |
