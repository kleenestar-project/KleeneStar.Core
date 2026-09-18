using KleeneStar.Core.WebWorkspaceTemplate;
using KleeneStar.Model.Entities;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;
using ClassEntity = KleeneStar.Model.Entities.Class;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.WorkspaceTemplateManager"/>.
    /// </summary>
    /// <remarks>
    /// Discovery is driven by the framework's plugin manager, which the fixture does not stand
    /// up - the tests therefore exercise what the manager does with a template rather than how it
    /// finds one, which is the half that carries the rules: applying a template creates its
    /// classes, applying it twice does not create them again, and an unknown key creates nothing.
    /// The discovery half is verified against the running host.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestWorkspaceTemplateManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("B7C8D9E0-1111-4111-8111-111111111111");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("B7C8D9E0-2222-4222-8222-222222222222");
        private static readonly Guid AuthorId = Guid.Parse("B7C8D9E0-3333-4333-8333-333333333333");

        /// <summary>
        /// A template with two classes, one of them a document, standing in for the ones a plugin
        /// would ship.
        /// </summary>
        private sealed class ProbeTemplate : IWorkspaceTemplate
        {
            public string Key => "test.probe";

            public string Name => "Probe";

            public string Description => "A probe.";

            public IIcon Icon => ImageIcon.FromString("/kleenestar/assets/icons/sd.svg");

            public string SuggestedKey => "PRB";

            public IEnumerable<string> Categories => ["Support"];

            public int Order => 1;

            public IEnumerable<WorkspaceTemplateClass> Classes =>
            [
                new WorkspaceTemplateClass
                {
                    Name = "Ticket",
                    Description = "Requests as they arrive.",
                    Icon = "/kleenestar/assets/icons/ticket.svg",
                    PortalVisible = true
                },
                new WorkspaceTemplateClass
                {
                    Name = "Knowledge",
                    Description = "The written answer.",
                    Icon = "/kleenestar/assets/icons/knowledge.svg",
                    Kind = ObjectKind.Document
                }
            ];
        }

        /// <summary>
        /// Seeds two workspaces and registers the probe template with the manager.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-tpl", Name = "templated" });
            db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "ws-oth", Name = "other" });
            db.SaveChanges();

            Register(new ProbeTemplate());
        }

        /// <summary>
        /// Puts a template into the manager's registry.
        /// </summary>
        /// <remarks>
        /// The registry is filled from the plugin manager, which the fixture has no instance of,
        /// so the registration is written straight into the private dictionary. That is the seam
        /// the tests need and the one place they may reach through it: everything else they
        /// assert goes through the public surface.
        /// </remarks>
        /// <param name="template">The template to register.</param>
        private static void Register(IWorkspaceTemplate template)
        {
            var manager = CoreHub.WorkspaceTemplateManager;

            var field = manager.GetType().GetField("_dictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("The registry field was renamed.");

            var dictionary = field.GetValue(manager)
                ?? throw new InvalidOperationException("The registry is null.");

            var contextType = typeof(CoreHub).Assembly.GetType("KleeneStar.Core.WebWorkspaceTemplate.WorkspaceTemplateContext")
                ?? throw new InvalidOperationException("The context type was renamed.");

            var context = Activator.CreateInstance(contextType);
            contextType.GetProperty("Template")!.SetValue(context, template);
            contextType.GetProperty("TemplateType")!.SetValue(context, template.GetType());

            var listType = typeof(List<>).MakeGenericType(typeof(IWorkspaceTemplateContext));
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            list.Add(context);

            // the key of the registry is the plugin, and there is none in a unit test - a null
            // key is refused by the concurrent dictionary, so a stand-in plugin context is used
            var pluginContext = new StubPluginContext();

            dictionary.GetType().GetMethod("TryAdd")!.Invoke(dictionary, [pluginContext, list]);
        }

        /// <summary>
        /// The plugin a registered template is filed under in a test.
        /// </summary>
        private sealed class StubPluginContext : IPluginContext
        {
            public IComponentId PluginId => null;
            public string PluginName => "test";
            public string Description => null;
            public string Manufacturer => null;
            public string Copyright => null;
            public string Version => null;
            public string License => null;
            public IRoute Icon => null;
            public Assembly Assembly => typeof(UnitTestWorkspaceTemplateManager).Assembly;
            public IConfiguration Settings => null;
        }

        /// <summary>
        /// A registered template is answered by its key and appears in the catalogue.
        /// </summary>
        [Fact]
        public void RegisteredTemplateIsFound()
        {
            Seed(nameof(RegisteredTemplateIsFound));

            Assert.Contains(CoreHub.WorkspaceTemplateManager.WorkspaceTemplates, x => x.Template.Key == "test.probe");

            var found = CoreHub.WorkspaceTemplateManager.GetWorkspaceTemplate("test.probe");

            Assert.NotNull(found);
            Assert.Equal("PRB", found.Template.SuggestedKey);
        }

        /// <summary>
        /// The lookup is case-insensitive and answers nothing for an unknown key - which is the
        /// ordinary answer for a workspace whose template has since been uninstalled, not an
        /// error.
        /// </summary>
        [Fact]
        public void UnknownTemplateIsNull()
        {
            Seed(nameof(UnknownTemplateIsNull));

            Assert.NotNull(CoreHub.WorkspaceTemplateManager.GetWorkspaceTemplate("TEST.PROBE"));
            Assert.Null(CoreHub.WorkspaceTemplateManager.GetWorkspaceTemplate("test.gone"));
            Assert.Null(CoreHub.WorkspaceTemplateManager.GetWorkspaceTemplate(null));
        }

        /// <summary>
        /// Applying a template creates its classes in the workspace, with the kind and the portal
        /// visibility the template declared.
        /// </summary>
        /// <remarks>
        /// The probe names a document class but no blog class, so the count is its two plus the
        /// one class the opening post needs and would otherwise have nowhere to live.
        /// </remarks>
        [Fact]
        public void ApplyCreatesTheClasses()
        {
            Seed(nameof(ApplyCreatesTheClasses));

            var created = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);

            Assert.Equal(3, created.Classes.Count);

            var classes = CoreHub.ClassManager
                .GetClasses(new Query<ClassEntity>().WhereEquals(x => x.WorkspaceId, WorkspaceId))
                .ToList();

            var ticket = classes.Single(x => x.Name == "Ticket");
            var knowledge = classes.Single(x => x.Name == "Knowledge");

            Assert.True(ticket.PortalVisible);
            Assert.Equal(ObjectKind.Issue, ticket.Kind);
            Assert.Equal(ObjectKind.Document, knowledge.Kind);
            Assert.Equal(ClassState.Active, knowledge.State);
        }

        /// <summary>
        /// A workspace whose template names no class of a prose kind is given the one class that
        /// kind's page needs - and only that one: the document page goes into the class the
        /// template already named rather than beside it.
        /// </summary>
        [Fact]
        public void ApplyAddsOnlyTheMissingProseClass()
        {
            Seed(nameof(ApplyAddsOnlyTheMissingProseClass));

            var created = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);

            Assert.DoesNotContain(created.Classes, x => x.Name == "Page");

            var news = Assert.Single(created.Classes, x => x.Name == "News");

            Assert.Equal(ObjectKind.Blog, news.Kind);
            Assert.Equal(WorkspaceId, news.WorkspaceId);

            Assert.Equal(created.Home.ClassId, created.Classes.Single(x => x.Name == "Knowledge").Id);
            Assert.Equal(created.OpeningPost.ClassId, news.Id);
        }

        /// <summary>
        /// Applying a template lays out both overviews with a starting set, not the whole
        /// catalogue: each kind leads with its own curated list - the tab strip has no built-in
        /// first entry, so the leading view is what the overview opens on - followed by the
        /// dashboard, and for issues the Scrum view.
        /// </summary>
        /// <remarks>
        /// The table, the list and the Kanban board are deliberately absent. They are one click
        /// away in the tab strip's own template picker, and a new workspace opening on six tabs
        /// of the same empty rows is worse than one opening on three.
        /// </remarks>
        [Fact]
        public void ApplyCreatesTheViews()
        {
            Seed(nameof(ApplyCreatesTheViews));

            var created = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);

            Assert.Equal(5, created.Views.Count);

            var issues = CoreHub.ObjectViewManager
                .GetViewsForWorkspace(WorkspaceId, ObjectKind.Issue)
                .OrderBy(x => x.Order)
                .ToList();

            var assets = CoreHub.ObjectViewManager
                .GetViewsForWorkspace(WorkspaceId, ObjectKind.Asset)
                .OrderBy(x => x.Order)
                .ToList();

            Assert.Equal
            (
                [ObjectViewType.Issues, ObjectViewType.Dashboard, ObjectViewType.ScrumSprint],
                issues.Select(x => x.ViewType)
            );

            Assert.Equal
            (
                [ObjectViewType.Assets, ObjectViewType.Dashboard],
                assets.Select(x => x.ViewType)
            );

            Assert.All(issues, x => Assert.Equal(ObjectViewState.Active, x.State));

            // what the user is left to add, rather than what happens to be missing
            Assert.DoesNotContain(issues.Concat(assets), x => x.ViewType is ObjectViewType.Table
                or ObjectViewType.List
                or ObjectViewType.Kanban);

            // the asset overview embeds no scrum template, so the type is not resolvable there
            Assert.DoesNotContain(assets, x => x.ViewType == ObjectViewType.ScrumSprint);
        }

        /// <summary>
        /// Applying a template writes the home page and the post announcing the workspace, both
        /// of them ordinary objects of their kind, keyed like every other object of the
        /// workspace and illustrated with the product's mark.
        /// </summary>
        [Fact]
        public void ApplyWritesTheProsePages()
        {
            Seed(nameof(ApplyWritesTheProsePages));

            var created = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId, AuthorId);

            Assert.NotNull(created.Home);
            Assert.NotNull(created.OpeningPost);

            Assert.Equal(ObjectKind.Document, created.Home.Kind);
            Assert.Equal(ObjectKind.Blog, created.OpeningPost.Kind);

            Assert.StartsWith("ws-tpl-", created.Home.Key);
            Assert.StartsWith("ws-tpl-", created.OpeningPost.Key);
            Assert.NotEqual(created.Home.Key, created.OpeningPost.Key);

            Assert.Equal(AuthorId, created.Home.CreatorId);
            Assert.Equal(AuthorId, created.OpeningPost.CreatorId);

            // the illustration is carried in the page rather than pointing at a file, so a
            // database that is copied to another installation keeps its pictures
            Assert.Contains("data:image/svg+xml;base64,", created.Home.Description);
            Assert.Contains("data:image/svg+xml;base64,", created.OpeningPost.Description);

            // the page says what the workspace holds, so a class name has to appear in it
            Assert.Contains("Ticket", created.Home.Description);

            // the home page is named as such rather than left to the fallback, which would stop
            // pointing at it the moment somebody adds a page whose title sorts earlier
            Assert.True(CoreHub.WorkspaceManager.IsHome(WorkspaceId, created.Home.Id));
            Assert.Equal(created.Home.Id, CoreHub.WorkspaceManager.GetHome(WorkspaceId)?.Id);
        }

        /// <summary>
        /// Applying the same template twice adds what is missing rather than a second set of
        /// everything - a retried create, or a template applied to a workspace somebody had
        /// already set up by hand, must not double any of it.
        /// </summary>
        [Fact]
        public void ApplyTwiceIsIdempotent()
        {
            Seed(nameof(ApplyTwiceIsIdempotent));

            CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);
            var second = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);

            Assert.Empty(second.Classes);
            Assert.Empty(second.Views);
            Assert.Null(second.Home);
            Assert.Null(second.OpeningPost);

            Assert.Equal(3, CoreHub.ClassManager
                .GetClasses(new Query<ClassEntity>().WhereEquals(x => x.WorkspaceId, WorkspaceId))
                .Count());

            Assert.Equal(5, CoreHub.ObjectViewManager
                .GetViewsForWorkspace(WorkspaceId)
                .Count());

            Assert.Equal(2, CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, WorkspaceId))
                .Count());
        }

        /// <summary>
        /// Everything lands in the workspace it was applied to and nowhere else.
        /// </summary>
        [Fact]
        public void ApplyTouchesOnlyItsWorkspace()
        {
            Seed(nameof(ApplyTouchesOnlyItsWorkspace));

            CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);

            Assert.Empty(CoreHub.ClassManager
                .GetClasses(new Query<ClassEntity>().WhereEquals(x => x.WorkspaceId, OtherWorkspaceId)));

            Assert.Empty(CoreHub.ObjectViewManager.GetViewsForWorkspace(OtherWorkspaceId));

            Assert.Empty(CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, OtherWorkspaceId)));
        }

        /// <summary>
        /// An unknown template or an unknown workspace creates nothing and throws nothing: the
        /// create endpoint applies whatever the payload named, and a payload naming an
        /// uninstalled template must still produce a workspace.
        /// </summary>
        [Fact]
        public void ApplyUnknownCreatesNothing()
        {
            Seed(nameof(ApplyUnknownCreatesNothing));

            AssertNothingCreated(CoreHub.WorkspaceTemplateManager.Apply("test.gone", WorkspaceId));
            AssertNothingCreated(CoreHub.WorkspaceTemplateManager.Apply("test.probe", Guid.NewGuid()));
            AssertNothingCreated(CoreHub.WorkspaceTemplateManager.Apply("test.probe", Guid.Empty));
            AssertNothingCreated(CoreHub.WorkspaceTemplateManager.Apply(null, WorkspaceId));

            Assert.Empty(CoreHub.ClassManager
                .GetClasses(new Query<ClassEntity>().WhereEquals(x => x.WorkspaceId, WorkspaceId)));
        }

        /// <summary>
        /// Asserts that an application produced nothing at all.
        /// </summary>
        /// <param name="result">The result to check.</param>
        private static void AssertNothingCreated(WorkspaceTemplateResult result)
        {
            Assert.Empty(result.Classes);
            Assert.Empty(result.Views);
            Assert.Null(result.Home);
            Assert.Null(result.OpeningPost);
        }
    }
}
