using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.SavedSearch
{
    /// <summary>
    /// Represents the delete confirmation form fragment for a saved search, opened from the
    /// "…" menu beside the search while it runs. Offered to the owner and to members of a group
    /// granted the admin policy; the endpoint asks the same question.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Delete>]
    [Cache]
    public sealed class SavedSearchDeleteFormFragment : FragmentControlDataFormDelete
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SavedSearchDeleteFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.SavedSearches.Index>();

            // the row the confirmation deletes; without it the form asks the endpoint for no
            // record at all
            ItemId = renderContext => renderContext.Request.GetParameter<SavedSearchIdParameter>()?.Value;
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var param = renderContext.Request.GetParameter<SavedSearchIdParameter>();

            return base.Render(renderContext, visualTree);
        }
    }
}

