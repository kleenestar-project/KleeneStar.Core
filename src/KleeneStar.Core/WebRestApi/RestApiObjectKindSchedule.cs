using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base for the object calendar endpoint of a kind's overview tab control.
    /// Each active object of the <see cref="Kind"/> becomes an entry placed by the date fields
    /// of its class (see <see cref="ObjectBoardProjection.ResolvePlan"/>), coloured by its
    /// workflow status category and linked to its detail page. A concrete subclass only fixes
    /// the kind it shows (issue, asset, …); each concrete endpoint registers at its own route,
    /// so the base must stay abstract.
    /// </summary>
    /// <remarks>
    /// The calendar asks for a period through the <c>from</c>/<c>to</c> query parameters. The
    /// filtering runs in memory, because an object's dates live in its field values rather
    /// than in a column a WebIndex query could narrow on.
    /// <para>
    /// Moving an entry writes the new dates back into the class' date fields. An edge the
    /// class models no field for is refused rather than half-applied (see
    /// <see cref="ObjectPlanWriter"/>), so a calendar over a class without dates behaves as
    /// read-only. Creating and deleting entries is refused throughout — an object is raised
    /// and retired through the object flow, not by drawing on a calendar — so the inherited
    /// defaults, which already refuse, are left in place.
    /// </para>
    /// </remarks>
    public abstract class RestApiObjectKindSchedule : RestApiSchedule
    {
        /// <summary>
        /// Gets the persisted kind key the calendar is scoped to.
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Applies an optional in-memory quickfilter to the calendar's objects. The default is
        /// a no-op, so the calendar shows every active object of the kind in the workspace.
        /// </summary>
        /// <param name="objects">The objects that would become entries.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered objects.</returns>
        protected virtual IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request) => objects;

        /// <summary>
        /// Returns the entries that overlap the requested period.
        /// </summary>
        /// <param name="from">The first day of the period, or <see langword="null"/>.</param>
        /// <param name="to">The day after the period, or <see langword="null"/>.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The entries.</returns>
        protected override IEnumerable<RestApiScheduleItem> RetrieveItems(DateTime? from, DateTime? to, IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                yield break;
            }

            var categories = ObjectBoardProjection.GetOrderedCategories();
            var categoriesById = categories.ToDictionary(x => x.Id, x => x);
            var contextByClass = new Dictionary<Guid, ObjectBoardClassContext>();

            foreach (var entity in GetActiveObjects(workspace.Id, request))
            {
                if (!contextByClass.TryGetValue(entity.ClassId, out var classContext))
                {
                    var cls = CoreHub.ClassManager.GetClass(entity.ClassId);
                    classContext = cls is null ? null : ObjectBoardProjection.BuildClassContext(cls);
                    contextByClass[entity.ClassId] = classContext;
                }

                var (start, end) = ObjectBoardProjection.ResolvePlan(entity, classContext);

                // an entry that merely touches the shown period still belongs on it, so the
                // test is an overlap rather than a containment
                if (from.HasValue && end.Date < from.Value.Date)
                {
                    continue;
                }

                if (to.HasValue && start.Date > to.Value.Date)
                {
                    continue;
                }

                var category = ObjectBoardProjection.ResolveCategory(entity.Id, classContext, categoriesById);

                yield return new RestApiScheduleItem
                {
                    Id = entity.Id.ToString(),
                    Title = string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : $"{entity.Key} · {entity.Summary}",
                    Start = Format(start.Date),
                    End = Format(end.Date),
                    AllDay = true,
                    Category = classContext?.Class?.Name,
                    ColorCss = ObjectBoardProjection.CategoryColorCss(category),
                    Uri = ResolveDetailUri(entity),
                    Meta = new Dictionary<string, string>
                    {
                        ["key"] = entity.Key,
                        ["status"] = ObjectBoardProjection.CategoryLabel(category)
                    }
                };
            }
        }

        /// <summary>
        /// Handles the PUT that persists a moved entry.
        /// </summary>
        /// <remarks>
        /// The verb is handled here rather than through the base's <c>Update(item, request)</c>
        /// hook, because that hook can only answer "gone": it returns the item or <c>null</c>,
        /// and the base maps <c>null</c> to a 404. A move this endpoint refuses is not a
        /// missing entry, it is a conflict with how the class is modelled.
        /// <para>
        /// The <c>[Method]</c> attribute is re-declared because <c>RestApiManager</c> reads it
        /// with <c>inherit: false</c> — an override without it takes PUT off the endpoint.
        /// The success body keeps the base's <c>{ success, item }</c> shape, which is what the
        /// schedule client reads the saved entry back out of.
        /// </para>
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>
        /// <c>200</c> with the stored entry, <c>404</c> when the id names no object of the kind,
        /// <c>409</c> when the move touches an edge the class models no field for, or
        /// <c>400</c> when the payload is malformed.
        /// </returns>
        [Method(RequestMethod.PUT)]
        public override IResponse Update(IRequest request)
        {
            var item = ReadItem(request);

            if (item is null)
            {
                return new ResponseBadRequest(new StatusMessage("Missing or malformed request body."));
            }

            if (string.IsNullOrWhiteSpace(item.Id))
            {
                return new ResponseBadRequest(new StatusMessage("Missing item id."));
            }

            try
            {
                if (!Guid.TryParse(item.Id, out var objectId))
                {
                    return new ResponseNotFound(new StatusMessage($"No item found for id '{item.Id}'."));
                }

                var entity = CoreHub.ObjectManager.GetObject(objectId);

                if (entity is null || !string.Equals(entity.Kind, Kind, StringComparison.OrdinalIgnoreCase))
                {
                    return new ResponseNotFound(new StatusMessage($"No item found for id '{item.Id}'."));
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
                    ObjectPlanWriter.ParseDate(item.Start),
                    ObjectPlanWriter.ParseDate(item.End)
                );

                if (!applied)
                {
                    return ObjectPlanWriter.Conflict(entity, context);
                }

                return new ResponseOK
                {
                    Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = true, item }, _wireOptions))
                }
                    .AddHeaderContentType("application/json");
            }
            catch (Exception ex)
            {
                return RestApiFault.BadRequest(request, ex, "error processing put request.");
            }
        }

        /// <summary>
        /// Deserializes the entry carried in the request body. The base reads the body with a
        /// private helper, so the override brings its own.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The entry, or <see langword="null"/> when the body is missing or malformed.</returns>
        private static RestApiScheduleItem ReadItem(IRequest request)
        {
            if (request is not Request requestData || requestData.Content is not { Length: > 0 })
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RestApiScheduleItem>(requestData.Content, _wireOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// The serializer profile of the entry wire shape. The properties carry their own
        /// <c>JsonPropertyName</c>, so only the tolerant read has to be configured.
        /// </summary>
        private static readonly JsonSerializerOptions _wireOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Returns the active objects of the kind in the workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace id.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The objects on the calendar.</returns>
        private List<Model.Entities.Object> GetActiveObjects(Guid workspaceId, IRequest request)
        {
            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .WhereEquals(x => x.Kind, Kind);

            var objects = CoreHub.ObjectManager.GetObjects(query)
                .Where(x => x.State == WorkspaceState.Active);

            return [.. ApplyQuickfilter(objects, request)];
        }

        /// <summary>
        /// Resolves the reading view of an object so a click on an entry opens the object.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <returns>The detail URI, or <see langword="null"/> when no page is registered.</returns>
        private static string ResolveDetailUri(Model.Entities.Object entity)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Detail>();

            return uri is null
                ? null
                : $"{uri}?id={entity.Id}";
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
    }
}
