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
    /// Renders the body-level page-modal container that hosts the draft comparison. The
    /// <i>show changes</i> entry <see cref="ObjectProseEditorFragmentBase"/> adds to the
    /// editor's overflow menu targets this container by id.
    /// </summary>
    /// <remarks>
    /// It stands beside the editor rather than inside it, and opens over it: the comparison is
    /// read once and closed, and the text that is not saved yet has to survive the look.
    /// <para>
    /// A <c>wx-webui-modal-page</c> (not <c>-form</c>) modal is required because the comparison
    /// is not a form; the page modal fetches the draft page and copies the element matching
    /// <see cref="ControlModalRemotePage.Selector"/> into the dialog body.
    /// </para>
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Edit>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Edit>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Cache]
    public sealed class ObjectDraftChangesModalFragment : ControlModalRemotePage, IFragmentControl<ControlModalRemotePage>
    {
        /// <summary>
        /// The well-known id the ⋯ "show changes" item targets.
        /// </summary>
        public const string ModalId = "modal-draft-changes";

        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectDraftChangesModalFragment(IFragmentContext fragmentContext)
            : base(ModalId)
        {
            FragmentContext = fragmentContext;

            Header = _ => "kleenestar.core:object.draft.changes.title";
            Selector = _ => $".{ObjectDraftChangesFragment.ContentClass}";
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
