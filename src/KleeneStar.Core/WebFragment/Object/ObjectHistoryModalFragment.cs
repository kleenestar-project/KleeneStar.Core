using KleeneStar.Core.WebPolicies;
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
    /// Renders the body-level page-modal container that hosts the object version history. The
    /// <see cref="ObjectItemHistoryMoreFragment"/> ⋯ item targets this container by id.
    /// </summary>
    /// <remarks>
    /// A <c>wx-webui-modal-page</c> (not <c>-form</c>) modal is required because the dialog
    /// content is a master-detail composite, not a <c>&lt;form&gt;</c>: the form modal injects
    /// only the children of a <c>&lt;form&gt;</c> element and would leave the dialog empty. The
    /// page modal fetches the history page and copies the element matching
    /// <see cref="ControlModalRemotePage.Selector"/> - the view the history fragments compose -
    /// into the modal body, where the client controllers (master-detail, split, list, search,
    /// pagination) bootstrap via the MutationObserver. The selector is a class rather than an id
    /// because a fragment's element id is derived from its fragment id and cannot be chosen.
    /// Rendered into <see cref="SectionBodySecondary"/> so the overlay sits at body level rather
    /// than nested inside a content column that could clip it.
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<ObjectViewPolicy>>]
    [Cache]
    public sealed class ObjectHistoryModalFragment : ControlModalRemotePage, IFragmentControl<ControlModalRemotePage>
    {
        /// <summary>
        /// The well-known id the ⋯ "History" item targets.
        /// </summary>
        public const string ModalId = "modal-history";

        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectHistoryModalFragment(IFragmentContext fragmentContext)
            : base(ModalId)
        {
            FragmentContext = fragmentContext;

            Header = _ => "kleenestar.core:object.history.title";
            Selector = _ => $".{ObjectHistoryViewFragment.ContentClass}";
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or <c>null</c>.</returns>
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
