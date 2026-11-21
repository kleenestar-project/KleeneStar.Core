using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// Represents the dialog that moves an object with its subtree into another workspace.
    /// </summary>
    /// <remarks>
    /// The dialog is the wizard <see cref="WebFragment.Object.ObjectTransferFormFragment"/>. The
    /// page serves every object kind, like the classification dialog beside it.
    /// </remarks>
    [WebIcon<IconShare>]
    [Title("kleenestar.core:transfer.dialog.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class Transfer : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Transfer()
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
