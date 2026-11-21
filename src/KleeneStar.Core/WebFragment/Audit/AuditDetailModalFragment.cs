using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Audit
{
    /// <summary>
    /// Renders the body-level page-modal container that hosts one audit event in full. The row
    /// option of the audit table targets this container by id.
    /// </summary>
    /// <remarks>
    /// A <c>wx-webui-modal-page</c> (not <c>-form</c>) modal is required because the content is
    /// a read-only panel rather than a <c>&lt;form&gt;</c>: the form modal injects only the
    /// children of a form element and would leave the dialog empty. The selector is a class
    /// rather than an id because a fragment's element id is derived from its fragment id and
    /// cannot be chosen. Rendered into <see cref="SectionBodySecondary"/> so the overlay sits at
    /// body level rather than nested inside a content column that could clip it.
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Audit.Index>]
    [Cache]
    public sealed class AuditDetailModalFragment : ControlModalRemotePage, IFragmentControl<ControlModalRemotePage>
    {
        /// <summary>
        /// The well-known id the row option of the audit table targets.
        /// </summary>
        public const string ModalId = "modal-audit";

        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public AuditDetailModalFragment(IFragmentContext fragmentContext)
            : base(ModalId)
        {
            FragmentContext = fragmentContext;

            Header = _ => "kleenestar.core:audit.detail.title";
            Selector = _ => $".{AuditDetailFragment.ContentClass}";
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
