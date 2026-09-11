using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Headline button on the reading view of a form-rendered object that leads to its edit
    /// route, where <see cref="ObjectFormEditFragment"/> draws the input mask.
    /// </summary>
    /// <remarks>
    /// It is the counterpart of <see cref="ObjectProseEditButtonFragment"/>, which stands on
    /// the same routes for prose-rendered objects, and the difference between the two is the
    /// whole difference between the renderers on the way in: prose opens a dialog over the
    /// page it was read on, because writing continues the reading; a mask is a page of its
    /// own, because filling in a form is a separate act with a save at the end of it.
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<FormRendererCondition>]
    [Cache]
    public sealed class ObjectFormEditButtonFragment : FragmentControlButtonLink
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the addressed
        /// object from the URL-bound object key.</param>
        public ObjectFormEditButtonFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;

            Text = _ => "kleenestar.core:object.renderer.form.edit.label";
            Icon = _ => new IconPen();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            Uri = renderContext => ObjectKindCatalog.ResolveEditUri(Resolve(renderContext));
        }

        /// <summary>
        /// Renders the button, or nothing when the request addresses no object or the
        /// object's kind has no dedicated edit route.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request) ||
                ObjectKindCatalog.ResolveEditUri(Resolve(renderContext)) is null)
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the object the request addresses.
        /// </summary>
        /// <param name="renderContext">The render context carrying the object key.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private Model.Entities.Object Resolve(IRenderControlContext renderContext)
        {
            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();

            return _objectManager.GetObjectByKey(keyParameter?.Value);
        }
    }
}
