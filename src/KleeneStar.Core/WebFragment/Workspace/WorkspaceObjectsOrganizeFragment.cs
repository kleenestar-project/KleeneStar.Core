using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Modal content fragment that renders the objects of a workspace as a movable
    /// <see cref="ControlTree"/> so they can be re-parented by drag and drop. The tree is
    /// wrapped in a marker element carrying the move endpoint; the client controller
    /// (<c>Assets/js/objectmovetree.js</c>, loaded by
    /// <see cref="WorkspaceObjectsOrganizeMoreFragment"/>) persists each move via that endpoint.
    /// </summary>
    /// <remarks>
    /// The tree mirrors the parent/child containment of the workspace's objects (capped at
    /// <see cref="MaxItems"/>, the "top 200", to keep the dialog responsive); objects whose
    /// parent is outside the fetched set become roots. A visited set keeps the recursion
    /// cycle-safe. Nodes deliberately carry no navigation URI so a drag gesture is never
    /// interpreted as a click-through.
    /// </remarks>
    [Title("kleenestar.core:workspace.organize.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Organize>]
    [Policy<WorkspaceViewPolicy>]
    [Cache]
    public sealed class WorkspaceObjectsOrganizeFragment : FragmentControlPanel
    {
        /// <summary>
        /// The maximum number of objects fetched for the tree ("top 200").
        /// </summary>
        private const int MaxItems = 200;

        private readonly IObjectManager _objectManager;
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to fetch the workspace objects.</param>
        /// <param name="workspaceManager">The workspace manager used to resolve the workspace from the request.</param>
        public WorkspaceObjectsOrganizeFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Renders the organize dialog content. Returns <c>null</c> when the fragment's render conditions
        /// exclude it or when no workspace can be resolved from the request.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            if (workspace is null)
            {
                return null;
            }

            var objects = GetObjects(workspace.Id);

            var tree = new ControlTree("workspace-organize-tree")
            {
                Layout = _ => TypeLayoutTree.Default,
                Movable = _ => true
            };

            var treeHtml = tree.Render(renderContext, visualTree, BuildNodes(objects));

            // wrap the tree in the marker element the client move-controller binds to; it reads
            // the endpoint from data-rest-uri and posts each drag-and-drop move there.
            var wrapper = new HtmlElementTextContentDiv()
            {
                Class = "wx-kleenestar-object-movetree"
            };
            wrapper.AddUserAttribute("data-rest-uri", CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Move>()?.ToString());
            wrapper.Add(treeHtml);

            // the instruction explains the gesture, not the hierarchy - it belongs above the
            // section rather than inside it, where it would be the first thing folded away
            var help = new ControlText("workspace-organize-help")
            {
                Text = _ => "kleenestar.core:workspace.organize.help",
                Format = _ => TypeFormatText.Paragraph,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            };

            // the tree draws its own structure through its indentation, so the section adds no
            // guide line beside it; and a dialog whose only content could be folded away offers
            // a control that leads nowhere, so the label stays a heading. the count is worth
            // showing because the dialog caps at MaxItems - it says whether this is everything.
            var section = new ControlSection("workspace-organize-section")
            {
                Header = _ => "kleenestar.core:workspace.organize.header",
                HeaderIcon = _ => new IconSitemap(),
                Layout = _ => TypeLayoutSection.Rule,
                Collapsible = _ => false,
                Guide = _ => false,
                Badge = objects.Count > 0 ? _ => objects.Count.ToString(CultureInfo.InvariantCulture) : null
            };

            if (objects.Count == 0)
            {
                section.Add(new ControlText("workspace-organize-empty")
                {
                    Text = _ => "kleenestar.core:workspace.organize.empty",
                    Format = _ => TypeFormatText.Paragraph
                });

                return WrapForModal(renderContext, visualTree, help, section);
            }

            section.Add(new ControlHtml("workspace-organize-tree-host")
            {
                Html = _ => wrapper.ToString()
            });

            return WrapForModal(renderContext, visualTree, help, section);
        }

        /// <summary>
        /// Wraps the rendered content in the host element the page-modal extracts. The id must
        /// match the <c>Selector</c> of <c>ObjectOrganizeModalFragment</c> (<c>#kleenestar-organize-content</c>),
        /// whose modal controller copies this element's children into the dialog body - so every
        /// control handed in here arrives in the dialog, in the order it was given.
        /// </summary>
        /// <param name="controls">The controls to host, in reading order.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The host element wrapping the rendered controls.</returns>
        private static IHtmlNode WrapForModal(IRenderControlContext renderContext, IVisualTreeControl visualTree, params IControl[] controls)
        {
            var host = new HtmlElementTextContentDiv()
            {
                Id = "kleenestar-organize-content"
            };

            foreach (var control in controls)
            {
                host.Add(control.Render(renderContext, visualTree));
            }

            return host;
        }

        /// <summary>
        /// Fetches the workspace's objects, ordered by key and capped at <see cref="MaxItems"/>.
        /// </summary>
        /// <param name="workspaceId">The owning workspace id.</param>
        /// <returns>The capped, ordered set of workspace objects. The list may be empty.</returns>
        private IReadOnlyList<Model.Entities.Object> GetObjects(Guid workspaceId)
        {
            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .OrderByAsc(x => x.Key)
                .WithPaging(0, MaxItems);

            return [.. _objectManager.GetObjects(query)];
        }

        /// <summary>
        /// Builds the root tree nodes from the fetched objects, grouping every object under its
        /// parent (when the parent is part of the fetched set) and promoting the rest to roots.
        /// The traversal is cycle-safe.
        /// </summary>
        /// <param name="objects">The fetched workspace objects.</param>
        /// <returns>The root tree items, each carrying its descendant sub-tree.</returns>
        private static IEnumerable<ControlTreeItem> BuildNodes(IReadOnlyList<Model.Entities.Object> objects)
        {
            var ids = new HashSet<Guid>(objects.Select(x => x.Id));

            var childrenByParent = objects
                .Where(x => x.ParentId.HasValue && x.ParentId.Value != x.Id && ids.Contains(x.ParentId.Value))
                .GroupBy(x => x.ParentId.Value)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Model.Entities.Object>)[.. g]);

            var visited = new HashSet<Guid>();
            var roots = new List<ControlTreeItem>();

            foreach (var root in objects.Where(x => !x.ParentId.HasValue || !ids.Contains(x.ParentId.Value)))
            {
                if (visited.Add(root.Id))
                {
                    roots.Add(BuildNode(root, childrenByParent, visited, 0));
                }
            }

            return roots;
        }

        /// <summary>
        /// Builds a single tree node for the supplied object and, recursively, its children. The
        /// node id is the object key so the client move-controller and the move endpoint can round
        /// trip it directly.
        /// </summary>
        /// <param name="obj">The object to render as a node.</param>
        /// <param name="childrenByParent">The parent-id to children lookup built from the fetched set.</param>
        /// <param name="visited">The set of already-rendered object ids, guarding against cycles.</param>
        /// <param name="depth">The current nesting depth; the root level is expanded by default.</param>
        /// <returns>The tree item representing <paramref name="obj"/> and its sub-tree.</returns>
        private static ControlTreeItem BuildNode(Model.Entities.Object obj, IReadOnlyDictionary<Guid, IReadOnlyList<Model.Entities.Object>> childrenByParent, ISet<Guid> visited, int depth)
        {
            var children = new List<ControlTreeItem>();

            if (childrenByParent.TryGetValue(obj.Id, out var list))
            {
                foreach (var child in list)
                {
                    if (visited.Add(child.Id))
                    {
                        children.Add(BuildNode(child, childrenByParent, visited, depth + 1));
                    }
                }
            }

            return new ControlTreeItem(obj.Key, [.. children])
            {
                Text = _ => $"{obj.Key}  {obj.Summary}",
                Tooltip = _ => obj.Summary,
                Icon = _ => ObjectIcon.Resolve(obj, new IconObject()),
                Expand = _ => depth == 0
            };
        }
    }
}
