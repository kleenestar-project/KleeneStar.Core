using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
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
    /// Entry in the actions menu of a document or a post that opens the object's relations
    /// (<see cref="WWW.Issue._objectkey_.Relations"/>).
    /// </summary>
    /// <remarks>
    /// The counterpart of <see cref="ObjectItemAttachmentsMoreFragment"/> for what the object is
    /// linked to, and like it the only way in: the toolbar over the text used to carry the same
    /// destination. It opens in a modal (<see cref="ObjectRelationsModalFragment"/>) rather than
    /// navigating, so the text stays on the screen behind it.
    /// </remarks>
    [Section<SectionHeadlineMorePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Order(21)]
    [Cache]
    public sealed class ObjectItemRelationsMoreFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public ObjectItemRelationsMoreFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconLinks();
            Text = renderContext => ObjectSidePageLink.Label
            (
                I18N.Translate(renderContext, "kleenestar.core:object.relations.card.header"),
                ObjectSidePageLink.CountRelations(ObjectSidePageLink.ResolveObject(renderContext))
            );
            PrimaryAction = renderContext => new ActionModal
            (
                ObjectRelationsModalFragment.ModalId,
                ObjectSidePageLink.ResolveRelationsUri(renderContext),
                TypeModalSize.Large
            );
        }

        /// <summary>
        /// Convert the fragment to HTML. Returns <c>null</c> when the request addresses no
        /// object.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return ObjectSidePageLink.ResolveRelationsUri(renderContext) is null
                ? null
                : base.Render(renderContext, visualTree);
        }
    }
}
