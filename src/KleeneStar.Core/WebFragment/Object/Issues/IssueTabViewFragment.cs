using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Composite view inside the <see cref="IssueTabViewTemplateFragment"/> tab template.
    /// The view itself is empty — the search, quickfilter, table, and pagination child
    /// fragments attach themselves via <c>[Scope&lt;IssueViewFragment&gt;]</c> and
    /// compose the recent/starred issue list declaratively.
    /// </summary>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabViewTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class IssueTabViewFragment : FragmentControlView, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Layout = _ => WebExpress.WebUI.WebControl.TypeLayoutView.ToggleGroup;
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
            return base.Render(renderContext, visualTree);
        }
    }
}
