using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Renders the body-level page-modal container that hosts the "Organize objects" object tree.
    /// The <see cref="ObjectItemOrganizeMoreFragment"/> ⋯ item targets this container by id.
    /// </summary>
    /// <remarks>
    /// A <c>wx-webui-modal-page</c> (not <c>-form</c>) modal is required because the dialog content
    /// is a tree in a flat section, not a <c>&lt;form&gt;</c>: the form modal only injects the children of a
    /// <c>&lt;form&gt;</c> element and would leave a non-form dialog empty. The page modal fetches
    /// the organize page and copies the element matching <see cref="ControlModalRemotePage.Selector"/>
    /// into the modal body, where the client controllers (tree + object-move controller) bootstrap
    /// via the MutationObserver. Rendered into <see cref="SectionBodySecondary"/> so the overlay sits
    /// at body level rather than nested inside a content column that could clip it.
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Insight._insightid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Search.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Cache]
    public sealed class ObjectOrganizeModalFragment : ControlModalRemotePage, IFragmentControl<ControlModalRemotePage>
    {
        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class with the well-known <c>modal-organize</c> id so the
        /// ⋯ "Organize objects" item can target it, and the selector of the content element the
        /// organize page wraps its tree in.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectOrganizeModalFragment(IFragmentContext fragmentContext)
            : base("modal-organize")
        {
            FragmentContext = fragmentContext;

            Header = _ => "kleenestar.core:workspace.organize.title";
            Selector = _ => "#kleenestar-organize-content";
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
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
