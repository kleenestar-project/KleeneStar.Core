using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
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
    /// The way out of the reduced object view: a headline button opening the full reading view
    /// of the object the pane is showing.
    /// </summary>
    /// <remarks>
    /// The reduced view is a summary, not a replacement - comments, attachments, the workflow
    /// transitions and the actions menu all stay behind on the reading view. A pane that shows
    /// less has to say where the rest is, or the omission reads as data that is missing rather
    /// than data that is one click away.
    /// <para>
    /// It sits in the headline rather than above the content: the pane's header is the one
    /// place a reader keeps in view while scrolling the summary, and putting the escape hatch
    /// there keeps it opposite the title instead of pushing the first content section down.
    /// </para>
    /// <para>
    /// The link leaves the frame instead of loading into it: the target is addressed by the
    /// kind of the object, so it is the issue view for an issue and the asset view for an
    /// asset, and it is meant to fill the window rather than the pane.
    /// </para>
    /// </remarks>
    [Section<SectionHeadlineSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(0)]
    [Cache]
    public sealed class ObjectPreviewOpenFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        public ObjectPreviewOpenFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Renders the button. Returns <c>null</c> when the fragment's render conditions exclude
        /// it, when no object can be resolved from the request, or when its kind has no reading
        /// view to open.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            // the request the pane was fetched with carries this object's key, and re-binding
            // would rewrite the trailing segment of any route that declares one - the frozen
            // form keeps the target pointing at the object the pane is showing
            var uri = CoreHub.ObjectKindManager.ResolveDetailUriFrozen(@object);

            if (uri is null)
            {
                return null;
            }

            return new ControlButtonLink("object-preview-open")
            {
                Text = ctx => I18N.Translate(ctx, "kleenestar.core:object.preview.open.label"),
                Tooltip = ctx => I18N.Translate(ctx, "kleenestar.core:object.preview.open.label"),
                Icon = _ => new IconArrowUpRightFromSquare(),
                Uri = _ => uri,
                Outline = _ => true,
                Size = _ => TypeSizeButton.Small,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None)
            }.Render(renderContext, visualTree);
        }
    }
}
