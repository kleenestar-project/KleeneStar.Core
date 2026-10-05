using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// Answers, for a request whose route names an insight, which insight it is, whether the
    /// caller may read or arrange it, and which objects it is about. Every endpoint behind an
    /// insight's tabs asks here, so the table, the boards, the plans and the reports agree on
    /// one set.
    /// </summary>
    /// <remarks>
    /// The objects are those the insight's <see cref="Model.Entities.Insight.Query"/> selects,
    /// read through <see cref="CoreHub.ObjectManager"/> - so security levels and the content
    /// visibility of the workspaces narrow them exactly as they narrow every other list, and an
    /// insight never shows what its reader could not open. A blank query selects everything the
    /// reader may see; a query that no longer compiles selects nothing, because an insight that
    /// silently widened to everything when its filter broke would be the worse surprise.
    /// </remarks>
    public static class InsightScope
    {
        /// <summary>
        /// The kind an insight's Kanban board configuration is stored under, beside the insight
        /// id - it keeps the board apart from any workspace's.
        /// </summary>
        public const string BoardKind = "insight";

        /// <summary>
        /// The key the quickfilters a user defines on an insight's tabs are stored under; the
        /// insight id is their context.
        /// </summary>
        public const string QuickfilterView = "insight";

        /// <summary>
        /// Resolves the insight the route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The insight, or <see langword="null"/> when the route names none.</returns>
        public static Model.Entities.Insight Resolve(IRequest request)
        {
            return Guid.TryParse(request?.GetParameter<InsightIdParameter>()?.Value, out var id) && id != Guid.Empty
                ? CoreHub.InsightManager.GetInsight(id)
                : null;
        }

        /// <summary>
        /// Determines whether the caller may read the content of an insight.
        /// </summary>
        /// <param name="insight">The insight. May be null, which answers false.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may read it.</returns>
        public static bool MayRead(Model.Entities.Insight insight, IRequest request)
        {
            return insight is not null
                && ContentAuthorization.MayUseInsight(insight.Id, request, typeof(WebPermissions.InsightReadContentPermission));
        }

        /// <summary>
        /// Determines whether the caller may arrange an insight - add, remove and reorder its
        /// tabs, change its board and its dashboard.
        /// </summary>
        /// <param name="insight">The insight. May be null, which answers false.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may arrange it.</returns>
        public static bool MayArrange(Model.Entities.Insight insight, IRequest request)
        {
            return insight is not null
                && ContentAuthorization.MayUseInsight(insight.Id, request, typeof(WebPermissions.InsightWriteContentPermission));
        }

        /// <summary>
        /// Resolves the insight the route names if the caller may read it.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The insight, or <see langword="null"/> when there is none or it is not readable.</returns>
        public static Model.Entities.Insight ResolveReadable(IRequest request)
        {
            var insight = Resolve(request);

            return MayRead(insight, request) ? insight : null;
        }

        /// <summary>
        /// Compiles the query of an insight into the condition its objects meet.
        /// </summary>
        /// <param name="insight">The insight.</param>
        /// <returns>
        /// The condition, <see langword="null"/> for a blank query (no narrowing), or a condition
        /// nothing meets when the query does not compile.
        /// </returns>
        public static Expression<Func<Model.Entities.Object, bool>> Predicate(Model.Entities.Insight insight)
        {
            if (string.IsNullOrWhiteSpace(insight?.Query))
            {
                return null;
            }

            return WqlFilter.Compile<Model.Entities.Object>(insight.Query) ?? (x => false);
        }

        /// <summary>
        /// Narrows a query to the objects of an insight.
        /// </summary>
        /// <param name="insight">The insight.</param>
        /// <param name="query">The query to narrow.</param>
        /// <returns>The narrowed query.</returns>
        public static IQuery<Model.Entities.Object> Apply(Model.Entities.Insight insight, IQuery<Model.Entities.Object> query)
        {
            var predicate = Predicate(insight);

            return predicate is null ? query : query.Where(predicate);
        }

        /// <summary>
        /// Returns the query over the objects of an insight.
        /// </summary>
        /// <param name="insight">The insight.</param>
        /// <returns>The query.</returns>
        public static IQuery<Model.Entities.Object> Query(Model.Entities.Insight insight)
        {
            return Apply(insight, new Query<Model.Entities.Object>());
        }

        /// <summary>
        /// Returns the objects of an insight the caller may see, in every state.
        /// </summary>
        /// <param name="insight">The insight.</param>
        /// <returns>The objects.</returns>
        public static IReadOnlyList<Model.Entities.Object> GetObjects(Model.Entities.Insight insight)
        {
            return insight is null ? [] : [.. CoreHub.ObjectManager.GetObjects(Query(insight))];
        }

        /// <summary>
        /// Determines whether an object belongs to an insight.
        /// </summary>
        /// <param name="insight">The insight.</param>
        /// <param name="entity">The object.</param>
        /// <returns><see langword="true"/> when the insight's query selects the object.</returns>
        public static bool Contains(Model.Entities.Insight insight, Model.Entities.Object entity)
        {
            if (insight is null || entity is null)
            {
                return false;
            }

            var predicate = Predicate(insight);

            if (predicate is null)
            {
                return true;
            }

            // the object was read through the manager, with its workspace attached, so a query
            // naming the workspace is answered the way the store answers it
            return WqlFilter.Apply(insight.Query, [entity]).Any();
        }

        /// <summary>
        /// Returns the classes the given objects belong to.
        /// </summary>
        /// <param name="objects">The objects.</param>
        /// <returns>The classes, each once.</returns>
        public static IReadOnlyList<Model.Entities.Class> GetClasses(IEnumerable<Model.Entities.Object> objects)
        {
            return
            [
                .. (objects ?? [])
                    .Select(x => x.ClassId)
                    .Distinct()
                    .Select(CoreHub.ClassManager.GetClass)
                    .Where(x => x is not null)
            ];
        }
    }
}
