using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// The shared base of the sidebar links that switch between the template overview and the
    /// hierarchy overview of a workspace. A link leads to its overview bound to the workspace of
    /// the route, is active on that overview and carries the number of listed records as badge.
    /// </summary>
    public abstract class TemplateAreaSidebarLinkFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected TemplateAreaSidebarLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Uri = ResolveUri;
            Active = renderContext => IsActive(renderContext)
                ? TypeActive.Active
                : TypeActive.None;
            Badge = ResolveBadge;
            BadgeColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Secondary);
        }

        /// <summary>
        /// Returns the address of the overview the link leads to, unbound.
        /// </summary>
        /// <returns>The address, or null.</returns>
        protected abstract IUri GetOverviewUri();

        /// <summary>
        /// Counts the records the overview lists for a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The count.</returns>
        protected abstract int Count(Guid workspaceId);

        /// <summary>
        /// Resolves the link target: the overview bound to the workspace of the route.
        /// </summary>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The bound address, or null.</returns>
        private IUri ResolveUri(IRenderControlContext renderContext)
        {
            return GetOverviewUri()?.BindParameters(renderContext.Request);
        }

        /// <summary>
        /// Resolves the badge: the number of listed records, left off when there are none.
        /// </summary>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The formatted count, or null.</returns>
        private string ResolveBadge(IRenderControlContext renderContext)
        {
            var key = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key);

            return workspace is null
                ? null
                : CountBadgeFormat.Format(Count(workspace.Id), renderContext.Request.Culture);
        }

        /// <summary>
        /// Determines whether the request addresses the link's overview.
        /// </summary>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>True when the link's overview is shown.</returns>
        private bool IsActive(IRenderControlContext renderContext)
        {
            var targetPath = string.Join("/", ResolveUri(renderContext)?.PathSegments ?? []);
            var currentPath = string.Join("/", renderContext.Request.Uri.PathSegments ?? []);

            return string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
