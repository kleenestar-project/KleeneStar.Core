using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebControl;
using Api = KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// Renders the workspace asset overview as a REST-backed tab control. The tab set is
    /// loaded from the asset <see cref="Api.Tab"/> REST endpoint, so the persisted asset
    /// views of the workspace appear as movable, closable tabs and new views can be added
    /// from the template picker. The tab templates attach themselves via
    /// <c>[Scope&lt;AssetTabFragment&gt;]</c>; the leading one is the
    /// <see cref="AssetTabViewTemplateFragment"/> with the curated asset list, followed by the
    /// classic table/list/tile view, the dashboard, and the Kanban board. Scrum boards are
    /// deliberately not offered for assets.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Cache]
    public sealed class AssetTabFragment : FragmentControlDataTab, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AssetTabFragment(IFragmentContext fragmentContext)
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
            ServiceFactory = _ => DataServiceDescriptor.TabData(CoreHub.GetUri<Api.Tab>().ToString());
        }
    }
}
