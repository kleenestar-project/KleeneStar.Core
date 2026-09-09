using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Shared base for the "recently opened" section of a kind overview / detail sidebar:
    /// a section header followed by the calling identity's most recently opened objects of
    /// the fragment's kind within the current workspace, each linking to its detail page.
    /// The section sits below the flat kind links. A concrete subclass fixes the
    /// <see cref="Kind"/> it lists and scopes itself to the pages the section appears on.
    /// </summary>
    /// <remarks>
    /// The recents are the workspace-local slice of the per-identity visit history
    /// (<see cref="IObjectManager.GetRecentObjects(Guid, int, string)"/>), capped at
    /// <see cref="MaxItems"/>. The header is always visible; without recents a disabled
    /// empty entry is shown instead, so the section communicates where recently opened
    /// objects will appear. Header and entries are emitted as siblings via
    /// <see cref="HtmlList"/> so the sidebar parser picks each of them up as a regular
    /// sidebar item (a wrapper element would be skipped). Entry URIs are frozen through
    /// <see cref="ObjectKindCatalog.ResolveDetailUriFrozen"/> so the sidebar's request
    /// re-bind on a detail page cannot repoint every entry at the current object.
    /// </remarks>
    public abstract class ObjectRecentSidebarFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// The maximum number of recently opened objects shown in the section.
        /// </summary>
        private const int MaxItems = 10;

        /// <summary>
        /// The number of recent visits scanned before the workspace filter is applied.
        /// Kept comfortably above <see cref="MaxItems"/> so a workspace with recent
        /// activity still fills the section even when the caller has visited objects in
        /// other workspaces more recently.
        /// </summary>
        private const int ScanCount = 100;

        private readonly IObjectManager _objectManager;
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Gets the persisted kind key the section lists (e.g.
        /// <see cref="Model.Entities.ObjectKind.Issue"/>).
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        /// <param name="objectManager">
        /// The object manager used to retrieve the recent objects. Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the workspace from the request. Cannot be null.
        /// </param>
        protected ObjectRecentSidebarFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Renders the section: the header followed by the recent object entries, or — when
        /// the workspace holds no recently opened objects of the kind — a disabled empty
        /// entry. Returns <c>null</c> only when the fragment's render conditions exclude it.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c> when suppressed.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var recents = GetRecentObjects(renderContext);
            var currentKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value;

            var header = new ControlSidebarItemHeader(Id + "-header")
            {
                Text = _ => "kleenestar.core:object.recent.label"
            };

            var nodes = new HtmlList(header.Render(renderContext, visualTree));

            if (recents.Count == 0)
            {
                var empty = new ControlSidebarItemLink(Id + "-empty")
                {
                    Text = _ => "kleenestar.core:object.recent.none.label",
                    Active = _ => TypeActive.Disabled
                };

                nodes.Add(empty.Render(renderContext, visualTree));

                return nodes;
            }

            foreach (var entry in recents)
            {
                var link = new ControlSidebarItemLink(Id + "-" + entry.Id.ToString("N"))
                {
                    Text = _ => entry.Summary,
                    Tooltip = _ => entry.Key,
                    // frozen so the sidebar's request re-bind cannot repoint every entry at
                    // the currently displayed object (see ResolveDetailUriFrozen)
                    Uri = _ => ObjectKindCatalog.ResolveDetailUriFrozen(entry),
                    Icon = _ => ObjectIcon.Resolve(entry, new IconObject()),
                    Active = _ => string.Equals(entry.Key, currentKey, StringComparison.OrdinalIgnoreCase)
                        ? TypeActive.Active
                        : TypeActive.None
                };

                nodes.Add(link.Render(renderContext, visualTree));
            }

            return nodes;
        }

        /// <summary>
        /// Fetches the calling identity's most recently opened objects of the kind that
        /// belong to the workspace resolved from the request, capped at
        /// <see cref="MaxItems"/>. Returns an empty list when no workspace or identity can
        /// be resolved.
        /// </summary>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The capped, recency-ordered set of objects. The list may be empty.</returns>
        private IReadOnlyList<Model.Entities.Object> GetRecentObjects(IRenderControlContext renderContext)
        {
            var request = renderContext?.Request;
            var keyParameter = request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            // on the object detail page the route carries the object key, not the workspace
            // key, so fall back to the workspace of the addressed object
            if (workspace is null)
            {
                var objectParameter = request?.GetParameter<ObjectKeyParameter>();
                workspace = _objectManager.GetObjectByKey(objectParameter?.Value)?.Workspace;
            }

            if (workspace is null)
            {
                return [];
            }

            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return [.. _objectManager.GetRecentObjects(ownerId, ScanCount, Kind)
                .Where(x => x.WorkspaceId == workspace.Id)
                .Take(MaxItems)];
        }
    }
}
