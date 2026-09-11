using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base for the object Gantt endpoint of a kind's overview tab control. Each
    /// active object of the <see cref="Kind"/> becomes a bar whose span comes from the date
    /// fields of its class (see <see cref="ObjectBoardProjection.ResolvePlan"/>) and whose
    /// progress comes from its workflow status category. A concrete subclass only fixes the
    /// kind it plans (issue, asset, …); each concrete endpoint registers at its own route, so
    /// the base must stay abstract.
    /// </summary>
    /// <remarks>
    /// Objects are grouped the way the model already groups them: an object whose parent is
    /// itself on the plan hangs under that parent, and everything else hangs under a synthetic
    /// container per class — the same grouping the Kanban board draws its swimlanes from.
    /// <para>
    /// Moving or resizing a bar writes the new dates back into the class' date fields. An edge
    /// the class models no field for is refused rather than half-applied (see
    /// <see cref="ObjectPlanWriter"/>), so a plan on a class without dates behaves as
    /// read-only instead of answering 200 to a change it drops. Creating and deleting bars is
    /// refused throughout: an object is raised and retired through the object flow, which
    /// stamps a key, a workflow and an audit trail that a dragged bar cannot.
    /// Dependency links are refused for the same reason — the model has no dependency relation
    /// for them to persist into.
    /// </para>
    /// </remarks>
    public abstract class RestApiObjectKindGantt : RestApiGantt
    {
        /// <summary>
        /// Gets the persisted kind key the plan is scoped to.
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Applies an optional in-memory quickfilter to the plan's objects. The default is a
        /// no-op, so the plan shows every active object of the kind in the workspace.
        /// </summary>
        /// <param name="objects">The objects that would become bars.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered objects.</returns>
        protected virtual IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request) => objects;

        /// <summary>
        /// Returns one container per class that has objects on the plan, followed by one bar
        /// per active object of the kind.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The tasks of the plan.</returns>
        protected override IEnumerable<RestApiGanttTask> RetrieveTasks(IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                yield break;
            }

            var objects = GetActiveObjects(workspace.Id, request);
            var onPlan = objects.Select(x => x.Id).ToHashSet();

            var categories = ObjectBoardProjection.GetOrderedCategories();
            var categoriesById = categories.ToDictionary(x => x.Id, x => x);
            var contextByClass = new Dictionary<Guid, ObjectBoardClassContext>();
            var identityById = new Dictionary<Guid, Identity>();

            // the containers come first so the client has a parent to attach to while it reads
            // the bars in one pass
            foreach (var classId in objects.Select(x => x.ClassId).Distinct())
            {
                var cls = CoreHub.ClassManager.GetClass(classId);

                if (cls is null)
                {
                    continue;
                }

                yield return new RestApiGanttTask
                {
                    Id = ContainerId(classId),
                    Label = cls.Name
                };
            }

            foreach (var entity in objects)
            {
                if (!contextByClass.TryGetValue(entity.ClassId, out var classContext))
                {
                    var cls = CoreHub.ClassManager.GetClass(entity.ClassId);
                    classContext = cls is null ? null : ObjectBoardProjection.BuildClassContext(cls);
                    contextByClass[entity.ClassId] = classContext;
                }

                var (start, end) = ObjectBoardProjection.ResolvePlan(entity, classContext);
                var category = ObjectBoardProjection.ResolveCategory(entity.Id, classContext, categoriesById);
                var assignee = ResolveIdentity(entity.AssigneeId, identityById);

                yield return new RestApiGanttTask
                {
                    Id = entity.Id.ToString(),
                    Label = string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : $"{entity.Key} · {entity.Summary}",
                    Start = FormatDate(start),
                    End = FormatDate(end),

                    // a span of one day is a bar, a span of none is a milestone — the client
                    // reads the zero duration and draws the diamond
                    Duration = (int)(end.Date - start.Date).TotalDays,
                    // an object that aggregates others reports what they have come to rather
                    // than what its own state says: a container is never itself "in progress",
                    // and a bar over a plan is where that difference is read
                    Progress = CoreHub.ObjectProgressManager.GetProgress(entity.Id).Percent,
                    Resources = assignee is null ? null : [assignee.Name],

                    // the model's own hierarchy wins where both ends are on the plan; the class
                    // container is what is left for a root object
                    ParentId = entity.ParentId is Guid parentId && onPlan.Contains(parentId)
                        ? parentId.ToString()
                        : ContainerId(entity.ClassId)
                };
            }
        }

        /// <summary>
        /// Returns the dependency links of the plan, which is always empty: the object model
        /// carries a containment hierarchy (expressed through the task parents) but no
        /// predecessor relation a link could be derived from.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>An empty sequence.</returns>
        protected override IEnumerable<RestApiGanttLink> RetrieveLinks(IRequest request)
        {
            return [];
        }

        /// <summary>
        /// Handles the PUT/PATCH that persists a moved or resized bar (<c>/tasks/{id}</c>).
        /// </summary>
        /// <remarks>
        /// The verb is handled here rather than through the base's <c>UpdateTask</c> hook,
        /// because that hook can only answer "gone": it returns the task or <c>null</c>, and
        /// the base maps <c>null</c> to a 404. A move this endpoint refuses is not a missing
        /// task, it is a conflict with how the class is modelled, and a client that is told
        /// 404 for a bar it can see cannot tell the two apart.
        /// <para>
        /// The routing reuses the base's own segment helpers, so a sub-path reaches the same
        /// place it would have without the override. The <c>[Method]</c> attributes are
        /// re-declared because <c>RestApiManager</c> reads them with <c>inherit: false</c> —
        /// an override without them takes the verb off the endpoint entirely.
        /// </para>
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>
        /// <c>200</c> with the stored task, <c>404</c> when the id names no object of the kind,
        /// <c>409</c> when the move touches an edge the class models no field for, or
        /// <c>400</c> when the payload is malformed.
        /// </returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            var segments = GetRelativeSegments(request);

            if (segments.Count != 2 || !EqualsSegment(segments[0], "tasks"))
            {
                return new ResponseNotFound();
            }

            try
            {
                var task = GetPayload<RestApiGanttTask>(request);

                if (task is null || !Guid.TryParse(segments[1], out var objectId))
                {
                    return new ResponseBadRequest(new StatusMessage("invalid task payload."));
                }

                var entity = CoreHub.ObjectManager.GetObject(objectId);

                if (entity is null || !string.Equals(entity.Kind, Kind, StringComparison.OrdinalIgnoreCase))
                {
                    return new ResponseNotFound(new StatusMessage($"task '{segments[1]}' not found."));
                }

                var cls = CoreHub.ClassManager.GetClass(entity.ClassId);
                var context = cls is null ? null : ObjectBoardProjection.BuildClassContext(cls);

                var applied = ObjectPlanWriter.TryApply
                (
                    entity,
                    context,
                    ObjectPlanWriter.ParseDate(task.Start),
                    ObjectPlanWriter.ParseDate(task.End)
                );

                if (!applied)
                {
                    return ObjectPlanWriter.Conflict(entity, context);
                }

                task.Id = entity.Id.ToString();

                return ToJsonResponse(task);
            }
            catch (Exception ex)
            {
                return RestApiFault.BadRequest(request, ex, "error processing put request.");
            }
        }

        /// <summary>
        /// Refuses to create a bar: an object is raised through the object flow, which stamps
        /// the key, the workflow and the audit trail a dragged bar carries none of.
        /// </summary>
        /// <param name="task">The task payload.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="null"/>, which the base maps to a bad request.</returns>
        protected override RestApiGanttTask CreateTask(RestApiGanttTask task, IRequest request)
        {
            return null;
        }

        /// <summary>
        /// Refuses to delete a bar: retiring an object is a lifecycle transition, not the
        /// removal of a row from a plan.
        /// </summary>
        /// <param name="id">The object id from the sub-path.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="false"/>.</returns>
        protected override bool DeleteTask(string id, IRequest request)
        {
            return false;
        }

        /// <summary>
        /// Refuses to create a dependency link: the model has no relation to persist it into.
        /// </summary>
        /// <param name="link">The link payload.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="null"/>, which the base maps to a bad request.</returns>
        protected override RestApiGanttLink CreateLink(RestApiGanttLink link, IRequest request)
        {
            return null;
        }

        /// <summary>
        /// Refuses to delete a dependency link; see <see cref="CreateLink"/>.
        /// </summary>
        /// <param name="id">The link id from the sub-path.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="false"/>.</returns>
        protected override bool DeleteLink(string id, IRequest request)
        {
            return false;
        }

        /// <summary>
        /// Returns the active objects of the kind in the workspace, ordered by their planned
        /// start so the grid reads chronologically.
        /// </summary>
        /// <param name="workspaceId">The workspace id.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The objects on the plan.</returns>
        private List<Model.Entities.Object> GetActiveObjects(Guid workspaceId, IRequest request)
        {
            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .WhereEquals(x => x.Kind, Kind);

            var objects = CoreHub.ObjectManager.GetObjects(query)
                .Where(x => x.State == WorkspaceState.Active);

            return [.. ApplyQuickfilter(objects, request).OrderBy(x => x.Created).ThenBy(x => x.Key)];
        }

        /// <summary>
        /// Returns the id of the synthetic container a class contributes to the plan. It is
        /// prefixed so it can never collide with an object id, which the update sub-path
        /// parses as a GUID.
        /// </summary>
        /// <param name="classId">The class id.</param>
        /// <returns>The container id.</returns>
        private static string ContainerId(Guid classId)
        {
            return $"class:{classId}";
        }

        /// <summary>
        /// Resolves the workspace addressed by the request route.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        private static Workspace GetWorkspace(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);
        }

        /// <summary>
        /// Resolves an identity through a request-scoped cache.
        /// </summary>
        /// <param name="identityId">The identity id, or <see langword="null"/>.</param>
        /// <param name="identityById">The cache.</param>
        /// <returns>The identity, or <see langword="null"/>.</returns>
        private static Identity ResolveIdentity(Guid? identityId, Dictionary<Guid, Identity> identityById)
        {
            if (identityId is not Guid id)
            {
                return null;
            }

            if (!identityById.TryGetValue(id, out var identity))
            {
                identity = CoreHub.IdentityManager.GetIdentity(id);
                identityById[id] = identity;
            }

            return identity;
        }

        /// <summary>
        /// Formats a date as the ISO day the gantt wire shape exchanges.
        /// </summary>
        /// <param name="value">The date.</param>
        /// <returns>The formatted date.</returns>
        private static string FormatDate(DateTime value)
        {
            return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
