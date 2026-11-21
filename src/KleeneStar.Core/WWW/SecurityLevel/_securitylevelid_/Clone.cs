using KleeneStar.Core.WebAttribute;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.SecurityLevel._securitylevelid_
{
    /// <summary>
    /// Represents a page that provides a form to clone a security level of a class.
    /// </summary>
    [WebIcon<IconClone>]
    [Title("kleenestar.core:securitylevel.clone.title")]
    [Scope<IScopeGeneral>]
    public sealed class Clone : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Clone()
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
