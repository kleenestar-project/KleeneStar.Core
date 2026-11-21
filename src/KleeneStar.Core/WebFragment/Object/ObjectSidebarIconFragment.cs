using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Represents the read-only workspace icon shown at the top of the sidebar on the
    /// kind overviews and the per-kind detail pages. It always displays the workspace
    /// icon so the detail pages carry the same workspace-navigation sidebar as their
    /// overview; the workspace is resolved from the route on the overviews and from the
    /// addressed object on a detail page.
    /// </summary>
    [Section<SectionSidebarPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Cache]
    public sealed class ObjectSidebarIconFragment : FragmentControlSidebarItemIcon
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation.
        /// Cannot be null.
        /// </param>
        /// <param name="objectManager">
        /// The object manager used to resolve the workspace of the addressed object on the
        /// detail pages. Cannot be null.
        /// </param>
        public ObjectSidebarIconFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;

            // the workspace icon is read-only wherever this fragment appears
            IconEdit = _ => false;
            Icon = renderContext => GetWorkspaceIcon(renderContext);
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the workspace icon: from the workspace-key route parameter on the
        /// overviews, otherwise from the workspace of the object addressed by the
        /// object-key parameter on a detail page.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information about the current HTTP request.
        /// </param>
        /// <returns>The workspace icon, or <see langword="null"/> when unresolvable.</returns>
        private IIcon GetWorkspaceIcon(IRenderControlContext renderContext)
        {
            var workspaceKey = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey?.Value);

            if (workspace is null)
            {
                var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
                workspace = _objectManager.GetObjectByKey(objectKey?.Value)?.Workspace;
            }

            return workspace?.Icon;
        }
    }
}
