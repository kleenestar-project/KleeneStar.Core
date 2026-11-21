using KleeneStar.Model.Entities;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Notifications
{
    /// <summary>
    /// Provides the quick filters of the notification center: by whether an entry has been
    /// seen, and by what happened.
    /// </summary>
    public sealed class Quickfilter : RestApiQuickfilter<UserNotification>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Quickfilter()
        {
        }

        /// <summary>
        /// Retrieves the quick filter items.
        /// </summary>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The quick filters offered above the table.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_unread",
                Name = I18N.Translate(request, "kleenestar.core:notification.center.filter.unread")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_read",
                Name = I18N.Translate(request, "kleenestar.core:notification.center.filter.read")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_created",
                Name = I18N.Translate(request, "kleenestar.core:notification.center.filter.created")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_updated",
                Name = I18N.Translate(request, "kleenestar.core:notification.center.filter.updated")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = "qf_deleted",
                Name = I18N.Translate(request, "kleenestar.core:notification.center.filter.deleted")
            };
        }
    }
}
