using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Headline button on the prose reading views (document and blog detail) that opens the
    /// prose editor standing on the same page.
    /// </summary>
    /// <remarks>
    /// The editor is a dialog that already stands on this page - the framework's
    /// <see cref="WebExpress.WebApp.WebControl.ControlDataModalEditor"/>, contributed by
    /// <see cref="ObjectProseEditorFragment"/> - so the button only opens it. Nothing is
    /// fetched and nothing is composed here: the text the dialog opens on, the draft it
    /// resumes and the publication it ends with are all the editor's.
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<ProseRendererCondition>]
    [Cache]
    public sealed class ObjectProseEditButtonFragment : FragmentControlButtonLink
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the addressed
        /// object from the URL-bound object key.</param>
        public ObjectProseEditButtonFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;

            Text = _ => "kleenestar.core:object.prose.edit.label";
            Icon = _ => new IconPen();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            PrimaryAction = _ => new ActionModal(ObjectProseEditorFragmentBase.ModalId);
        }

        /// <summary>
        /// Convert the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();

            return _objectManager.GetObjectByKey(keyParameter?.Value) is null
                ? null
                : base.Render(renderContext, visualTree);
        }
    }
}
