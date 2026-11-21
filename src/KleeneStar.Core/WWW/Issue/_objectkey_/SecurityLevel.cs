using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// Represents the dialog that classifies a single object.
    /// </summary>
    /// <remarks>
    /// This is where the permission dialog of an object used to be. An object carries no grants
    /// of its own any more - who may see it follows from its security level - so the entry in
    /// the overflow menu leads here instead.
    /// </remarks>
    [WebIcon<IconShieldHalved>]
    [Title("kleenestar.core:securitylevel.object.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class SecurityLevel : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public SecurityLevel()
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
