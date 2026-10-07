using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPolicies;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// The class-tree section of the asset overview sidebar: a section header ("Asset
    /// classes") followed by the workspace's asset classes as hierarchical sidebar links
    /// mirroring their inheritance. A click on a class opens the asset overview narrowed to
    /// the objects of that class and of every class inheriting from it
    /// (<see cref="ObjectClassFilter"/>); the plain "Assets" kind link above leads back to all
    /// of them.
    /// </summary>
    /// <remarks>
    /// The tree is drawn from <see cref="Model.Entities.Class.InheritedId"/>, not from
    /// <see cref="Model.Entities.Class.ParentId"/>: a class whose base class is not one of the listed ones -
    /// a class of another workspace, a class of another kind, a class the caller may not read
    /// - is promoted to a root entry. A visited set guards the recursion against an
    /// inheritance cycle older data may carry. Abstract classes are listed like any other:
    /// they have no objects of their own, but their entry gathers those of their descendants.
    /// The links are not bound against the request, which would otherwise turn every entry's
    /// <c>class</c> into the one the page already shows (see <see cref="UnboundSidebarItemLink"/>).
    /// </remarks>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(10)]
    [Cache]
    public sealed class AssetSidebarClassTreeFragment : FragmentControlSidebarItemLink
    {
        private readonly IObjectManager _objectManager;
        private readonly IWorkspaceManager _workspaceManager;
        private readonly IClassManager _classManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        /// <param name="objectManager">
        /// The object manager used to resolve the asset a detail page shows. Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the workspace from the request. Cannot be null.
        /// </param>
        /// <param name="classManager">
        /// The class manager used to retrieve the asset classes. Cannot be null.
        /// </param>
        public AssetSidebarClassTreeFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IWorkspaceManager workspaceManager, IClassManager classManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _workspaceManager = workspaceManager;
            _classManager = classManager;
        }

        /// <summary>
        /// Renders the section: the header followed by the root entries of the class tree,
        /// or - when the workspace has no asset class - by a disabled empty entry. Returns
        /// <c>null</c> only when the fragment's render conditions exclude it.
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

            var request = renderContext?.Request;
            var objectKey = request?.GetParameter<ObjectKeyParameter>()?.Value;
            var asset = string.IsNullOrEmpty(objectKey) ? null : _objectManager.GetObjectByKey(objectKey);
            var workspace = _workspaceManager.GetWorkspaceByKey(request?.GetParameter<WorkspaceKeyParameter>()?.Value)
                ?? asset?.Workspace;
            var classes = GetClasses(workspace);

            // the overview highlights the class it is narrowed to, a detail page the class of
            // the asset it shows; either way the path down to it is expanded
            var currentId = asset?.ClassId ?? ObjectClassFilter.Resolve(request)?.Id;
            var activePath = ResolveActivePath(classes, currentId);

            var header = new ControlSidebarItemHeader(Id + "-header")
            {
                Text = _ => "kleenestar.core:object.kind.assets.classes.label"
            };

            var nodes = new HtmlList(header.Render(renderContext, visualTree));

            if (classes.Count == 0)
            {
                var empty = new ControlSidebarItemLink("asset-class-empty")
                {
                    Text = _ => "kleenestar.core:object.kind.assets.classes.none.label",
                    Active = _ => TypeActive.Disabled
                };

                nodes.Add(empty.Render(renderContext, visualTree));

                return nodes;
            }

            foreach (var entry in BuildEntries(workspace, classes, currentId, activePath))
            {
                nodes.Add(entry.Render(renderContext, visualTree));
            }

            return nodes;
        }

        /// <summary>
        /// Fetches the active asset classes of the workspace whose objects the caller may
        /// read, ordered by name.
        /// </summary>
        /// <param name="workspace">The workspace, or <c>null</c>.</param>
        /// <returns>The classes. The list may be empty.</returns>
        private IReadOnlyList<Model.Entities.Class> GetClasses(Model.Entities.Workspace workspace)
        {
            if (workspace is null)
            {
                return [];
            }

            var query = new Query<Model.Entities.Class>()
                .WhereEquals(x => x.WorkspaceId, workspace.Id);

            return [.. _classManager.GetClasses(query)
                .Where(x => x.State == ClassState.Active)
                .Where(x => ObjectKind.Normalize(x.Kind) == ObjectKind.Asset)
                .Where(x => ContentVisibility.MayReadObjectsOf(x.Id))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Resolves the set of class ids that must be expanded so the current class is
        /// visible: the class itself and each of its base classes within the listed set.
        /// </summary>
        /// <param name="classes">The listed classes.</param>
        /// <param name="currentId">The class the page shows, or <c>null</c>.</param>
        /// <returns>The ids on the path from a root down to the current class.</returns>
        private static IReadOnlySet<Guid> ResolveActivePath(IReadOnlyList<Model.Entities.Class> classes, Guid? currentId)
        {
            var path = new HashSet<Guid>();

            if (currentId is null)
            {
                return path;
            }

            var byId = classes.ToDictionary(x => x.Id);
            var cursor = byId.GetValueOrDefault(currentId.Value);

            // walk up the inheritance chain; the visited guard (path.Add) also breaks cycles
            while (cursor is not null && path.Add(cursor.Id))
            {
                cursor = cursor.InheritedId.HasValue
                    ? byId.GetValueOrDefault(cursor.InheritedId.Value)
                    : null;
            }

            return path;
        }

        /// <summary>
        /// Builds the root link entries, grouping every class under its base class (when the
        /// base class is listed) and promoting the rest to root entries.
        /// </summary>
        /// <param name="workspace">The workspace the overview belongs to.</param>
        /// <param name="classes">The listed classes.</param>
        /// <param name="currentId">The class the page shows, or <c>null</c>.</param>
        /// <param name="activePath">The ids to force-expand so the current class is visible.</param>
        /// <returns>The root entries, each carrying its derived classes.</returns>
        private static IEnumerable<IControlSidebarItem> BuildEntries(Model.Entities.Workspace workspace, IReadOnlyList<Model.Entities.Class> classes, Guid? currentId, IReadOnlySet<Guid> activePath)
        {
            var ids = classes.Select(x => x.Id).ToHashSet();

            var derivedByBase = classes
                .Where(x => x.InheritedId.HasValue && x.InheritedId.Value != x.Id && ids.Contains(x.InheritedId.Value))
                .GroupBy(x => x.InheritedId.Value)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Model.Entities.Class>)[.. g]);

            var visited = new HashSet<Guid>();

            foreach (var root in classes.Where(x => !x.InheritedId.HasValue || !ids.Contains(x.InheritedId.Value)))
            {
                if (visited.Add(root.Id))
                {
                    yield return BuildEntry(workspace, root, derivedByBase, visited, 0, currentId, activePath);
                }
            }
        }

        /// <summary>
        /// Builds a single link entry for a class and, recursively, the classes derived from
        /// it. Root entries start expanded so the first level of derivation is visible.
        /// </summary>
        /// <param name="workspace">The workspace the overview belongs to.</param>
        /// <param name="class">The class to render as an entry.</param>
        /// <param name="derivedByBase">The base-class-id to derived-classes lookup.</param>
        /// <param name="visited">The set of already-rendered class ids, guarding against cycles.</param>
        /// <param name="depth">The current nesting depth; the root level is expanded by default.</param>
        /// <param name="currentId">The class the page shows, or <c>null</c>.</param>
        /// <param name="activePath">The ids to force-expand so the current class is visible.</param>
        /// <returns>The link entry representing <paramref name="class"/> and its subtree.</returns>
        private static IControlSidebarItem BuildEntry(Model.Entities.Workspace workspace, Model.Entities.Class @class, IReadOnlyDictionary<Guid, IReadOnlyList<Model.Entities.Class>> derivedByBase, ISet<Guid> visited, int depth, Guid? currentId, IReadOnlySet<Guid> activePath)
        {
            var description = ProseText.ToPlainText(@class.Description);

            var entry = new UnboundSidebarItemLink("asset-class-" + @class.Id.ToString("N"))
            {
                Text = _ => @class.Name,
                Tooltip = _ => string.IsNullOrWhiteSpace(description) ? @class.Name : description,
                Uri = _ => ClassUri(workspace, @class),
                Icon = _ => (IIcon)@class.Icon ?? new IconCube(),
                Active = _ => @class.Id == currentId
                    ? TypeActive.Active
                    : TypeActive.None,
                Expanded = _ => depth == 0 || activePath.Contains(@class.Id)
            };

            if (derivedByBase.TryGetValue(@class.Id, out var derived))
            {
                foreach (var child in derived)
                {
                    if (visited.Add(child.Id))
                    {
                        entry.Add(BuildEntry(workspace, child, derivedByBase, visited, depth + 1, currentId, activePath));
                    }
                }
            }

            return entry;
        }

        /// <summary>
        /// Returns the address of the asset overview narrowed to a class.
        /// </summary>
        /// <param name="workspace">The workspace the overview belongs to.</param>
        /// <param name="class">The class.</param>
        /// <returns>The address.</returns>
        private static IUri ClassUri(Model.Entities.Workspace workspace, Model.Entities.Class @class)
        {
            // a fresh address on every call, so the query added here accumulates nowhere
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>()?
                .BindParameters(new WorkspaceKeyParameter(workspace.Key))?
                .Add(new UriQuery(ObjectClassFilter.Parameter, @class.Id.ToString()));
        }
    }
}
