using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Fields
{
    /// <summary>
    /// Represents a selectable field type for use in REST API selection scenarios.
    /// </summary>
    [Title("Field type")]
    public sealed class FieldType : RestApiSelection<Model.Entities.Field>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public FieldType()
        {
        }

        /// <summary>
        /// Retrieves a queryable collection of index items that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// An enumerable collection of selection items that satisfy the query 
        /// criteria. The collection is empty if no items match.
        /// </returns>
        protected override IQuery<Model.Entities.Field> Filter(string filter, IQuery<Model.Entities.Field> query, IRequest request)
        {
            return query;
        }

        /// <summary>
        /// Retrieves a queryable collection of index items that match the specified query criteria.
        /// </summary>
        /// <param name="query">
        /// An object containing the query parameters used to filter and select index items. Cannot 
        /// be null.
        /// </param>
        /// <param name="context">
        /// The context in which the query is executed. Provides additional information or constraints 
        /// for the retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// An enumerable collection of selection items that satisfy the query 
        /// criteria. The collection is empty if no items match.
        /// </returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Model.Entities.Field> query, IQueryContext context, IRequest request)
        {
            // the offered types are derived from the enum rather than listed here, so a type
            // added to the model is selectable without a second edit in this endpoint
            var list = System.Enum.GetValues<Model.Entities.FieldType>()
                .Where(x => x.Id() != System.Guid.Empty)
                .Select(x => new RestApiSelectionItem()
                {
                    Id = x.Id(),
                    Text = I18N.Translate(request, x.Text()),
                    Color = x.Color()
                })
                .OrderBy(x => x.Text, System.StringComparer.CurrentCulture)
                .ToList();

            return list.AsQueryable();
        }
    }
}
