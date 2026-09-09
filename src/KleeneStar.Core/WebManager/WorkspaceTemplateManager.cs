using KleeneStar.Core.WebWorkspaceTemplate;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebLog;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the workspace templates the installed plugins define.
    /// </summary>
    /// <remarks>
    /// The registration follows the framework's <c>FragmentManager</c>: the plugin manager is the
    /// source of truth about what is installed, so this manager subscribes to it rather than
    /// scanning the process, and it keeps its registrations keyed by plugin so a plugin that goes
    /// away takes exactly its own templates with it. Discovery is by interface rather than by
    /// attribute, because a template has nothing to declare beyond being one.
    /// </remarks>
    public sealed class WorkspaceTemplateManager : IWorkspaceTemplateManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// The registrations, keyed by the plugin that defines them. A dictionary per plugin
        /// rather than one flat list, because removal is per plugin and has to be exact.
        /// </summary>
        private readonly ConcurrentDictionary<IPluginContext, List<IWorkspaceTemplateContext>> _dictionary = new();

        /// <summary>
        /// Raised after a template has been registered.
        /// </summary>
        public event EventHandler<IWorkspaceTemplateContext> AddWorkspaceTemplate;

        /// <summary>
        /// Raised after a template has been dropped.
        /// </summary>
        public event EventHandler<IWorkspaceTemplateContext> RemoveWorkspaceTemplate;

        /// <summary>
        /// Gets every registered template, in catalogue order.
        /// </summary>
        public IEnumerable<IWorkspaceTemplateContext> WorkspaceTemplates => Order(_dictionary.Values.SelectMany(x => x));

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private WorkspaceTemplateManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            if (_componentHub?.PluginManager is not null)
            {
                _componentHub.PluginManager.AddPlugin += OnAddPlugin;
                _componentHub.PluginManager.RemovePlugin += OnRemovePlugin;

                // the manager is built while the core's own components are registered, which is
                // before every other plugin has arrived - so the plugins already known are swept
                // here and the rest reach it through the event above. Neither pass alone is
                // enough: the sweep misses what comes later, the event misses what came first.
                foreach (var pluginContext in _componentHub.PluginManager.Plugins)
                {
                    Register(pluginContext);
                }
            }
        }

        /// <summary>
        /// Discovers the templates a plugin defines and registers them.
        /// </summary>
        /// <param name="pluginContext">The plugin to scan.</param>
        private void Register(IPluginContext pluginContext)
        {
            if (pluginContext?.Assembly is null || _dictionary.ContainsKey(pluginContext))
            {
                return;
            }

            var registered = new List<IWorkspaceTemplateContext>();

            foreach (var templateType in GetTemplateTypes(pluginContext))
            {
                var template = Instantiate(templateType);

                if (template is null || string.IsNullOrWhiteSpace(template.Key))
                {
                    continue;
                }

                registered.Add(new WorkspaceTemplateContext
                {
                    PluginContext = pluginContext,
                    TemplateType = templateType,
                    Template = template
                });
            }

            if (registered.Count == 0)
            {
                return;
            }

            _dictionary[pluginContext] = registered;

            Log(pluginContext, registered);

            foreach (var context in registered)
            {
                AddWorkspaceTemplate?.Invoke(this, context);
            }
        }

        /// <summary>
        /// Returns the template types of a plugin.
        /// </summary>
        /// <remarks>
        /// A plugin whose types cannot all be loaded is not a reason to lose the ones that can:
        /// an assembly referencing something absent throws on the whole set, and the templates
        /// that resolved are still valid.
        /// </remarks>
        /// <param name="pluginContext">The plugin to scan.</param>
        /// <returns>The candidate types.</returns>
        private static IEnumerable<Type> GetTemplateTypes(IPluginContext pluginContext)
        {
            Type[] types;

            try
            {
                types = pluginContext.Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                types = [.. ex.Types.Where(x => x is not null)];
            }

            return types
                .Where(x => x.IsClass && !x.IsAbstract && x.IsPublic)
                .Where(x => typeof(IWorkspaceTemplate).IsAssignableFrom(x));
        }

        /// <summary>
        /// Instantiates a template type.
        /// </summary>
        /// <param name="templateType">The type to instantiate.</param>
        /// <returns>The template, or <see langword="null"/> when it could not be created.</returns>
        private IWorkspaceTemplate Instantiate(Type templateType)
        {
            try
            {
                return Activator.CreateInstance(templateType) as IWorkspaceTemplate;
            }
            catch (Exception ex)
            {
                // a template that throws in its constructor is a defect of the plugin that ships
                // it, and the rest of the catalogue must still be offered
                _httpServerContext?.Log?.Warning(templateType.FullName + ": " + ex.Message);

                return null;
            }
        }

        /// <summary>
        /// Drops the registrations of a plugin.
        /// </summary>
        /// <param name="pluginContext">The plugin that was removed.</param>
        private void Remove(IPluginContext pluginContext)
        {
            if (pluginContext is null || !_dictionary.TryRemove(pluginContext, out var removed))
            {
                return;
            }

            foreach (var context in removed)
            {
                RemoveWorkspaceTemplate?.Invoke(this, context);
            }
        }

        /// <summary>
        /// Handles the arrival of a plugin.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The plugin context.</param>
        private void OnAddPlugin(object sender, IPluginContext e)
        {
            Register(e);
        }

        /// <summary>
        /// Handles the removal of a plugin.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The plugin context.</param>
        private void OnRemovePlugin(object sender, IPluginContext e)
        {
            Remove(e);
        }

        /// <summary>
        /// Returns the templates the supplied plugin defines.
        /// </summary>
        /// <param name="pluginContext">The plugin.</param>
        /// <returns>Its templates, in catalogue order.</returns>
        public IEnumerable<IWorkspaceTemplateContext> GetWorkspaceTemplates(IPluginContext pluginContext)
        {
            return pluginContext is not null && _dictionary.TryGetValue(pluginContext, out var templates)
                ? Order(templates)
                : [];
        }

        /// <summary>
        /// Returns the template with the supplied key.
        /// </summary>
        /// <param name="key">The stable key of the template.</param>
        /// <returns>The registration, or <see langword="null"/>.</returns>
        public IWorkspaceTemplateContext GetWorkspaceTemplate(string key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? null
                : WorkspaceTemplates.FirstOrDefault(x => string.Equals(x.Template.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Sets a workspace up from a template: its classes, the starting views of its issue and
        /// asset overviews, its home page and the post announcing it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It is four steps rather than one because an empty workspace is worth almost nothing.
        /// A workspace created from a template used to arrive with its classes and nothing else -
        /// every overview an empty tab strip, no page saying what the place is for, no first
        /// entry in its timeline - and the afternoon the templates exist to save was spent
        /// clicking those together instead.
        /// </para>
        /// <para>
        /// The order is the order of dependence: the classes belong to the workspace, the views
        /// present the objects of a kind, and the two pages are objects and need a class of the
        /// right kind to live in. Each step is skipped where the workspace already carries what
        /// it would create, so applying a template twice - a retried create, or a template
        /// applied to a workspace somebody had already set up by hand - adds what is missing
        /// instead of a second set of everything.
        /// </para>
        /// <para>
        /// A step that finds nothing to work with does nothing and does not stop the ones after
        /// it. A workspace whose classes were created while its home page failed is one an
        /// administrator can finish by hand; a create that failed halfway and rolled the classes
        /// back would leave them with nothing.
        /// </para>
        /// </remarks>
        /// <param name="key">The stable key of the template to apply.</param>
        /// <param name="workspaceId">The workspace to set up.</param>
        /// <param name="identityId">Who is doing this, recorded as the author of the two pages
        /// and of the commits that create them. Empty when it is not known.</param>
        /// <param name="culture">The language the two pages are written in. Null falls back to
        /// the installation's own, which is what a caller with no request behind it has.</param>
        /// <returns>What was created.</returns>
        public WorkspaceTemplateResult Apply(string key, Guid workspaceId, Guid identityId = default, CultureInfo culture = null)
        {
            var template = GetWorkspaceTemplate(key)?.Template;
            var workspace = workspaceId == Guid.Empty ? null : CoreHub.WorkspaceManager.GetWorkspace(workspaceId);

            if (template is null || workspace is null)
            {
                return WorkspaceTemplateResult.Empty;
            }

            var classes = new List<Class>(ApplyClasses(template, workspaceId));
            var views = ApplyViews(workspaceId);

            // the pages describe the workspace as it now stands, so they are written from every
            // class it carries rather than only from the ones this call created - a template
            // applied to a workspace that already had classes would otherwise leave its home
            // page listing half of it
            var all = CoreHub.ClassManager
                .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspaceId))
                .ToList();

            // both prose classes are settled before either page is written, so each page lists
            // the workspace as it finally stands rather than as it stood halfway through
            foreach (var kind in new[] { ObjectKind.Document, ObjectKind.Blog })
            {
                if (all.Any(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var host = CreateProseClass(workspaceId, kind);

                if (host is not null)
                {
                    classes.Add(host);
                    all.Add(host);
                }
            }

            all = [.. all.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)];

            return new WorkspaceTemplateResult
            {
                Classes = classes,
                Views = views,
                Home = ApplyProse(workspace, template, all, ObjectKind.Document, identityId, culture),
                OpeningPost = ApplyProse(workspace, template, all, ObjectKind.Blog, identityId, culture)
            };
        }

        /// <summary>
        /// Creates the classes the template describes.
        /// </summary>
        /// <remarks>
        /// A name the workspace already carries is skipped rather than duplicated. The name is
        /// what is compared, not the descriptor: a class an administrator renamed is a different
        /// class, and one they kept is the same one however it was created.
        /// </remarks>
        /// <param name="template">The template being applied.</param>
        /// <param name="workspaceId">The workspace the classes are created in.</param>
        /// <returns>The classes created.</returns>
        private static IReadOnlyList<Class> ApplyClasses(IWorkspaceTemplate template, Guid workspaceId)
        {
            var existing = CoreHub.ClassManager
                .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspaceId))
                .Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var created = new List<Class>();

            foreach (var descriptor in template.Classes ?? [])
            {
                if (string.IsNullOrWhiteSpace(descriptor.Name) || !existing.Add(descriptor.Name))
                {
                    continue;
                }

                created.Add(CreateClass
                (
                    workspaceId,
                    descriptor.Name,
                    descriptor.Description,
                    descriptor.Icon,
                    descriptor.Kind,
                    descriptor.Renderer,
                    descriptor.PortalVisible,
                    descriptor.Sealed,
                    descriptor.AccessModifier
                ));
            }

            return created;
        }

        /// <summary>
        /// Creates one class in a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace the class belongs to.</param>
        /// <param name="name">The class name.</param>
        /// <param name="description">What the class holds - free text, or an
        /// internationalization key when a template wrote it.</param>
        /// <param name="icon">The path of the icon, or null.</param>
        /// <param name="kind">The kind of object the class holds.</param>
        /// <param name="renderer">The renderer the objects are read and written through, or null
        /// to follow the default of the kind.</param>
        /// <param name="portalVisible">Whether customers may file objects of it.</param>
        /// <param name="sealed">Whether the class may be specialized further.</param>
        /// <param name="accessModifier">Who may see it.</param>
        /// <returns>The class.</returns>
        private static Class CreateClass
        (
            Guid workspaceId,
            string name,
            string description,
            string icon,
            string kind,
            string renderer = null,
            bool portalVisible = false,
            bool @sealed = false,
            AccessModifier accessModifier = AccessModifier.Public
        )
        {
            var @class = new Class
            {
                Name = name,
                Description = description,
                Icon = string.IsNullOrWhiteSpace(icon) ? null : ImageIcon.FromString(icon),
                Kind = kind,
                Renderer = renderer,
                PortalVisible = portalVisible,
                Sealed = @sealed,
                AccessModifier = accessModifier,
                WorkspaceId = workspaceId,
                State = ClassState.Active,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.ClassManager.Add(@class);

            return @class;
        }

        /// <summary>
        /// Creates the standard tabs of the issue and asset overviews.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two overviews keep separate tab sets - the same layout can exist once for issues
        /// and once for assets - and each kind leads with its own curated view, because that is
        /// the default entry of the page hosting the tab control and the tab strip has no
        /// built-in first entry: what a workspace opens on is whichever view is ordered first.
        /// </para>
        /// <para>
        /// It is a <b>starting</b> set, not the full catalogue. The seeded workspaces carry every
        /// layout, and giving a new workspace all of them the same way turned out to be the wrong
        /// default: a table and a list of the same rows, beside a board nobody had asked for, is
        /// six tabs to read before the first item exists. What is created is the curated list to
        /// land on, the dashboard to see the shape of the work, and - for issues - the Scrum
        /// view. Table, list and Kanban are one click away in the tab strip's own template
        /// picker, and are left to whoever decides they want them.
        /// </para>
        /// <para>
        /// Assets get no Scrum view: the asset overview embeds no Scrum template, so the type is
        /// neither offered nor resolvable there.
        /// </para>
        /// </remarks>
        /// <param name="workspaceId">The workspace the tabs belong to.</param>
        /// <returns>The tabs created.</returns>
        private static IReadOnlyList<ObjectView> ApplyViews(Guid workspaceId)
        {
            var created = new List<ObjectView>();

            created.AddRange(ApplyViews(workspaceId, ObjectKind.Issue,
            [
                (ObjectViewType.Issues, "Issues"),
                (ObjectViewType.Dashboard, "Dashboard"),
                (ObjectViewType.ScrumSprint, "Scrum")
            ]));

            created.AddRange(ApplyViews(workspaceId, ObjectKind.Asset,
            [
                (ObjectViewType.Assets, "Assets"),
                (ObjectViewType.Dashboard, "Dashboard")
            ]));

            return created;
        }

        /// <summary>
        /// Creates the tab set of one object kind.
        /// </summary>
        /// <param name="workspaceId">The workspace the tabs belong to.</param>
        /// <param name="kind">The object kind whose overview the tabs belong to.</param>
        /// <param name="views">The tabs, in the order they are offered in.</param>
        /// <returns>The tabs created.</returns>
        private static IReadOnlyList<ObjectView> ApplyViews(Guid workspaceId, string kind, (ObjectViewType Type, string Name)[] views)
        {
            var existing = CoreHub.ObjectViewManager
                .GetViewsForWorkspace(workspaceId, kind)
                .Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var created = new List<ObjectView>();

            for (var order = 0; order < views.Length; order++)
            {
                var (type, name) = views[order];

                if (!existing.Add(name))
                {
                    continue;
                }

                var view = new ObjectView
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = ViewDescription(type, kind),
                    Kind = kind,
                    ViewType = type,
                    Order = order,
                    State = ObjectViewState.Active,
                    WorkspaceId = workspaceId,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                CoreHub.ObjectViewManager.AddObjectView(view);
                created.Add(view);
            }

            return created;
        }

        /// <summary>
        /// Returns what a tab is described as in the view administration.
        /// </summary>
        /// <remarks>
        /// The description is stored, and it is stored untranslated, exactly as the seeder writes
        /// it and as the tab control's own "add view" writes the name: a view is data an
        /// administrator renames and rewrites, not a caption of the product.
        /// </remarks>
        /// <param name="type">The kind of view.</param>
        /// <param name="kind">The object kind the view presents.</param>
        /// <returns>The description, or null for a type this does not create - which is what a
        /// tab added by hand carries too.</returns>
        private static string ViewDescription(ObjectViewType type, string kind)
        {
            var subject = string.Equals(kind, ObjectKind.Asset, StringComparison.OrdinalIgnoreCase)
                ? "assets"
                : "issues";

            return type switch
            {
                ObjectViewType.Issues or ObjectViewType.Assets => $"Most recently updated {subject} with personal filters.",
                ObjectViewType.Dashboard => $"Aggregated dashboard of the {subject}.",
                ObjectViewType.ScrumSprint => "Active Scrum sprint and product backlog.",
                _ => null
            };
        }

        /// <summary>
        /// Writes the page of one prose kind - the home page for documents, the opening post for
        /// blogs - into a class of that kind.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The class it is written into is one the workspace already has, which is one the
        /// template named when it named one - so a template with a knowledge base or an
        /// announcement channel gets its page there rather than beside it - and otherwise the
        /// one the caller created for it. A home page is not an optional extra of a workspace,
        /// and a class holding one page is a smaller surprise than a workspace with nowhere to
        /// write.
        /// </para>
        /// <para>
        /// Nothing is written when the workspace already holds a page of that kind. That is the
        /// idempotency of the whole step, and it is deliberately coarse: what must not happen on
        /// a retried create is a second home page, and any document in the workspace means
        /// somebody - or an earlier run - has already had that thought.
        /// </para>
        /// </remarks>
        /// <param name="workspace">The workspace the page belongs to.</param>
        /// <param name="template">The template being applied.</param>
        /// <param name="classes">Every class the workspace carries.</param>
        /// <param name="kind">The prose kind to write.</param>
        /// <param name="identityId">The author.</param>
        /// <param name="culture">The language the page is written in.</param>
        /// <returns>The page, or <see langword="null"/> when one already existed or the workspace
        /// has no class of the kind to write it into.</returns>
        private static Model.Entities.Object ApplyProse
        (
            Workspace workspace,
            IWorkspaceTemplate template,
            IReadOnlyList<Class> classes,
            string kind,
            Guid identityId,
            CultureInfo culture
        )
        {
            var host = classes.FirstOrDefault(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase));

            if (host is null)
            {
                return null;
            }

            var occupied = CoreHub.ObjectManager
                .GetObjects(new Query<Model.Entities.Object>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                .Any(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase));

            if (occupied)
            {
                return null;
            }

            var document = string.Equals(kind, ObjectKind.Document, StringComparison.OrdinalIgnoreCase);

            var page = new Model.Entities.Object
            {
                Id = Guid.NewGuid(),
                Key = CoreHub.ObjectManager.NextObjectKey(workspace.Id),
                Summary = document
                    ? WorkspaceTemplateContent.HomeSummary(workspace, culture)
                    : WorkspaceTemplateContent.OpeningPostSummary(workspace, culture),
                Description = document
                    ? WorkspaceTemplateContent.HomeBody(workspace, classes, culture)
                    : WorkspaceTemplateContent.OpeningPostBody(workspace, template, classes, culture),
                Icon = host.Icon,
                Kind = kind,
                State = WorkspaceState.Active,
                WorkspaceId = workspace.Id,
                ClassId = host.Id,
                CreatorId = identityId == Guid.Empty ? null : identityId,
                UpdaterId = identityId == Guid.Empty ? null : identityId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.ObjectManager.Add(page);

            // the home page is named as such rather than left to be guessed. The overview falls
            // back to the first root of the page tree, which would happen to be this page today
            // and stop being it the moment somebody adds one whose title sorts earlier
            if (document)
            {
                CoreHub.WorkspaceManager.SetHome(workspace.Id, page.Id);
            }

            return page;
        }

        /// <summary>
        /// Creates the class a prose page needs when the template names none of that kind.
        /// </summary>
        /// <remarks>
        /// The names are untranslated, like every other class name: a class name is data an
        /// administrator renames, not a caption of the product. The descriptions are
        /// internationalization keys, which is what a template writes too.
        /// </remarks>
        /// <param name="workspaceId">The workspace the class belongs to.</param>
        /// <param name="kind">The prose kind the class holds.</param>
        /// <returns>The class, or <see langword="null"/> for a kind that is not a prose kind.</returns>
        private static Class CreateProseClass(Guid workspaceId, string kind)
        {
            if (string.Equals(kind, ObjectKind.Document, StringComparison.OrdinalIgnoreCase))
            {
                return CreateClass
                (
                    workspaceId,
                    "Page",
                    "kleenestar.core:workspace.template.class.page",
                    "/kleenestar/assets/icons/doc.svg",
                    ObjectKind.Document
                );
            }

            if (string.Equals(kind, ObjectKind.Blog, StringComparison.OrdinalIgnoreCase))
            {
                return CreateClass
                (
                    workspaceId,
                    "News",
                    "kleenestar.core:workspace.template.class.news",
                    "/kleenestar/assets/icons/release.svg",
                    ObjectKind.Blog
                );
            }

            return null;
        }

        /// <summary>
        /// Puts a set of registrations into the order they are offered in.
        /// </summary>
        /// <param name="templates">The registrations.</param>
        /// <returns>The ordered registrations.</returns>
        private static IEnumerable<IWorkspaceTemplateContext> Order(IEnumerable<IWorkspaceTemplateContext> templates)
        {
            return [.. templates
                .OrderBy(x => x.Template.Order)
                .ThenBy(x => x.Template.Key, StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Announces what a plugin contributed, the way the framework's managers announce what
        /// they found.
        /// </summary>
        /// <remarks>
        /// Per plugin rather than once over the whole catalogue, because the catalogue is never
        /// complete at one moment: this manager is built while the core registers its components,
        /// and every plugin after that arrives through an event.
        /// </remarks>
        /// <param name="pluginContext">The plugin that contributed.</param>
        /// <param name="templates">Its templates.</param>
        private void Log(IPluginContext pluginContext, IEnumerable<IWorkspaceTemplateContext> templates)
        {
            if (_httpServerContext?.Log is null)
            {
                return;
            }

            using var frame = new LogFrameSimple(_httpServerContext.Log);

            var lines = new List<string> { "Workspace templates of '" + pluginContext.PluginId + "':" };

            lines.AddRange(Order(templates).Select(x => string.Empty.PadRight(2) + x.Template.Key));

            _httpServerContext.Log.Info(string.Join(Environment.NewLine, lines));
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            if (_componentHub?.PluginManager is not null)
            {
                _componentHub.PluginManager.AddPlugin -= OnAddPlugin;
                _componentHub.PluginManager.RemovePlugin -= OnRemovePlugin;
            }

            GC.SuppressFinalize(this);
        }
    }
}
