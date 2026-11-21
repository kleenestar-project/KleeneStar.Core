using KleeneStar.Core.WebManager;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WWW.Profile.Tokens
{
    /// <summary>
    /// Deletes the token named by the <c>id</c> query parameter and returns to the list. The URL
    /// is <c>/profile/tokens/delete?id={guid}</c>.
    /// </summary>
    /// <remarks>
    /// Deleting removes the record entirely, whereas <see cref="Revoke"/> keeps it as a
    /// disabled entry. The list therefore offers deleting only for tokens that are already
    /// revoked or expired — a token still in use is revoked first.
    /// </remarks>
    [Scope<IScopeGeneral>]
    public sealed class Delete : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IAccessTokenManager _accessTokenManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="accessTokenManager">
        /// The manager used to resolve and delete the addressed token. Cannot be null.
        /// </param>
        public Delete(IAccessTokenManager accessTokenManager)
        {
            _accessTokenManager = accessTokenManager;
        }

        /// <summary>
        /// Processing of the resource: deletes the addressed token, then redirects to the list.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var id = renderContext?.Request?.GetParameter<ParameterId>()?.Value;
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request);

            if (Guid.TryParse(id, out var tokenId))
            {
                var token = _accessTokenManager.GetAccessToken(tokenId);

                // a caller may only delete their own tokens
                if (token is not null && token.OwnerId == ownerId)
                {
                    _accessTokenManager.Remove(tokenId);
                }
            }

            throw new RedirectException(CoreHub.GetUri<Index>());
        }
    }
}
