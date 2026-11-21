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
    /// Renders the body-level page-modal container that hosts the files of a document or a post.
    /// The <see cref="ObjectItemAttachmentsMoreFragment"/> ⋯ item targets this container by id.
    /// </summary>
    /// <remarks>
    /// A reader of a document is reading it. Sending them to a page of their own to see what is
    /// attached takes the text away and makes coming back their problem; the modal shows the
    /// files over the text and gives it back when it closes.
    /// <para>
    /// It fetches the files page and copies the section the attachment card renders into the
    /// dialog, where the client controllers (file view, upload) bootstrap through the
    /// MutationObserver. The selector is the id that card gives its own section - unlike the
    /// history modal, which has to use a class because the element it wants is a fragment's own
    /// and its id is derived rather than chosen.
    /// </para>
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<ObjectViewPolicy>>]
    [Cache]
    public sealed class ObjectAttachmentsModalFragment : ControlModalRemotePage, IFragmentControl<ControlModalRemotePage>
    {
        /// <summary>
        /// The well-known id the ⋯ "Attachments" item targets.
        /// </summary>
        public const string ModalId = "modal-attachments";

        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectAttachmentsModalFragment(IFragmentContext fragmentContext)
            : base(ModalId)
        {
            FragmentContext = fragmentContext;

            Header = _ => "kleenestar.core:object.attachment.card.header";
            Selector = _ => "#object-attachment-section";
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
