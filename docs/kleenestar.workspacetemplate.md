![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Workspace Template Concept

An empty workspace is one click away and worth almost nothing. What takes an afternoon is the decision underneath it: that a service desk needs a ticket, an incident, a service request, a problem, a change, a knowledge base and an announcement channel — and that three of those are the ones customers file. A workspace is not a name and a key. It is a set of classes, and choosing them one at a time afterwards is exactly the work somebody has already done elsewhere.

**KleeneStar** therefore lets a workspace be created from a **template**: a description of what a workspace is for and which classes it starts with. Templates are not rows in a table. They are **classes in a plugin**, discovered at runtime, and that is the whole point of the design.

Workspace templates bundle:
- A description of one shape of workspace — what it is for, what it is called, the key it proposes, the categories it belongs under.
- The classes a workspace created from it starts with, each with its object kind, its portal visibility and its access.
- Discovery from the installed plugins, so the catalogue is exactly as long as the installation says.
- A creation wizard that asks for the shape first and the paperwork second.

## Why Code, not Data

A template could have been an entity: a row an administrator edits, with child rows for its classes. It is not, and the reason is what a template *is*.

A template is knowledge about a domain, written down once and true for every installation that shares that domain. Kept as data it would have to be seeded into every new database, migrated whenever its shape changed, and held in step with each deployment by hand — and the first administrator to edit a seeded row would leave the installation permanently unable to take the improved version. Kept as code it is versioned, installable and removable by the same mechanism as everything else a plugin brings, and an installation that wants its own shapes ships its own plugin instead of editing somebody else's rows.

It follows that templates are **read-only at runtime**. What an administrator changes is the workspace a template produced, never the template. The two part company at the moment of creation and never meet again: nothing links a workspace back to the template it came from, because a workspace that drifted from its template is not wrong, it is finished.

|                    | Template as data                                  | Template as code
|--------------------|---------------------------------------------------|--------------------------------------------
| Lives in           | the installation's database                       | a plugin assembly
| Arrives by         | seeding                                           | installing the plugin
| Changes by         | editing a row                                     | shipping a version
| Removed by         | deleting rows, everywhere, by hand                | uninstalling the plugin
| Editable in the UI | yes — and then unable to take an update           | no
| Own shapes by      | editing the shipped rows                          | shipping a plugin beside them

## The Manager

`WorkspaceTemplateManager` is modelled on the framework's `FragmentManager`, and works the same way:

- it subscribes to the **plugin manager**, which is the source of truth about what is installed, rather than scanning the process;
- it scans the assembly of every plugin for public, non-abstract types implementing `IWorkspaceTemplate`, and instantiates them by reflection;
- it keeps its registrations **keyed by plugin**, so a plugin that goes away takes exactly its own templates with it;
- it announces what each plugin contributed to the log, and raises an event per registration and removal.

Two things differ from the fragment manager, and both follow from what a template is.

**Discovery is by interface, not by attribute.** A fragment declares a section, a scope, conditions and policies — it needs attributes because it has things to say about *where* it belongs. A template has nothing to declare beyond being one.

**A template is not bound to an application.** A fragment is a piece of one particular page and means nothing outside the application that page belongs to. A workspace is the installation's, not an application's, so the registry has one dimension where the fragment manager has two.

There is one ordering subtlety worth knowing about. The manager is constructed while the **core's** components are registered, which is before every other plugin has arrived. Neither discovery pass alone is therefore enough: the constructor sweeps the plugins already known, and the `AddPlugin` event catches the rest. A manager written with only one of the two would work in exactly half the installations.

## Applying a Template

Setting the workspace up is the manager's job (`Apply`) rather than the caller's, because it is the same act wherever it is triggered from — the wizard today, an import or a scripted setup tomorrow.

It is **four** steps, not one. A workspace that arrived with its classes and nothing else still had to be assembled by hand — both overviews an empty tab strip, no page saying what the place is for, an empty timeline — which is the afternoon the templates exist to save in the first place. So applying a template produces:

| Step | What is created                                                                 | Skipped when
|------|---------------------------------------------------------------------------------|--------------------------------------
| 1    | the **classes** the template names                                               | the workspace already carries the name
| 2    | the starting **views** of the issue and asset overviews                          | the kind already carries a tab of that name
| 3    | the **home page**, a document                                                    | the workspace already holds a document
| 4    | the **opening post**, a blog entry                                               | the workspace already holds a post

The order is the order of dependence. Everything is created **after** the workspace, because it all belongs to it: there is nothing to attach any of it to until it exists. A workspace stored while its setup failed is one an administrator can finish by hand; the other order would leave classes belonging to nothing. For the same reason a step that fails does not stop the ones after it.

The two overviews keep separate tab sets, so the same layout exists once for issues and once for assets:

| Overview | Views created                           | Left to the user
|----------|-----------------------------------------|--------------------------------
| Issues   | curated list, dashboard, Scrum          | table, list, Kanban, Gantt, scheduler
| Assets   | curated list, dashboard                 | table, list, Kanban

Each leads with its own curated list, because the tab strip has **no built-in first entry** — everything in it comes from the persisted views, so whichever view is ordered first is what the overview opens on.

It is a *starting* set, not the catalogue. The seeded workspaces carry every layout, and handing a new workspace all of them turned out to be the wrong default: a table and a list of the same rows, beside a board nobody asked for, is six tabs to read before the first item exists. What is created is somewhere to land, the shape of the work, and — for issues — the Scrum view. Everything else is one click away in the tab strip's own template picker, and is left to whoever decides they want it. Assets get no Scrum view at all: the asset overview embeds no Scrum template, so the type is neither offered nor resolvable there.

The two pages need a class of their kind to live in. It is one the workspace already has when the template names one — a knowledge base gets the home page, an announcement channel gets the post — and otherwise the workspace is given the one class it is missing (`Page` for documents, `News` for posts). A home page is not an optional extra of a workspace, and a class holding one page is a smaller surprise than a workspace with nowhere to write.

The home page is **named as such** (`Workspace.HomeId`) rather than left to be guessed. Without a choice the document overview falls back to the first root of the page tree by summary, which would happen to be this page today and stop being it the moment somebody adds one whose title sorts earlier. Anybody can point it somewhere else later from the document overview's own more menu — *choose home page* — and picking the automatic entry returns to the fallback.

What the pages say is written by `WorkspaceTemplateContent`, and it is written **once, at creation**, in the language of whoever created the workspace rather than the installation's default: the person filling in the wizard is reading it in one particular language, and an author is going to rewrite the page anyway. A page that silently changed language under an author who had edited it would be worse than one written in the wrong one. Both carry a banner drawn from the product's own mark in the workspace's accent colour, inline as a `data:` SVG so it needs no route, no file and no cleanup, and survives a database copied to another installation.

The class descriptions are written the same way. A template names them as i18n keys - a template is code, and code speaks every language of the installation - but a class is data: its description is read in the class table, edited in the class dialog and shown on every surface that lists classes, and none of those translates a stored string. Stored as a key, the description stayed `kleenestar.templates:template.servicedesk.class.ticket` everywhere but in the opening post, which happened to pass it through the translator. `Apply` therefore resolves each key once, in the creator's language, and the class carries a sentence (`WorkspaceTemplateManager.Describe`). A description that is not a key comes back unchanged, so a template may name free text as well.

Applying a template twice adds what is missing rather than a second set of everything, per the table above — so a retried create, or a template applied to a workspace somebody had already set up by hand, does the useful thing instead of the destructive one.

An unknown template key creates nothing and raises nothing. That is the ordinary answer for a workspace whose template has since been uninstalled, and for every caller of the REST API that is not the wizard. A workspace created **without** a template gets none of this either: the empty-workspace card means an empty workspace.

## The Creation Wizard

The "new workspace" dialog is a two-step wizard, in the same shape as the object wizard:

1. **Template** — the templates as cards, each stating what the workspace is for and which classes it starts with, searchable by name and description. The card projects the template's suggested key into the next step, which is the one field nobody has an opinion about until they have had to invent one.
2. **Details** — the name, the key, the categories, the description, who may see it.

The steps are in that order because the first is the decision and the second is paperwork.

The step always carries an **empty workspace** card besides whatever the plugins offer, and that card is visible whatever the search says. A workspace set up by hand has to stay one click away, and on an installation with no template plugin it is the only way through.

### The required fields are actually required

The name and the key are marked required, and for a long time nothing checked either of them: a workspace could be created with no name at all, and it was then a row no list could address. Three things were wrong at once, and all three are fixed:

- **The form did not ask.** `ControlDataFormItemInputUnique` takes a `Required` resolver and the form renderer paints the asterisk from it, but the resolver never reached the DOM — the control rendered a bare host div and its client controller built the real `<input>` afterwards with neither `required` nor the framework's own `data-wx-required`, so the validator passed an empty value. **Fixed in WebExpress**: the control now emits `data-required` on the host and `webexpress.webapp.input.unique.js` carries it onto the input as `data-wx-required`, the way the tile control always did. KleeneStar carried a page-wide script for this between 2026-09-04 and the framework fix; it is gone.
- **The endpoint did not check.** `RestApiCrud` reads the entity's `Validate…` attributes, and it reads them per field the payload carries — a field the payload omits is not checked, which is right for an update and wrong for a create. `Workspace.Name` carried no attribute at all. It has `[ValidateRequired]` now, and `/api/1/workspaces` demands both fields on a create whether the payload mentions them or not, and refuses a key or a name another workspace already holds.
- **The advice contradicted the product.** `/api/1/workspaces/uniquekey` matched lower case only, so it reported the wizard's own suggested key — `SD`, `DEV` — as unavailable. It now matches the shape the create endpoint enforces.

## Where the Pieces Live

| Concern                         | Where
|---------------------------------|--------------------------------------------------
| What a template is              | `IWorkspaceTemplate`, `WorkspaceTemplateClass` (`KleeneStar.Core/WebWorkspaceTemplate`)
| One registration                | `IWorkspaceTemplateContext`
| Discovery and application       | `IWorkspaceTemplateManager` / `WorkspaceTemplateManager`
| What an application produced    | `WorkspaceTemplateResult`
| What the two pages say          | `WorkspaceTemplateContent`
| The wizard                      | `WorkspaceAddFormFragment`
| Applying on create              | `/api/1/workspaces` — `ApplyTemplate` in its `Create`
| Refusing a nameless workspace   | `/api/1/workspaces` — `Validate`
| The templates shipped           | the `KleeneStar.Templates` plugin

## Writing a Template

Implement the interface in a public, non-abstract class with a parameterless constructor. Nothing is registered; the manager finds it in the plugin's assembly:

```csharp
public sealed class ResearchTemplate : IWorkspaceTemplate
{
    public string Key => "acme.templates.research";
    public string Name => "acme.templates:template.research.name";
    public string Description => "acme.templates:template.research.description";
    public IIcon Icon => ImageIcon.FromString("/kleenestar/assets/icons/task.svg");
    public string SuggestedKey => "RES";
    public IEnumerable<string> Categories => ["Engineering"];
    public int Order => 100;

    public IEnumerable<WorkspaceTemplateClass> Classes =>
    [
        new() { Name = "Study",   Description = "…", Icon = "…", Kind = ObjectKind.Issue },
        new() { Name = "Dataset", Description = "…", Icon = "…", Kind = ObjectKind.Asset }
    ];
}
```

The key is stable and outlives renames of the class; the name and description are i18n keys, and so are the class descriptions - resolved once when the workspace is created, see above. Class names are **not** translated, because a class name is data an administrator renames rather than a caption of the product.

A plugin has to declare an application — the framework refuses one that belongs to nothing — so a template-only plugin names the application it extends rather than defining one of its own.

## Related Concepts

- [Workspaces](kleenestar.workspace.md) — what a template creates.
- [Classes](kleenestar.class.md) — what a template fills it with.
