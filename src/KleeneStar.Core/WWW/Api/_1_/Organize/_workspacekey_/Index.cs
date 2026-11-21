using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Organize._workspacekey_
{
    /// <summary>
    /// The object tree of a workspace for the organize dialog, served one level per request to
    /// the framework's <c>ControlDataTree</c> (<see cref="RestApiTree{TIndexItem}"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A node is an object of the route's workspace, its id the object id, its label key and
    /// summary, its icon the class icon (<see cref="ObjectIcon"/>); a node carries no address, so
    /// a drag is never read as a click-through. Every read goes through
    /// <see cref="WebManager.IObjectManager"/>, so security levels and content visibility bite.
    /// The roots are the objects without a parent and the objects whose parent the caller may not
    /// read; the home page of the workspace is no node.
    /// </para>
    /// <para>
    /// <c>PUT { node, target, position }</c> re-parents the node: under the target for
    /// <c>child</c>, beside it (under the target's visible parent, or at the root) for
    /// <c>above</c> and <c>below</c>; objects have no order among siblings. The move goes through
    /// <see cref="ObjectMove.Attempt"/>, so authorization (<c>ContentAuthorization.MayMove</c>),
    /// the hierarchy rules and the hierarchy lock are those of <c>/api/1/objects/move</c>. A
    /// refused move is a <see cref="RestApiRefusal"/>: the framework answers it with 400 and its
    /// message, and the tree takes the move back.
    /// </para>
    /// </remarks>
    [WorkspaceKeySegment]
    [Title("kleenestar.core:workspace.organize.title")]
    [Cache]
    public sealed class Index : RestApiTree<ObjectEntity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Returns the nodes below a parent, ordered by key.
        /// </summary>
        /// <param name="parentId">The id of the parent node, or <see langword="null"/> for the roots.</param>
        /// <param name="context">The query context (unused; objects are read through the object manager).</param>
        /// <param name="request">The request carrying the workspace key.</param>
        /// <returns>The nodes.</returns>
        protected override IEnumerable<RestApiTreeNode> RetrieveChildren(string parentId, IQueryContext context, IRequest request)
        {
            var workspace = ResolveWorkspace(request);

            if (workspace is null)
            {
                return [];
            }

            List<ObjectEntity> objects;

            if (string.IsNullOrEmpty(parentId))
            {
                objects = Roots(workspace.Id);
            }
            else if (Guid.TryParse(parentId, out var id) && Read(id, workspace.Id) is not null)
            {
                objects = [.. CoreHub.ObjectManager.GetObjects(new Query<ObjectEntity>()
                    .WhereEquals(x => x.WorkspaceId, workspace.Id)
                    .Where(x => x.ParentId == id))];
            }
            else
            {
                return [];
            }

            objects = [.. objects
                .Where(x => !CoreHub.WorkspaceManager.IsHome(x))
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

            var ids = objects.Select(x => x.Id).ToList();
            var parents = ids.Count == 0
                ? []
                : CoreHub.ObjectManager.GetObjects(new Query<ObjectEntity>()
                    .WhereEquals(x => x.WorkspaceId, workspace.Id)
                    .Where(x => x.ParentId != null && ids.Contains(x.ParentId.Value)))
                    .Select(x => x.ParentId.Value)
                    .ToHashSet();

            return [.. objects.Select(x => new RestApiTreeNode
            {
                Id = x.Id.ToString(),
                Label = x.Key + "  " + x.Summary,
                Icon = ObjectIcon.Resolve(x)?.Uri?.ToString(),
                HasChildren = parents.Contains(x.Id)
            })];
        }

        /// <summary>
        /// Returns the id of the visible parent of a node.
        /// </summary>
        /// <param name="id">The node id.</param>
        /// <param name="context">The query context (unused).</param>
        /// <param name="request">The request carrying the workspace key.</param>
        /// <returns>The parent id, or <see langword="null"/> for a root node.</returns>
        protected override string RetrieveParent(string id, IQueryContext context, IRequest request)
        {
            var workspace = ResolveWorkspace(request);

            if (workspace is null || !Guid.TryParse(id, out var nodeId))
            {
                return null;
            }

            return VisibleParent(Read(nodeId, workspace.Id), workspace.Id)?.Id.ToString();
        }

        /// <summary>
        /// Re-parents a node through <see cref="ObjectMove.Attempt"/>.
        /// </summary>
        /// <param name="node">The id of the moved node.</param>
        /// <param name="target">The id of the node it is placed at.</param>
        /// <param name="position">Above, below or child of the target.</param>
        /// <param name="context">The query context (unused).</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the object was moved.</returns>
        /// <exception cref="RestApiRefusal">The move is refused; the message says why.</exception>
        protected override bool MoveNode(string node, string target, RestApiTreeMovePosition position, IQueryContext context, IRequest request)
        {
            var workspace = ResolveWorkspace(request);
            var moved = workspace is not null && Guid.TryParse(node, out var nodeId) ? Read(nodeId, workspace.Id) : null;
            var anchor = workspace is not null && Guid.TryParse(target, out var targetId) ? Read(targetId, workspace.Id) : null;

            if (moved is null || anchor is null)
            {
                throw new RestApiRefusal("The object does not exist.");
            }

            var parent = position == RestApiTreeMovePosition.Child ? anchor : VisibleParent(anchor, workspace.Id);

            if (parent?.Id == moved.ParentId)
            {
                return true;
            }

            var result = ObjectMove.Attempt(moved, parent, default, request);

            return result.Status == 200
                ? true
                : throw new RestApiRefusal(result.Error ?? "The move was refused.");
        }

        /// <summary>
        /// Resolves the workspace of the route.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        private static Model.Entities.Workspace ResolveWorkspace(IRequest request)
        {
            return CoreHub.WorkspaceManager.GetWorkspaceByKey(request?.GetParameter<WorkspaceKeyParameter>()?.Value);
        }

        /// <summary>
        /// Reads an object of the workspace through the object manager.
        /// </summary>
        /// <param name="id">The object id.</param>
        /// <param name="workspaceId">The workspace id.</param>
        /// <returns>The object, or <see langword="null"/> when it is unknown, unreadable or of another workspace.</returns>
        private static ObjectEntity Read(Guid id, Guid workspaceId)
        {
            var @object = CoreHub.ObjectManager.GetObject(id);

            return @object?.WorkspaceId == workspaceId ? @object : null;
        }

        /// <summary>
        /// Returns the parent of an object when the caller may read it.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="workspaceId">The workspace id.</param>
        /// <returns>The parent, or <see langword="null"/> for a root.</returns>
        private static ObjectEntity VisibleParent(ObjectEntity @object, Guid workspaceId)
        {
            return @object?.ParentId is Guid parentId ? Read(parentId, workspaceId) : null;
        }

        /// <summary>
        /// Returns the root objects of a workspace: those without a parent and those whose parent
        /// the caller may not read.
        /// </summary>
        /// <param name="workspaceId">The workspace id.</param>
        /// <returns>The root objects.</returns>
        private static List<ObjectEntity> Roots(Guid workspaceId)
        {
            var roots = CoreHub.ObjectManager.GetObjects(new Query<ObjectEntity>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .Where(x => x.ParentId == null)).ToList();

            var children = CoreHub.ObjectManager.GetObjects(new Query<ObjectEntity>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .Where(x => x.ParentId != null)).ToList();

            if (children.Count == 0)
            {
                return roots;
            }

            var parentIds = children.Select(x => x.ParentId.Value).Distinct().ToList();
            var visible = CoreHub.ObjectManager.GetObjects(new Query<ObjectEntity>()
                .Where(x => parentIds.Contains(x.Id)))
                .Select(x => x.Id)
                .ToHashSet();

            roots.AddRange(children.Where(x => !visible.Contains(x.ParentId.Value)));

            return roots;
        }
    }
}
