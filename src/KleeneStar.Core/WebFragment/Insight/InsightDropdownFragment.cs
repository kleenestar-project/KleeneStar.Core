using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// Represents a dropdown fragment within an insight, providing functionality to interact with and 
    /// manage insight options.
    /// </summary>
    [Section<SectionAppNavigationPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Scope<IScopeStatusPage>]
    [Cache]
    public sealed class InsightDropdownFragment : InsightDropdownControl, IFragmentControl<InsightDropdownControl>, IFragmentControlNavigationItem
    {
        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public InsightDropdownFragment(IFragmentContext fragmentContext)
            : base(fragmentContext?.FragmentId?.ToString())
        {
            FragmentContext = fragmentContext;
            Text = _ => "kleenestar.core:insight.dropdown.label";
            // the icon of the insight overview, as the per-kind menus carry their kind's
            Icon = _ => new WebExpress.WebUI.WebIcon.IconChartPie();
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            // insights are for signed-in callers; anybody else would open a menu of nothing
            if (!FragmentContext.Conditions.Check(renderContext?.Request)
                || !global::KleeneStar.Core.WebPermission.ContentVisibility.MayUseInsights())
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
