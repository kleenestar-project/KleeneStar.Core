using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// the endpoints live in KleeneStar.Core.WWW.Api._1_.SecurityLevels, so the bare entity
// name would resolve to the namespace rather than to the type
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WWW.Api._1_.SecurityLevels
{
    /// <summary>
    /// Serves the selectable states of a security level.
    /// </summary>
    [Title("Security level state")]
    public sealed class State : RestApiSelection<SecurityLevelEntity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public State()
        {
        }

        /// <summary>
        /// Returns the states a security level can be in.
        /// </summary>
        /// <param name="query">The query parameters. Cannot be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The selection items.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<SecurityLevelEntity> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>()
            {
                new()
                {
                    Id = SecurityLevelState.Active.Id(),
                    Text = I18N.Translate(request, SecurityLevelState.Active.Text()),
                    Color = SecurityLevelState.Active.Color()
                },
                new()
                {
                    Id = SecurityLevelState.Archived.Id(),
                    Text = I18N.Translate(request, SecurityLevelState.Archived.Text()),
                    Color = SecurityLevelState.Archived.Color()
                }
            };

            return list.AsQueryable();
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <param name="filter">The filter expression to apply.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<SecurityLevelEntity> Filter(string filter, IQuery<SecurityLevelEntity> query, IRequest request)
        {
            if (filter is null || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase
            (
                x => x.Name, filter
            );
        }
    }
}
