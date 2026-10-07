using KleeneStar.Core.WebInsight;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Renders an insight as a REST-backed tab control, the way the issue overview of a
    /// workspace renders its views. The tabs are the insight's <see cref="Model.Entities.InsightView"/>s,
    /// loaded from <c>/api/1/insights/{insightid}/tab</c>; they can be added from the template
    /// picker, moved and closed. Each tab type contributes a template scoped to this fragment
    /// (<c>InsightTab…TemplateFragment</c>) and registered with the
    /// <see cref="WebInsight.InsightViewTypeCatalog"/>.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Insight._insightid_.Index>]
    [Cache]
    public sealed class InsightTabFragment : FragmentControlDataTab, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // adding, moving and the tab menu (rename, color, delete) are offered to whoever
            // may arrange the insight; a read-only control offers none of them, and the endpoint
            // asks the same question again
            Readonly = ctx => !InsightScope.MayArrange(InsightScope.Resolve(ctx?.Request), ctx?.Request);
            MovableTab = _ => true;
            EditableTab = _ => true;
            DeletableTab = _ => true;
            Layout = _ => TypeLayoutTab.Underline;
            ServiceFactory = _ => DataServiceDescriptor.TabData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Tab>().ToString());
        }
    }
}
