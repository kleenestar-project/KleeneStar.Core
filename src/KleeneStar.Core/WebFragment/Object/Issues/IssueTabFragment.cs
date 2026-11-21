using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Renders the workspace issue overview as a REST-backed tab control. The tab set is
    /// loaded from the <see cref="Api.Tab"/> REST endpoint, so the persisted object views
    /// of the workspace appear as movable, closable tabs and new views can be added from
    /// the template picker. The tab templates attach themselves via
    /// <c>[Scope&lt;ObjectTabFragment&gt;]</c>; the leading one is the
    /// <see cref="IssueTabViewTemplateFragment"/> with the curated issue list, followed
    /// by the <see cref="IssueTabViewTemplateFragment"/>, which composes the classic
    /// object view from the table, tile, list, search, quickfilter and pagination
    /// fragments.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<WWW.Issues._workspacekey_.Index>]
    [Cache]
    public sealed class IssueTabFragment : FragmentControlDataTab, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // adding, moving and the tab menu (rename, color, delete) are offered to whoever
            // may change the workspace's content; a read-only control offers none of them, and the endpoint
            // asks the same question again
            Readonly = ctx => !ObjectViewTabs.MayArrange(ctx?.Request);
            MovableTab = _ => true;
            EditableTab = _ => true;
            DeletableTab = _ => true;
            Layout = _ => TypeLayoutTab.Underline;
            ServiceFactory = _ => DataServiceDescriptor.TabData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.Tab>().ToString());
        }
    }
}
