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
    /// Dropdown item in the object headline 'more' overflow menu that toggles the
    /// calling identity's star on the current object. The label reflects the current
    /// state; following the link flips it via the
    /// <see cref="global::KleeneStar.Core.WWW.Issue._objectkey_.Favorite"/> redirect
    /// page. Starred objects surface in the issue overview's "starred" quickfilter.
    /// </summary>
    [Section<SectionHeadlineMorePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Cache]
    public sealed class ObjectItemFavoriteMoreFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        public ObjectItemFavoriteMoreFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = renderContext => IsFavorite(renderContext)
                ? "kleenestar.core:object.favorite.remove.label"
                : "kleenestar.core:object.favorite.add.label";
            Icon = _ => new IconStar();
            Uri = renderContext => CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Favorite>()?
                .BindParameters(renderContext.Request);
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
            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Determines whether the calling identity has starred the object addressed by
        /// the current request.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information about the current HTTP request.
        /// </param>
        /// <returns><see langword="true"/> when the object is starred.</returns>
        private static bool IsFavorite(IRenderControlContext renderContext)
        {
            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var objectEntity = CoreHub.ObjectManager.GetObjectByKey(keyParameter);

            if (objectEntity is null)
            {
                return false;
            }

            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

            return CoreHub.ObjectManager.IsFavorite(ownerId, objectEntity.Id);
        }
    }
}
