using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebLog;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Maintains and answers the materialized paths (<see cref="Model.Entities.ObjectPath"/>) -
    /// the derived index that turns subtree questions into one range query
    /// (<c>kleenestar.hierarchy.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Writes</b> follow every change of <c>ParentId</c>: <see cref="ObjectManager.Add"/> writes
    /// the new object's row, <see cref="ObjectManager.Update"/> rewrites the moved object's row and
    /// - in one statement - the paths of its whole subtree. A failed index write never fails the
    /// object write; it turns the index off (<see cref="Ready"/>) until the next start rebuilds it.
    /// </para>
    /// <para>
    /// <b>Reads</b> use the index only while <see cref="Ready"/>: set by <see cref="Rebuild"/>, which
    /// <c>KleeneStarApplication.Run</c> calls after the hierarchy verifier broke any cycle, so a
    /// stale or half-filled index (an object written by the seeder, a manual edit, a crash between
    /// the two writes) is never trusted. Until then every caller keeps its level-by-level walk over
    /// <c>ParentId</c>, which stays the only truth.
    /// </para>
    /// </remarks>
    public static class ObjectPathIndex
    {
        private static volatile bool _ready;

        /// <summary>
        /// Gets whether the index is complete and may be read. Set by <see cref="Rebuild"/>, cleared
        /// by a failed write or by <see cref="Disable"/>.
        /// </summary>
        public static bool Ready => _ready;

        /// <summary>
        /// Turns the index off until the next rebuild.
        /// </summary>
        public static void Disable()
        {
            _ready = false;
        }

        /// <summary>
        /// Computes the path of an object below the supplied parent.
        /// </summary>
        /// <param name="parentId">The parent, or <see langword="null"/> for the root.</param>
        /// <returns>The path and the depth.</returns>
        public static (string Path, int Depth) Compute(Guid? parentId)
        {
            if (parentId is not Guid id)
            {
                return ("/", 0);
            }

            var row = ModelHub.GetObjectPath(id);

            if (row is not null)
            {
                return (row.Path + ModelHub.PathSegment(id), row.Depth + 1);
            }

            // the parent has no row (yet): walk its ancestry over the parent relation, unrestricted -
            // the path is the tree's, not what the caller may see
            var ancestors = new List<Guid> { id };
            var visited = new HashSet<Guid> { id };
            var maxDepth = (CoreHub.HierarchySettings ?? new HierarchySettings()).EffectiveMaxDepth;
            var current = ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.Id == id)).FirstOrDefault();

            while (current?.ParentId is Guid next && visited.Add(next) && ancestors.Count <= maxDepth)
            {
                ancestors.Add(next);
                current = ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.Id == next)).FirstOrDefault();
            }

            ancestors.Reverse();

            return ("/" + string.Concat(ancestors.Select(ModelHub.PathSegment)), ancestors.Count);
        }

        /// <summary>
        /// Writes the row of a new object.
        /// </summary>
        /// <param name="object">The object just added.</param>
        public static void Added(ObjectEntity @object)
        {
            Guard(() =>
            {
                var (path, depth) = Compute(@object.ParentId);

                ModelHub.SetObjectPath(@object.Id, @object.WorkspaceId, path, depth);
            });
        }

        /// <summary>
        /// Rewrites the rows of a moved object and its subtree.
        /// </summary>
        /// <param name="object">The object, with its new parent.</param>
        public static void Moved(ObjectEntity @object)
        {
            Guard(() =>
            {
                var old = ModelHub.GetObjectPath(@object.Id);
                var (path, depth) = Compute(@object.ParentId);

                ModelHub.SetObjectPath(@object.Id, @object.WorkspaceId, path, depth);

                if (old is null)
                {
                    // the subtree's rows cannot be shifted from a prefix nobody wrote; the next
                    // rebuild sets them, and until then nobody reads them
                    _ready = false;

                    return;
                }

                var segment = ModelHub.PathSegment(@object.Id);

                ModelHub.MoveObjectPaths(@object.WorkspaceId, old.Path + segment, path + segment, depth - old.Depth);
            });
        }

        /// <summary>
        /// Returns the prefix every path below the object starts with, or <see langword="null"/>
        /// when the index is off or the object has no row.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <param name="row">Its row.</param>
        /// <returns>The prefix.</returns>
        private static string Prefix(Guid objectId, out Model.Entities.ObjectPath row)
        {
            row = _ready ? ModelHub.GetObjectPath(objectId) : null;

            return row is null ? null : row.Path + ModelHub.PathSegment(objectId);
        }

        /// <summary>
        /// Returns the ids below an object (shallow first, at most <paramref name="limit"/>), or
        /// <see langword="null"/> when the index cannot answer.
        /// </summary>
        /// <param name="objectId">The root.</param>
        /// <param name="limit">The most ids.</param>
        /// <returns>The ids, or <see langword="null"/>.</returns>
        public static IReadOnlyList<(Guid ObjectId, int Depth)> Subtree(Guid objectId, int limit)
        {
            var prefix = Prefix(objectId, out var row);

            return prefix is null ? null : ModelHub.GetSubtree(row.WorkspaceId, prefix, limit);
        }

        /// <summary>
        /// Counts the objects below an object, or <see langword="null"/> when the index cannot
        /// answer.
        /// </summary>
        /// <param name="objectId">The root.</param>
        /// <returns>The count, or <see langword="null"/>.</returns>
        public static int? Count(Guid objectId)
        {
            var prefix = Prefix(objectId, out var row);

            return prefix is null ? null : ModelHub.CountSubtree(row.WorkspaceId, prefix);
        }

        /// <summary>
        /// Returns how many levels the subtree below an object reaches (0 without children), or
        /// <see langword="null"/> when the index cannot answer.
        /// </summary>
        /// <param name="objectId">The root.</param>
        /// <returns>The height, or <see langword="null"/>.</returns>
        public static int? Height(Guid objectId)
        {
            var prefix = Prefix(objectId, out var row);

            if (prefix is null)
            {
                return null;
            }

            var deepest = ModelHub.MaxDepthBelow(row.WorkspaceId, prefix);

            return deepest < 0 ? 0 : deepest - row.Depth;
        }

        /// <summary>
        /// Returns the ancestors of an object, root first, or <see langword="null"/> when the index
        /// cannot answer.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The ancestor ids, or <see langword="null"/>.</returns>
        public static IReadOnlyList<Guid> Ancestors(Guid objectId)
        {
            var row = _ready ? ModelHub.GetObjectPath(objectId) : null;

            return row is null ? null : Parse(row.Path);
        }

        /// <summary>
        /// Splits a path into its ancestor ids.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The ids, root first.</returns>
        public static IReadOnlyList<Guid> Parse(string path)
        {
            return [.. (path ?? string.Empty)
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Guid.TryParseExact(x, "N", out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)];
        }

        /// <summary>
        /// Rebuilds the index from the parent relation: writes the rows that are missing or differ,
        /// removes the rows of objects that are gone, and turns the index on.
        /// </summary>
        /// <remarks>
        /// One scan of <c>(Id, ParentId, WorkspaceId)</c> and one of the path rows; only differences
        /// are written, so a consistent store costs two reads. An object inside a cycle (the
        /// verifier runs first and breaks them) gets no row, and the index stays off.
        /// </remarks>
        /// <param name="log">The log for the report, or <see langword="null"/>.</param>
        /// <returns>The number of rows written or removed.</returns>
        public static int Rebuild(ILog log = null)
        {
            _ready = false;

            var hierarchy = ModelHub.GetObjectHierarchy();
            var parents = hierarchy.ToDictionary(x => x.Id, x => x.ParentId);
            var workspaces = hierarchy.ToDictionary(x => x.Id, x => x.WorkspaceId);
            var memo = new Dictionary<Guid, (string Path, int Depth)>();
            var broken = 0;

            (string Path, int Depth)? Resolve(Guid id, HashSet<Guid> visiting)
            {
                if (memo.TryGetValue(id, out var known))
                {
                    return known;
                }

                if (!visiting.Add(id))
                {
                    return null;
                }

                (string Path, int Depth) result;

                if (parents.TryGetValue(id, out var parent) && parent is Guid parentId && parents.ContainsKey(parentId))
                {
                    var above = Resolve(parentId, visiting);

                    if (above is null)
                    {
                        return null;
                    }

                    result = (above.Value.Path + ModelHub.PathSegment(parentId), above.Value.Depth + 1);
                }
                else
                {
                    result = ("/", 0);
                }

                memo[id] = result;

                return result;
            }

            var wanted = new List<(Guid, Guid, string, int)>();

            foreach (var id in parents.Keys)
            {
                var path = Resolve(id, []);

                if (path is null)
                {
                    broken++;
                    continue;
                }

                wanted.Add((id, workspaces[id], path.Value.Path, path.Value.Depth));
            }

            var stale = ModelHub.GetAllObjectPaths()
                .Select(x => x.ObjectId)
                .Where(x => !parents.ContainsKey(x))
                .ToList();

            var changes = ModelHub.WriteObjectPaths(wanted, stale);

            if (broken == 0)
            {
                _ready = true;
            }

            log?.Info($"Object paths: {wanted.Count} objects, {changes} row(s) written or removed{(broken > 0 ? $", {broken} in a cycle - index stays off" : string.Empty)}.");

            return changes;
        }

        /// <summary>
        /// Runs an index write without letting it fail the object write.
        /// </summary>
        /// <param name="write">The write.</param>
        private static void Guard(Action write)
        {
            try
            {
                write();
            }
            catch (Exception ex)
            {
                _ready = false;
                CoreHub.HttpServerContext?.Log?.Exception(ex);
            }
        }
    }
}
