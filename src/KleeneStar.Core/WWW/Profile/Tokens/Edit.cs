using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Profile.Tokens
{
    /// <summary>
    /// Hosts the form that renames a personal access token and changes the scopes it grants.
    /// Opened as a modal from the token list with the token's id in the <c>id</c> query
    /// parameter; the form itself is
    /// <see cref="WebFragment.Profile.ProfileTokenEditFormFragment"/>.
    /// </summary>
    [WebIcon<IconPen>]
    [Title("kleenestar.core:profile.tokens.edit.title")]
    [Scope<IScopeGeneral>]
    public sealed class Edit : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Edit()
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
