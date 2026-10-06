using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
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
    /// </para>
    /// <para>
    /// <b>A dependency is an object relation.</b> The links of the plan are the relations
    /// between two of its bars whose type carries <see cref="RelationEffect.BlocksCompletion"/>,
    /// drawn from the blocking source to the blocked target - the relation the workflow guard
    /// already enforces, so what the plan shows is what a transition will refuse. Drawing a link
    /// stores such a relation (the first active blocking type that accepts the pair), changing
    /// its type records the Gantt type in the relation's metadata
    /// (<see cref="LinkTypeMetadata"/>, finish-to-start when absent), and deleting it removes the
    /// relation. The ends of a relation never move (<c>ModelHub.Update</c> does not write them),
    /// so an edit that re-points a link is refused.
    /// </para>
    /// <para>
    /// The plan counts in working days when every class on it is timed by the same calendar
    /// (<see cref="ObjectPlanCalendar"/>), otherwise in calendar days.
    /// </para>
    /// </remarks>
    public abstract class RestApiObjectKindGantt : RestApiGantt
    {
        /// <summary>
        /// The relation metadata key holding the Gantt type of a dependency (FS, SS, FF, SF).
        /// </summary>
        public const string LinkTypeMetadata = "gantt.type";

        /// <summary>
        /// The upper bound of objects a cycle check visits before it gives up and refuses.
        /// </summary>
        private const int CycleBudget = 5000;

        /// <summary>
        /// The objects of the plan, read once per request: the tasks, the links and the
        /// calendar are three hooks of the same retrieve and must describe the same set.
        /// </summary>
        private static readonly ConditionalWeakTable<IRequest, List<Model.Entities.Object>> _plans = [];

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
        /// Returns the query over the objects the view is made of. The default is the objects of
        /// the <see cref="Kind"/> in the workspace the route names; an insight's view overrides
        /// it with the objects its query selects.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The query, or <see langword="null"/> when the route names nothing to show.</returns>
        protected virtual IQuery<Model.Entities.Object> ResolveScope(IRequest request)
        {
            var workspace = GetWorkspace(request);

            return workspace is null
                ? null
                : new Query<Model.Entities.Object>()
                    .WhereEquals(x => x.WorkspaceId, workspace.Id)
                    .WhereEquals(x => x.Kind, Kind);
        }

        /// <summary>
        /// Determines whether an object an edit addresses belongs to the view. The default
        /// accepts the objects of the <see cref="Kind"/>.
        /// </summary>
        /// <param name="entity">The object, already read through the object manager.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns><see langword="true"/> when the object is one the view shows.</returns>
        protected virtual bool InScope(Model.Entities.Object entity, IRequest request)
        {
            return string.Equals(entity.Kind, Kind, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns one container per class that has objects on the plan, followed by one bar
        /// per active object of the kind.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The tasks of the plan.</returns>
        protected override IEnumerable<RestApiGanttTask> RetrieveTasks(IRequest request)
        {
            var objects = GetPlan(request);

            if (objects is null)
            {
                yield break;
            }

            var onPlan = objects.Select(x => x.Id).ToHashSet();

            var categories = ObjectBoardProjection.GetOrderedCategories();
            var categoriesById = categories.ToDictionary(x => x.Id, x => x);
            var contextByClass = new Dictionary<Guid, ObjectBoardClassContext>();
            var identityById = new Dictionary<Guid, Identity>();

            // the containers come first so the client has a parent to attach to while it reads
            // the bars in one pass; their span and duration are rolled up by the client
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

                    // a span of none is a milestone and says so; any other duration is left to
                    // the client, which counts it from the dates - in working days when the
                    // plan carries a calendar, which a count of calendar days sent from here
                    // would contradict
                    Duration = start.Date == end.Date ? 0 : null,
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
        /// Returns the dependencies among the bars: every blocking relation whose two ends are
        /// both on the plan.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The links of the plan.</returns>
        protected override IEnumerable<RestApiGanttLink> RetrieveLinks(IRequest request)
        {
            var objects = GetPlan(request);

            if (objects is null || objects.Count == 0)
            {
                return [];
            }

            return CoreHub.ObjectRelationManager
                .GetRelationsAmong(objects.Select(x => x.Id))
                .Where(IsDependency)
                .Where(x => x.SourceObjectId != x.TargetObjectId)
                .Select(ToLink)
                .ToList();
        }

        /// <summary>
        /// Returns the working calendar shared by the classes on the plan.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The calendar, or <see langword="null"/> for calendar days.</returns>
        protected override RestApiGanttCalendar RetrieveCalendar(IRequest request)
        {
            var objects = GetPlan(request);

            return objects is null || objects.Count == 0
                ? null
                : ObjectPlanCalendar.Resolve(objects.Select(x => x.ClassId));
        }

        /// <summary>
        /// Handles the PUT/PATCH that persists a moved or resized bar (<c>/tasks/{id}</c>) or a
        /// changed dependency (<c>/links/{id}</c>).
        /// </summary>
        /// <remarks>
        /// A task is handled here rather than through the base's <c>UpdateTask</c> hook,
        /// because that hook can only answer "gone": it returns the task or <c>null</c>, and
        /// the base maps <c>null</c> to a 404. A move this endpoint refuses is not a missing
        /// task, it is a conflict with how the class is modelled, and a client that is told
        /// 404 for a bar it can see cannot tell the two apart. A link is left to the base,
        /// which validates the payload and calls <see cref="UpdateLink"/>.
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

            if (segments.Count == 2 && EqualsSegment(segments[0], "links"))
            {
                return base.Update(request);
            }

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

                if (entity is null || !InScope(entity, request))
                {
                    return new ResponseNotFound(new StatusMessage($"task '{segments[1]}' not found."));
                }

                // moving a plan is changing the object
                if (!ContentAuthorization.MayWrite(entity, request))
                {
                    return new ResponseForbidden();
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
        /// Stores a drawn dependency as a blocking relation from the source bar to the target
        /// bar. The relation type is the first active blocking type, by order, that the relation
        /// catalog accepts for the pair - the same validation the relation surface applies, so a
        /// link cannot exist that the object page would have refused.
        /// </summary>
        /// <param name="link">The validated link payload.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The stored link, carrying the relation id.</returns>
        /// <exception cref="RestApiRefusal">The link is refused for a reason the user can act on.</exception>
        protected override RestApiGanttLink CreateLink(RestApiGanttLink link, IRequest request)
        {
            var source = ResolveEnd(link.From, request);
            var target = ResolveEnd(link.To, request);

            if (!ObjectRelationAuthorization.MayWrite(source, request))
            {
                throw Refuse(request, "forbidden");
            }

            // a blocking cycle could never be completed: each object waits for the next
            if (Reaches(target.Id, source.Id))
            {
                throw Refuse(request, "cycle");
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var neighbourhood = Neighbourhood(source, target);
            RelationValidationResult rejected = null;

            foreach (var type in DependencyTypes())
            {
                var candidate = new Relation
                {
                    System = type.System,
                    Type = type.Id,
                    Source = Reference(source),
                    Target = Reference(target)
                };

                candidate.Metadata[LinkTypeMetadata] = link.Type;

                var validation = RelationRegistry.Validate
                (
                    candidate,
                    reference => ObjectRelationProjection.ResolveObject(reference?.Key) is not null,
                    neighbourhood
                );

                if (!validation.IsValid)
                {
                    rejected ??= validation;
                    continue;
                }

                var entity = ObjectRelationProjection.ToEntity(candidate, identityId);

                CoreHub.ObjectRelationManager.Add(entity);

                return ToLink(entity);
            }

            throw rejected is null
                ? Refuse(request, "notype")
                : Refuse(request, rejected);
        }

        /// <summary>
        /// Changes the Gantt type of a dependency. The ends stay where they are; a payload that
        /// re-points the link is refused, because the store keeps the ends of a relation.
        /// </summary>
        /// <param name="id">The relation id from the sub-path.</param>
        /// <param name="link">The validated replacement link.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The stored link, or <see langword="null"/> when the id names no dependency of the plan.</returns>
        /// <exception cref="RestApiRefusal">The change is refused for a reason the user can act on.</exception>
        protected override RestApiGanttLink UpdateLink(string id, RestApiGanttLink link, IRequest request)
        {
            var stored = ResolveDependency(id, request, out var source);

            if (stored is null)
            {
                return null;
            }

            if (!SameObject(link.From, stored.SourceObjectId) || !SameObject(link.To, stored.TargetObjectId))
            {
                throw Refuse(request, "ends");
            }

            if (!ObjectRelationAuthorization.MayWrite(source, request))
            {
                throw Refuse(request, "forbidden");
            }

            stored.Metadata ??= [];
            stored.Metadata[LinkTypeMetadata] = link.Type;

            CoreHub.ObjectRelationManager.Update(stored);

            return ToLink(stored);
        }

        /// <summary>
        /// Removes the relation behind a dependency.
        /// </summary>
        /// <param name="id">The relation id from the sub-path.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns><see langword="true"/> when the dependency existed and was removed.</returns>
        /// <exception cref="RestApiRefusal">The caller may not change the relations of the source.</exception>
        protected override bool DeleteLink(string id, IRequest request)
        {
            var stored = ResolveDependency(id, request, out var source);

            if (stored is null)
            {
                return false;
            }

            if (!ObjectRelationAuthorization.MayWrite(source, request))
            {
                throw Refuse(request, "forbidden");
            }

            CoreHub.ObjectRelationManager.Remove(stored);

            return true;
        }

        /// <summary>
        /// Returns the objects of the plan, read once per request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The objects, or <see langword="null"/> when the route names nothing to show.</returns>
        private List<Model.Entities.Object> GetPlan(IRequest request)
        {
            if (request is not null && _plans.TryGetValue(request, out var cached))
            {
                return cached;
            }

            var scope = ResolveScope(request);
            var objects = scope is null ? null : GetActiveObjects(scope, request);

            if (request is not null && objects is not null)
            {
                _plans.AddOrUpdate(request, objects);
            }

            return objects;
        }

        /// <summary>
        /// Returns the active objects of the kind in the workspace, ordered by their planned
        /// start so the grid reads chronologically.
        /// </summary>
        /// <param name="scope">The query over the objects of the view.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The objects on the plan.</returns>
        private List<Model.Entities.Object> GetActiveObjects(IQuery<Model.Entities.Object> scope, IRequest request)
        {
            var objects = CoreHub.ObjectManager.GetObjects(scope)
                .Where(x => x.State == WorkspaceState.Active);

            return [.. ApplyQuickfilter(objects, request).OrderBy(x => x.Created).ThenBy(x => x.Key)];
        }

        /// <summary>
        /// Resolves an end of a drawn link to an object of the plan.
        /// </summary>
        /// <param name="taskId">The task id the client sent.</param>
        /// <param name="request">The request.</param>
        /// <returns>The object.</returns>
        /// <exception cref="RestApiRefusal">The id names a class group or no object of the plan.</exception>
        private Model.Entities.Object ResolveEnd(string taskId, IRequest request)
        {
            // a class group is a heading of the plan, not something that can be waited for
            if (!Guid.TryParse(taskId, out var objectId))
            {
                throw Refuse(request, "container");
            }

            var entity = CoreHub.ObjectManager.GetObject(objectId);

            return entity is not null && InScope(entity, request)
                ? entity
                : throw Refuse(request, "unknown");
        }

        /// <summary>
        /// Resolves the relation behind a link of the plan, together with its source object.
        /// </summary>
        /// <param name="id">The relation id.</param>
        /// <param name="request">The request.</param>
        /// <param name="source">The source object, read through the object manager.</param>
        /// <returns>
        /// The relation, or <see langword="null"/> when the id names no dependency whose two ends
        /// the caller sees on this plan.
        /// </returns>
        private ObjectRelation ResolveDependency(string id, IRequest request, out Model.Entities.Object source)
        {
            source = null;

            var stored = Guid.TryParse(id, out var relationId)
                ? CoreHub.ObjectRelationManager.GetRelation(relationId)
                : null;

            if (stored is null || !IsDependency(stored) || stored.TargetObjectId is not Guid targetId)
            {
                return null;
            }

            var from = CoreHub.ObjectManager.GetObject(stored.SourceObjectId);
            var to = CoreHub.ObjectManager.GetObject(targetId);

            if (from is null || to is null || !InScope(from, request) || !InScope(to, request))
            {
                return null;
            }

            source = from;

            return stored;
        }

        /// <summary>
        /// Determines whether a relation is a dependency the plan draws: an object-to-object
        /// relation that is not obsolete and whose type blocks completion.
        /// </summary>
        /// <param name="relation">The relation.</param>
        /// <returns><see langword="true"/> for a dependency.</returns>
        private static bool IsDependency(ObjectRelation relation)
        {
            return relation?.TargetObjectId is not null
                && relation.Status != RelationStatus.Obsolete
                && RelationRegistry.GetType(relation.TypeKey)?.Effect == RelationEffect.BlocksCompletion;
        }

        /// <summary>
        /// Returns the relation types a drawn link may become, best first.
        /// </summary>
        /// <returns>The active object relation types that block completion.</returns>
        private static IEnumerable<IRelationType> DependencyTypes()
        {
            return RelationRegistry
                .TypesOf(RelationSystem.Object, activeOnly: true)
                .Where(x => x.Effect == RelationEffect.BlocksCompletion)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the relations already touching either end of a candidate, which the duplicate
        /// and cardinality checks of the catalog are evaluated against.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="target">The target object.</param>
        /// <returns>The neighbouring relations in the framework shape.</returns>
        private static List<Relation> Neighbourhood(Model.Entities.Object source, Model.Entities.Object target)
        {
            return
            [
                .. new[] { source.Id, target.Id }
                    .Distinct()
                    .SelectMany(CoreHub.ObjectRelationManager.GetRelations)
                    .DistinctBy(x => x.Id)
                    .Where(x => x.SourceObject is not null)
                    .Select(x => new Relation
                    {
                        Id = x.Id.ToString(),
                        System = x.System,
                        Type = x.TypeKey,
                        Direction = x.Direction,
                        Status = x.Status,
                        Source = Reference(x.SourceObject),
                        Target = x.TargetObject is null
                            ? new RelationReference { Uri = x.TargetUri, Title = x.TargetTitle }
                            : Reference(x.TargetObject)
                    })
            ];
        }

        /// <summary>
        /// Projects an object onto the reference the relation catalog validates: its key and its
        /// class. The full projection of the relation surface also resolves the detail address
        /// and the workflow state, which a validation never reads.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The reference.</returns>
        private static RelationReference Reference(Model.Entities.Object @object)
        {
            return new RelationReference
            {
                Key = @object.Key,
                Class = ObjectRelationProjection.ClassNameOf(@object),
                Title = @object.Summary
            };
        }

        /// <summary>
        /// Determines whether one object is already waited for, directly or through others, by
        /// the dependency chain starting at another - which a new link back would close into a
        /// cycle.
        /// </summary>
        /// <remarks>
        /// The walk follows every dependency, not only those on the plan: a cycle running
        /// through an object the plan does not show is still one no workflow can complete. A walk
        /// that exhausts its budget answers <see langword="true"/>, refusing rather than storing
        /// a link it could not check.
        /// </remarks>
        /// <param name="from">The object the walk starts at.</param>
        /// <param name="to">The object looked for.</param>
        /// <returns><see langword="true"/> when <paramref name="to"/> is reachable.</returns>
        private static bool Reaches(Guid from, Guid to)
        {
            var visited = new HashSet<Guid> { from };
            var pending = new Queue<Guid>([from]);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();

                if (current == to)
                {
                    return true;
                }

                if (visited.Count > CycleBudget)
                {
                    return true;
                }

                foreach (var relation in CoreHub.ObjectRelationManager.GetRelations(current))
                {
                    if (relation.SourceObjectId == current
                        && relation.TargetObjectId is Guid next
                        && IsDependency(relation)
                        && visited.Add(next))
                    {
                        pending.Enqueue(next);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Projects a stored dependency onto the Gantt wire shape.
        /// </summary>
        /// <param name="relation">The relation.</param>
        /// <returns>The link.</returns>
        private static RestApiGanttLink ToLink(ObjectRelation relation)
        {
            var type = relation.Metadata is not null && relation.Metadata.TryGetValue(LinkTypeMetadata, out var stored)
                ? stored?.Trim().ToUpperInvariant()
                : null;

            return new RestApiGanttLink
            {
                Id = relation.Id.ToString(),
                From = relation.SourceObjectId.ToString(),
                To = relation.TargetObjectId?.ToString(),
                Type = type is "FS" or "SS" or "FF" or "SF" ? type : "FS"
            };
        }

        /// <summary>
        /// Determines whether a task id the client sent names the given object.
        /// </summary>
        /// <param name="taskId">The task id.</param>
        /// <param name="objectId">The object id.</param>
        /// <returns><see langword="true"/> when both name the same object.</returns>
        private static bool SameObject(string taskId, Guid? objectId)
        {
            return Guid.TryParse(taskId, out var parsed) && parsed == objectId;
        }

        /// <summary>
        /// Builds the refusal of a link for one of the plan's own reasons.
        /// </summary>
        /// <param name="request">The request, whose language the reason is written in.</param>
        /// <param name="reason">The reason suffix of the resource key.</param>
        /// <returns>The refusal to throw.</returns>
        private static RestApiRefusal Refuse(IRequest request, string reason)
        {
            return new RestApiRefusal(I18N.Translate(request, $"kleenestar.core:object.view.plan.link.{reason}"));
        }

        /// <summary>
        /// Builds the refusal of a link the relation catalog rejected, in the words the relation
        /// surface uses for the same rejection.
        /// </summary>
        /// <param name="request">The request, whose language the reason is written in.</param>
        /// <param name="validation">The failed validation.</param>
        /// <returns>The refusal to throw.</returns>
        private static RestApiRefusal Refuse(IRequest request, RelationValidationResult validation)
        {
            var key = $"webexpress.webapp:{validation.Code}";
            var translated = I18N.Translate(request, key);

            return new RestApiRefusal(translated == key ? validation.Message : translated);
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
