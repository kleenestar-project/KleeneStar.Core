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
        private static readonly Guid ToDoCategoryId = Guid.Parse("B7C8D9E0-4444-4444-8444-444444444441");
        private static readonly Guid WaitingCategoryId = Guid.Parse("B7C8D9E0-4444-4444-8444-444444444442");
        private static readonly Guid DoneCategoryId = Guid.Parse("B7C8D9E0-4444-4444-8444-444444444443");

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
                    PortalVisible = true,
                    Fields =
                    [
                        new WorkspaceTemplateField { Name = "Description", Type = FieldType.Text, Portal = true },
                        new WorkspaceTemplateField { Name = "Status", Type = FieldType.Workflow },
                        new WorkspaceTemplateField { Name = "Priority", Type = FieldType.Priority },
                        new WorkspaceTemplateField { Name = "Urgency", Type = FieldType.Selection, Options = ["High", "Low"], Required = true, Portal = true },
                        new WorkspaceTemplateField { Name = "Resolution", Type = FieldType.Multiline, Tab = WorkspaceTemplateField.DetailsTab, OnCreate = false }
                    ],
                    Priorities =
                    [
                        new WorkspaceTemplatePriority { Name = "P1" },
                        new WorkspaceTemplatePriority { Name = "P2" }
                    ],
                    Workflow = new WorkspaceTemplateWorkflow
                    {
                        Name = "Ticket flow",
                        Statuses =
                        [
                            new WorkspaceTemplateStatus { Name = "Open", Category = WorkspaceTemplateStatus.ToDo },
                            new WorkspaceTemplateStatus { Name = "Waiting", Category = WorkspaceTemplateStatus.Waiting },
                            new WorkspaceTemplateStatus { Name = "Done", Category = WorkspaceTemplateStatus.Done, IsEnd = true }
                        ],
                        Transitions =
                        [
                            new WorkspaceTemplateTransition { Name = "Wait", From = "Open", To = "Waiting" },
                            new WorkspaceTemplateTransition { Name = "Resume", From = "Waiting", To = "Open" },
                            new WorkspaceTemplateTransition { Name = "Close", From = "Open", To = "Done" },
                            // names a state that is not declared, and connects nothing
                            new WorkspaceTemplateTransition { Name = "Vanish", From = "Open", To = "Nowhere" }
                        ]
                    },
                    Calendars =
                    [
                        new WorkspaceTemplateCalendar
                        {
                            Name = "Office",
                            TimeZone = "Europe/Berlin",
                            IsDefault = true,
                            BusinessHours = [new WorkspaceTemplateBusinessHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))],
                            Holidays = [new WorkspaceTemplateHoliday(new DateOnly(2026, 12, 25), "Christmas Day")]
                        }
                    ],
                    Slas =
                    [
                        new WorkspaceTemplateSla
                        {
                            Name = "Standard",
                            Calendar = "Office",
                            PauseOn = ["Waiting"],
                            Targets = [new WorkspaceTemplateSlaTarget("First response", SlaTargetKind.Response, 4, SlaTargetUnit.Hours)],
                            Scope = [new WorkspaceTemplateSlaScope(SlaScopeRuleType.Priority, "P1")]
                        },
                        new WorkspaceTemplateSla
                        {
                            // names a calendar the class does not have, and runs in the default one
                            Name = "Elsewhere",
                            Calendar = "Mars",
                            Targets = [new WorkspaceTemplateSlaTarget("Resolution", SlaTargetKind.Resolution, 2, SlaTargetUnit.BusinessDays)]
                        }
                    ]
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

            // the installation's status categories, which a template's states name by name
            db.StatusCategories.Add(new StatusCategory { Id = ToDoCategoryId, Name = "ToDo", IsDefault = true });
            db.StatusCategories.Add(new StatusCategory { Id = WaitingCategoryId, Name = "Waiting" });
            db.StatusCategories.Add(new StatusCategory { Id = DoneCategoryId, Name = "Done" });

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
        /// A workspace description written in the prose editor is stored as the editor's
        /// document; the home page leads with its words, not with the serialization.
        /// </summary>
        [Fact]
        public void ApplyRendersTheWorkspaceDescription()
        {
            Seed(nameof(ApplyRendersTheWorkspaceDescription));

            using (var db = CoreHubFixture.CreateDbContext(nameof(ApplyRendersTheWorkspaceDescription)))
            {
                db.Workspaces.Single(x => x.Id == WorkspaceId).Description = """
                    {"version":1,"doc":{"type":"doc","id":"n1","children":[{"type":"p","id":"n2","children":[{"type":"text","text":"Where the service desk works.","marks":{}}]}]}}
                    """;
                db.SaveChanges();
            }

            var created = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId, AuthorId);

            Assert.Contains("Where the service desk works.", created.Home.Description);
            Assert.DoesNotContain("\"version\"", created.Home.Description);
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
            Assert.Empty(second.Fields);
            Assert.Empty(second.Priorities);
            Assert.Empty(second.Statuses);
            Assert.Empty(second.Workflows);
            Assert.Empty(second.Forms);
            Assert.Empty(second.Calendars);
            Assert.Empty(second.Slas);
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
        /// Applying a template writes the structure it declares into the class it creates: the
        /// priority scale in order, the states with their categories, the workflow placing them
        /// with a start and an end, and the fields - the status field bound to that workflow.
        /// </summary>
        /// <remarks>
        /// A transition naming a state the template does not declare connects nothing and is
        /// dropped rather than stored with a dangling end.
        /// </remarks>
        [Fact]
        public void ApplyWritesTheStructure()
        {
            Seed(nameof(ApplyWritesTheStructure));

            var result = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);
            var ticket = result.Classes.Single(x => x.Name == "Ticket");

            Assert.Equal(["P1", "P2"], CoreHub.PriorityManager
                .GetPriorities(new Query<Priority>().WhereEquals(x => x.ClassId, ticket.Id))
                .OrderBy(x => x.Order)
                .Select(x => x.Name));

            var statuses = CoreHub.StatusManager
                .GetStatuses(new Query<Status>().WhereEquals(x => x.ClassId, ticket.Id))
                .ToDictionary(x => x.Name);

            Assert.Equal(ToDoCategoryId, statuses["Open"].CategoryId);
            Assert.Equal(WaitingCategoryId, statuses["Waiting"].CategoryId);
            Assert.Equal(DoneCategoryId, statuses["Done"].CategoryId);

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(Assert.Single(result.Workflows).Id);

            Assert.Equal(ticket.Id, workflow.ClassId);
            Assert.Equal(3, workflow.WorkflowStatuses.Count);
            Assert.True(workflow.WorkflowStatuses.Single(x => x.StatusId == statuses["Open"].Id).IsStart);
            Assert.True(workflow.WorkflowStatuses.Single(x => x.StatusId == statuses["Done"].Id).IsEnd);
            Assert.Equal(["Close", "Resume", "Wait"], workflow.Transitions.Select(x => x.Name).Order());

            var fields = CoreHub.FieldManager
                .GetFields(new Query<Field>().WhereEquals(x => x.ClassId, ticket.Id))
                .ToDictionary(x => x.Name);

            Assert.Equal(5, fields.Count);
            Assert.Equal(workflow.Id, fields["Status"].WorkflowId);
            Assert.Null(fields["Urgency"].WorkflowId);
            Assert.True(fields["Urgency"].Required);
            Assert.Equal(["High", "Low"], fields["Urgency"].Options);
        }

        /// <summary>
        /// The create, edit and view forms are derived from the fields - each field on the tab it
        /// names, the create form without what only the team fills in later - and a
        /// portal-visible class gets the self-service form of its portal fields, flagged as the
        /// portal's request template.
        /// </summary>
        [Fact]
        public void ApplyDerivesTheForms()
        {
            Seed(nameof(ApplyDerivesTheForms));

            var result = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);
            var ticket = result.Classes.Single(x => x.Name == "Ticket");

            var fields = CoreHub.FieldManager
                .GetFields(new Query<Field>().WhereEquals(x => x.ClassId, ticket.Id))
                .ToDictionary(x => x.Id, x => x.Name);

            List<(string Tab, string[] Fields)> Layout(FormType type, bool portal = false)
            {
                var form = result.Forms.Single(x => x.ClassId == ticket.Id && x.FormType == type && x.PortalTemplate == portal);

                Assert.True(form.PortalTemplate == portal);
                var structure = CoreHub.FormManager.GetFormWithStructure(form.Id);

                return [.. structure.Tabs
                    .OrderBy(t => t.Position)
                    .Select(t => (t.Name, t.Elements
                        .OfType<FormFieldRefElement>()
                        .OrderBy(e => e.Position)
                        .Select(e => fields[e.FieldId])
                        .ToArray()))];
            }

            // the standard forms the class was born with are filled, not doubled
            Assert.Single(CoreHub.FormManager.GetForms(new Query<Form>().WhereEquals(x => x.ClassId, ticket.Id)), x => x.FormType == FormType.Edit);

            var edit = Layout(FormType.Edit);

            Assert.Equal(2, edit.Count);
            Assert.Equal(WorkspaceTemplateField.GeneralTab, edit[0].Tab);
            Assert.Equal(["Description", "Status", "Priority", "Urgency"], edit[0].Fields);
            Assert.Equal(WorkspaceTemplateField.DetailsTab, edit[1].Tab);
            Assert.Equal(["Resolution"], edit[1].Fields);

            Assert.Equal(edit.SelectMany(x => x.Fields), Layout(FormType.View).SelectMany(x => x.Fields));

            var create = Layout(FormType.Create);

            Assert.DoesNotContain("Resolution", create.SelectMany(x => x.Fields));

            var portal = Assert.Single(Layout(FormType.Default, portal: true));

            Assert.Equal(["Description", "Urgency"], portal.Fields);
        }

        /// <summary>
        /// Calendars and agreements are written with their children, each agreement running in
        /// the calendar it names - or in the class's default calendar when it names one the
        /// class does not have - and pausing in the states it names.
        /// </summary>
        [Fact]
        public void ApplyWritesCalendarsAndAgreements()
        {
            Seed(nameof(ApplyWritesCalendarsAndAgreements));

            var result = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);
            var calendar = CoreHub.CalendarManager.GetCalendar(Assert.Single(result.Calendars).Id);

            Assert.Equal("Office", calendar.Name);
            Assert.True(calendar.IsDefault);

            // every weekday is written, the unworked ones disabled
            Assert.Equal(7, calendar.BusinessHours.Count);
            Assert.Single(calendar.BusinessHours, x => x.Enabled);
            Assert.Single(calendar.Holidays);

            var slas = result.Slas.ToDictionary(x => x.Name, x => CoreHub.SlaManager.GetSla(x.Id));

            Assert.Equal(calendar.Id, slas["Standard"].CalendarId);
            Assert.Equal(calendar.Id, slas["Elsewhere"].CalendarId);
            Assert.Equal("Waiting", slas["Standard"].PauseOn);
            Assert.Single(slas["Standard"].Targets);
            Assert.Equal("P1", Assert.Single(slas["Standard"].Scope).Value);
        }

        /// <summary>
        /// A class that declares no structure gets none - the core adds nothing of its own. The
        /// probe's knowledge base is prose and declares nothing; the prose class the manager
        /// creates for the opening post declares nothing either. The empty standard forms every
        /// class is born with stay empty.
        /// </summary>
        [Fact]
        public void ApplyAddsNoStructureOfItsOwn()
        {
            Seed(nameof(ApplyAddsNoStructureOfItsOwn));

            var result = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);
            var ticket = result.Classes.Single(x => x.Name == "Ticket");

            foreach (var @class in result.Classes.Where(x => x.Id != ticket.Id))
            {
                Assert.Empty(CoreHub.FieldManager.GetFields(new Query<Field>().WhereEquals(x => x.ClassId, @class.Id)));
                Assert.All(CoreHub.FormManager.GetForms(new Query<Form>().WhereEquals(x => x.ClassId, @class.Id)),
                    x => Assert.Empty(CoreHub.FormManager.GetFormWithStructure(x.Id).Tabs));
                Assert.Empty(CoreHub.WorkflowManager.GetWorkflows(new Query<Workflow>().WhereEquals(x => x.ClassId, @class.Id)));
            }
        }

        /// <summary>
        /// A class the workspace already carried keeps its shape: the structure is written only
        /// into classes the application created, never beside what an administrator built.
        /// </summary>
        [Fact]
        public void ApplyLeavesAnExistingClassAlone()
        {
            Seed(nameof(ApplyLeavesAnExistingClassAlone));

            var existing = new ClassEntity
            {
                Id = Guid.NewGuid(),
                Name = "Ticket",
                WorkspaceId = WorkspaceId,
                Kind = ObjectKind.Issue,
                State = ClassState.Active
            };

            CoreHub.ClassManager.Add(existing);

            var result = CoreHub.WorkspaceTemplateManager.Apply("test.probe", WorkspaceId);

            Assert.DoesNotContain(result.Classes, x => x.Name == "Ticket");
            Assert.Empty(result.Fields);
            Assert.Empty(CoreHub.FieldManager.GetFields(new Query<Field>().WhereEquals(x => x.ClassId, existing.Id)));
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
            Assert.Empty(result.Fields);
            Assert.Empty(result.Forms);
            Assert.Empty(result.Workflows);
            Assert.Empty(result.Views);
            Assert.Null(result.Home);
            Assert.Null(result.OpeningPost);
        }
    }
}
