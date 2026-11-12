using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Templates._workspacekey_
{
    /// <summary>
    /// Provides the classes the workspace's templates instantiate, as options of the quick filter's
    /// class dropdown.
    /// </summary>
    /// <remarks>
    /// Only classes that actually carry a template are offered, because an option that matches
    /// nothing is one the overview can never show a result for. Each option carries the number of
    /// templates it would show as its badge. The options need to declare neither their filter
    /// group nor their exclusivity: a single-choice dropdown applies both to everything it loads,
    /// so picking a class replaces the previous choice rather than adding to it.
    /// </remarks>
    [Title("kleenestar.core:template.class.label")]
    [Cache]
    public sealed class Classes : RestApiQuickfilter<Model.Entities.Template>
    {
        /// <summary>
        /// The filter group the class options share.
        /// </summary>
        public const string FilterGroup = "template-class";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Classes()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns how a WQL statement is turned into a query: translatable, because the
        /// query runs against the database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <summary>
        /// Retrieves the classes the workspace's templates are bound to.
        /// </summary>
        /// <param name="context">
        /// The context in which the query is executed.
        /// </param>
        /// <param name="request">
        /// The request that provides the operational context.
        /// </param>
        /// <returns>
        /// The quick filter options, one per class in use. The collection may be empty.
        /// </returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);

            if (workspace is null)
            {
                return [];
            }

            var query = new Query<Model.Entities.Template>()
                .Where(x => x.Class.WorkspaceId == workspace.Id);

            return CoreHub.TemplateManager.GetTemplates(query)
                .Where(x => x.Class is not null)
                .GroupBy(x => x.Class.Id)
                .Select(g => new
                {
                    Class = g.First().Class,
                    Count = g.Count()
                })
                .OrderBy(x => x.Class.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiQuickfilterItem()
                {
                    Id = TemplateClassFilter.ToFilterId(x.Class.Id),
                    Name = x.Class.Name,
                    Icon = x.Class.Icon,
                    Badge = x.Count.ToString()
                });
        }
    }
}
