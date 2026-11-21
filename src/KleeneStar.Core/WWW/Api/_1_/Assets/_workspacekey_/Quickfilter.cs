using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the
// quickfilter type argument reads naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_
{
    /// <summary>
    /// Quickfilter endpoint of the asset overview. Offers the personal scopes as
    /// toggleable chips: starred assets, assets assigned to the caller, assets created
    /// by the caller, and the archived history. The <see cref="Table"/> endpoint
    /// translates the selected ids back into filters.
    /// </summary>
    [Cache]
    public sealed class Quickfilter : RestApiQuickfilter<ObjectEntity>
    {
        /// <summary>
        /// The quickfilter id prefix shared by every asset chip.
        /// </summary>
        public const string IdPrefix = "qf_";

        /// <summary>Quickfilter id of the starred-assets chip.</summary>
        public const string StarredId = IdPrefix + "starred";

        /// <summary>Quickfilter id of the assigned-to-me chip.</summary>
        public const string MineId = IdPrefix + "mine";

        /// <summary>Quickfilter id of the created-by-me chip.</summary>
        public const string CreatedId = IdPrefix + "created";

        /// <summary>Quickfilter id of the archived chip.</summary>
        public const string ArchivedId = IdPrefix + "archived";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Quickfilter()
        {
        }

        /// <summary>
        /// Retrieves the asset quickfilter options.
        /// </summary>
        /// <param name="context">The query context (unused — the options are static).</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The personal asset scopes as quickfilter items.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = StarredId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.assets.filter.starred")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = MineId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.assets.filter.mine")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = CreatedId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.assets.filter.created")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = ArchivedId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.assets.filter.archived")
            };
        }
    }
}
