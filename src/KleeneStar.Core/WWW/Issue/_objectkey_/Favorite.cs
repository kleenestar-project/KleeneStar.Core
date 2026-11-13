using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// Toggles the calling identity's star on the addressed object and redirects back
    /// to the object detail page. The URL is <c>/object/{objectkey}/favorite</c>; the
    /// <c>{objectkey}</c> segment is declared by the sibling <see cref="Index"/> page,
    /// so this sibling must NOT redeclare it.
    /// </summary>
    /// <remarks>
    /// The toggle is reached from the object headline's more menu and from the issue
    /// overview's row menu, whose labels already reflect the current state, so a single
    /// navigating link is enough: opening the page flips the star and the subsequent
    /// redirect re-renders the detail page (and the issue overview's starred filter)
    /// with the new state. Persistence and the confirmation toast are owned by
    /// <see cref="IObjectManager.SetFavorite"/>.
    /// </remarks>
    [Scope<IScopeGeneral>]
    public sealed class Favorite : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="objectManager">
        /// The object manager used to resolve the object and toggle the star. Cannot be null.
        /// </param>
        public Favorite(IObjectManager objectManager)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Processing of the resource: flips the star of the addressed object for the
        /// calling identity, then redirects to the object detail page.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var keyParameter = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var objectEntity = _objectManager.GetObjectByKey(keyParameter);

            if (objectEntity is not null)
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
                var isFavorite = _objectManager.IsFavorite(ownerId, objectEntity.Id);
                _objectManager.SetFavorite(ownerId, objectEntity.Id, !isFavorite);
            }

            // back to the page the link was clicked on (detail page or the overview hosting the
            // preview pane); without a usable referrer, the detail page
            throw new RedirectException
            (
                global::KleeneStar.Core.WebRestApi.ReturnAddress.Resolve
                (
                    renderContext.Request,
                    CoreHub.GetUri<Index>()
                        .BindParameters(new ObjectKeyParameter(keyParameter?.Value))
                )
            );
        }
    }
}
