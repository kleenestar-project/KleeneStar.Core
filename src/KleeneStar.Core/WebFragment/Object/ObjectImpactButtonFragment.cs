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
    /// Button in the headline of the relation page that opens the impact analysis of the same
    /// object.
    /// </summary>
    /// <remarks>
    /// It sits where it does because that is where the question arises: a reader looking at what
    /// an object is connected to is one step away from asking what happens to those connections
    /// when the object moves, and the relation surface cannot answer it - it shows the relations
    /// of one object, and a consequence three steps out belongs to none of them.
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Relations>]
    [Order(5)]
    [Cache]
    public sealed class ObjectImpactButtonFragment : FragmentControlButtonLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectImpactButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:object.impact.open.label";
            Icon = _ => new IconShareNodes();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            Outline = _ => true;
            Uri = ObjectSidePageLink.ResolveImpactUri;
        }

        /// <summary>
        /// Renders the button, or nothing when the request addresses no object.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return ObjectSidePageLink.ResolveImpactUri(renderContext) is null
                ? null
                : base.Render(renderContext, visualTree);
        }
    }
}
