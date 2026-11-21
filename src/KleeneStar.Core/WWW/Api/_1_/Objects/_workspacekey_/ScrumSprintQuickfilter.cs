using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// The entity type Object collides with System.Object; alias it so the
// quickfilter type argument reads naturally.
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Quickfilter endpoint of the Scrum sprint tab's active-sprint Kanban board. Offers the
    /// personal scopes as toggleable chips: issues assigned to the caller and starred
    /// issues. The selected chip ids flow through the sprint board ViewState into the
    /// <c>f</c> query parameter the <see cref="ScrumSprintKanban"/> endpoint applies.
    /// </summary>
    [Cache]
    public sealed class ScrumSprintQuickfilter : RestApiQuickfilter<ObjectEntity>
    {
        /// <summary>The quickfilter id prefix shared by every sprint chip.</summary>
        public const string IdPrefix = "qf_";

        /// <summary>Quickfilter id of the assigned-to-me chip.</summary>
        public const string MineId = IdPrefix + "mine";

        /// <summary>Quickfilter id of the starred chip.</summary>
        public const string StarredId = IdPrefix + "starred";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public ScrumSprintQuickfilter()
        {
        }

        /// <summary>
        /// Retrieves the sprint quickfilter options.
        /// </summary>
        /// <param name="context">The query context (unused — the options are static).</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The personal sprint scopes as quickfilter items.</returns>
        protected override IEnumerable<RestApiQuickfilterItem> RetrieveItems(IQueryContext context, IRequest request)
        {
            yield return new RestApiQuickfilterItem()
            {
                Id = MineId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.mine")
            };

            yield return new RestApiQuickfilterItem()
            {
                Id = StarredId,
                Name = I18N.Translate(request, "kleenestar.core:object.kind.issues.filter.starred")
            };
        }
    }
}
