using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Composite view of the object version history dialog. The view itself is empty — the
    /// search, list and pagination child fragments attach themselves via
    /// <c>[Scope&lt;ObjectHistoryViewFragment&gt;]</c> and compose the dialog declaratively,
    /// the same way the issue overview is composed.
    /// </summary>
    /// <remarks>
    /// The marker class is what the modal copies: a fragment's element id is derived from its
    /// fragment id and cannot be chosen, so the dialog is addressed by a class instead of by an
    /// id. Copying the view rather than the page's whole content region also leaves the page
    /// headline behind, which the modal already shows in its own header.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.History>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<ObjectViewPolicy>>]
    [Cache]
    public sealed class ObjectHistoryViewFragment : FragmentControlView, IScope
    {
        /// <summary>
        /// The class the modal's selector addresses the dialog content by.
        /// </summary>
        public const string ContentClass = "kleenestar-history-content";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectHistoryViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Classes = [ContentClass];
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
