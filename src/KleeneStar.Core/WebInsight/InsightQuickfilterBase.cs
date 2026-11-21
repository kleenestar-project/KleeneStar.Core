using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebRestApi;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the quickfilter type
// argument reads naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// Project-wide base of the quickfilter bars of an insight's tabs: the personal chips
    /// (starred, assigned to me, created by me) followed by the filters users defined for the
    /// insight, which are stored under <see cref="InsightScope.QuickfilterView"/> with the
    /// insight id as their context. Each concrete endpoint registers at its own route, so the
    /// base must stay abstract.
    /// </summary>
    public abstract class InsightQuickfilterBase : RestApiQuickfilter<ObjectEntity>
    {
        /// <summary>
        /// Gets a value indicating whether the bar offers the archived chip, which flips the
        /// table and the list to the archived objects. The boards and plans show active objects
        /// only and leave it out.
        /// </summary>
        protected abstract bool OffersArchived { get; }

        /// <summary>
        /// Returns the chips of the bar.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The chips.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = ObjectKindQuickfilter.StarredId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.starred")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = ObjectKindQuickfilter.MineId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.mine")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = ObjectKindQuickfilter.CreatedId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.created")
            };

            if (OffersArchived)
            {
                yield return new RestApiQuickfilterItem()
                {
                    Id = ObjectKindQuickfilter.ArchivedId,
                    Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.archived")
                };
            }

            foreach (var item in CustomQuickfilterSupport.Items(InsightScope.QuickfilterView, ContextOf(request), request))
            {
                yield return item;
            }
        }

        /// <summary>
        /// Reads a user-defined filter.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <param name="id">The filter id.</param>
        /// <returns>The filter.</returns>
        protected override object RetrieveItem(IQueryContext context, IRequest request, string id)
        {
            return CustomQuickfilterSupport.Read(id, InsightScope.QuickfilterView);
        }

        /// <summary>
        /// Creates a user-defined filter on the insight the route names.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <param name="payload">The filter.</param>
        /// <returns>The created chip.</returns>
        protected override RestApiQuickfilterItem CreateItem(IQueryContext context, IRequest request, RestApiQuickfilterPayload payload)
        {
            return CustomQuickfilterSupport.Create(payload, InsightScope.QuickfilterView, ContextOf(request), request);
        }

        /// <summary>
        /// Changes a user-defined filter.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <param name="payload">The filter.</param>
        /// <returns>The changed chip.</returns>
        protected override RestApiQuickfilterItem UpdateItem(IQueryContext context, IRequest request, RestApiQuickfilterPayload payload)
        {
            return CustomQuickfilterSupport.Update(payload, InsightScope.QuickfilterView, request);
        }

        /// <summary>
        /// Deletes a user-defined filter.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <param name="id">The filter id.</param>
        /// <returns><see langword="true"/> when it was deleted.</returns>
        protected override bool DeleteItem(IQueryContext context, IRequest request, string id)
        {
            return CustomQuickfilterSupport.Delete(id, InsightScope.QuickfilterView);
        }

        /// <summary>
        /// Returns the context the filters of the route's insight are stored under.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The insight id.</returns>
        public static string ContextOf(IRequest request)
        {
            return request?.GetParameter<InsightIdParameter>()?.Value;
        }
    }
}
