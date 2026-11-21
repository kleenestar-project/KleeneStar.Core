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
    /// Revokes the token named by the <c>id</c> query parameter and returns to the list. The URL
    /// is <c>/profile/tokens/revoke?id={guid}</c>.
    /// </summary>
    /// <remarks>
    /// A revoked token stops authenticating requests but stays in the list, so the owner can
    /// still see that it existed and what it was allowed to do. Deleting it outright is the
    /// separate <see cref="Delete"/> action.
    /// </remarks>
    [Scope<IScopeGeneral>]
    public sealed class Revoke : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IAccessTokenManager _accessTokenManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="accessTokenManager">
        /// The manager used to resolve and revoke the addressed token. Cannot be null.
        /// </param>
        public Revoke(IAccessTokenManager accessTokenManager)
        {
            _accessTokenManager = accessTokenManager;
        }

        /// <summary>
        /// Processing of the resource: revokes the addressed token, then redirects to the list.
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

                // a caller may only revoke their own tokens
                if (token is not null && token.OwnerId == ownerId)
                {
                    _accessTokenManager.Revoke(tokenId);
                }
            }

            throw new RedirectException(CoreHub.GetUri<Index>());
        }
    }
}
