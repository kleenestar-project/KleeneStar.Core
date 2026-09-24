using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// What the start page of a class says about it: how it is used, where its objects stand, what
    /// changed last, and whether it is set up completely - read once per render.
    /// </summary>
    /// <remarks>
    /// The page used to repeat the counts the sidebar beside it already shows (fields, forms,
    /// priorities, statuses, workflows) as big numbers, next to a sample chart. What an
    /// administrator opens a class for is different: is it in use, is work piling up in one
    /// state, and is anything missing that makes it awkward to use - a class without a workflow,
    /// fields no form shows, a workspace nobody administered. This type answers those questions;
    /// <see cref="ClassOverviewFragment"/> draws them.
    /// </remarks>
    public sealed class ClassOverview
    {
        /// <summary>
        /// How many weeks the creation history covers.
        /// </summary>
        public const int Weeks = 12;

        /// <summary>
        /// How many objects the "recently changed" list shows.
        /// </summary>
        public const int RecentCount = 8;

        /// <summary>
        /// Gets the number of objects of the class the caller may read.
        /// </summary>
        public int Total { get; private init; }

        /// <summary>
        /// Gets the number of objects not in a done state, or <see langword="null"/> when the
        /// class has no workflow to say so.
        /// </summary>
        public int? Open { get; private init; }

        /// <summary>
        /// Gets the number of objects created in the last 30 days.
        /// </summary>
        public int CreatedRecently { get; private init; }

        /// <summary>
        /// Gets the number of objects changed in the last 7 days.
        /// </summary>
        public int UpdatedRecently { get; private init; }

        /// <summary>
        /// Gets the objects per status category, in board order; empty without a workflow.
        /// </summary>
        public IReadOnlyList<CategoryShare> Categories { get; private init; } = [];

        /// <summary>
        /// Gets the objects created per week, oldest week first, <see cref="Weeks"/> entries.
        /// </summary>
        public IReadOnlyList<int> WeeklyCreated { get; private init; } = [];

        /// <summary>
        /// Gets the objects changed last, newest first.
        /// </summary>
        public IReadOnlyList<RecentObject> Recent { get; private init; } = [];

        /// <summary>
        /// Gets the setup checks, the ones that need attention first.
        /// </summary>
        public IReadOnlyList<SetupCheck> Checks { get; private init; } = [];

        /// <summary>
        /// Reads the overview of a class.
        /// </summary>
        /// <param name="class">The class.</param>
        /// <param name="now">The current time (UTC), injected so the windows are testable.</param>
        /// <returns>The overview.</returns>
        public static ClassOverview Build(Model.Entities.Class @class, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(@class);

            var objects = CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().WhereEquals(x => x.ClassId, @class.Id))
                .ToList();

            var context = ObjectBoardProjection.BuildClassContext(@class);
            var hasWorkflow = context?.WorkflowField is not null;
            var categoriesById = hasWorkflow
                ? CoreHub.StatusManager.GetStatusCategories(new Query<StatusCategory>()).ToDictionary(x => x.Id)
                : new Dictionary<Guid, StatusCategory>();
            var categoryOf = hasWorkflow
                ? objects.ToDictionary(x => x.Id, x => ObjectBoardProjection.ResolveCategory(x.Id, context, categoriesById))
                : [];

            return new ClassOverview
            {
                Total = objects.Count,
                Open = hasWorkflow ? objects.Count(x => !IsDone(categoryOf[x.Id])) : null,
                CreatedRecently = objects.Count(x => x.Created >= now.AddDays(-30)),
                UpdatedRecently = objects.Count(x => x.Updated >= now.AddDays(-7)),
                Categories = hasWorkflow ? Shares(objects, categoryOf) : [],
                WeeklyCreated = Weekly(objects, now),
                Recent = [.. objects
                    .OrderByDescending(x => x.Updated)
                    .Take(RecentCount)
                    .Select(x => new RecentObject(x, hasWorkflow ? categoryOf[x.Id] : null))],
                Checks = [.. Check(@class, context)
                    .OrderBy(x => x.State == SetupCheckState.Warning ? 0 : x.State == SetupCheckState.Info ? 1 : 2)]
            };
        }

        /// <summary>
        /// Counts the objects per status category, in board order, plus the ones without a state.
        /// </summary>
        private static IReadOnlyList<CategoryShare> Shares(List<ObjectEntity> objects, Dictionary<Guid, StatusCategory> categoryOf)
        {
            var shares = ObjectBoardProjection.GetOrderedCategories()
                .Select(c => new CategoryShare(
                    ObjectBoardProjection.CategoryLabel(c),
                    ObjectBoardProjection.CategoryColorCss(c),
                    objects.Count(x => categoryOf[x.Id]?.Id == c.Id)))
                .Where(x => x.Count > 0)
                .ToList();

            var none = objects.Count(x => categoryOf[x.Id] is null);

            if (none > 0)
            {
                shares.Add(new CategoryShare(null, "wx-color-secondary", none));
            }

            return shares;
        }

        /// <summary>
        /// Counts the objects created per week over the last <see cref="Weeks"/> weeks.
        /// </summary>
        private static IReadOnlyList<int> Weekly(List<ObjectEntity> objects, DateTime now)
        {
            // weeks start on monday, the way a working week is counted here
            var thisWeek = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            var first = thisWeek.AddDays(-7 * (Weeks - 1));

            return [.. Enumerable.Range(0, Weeks)
                .Select(i => first.AddDays(7 * i))
                .Select(start => objects.Count(x => x.Created >= start && x.Created < start.AddDays(7)))];
        }

        /// <summary>
        /// Determines whether a status category means the work is done.
        /// </summary>
        private static bool IsDone(StatusCategory category)
        {
            return ObjectBoardProjection.Normalize(category?.Name) == "done";
        }

        /// <summary>
        /// Runs the setup checks that apply to the kind and renderer of the class.
        /// </summary>
        private static IEnumerable<SetupCheck> Check(Model.Entities.Class @class, ObjectBoardClassContext context)
        {
            var classId = new ClassIdParameter(@class.Id);
            var kind = ObjectKind.Normalize(@class.Kind);
            var tracksWork = kind is ObjectKind.Issue or ObjectKind.Asset;
            var structured = !ObjectRendererCatalog.IsRenderedAs(@class, ObjectRenderer.Prose);
            var serviceLevels = ObjectKindCatalog.GetKind(@class.Kind)?.ServiceLevels ?? true;

            // the workflow is what gives an object a state; a work item without one cannot move
            var workflows = CoreHub.WorkflowManager.GetWorkflows(classId).ToList();

            if (workflows.Count == 0)
            {
                if (tracksWork)
                {
                    yield return new SetupCheck(SetupCheckState.Warning, "workflow.none", [],
                        Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Workflows._classid_.Index>()?.BindParameters(classId)));
                }
            }
            else
            {
                var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(workflows[0].Id) ?? workflows[0];

                yield return new SetupCheck(SetupCheckState.Ok, "workflow.ok",
                    [workflow.Name, workflow.WorkflowStatuses?.Count ?? workflow.Statuses?.Count ?? 0, workflow.Transitions?.Count ?? 0],
                    Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Workflows._classid_.Index>()?.BindParameters(classId)));
            }

            // a field only reaches the reader through a form; a prose class has neither
            if (structured)
            {
                var fields = CoreHub.FieldManager.GetFields(classId).Where(x => x.State == FieldState.Active).ToList();
                var forms = CoreHub.FormManager.GetForms(classId)
                    .Select(x => CoreHub.FormManager.GetFormWithStructure(x.Id) ?? x)
                    .ToList();
                var placed = forms.SelectMany(PlacedFields).ToHashSet();
                var fieldsUri = Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Fields._classid_.Index>()?.BindParameters(classId));
                var formsUri = Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Forms._classid_.Index>()?.BindParameters(classId));

                if (fields.Count == 0)
                {
                    yield return new SetupCheck(SetupCheckState.Warning, "fields.none", [], fieldsUri);
                }
                else
                {
                    var unplaced = fields.Count(x => !placed.Contains(x.Id));

                    yield return unplaced > 0
                        ? new SetupCheck(SetupCheckState.Warning, "fields.unplaced", [unplaced, fields.Count], formsUri)
                        : new SetupCheck(SetupCheckState.Ok, "fields.ok", [fields.Count], formsUri);
                }

                var create = forms.FirstOrDefault(x => x.FormType == FormType.Create);

                if (create is not null && !PlacedFields(create).Any())
                {
                    yield return new SetupCheck(SetupCheckState.Warning, "createform.empty", [],
                        Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Form._formid_.Index>()?.BindParameters(new FormIdParameter(create.Id))));
                }
            }

            if (kind == ObjectKind.Issue)
            {
                var priorities = CoreHub.PriorityManager.GetPriorities(classId).Count();
                var prioritiesUri = Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Priorities._classid_.Index>()?.BindParameters(classId));

                yield return priorities == 0
                    ? new SetupCheck(SetupCheckState.Info, "priorities.none", [], prioritiesUri)
                    : new SetupCheck(SetupCheckState.Ok, "priorities.ok", [priorities], prioritiesUri);
            }

            if (serviceLevels && tracksWork)
            {
                var slas = CoreHub.SlaManager.GetSlas(@class.Id).Count(x => x.State == SlaPolicyState.Active);
                var slasUri = Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Slas._classid_.Index>()?.BindParameters(classId));

                yield return slas == 0
                    ? new SetupCheck(SetupCheckState.Info, "sla.none", [], slasUri)
                    : new SetupCheck(SetupCheckState.Ok, "sla.ok", [slas], slasUri);
            }

            // the create wizard offers a class's templates as the starting points of an object
            var templates = CoreHub.TemplateManager
                .GetTemplates(new Query<Model.Entities.Template>().WhereEquals(x => x.ClassId, @class.Id))
                .Count(x => x.State == TemplateState.Active);
            var workspaceKey = CoreHub.WorkspaceManager.GetWorkspace(@class.WorkspaceId)?.Key;
            var templatesUri = Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>()?.BindParameters(new WorkspaceKeyParameter(workspaceKey)));

            yield return templates == 0
                ? new SetupCheck(SetupCheckState.Info, "templates.none", [], templatesUri)
                : new SetupCheck(SetupCheckState.Ok, "templates.ok", [templates], templatesUri);

            var levels = CoreHub.SecurityLevelManager.GetSecurityLevels(classId).ToList();

            if (levels.Count > 0)
            {
                var fallback = CoreHub.SecurityLevelManager.GetDefaultSecurityLevel(@class.Id);
                var levelsUri = Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.SecurityLevels._classid_.Index>()?.BindParameters(classId));

                yield return fallback is null
                    ? new SetupCheck(SetupCheckState.Info, "securitylevels.nodefault", [levels.Count], levelsUri)
                    : new SetupCheck(SetupCheckState.Ok, "securitylevels.ok", [levels.Count, fallback.Name], levelsUri);
            }

            // grants on the class or its workspace; none means the content is open to everybody
            var grants = CoreHub.PermissionManager.GetAssignments(PermissionScope.Class, @class.Id.ToString()).Count()
                + CoreHub.PermissionManager.GetAssignments(PermissionScope.Workspace, @class.WorkspaceId.ToString()).Count();
            var permissionsUri = Uri(() => CoreHub.GetUri<global::KleeneStar.Core.WWW.Class._classid_.Permission>()?.BindParameters(classId));

            yield return grants == 0
                ? new SetupCheck(SetupCheckState.Warning, "permissions.none", [], permissionsUri, Modal: true)
                : new SetupCheck(SetupCheckState.Ok, "permissions.ok", [grants], permissionsUri, Modal: true);

            if (@class.PortalVisible)
            {
                yield return new SetupCheck(SetupCheckState.Ok, "portal", [], null);
            }
        }

        /// <summary>
        /// Returns the fields a form places, at any depth of its tabs and groups.
        /// </summary>
        private static IEnumerable<Guid> PlacedFields(Model.Entities.Form form)
        {
            static IEnumerable<FormElement> Flatten(IEnumerable<FormElement> elements) =>
                (elements ?? []).SelectMany(x => new[] { x }.Concat(Flatten(x.Children)));

            return Flatten((form?.Tabs ?? []).SelectMany(x => x.Elements ?? []))
                .OfType<FormFieldRefElement>()
                .Select(x => x.FieldId);
        }

        /// <summary>
        /// Resolves an address, answering <see langword="null"/> where no sitemap is wired (a
        /// test fixture) rather than failing the whole overview.
        /// </summary>
        private static IUri Uri(Func<IUri> resolve)
        {
            try
            {
                return resolve();
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The objects of one status category.
    /// </summary>
    /// <param name="Label">The category label, or <see langword="null"/> for objects without a state.</param>
    /// <param name="ColorCss">The color class the boards use for the category.</param>
    /// <param name="Count">The number of objects.</param>
    public sealed record CategoryShare(string Label, string ColorCss, int Count);

    /// <summary>
    /// An object in the "recently changed" list.
    /// </summary>
    /// <param name="Object">The object.</param>
    /// <param name="Category">Its status category, or <see langword="null"/>.</param>
    public sealed record RecentObject(ObjectEntity Object, StatusCategory Category);

    /// <summary>
    /// The verdict of a setup check.
    /// </summary>
    public enum SetupCheckState
    {
        /// <summary>Set up.</summary>
        Ok,

        /// <summary>Missing in a way that gets in the way of using the class.</summary>
        Warning,

        /// <summary>Worth knowing, not necessarily to be changed.</summary>
        Info
    }

    /// <summary>
    /// One line of the setup check list.
    /// </summary>
    /// <param name="State">The verdict.</param>
    /// <param name="Key">The text key, relative to <c>kleenestar.core:class.overview.check.</c>.</param>
    /// <param name="Args">The values the text names.</param>
    /// <param name="Uri">Where it is set up, or <see langword="null"/>.</param>
    /// <param name="Modal">Whether that address is a dialog.</param>
    public sealed record SetupCheck(SetupCheckState State, string Key, object[] Args, IUri Uri, bool Modal = false);
}
