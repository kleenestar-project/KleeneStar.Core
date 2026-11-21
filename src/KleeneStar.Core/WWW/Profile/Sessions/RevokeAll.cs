using KleeneStar.Core.WebManager;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW.Profile.Sessions
{
    /// <summary>
    /// Ends every session of the calling identity except the current one and returns to the
    /// list. The URL is <c>/profile/sessions/revokeall</c>.
    /// </summary>
    [Scope<IScopeGeneral>]
    public sealed class RevokeAll : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly IIdentitySessionManager _sessionManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="sessionManager">
        /// The manager used to end the other sessions. Cannot be null.
        /// </param>
        public RevokeAll(IIdentitySessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Processing of the resource: signs the account out everywhere except here, then
        /// redirects to the list.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            _sessionManager.RevokeOthers(renderContext?.Request);

            throw new RedirectException(CoreHub.GetUri<Index>());
        }
    }
}
