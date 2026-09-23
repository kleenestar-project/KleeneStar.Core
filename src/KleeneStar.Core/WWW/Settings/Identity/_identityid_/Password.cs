using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings.Identity._identityid_
{
    /// <summary>
    /// Represents the dialog that issues a one-time link setting the password of an internal
    /// account. The form is <see cref="WebFragment.Identity.IdentityPasswordResetFormFragment"/>.
    /// </summary>
    [WebIcon<IconKey>]
    [Title("kleenestar.core:setting.identity.password.title")]
    [Scope<IScopeGeneral>]
    public sealed class Password : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Password()
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
