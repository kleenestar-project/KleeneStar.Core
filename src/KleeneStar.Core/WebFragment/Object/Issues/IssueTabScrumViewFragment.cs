using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Provides the switchable working area of the Scrum tab: the active-sprint board and the
    /// product backlog as the two views of one view control, below the team workload (order 0)
    /// and the sprint burn-down (order 1) that both views share.
    /// </summary>
    /// <remarks>
    /// The fragment IS the view control — like <see cref="Class.ClassViewFragment"/> it derives
    /// from <see cref="FragmentControlView"/> and collects its views from the fragments scoped
    /// to it, so a further view (a sprint report, a roadmap) is added by registering another
    /// view item rather than by editing this file. The toggle-group layout renders the switch
    /// as the segmented control the view is designed around.
    /// </remarks>
    [Section<SectionTabTemplatePrimary>]
    [Scope<IssueTabScrumTemplateFragment>]
    [Order(2)]
    [Cache]
    public sealed class IssueTabScrumViewFragment : FragmentControlView
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Layout = _ => TypeLayoutView.ToggleGroup;
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
