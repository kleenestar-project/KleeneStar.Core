using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Shared base for the sidebar links that lead from the workspace objects pages to
    /// the per-kind overviews (documents, blogs, issues, …). A concrete subclass binds
    /// one <see cref="IObjectKind"/> descriptor and contributes the section, scope, and
    /// order attributes; everything else — icon, label, target URI, and active-state
    /// highlighting — derives from the descriptor.
    /// </summary>
    /// <remarks>
    /// Add-ons introduce the sidebar link of a new kind by deriving from this base with
    /// their registered descriptor and scoping the subclass to the objects pages the
    /// link should appear on (see <see cref="Documents.DocumentSidebarLinkFragment"/>
    /// for the reference wiring).
    /// </remarks>
    public abstract class ObjectKindSidebarLinkFragment : FragmentControlSidebarItemLink
    {
        private readonly IObjectKind _kind;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        /// <param name="kind">
        /// The kind descriptor the link represents. Cannot be null.
        /// </param>
        protected ObjectKindSidebarLinkFragment(IFragmentContext fragmentContext, IObjectKind kind)
            : base(fragmentContext)
        {
            _kind = kind;

            Icon = _ => _kind.Icon;
            Text = _ => _kind.Label;
            Uri = ResolveUri;
            Active = renderContext => IsActive(renderContext)
                ? TypeActive.Active
                : TypeActive.None;
            Badge = ResolveBadge;
            BadgeColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Secondary);
        }

        /// <summary>
        /// Resolves the badge: the number of active objects of the kind in the workspace.
        /// </summary>
        /// <remarks>
        /// A kind without objects shows no badge rather than a zero. The link is only
        /// rendered where a class of the kind exists, so an empty count means "configured
        /// but nothing filed yet" — a neutral state the sidebar does not need to shout
        /// about, and one a "0" would read as an error next to the populated kinds.
        /// </remarks>
        /// <param name="renderContext">
        /// The rendering context that provides information about the current HTTP request.
        /// </param>
        /// <returns>The count, or null when the kind holds nothing.</returns>
        private string ResolveBadge(IRenderControlContext renderContext)
        {
            var count = ObjectKindScope.Count(renderContext?.Request, _kind.Key);

            return CountBadgeFormat.Format(count, renderContext?.Request?.Culture);
        }

        /// <summary>
        /// Resolves the link target: the kind's overview page bound to the workspace of
        /// the current request. On the kind overviews the workspace key comes straight
        /// from the route; on the object detail page it is resolved through the
        /// addressed object.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information about the current HTTP request.
        /// </param>
        /// <returns>The bound overview URI, or <see langword="null"/> when unresolvable.</returns>
        private IUri ResolveUri(IRenderControlContext renderContext)
        {
            var uri = _kind.OverviewUri;

            if (uri is null)
            {
                return null;
            }

            var workspaceKey = ResolveWorkspaceKey(renderContext);

            return workspaceKey is null
                ? uri.BindParameters(renderContext.Request)
                : uri.BindParameters(new WorkspaceKeyParameter(workspaceKey));
        }

        /// <summary>
        /// Resolves the workspace key of the current request: directly from the
        /// workspace-key parameter when present, otherwise through the workspace of the
        /// object addressed by the object-key parameter.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information about the current HTTP request.
        /// </param>
        /// <returns>The workspace key, or <see langword="null"/> when unresolvable.</returns>
        private static string ResolveWorkspaceKey(IRenderControlContext renderContext)
        {
            var workspaceParameter = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();

            if (!string.IsNullOrWhiteSpace(workspaceParameter?.Value))
            {
                return workspaceParameter.Value;
            }

            var objectParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var objectEntity = CoreHub.ObjectManager.GetObjectByKey(objectParameter?.Value);

            return objectEntity?.Workspace?.Key;
        }

        /// <summary>
        /// Determines whether the current request addresses the kind's overview page, in
        /// which case the link is highlighted as active.
        /// </summary>
        /// <param name="renderContext">
        /// The rendering context that provides information about the current HTTP request.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the current request URI matches the kind's overview
        /// URI; otherwise, <see langword="false"/>.
        /// </returns>
        private bool IsActive(IRenderControlContext renderContext)
        {
            var target = ResolveUri(renderContext);
            var targetPath = string.Join("/", target?.PathSegments ?? []);
            var currentPath = string.Join("/", renderContext.Request.Uri.PathSegments ?? []);

            return string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
