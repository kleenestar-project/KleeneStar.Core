using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Profile
{
    /// <summary>
    /// Serves the selectable first days of the week to the selection on the account page.
    /// </summary>
    [Title("Week start")]
    public sealed class Weekstart : RestApiSelection<Model.Entities.Identity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Weekstart()
        {
        }

        /// <summary>
        /// Retrieves the selectable week starts.
        /// </summary>
        /// <param name="query">The query criteria; paging is applied by the base class.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The selectable week starts.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems
        (
            IQuery<Model.Entities.Identity> query,
            IQueryContext context,
            IRequest request
        )
        {
            var list = new List<RestApiSelectionItem>()
            {
                new()
                {
                    Id = WeekStart.Monday.Id(),
                    Text = I18N.Translate(request, WeekStart.Monday.Text())
                },
                new()
                {
                    Id = WeekStart.Sunday.Id(),
                    Text = I18N.Translate(request, WeekStart.Sunday.Text())
                },
                new()
                {
                    Id = WeekStart.Saturday.Id(),
                    Text = I18N.Translate(request, WeekStart.Saturday.Text())
                }
            };

            return list.AsQueryable();
        }

        /// <summary>
        /// Applies the search term typed into the selection.
        /// </summary>
        /// <param name="filter">The search term, or null when nothing was typed.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<Model.Entities.Identity> Filter
        (
            string filter,
            IQuery<Model.Entities.Identity> query,
            IRequest request
        )
        {
            // three fixed entries, built in RetrieveItems rather than queried
            return query;
        }
    }
}
