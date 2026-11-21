using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.SetPassword
{
    /// <summary>
    /// The page a one-time reset link opens: it sets the password of the internal account the
    /// link was issued for. The form is <see cref="WebFragment.Password.PasswordResetFormFragment"/>.
    /// </summary>
    /// <remarks>
    /// The visitor is nobody yet - they are about to set the password they will sign in with -
    /// so the page asks for no identity; the secret in the link is the authorization.
    /// </remarks>
    [WebIcon<IconKey>]
    [Title("kleenestar.core:password.reset.title")]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
