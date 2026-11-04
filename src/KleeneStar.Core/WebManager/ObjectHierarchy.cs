using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Structural reads of the object hierarchy (<c>Object.ParentId</c>) that the recursive
    /// operations, the transfer and WQL <c>within(…)</c> share: an object read without narrowing
    /// and the ids of a subtree.
    /// </summary>
    /// <remarks>
    /// Membership in a subtree is structural, not a question of what a caller may see: the ids
    /// are answered unrestricted, and whoever asked reads the rows narrowed. The path index
    /// (<see cref="ObjectPathIndex"/>) answers a subtree in one range query while it is ready;
    /// otherwise the levels are walked, bounded by the configured depth.
    /// </remarks>
    public static class ObjectHierarchy
    {
        /// <summary>
        /// The most ids a subtree read answers.
        /// </summary>
        public const int SubtreeLimit = 50000;

        /// <summary>
        /// Reads an object without narrowing - for the structural checks the system makes on its
        /// own behalf.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        public static ObjectEntity Read(Guid objectId)
        {
            return ModelHub.GetObjects(new Query<ObjectEntity>()
                    .Where(x => x.Id == objectId)
                    .WithPaging(0, 1))
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the ids of every object below an object (not the object itself), at most
        /// <see cref="SubtreeLimit"/>.
        /// </summary>
        /// <param name="objectId">The root of the subtree.</param>
        /// <returns>The ids.</returns>
        public static IReadOnlySet<Guid> GetDescendantIds(Guid objectId)
        {
            var result = new HashSet<Guid>();

            if (objectId == Guid.Empty)
            {
                return result;
            }

            // one range query when the path index is complete
            if (ObjectPathIndex.Subtree(objectId, SubtreeLimit) is { } indexed)
            {
                result.UnionWith(indexed.Select(x => x.ObjectId));

                return result;
            }

            var visited = new HashSet<Guid> { objectId };
            var frontier = new List<Guid> { objectId };
            var depth = (CoreHub.HierarchySettings ?? new HierarchySettings()).EffectiveMaxDepth;

            for (var level = 0; level < depth && frontier.Count > 0 && result.Count < SubtreeLimit; level++)
            {
                var ids = frontier;

                frontier = [.. ModelHub.GetObjects(new Query<ObjectEntity>()
                        .Where(x => x.ParentId != null && ids.Contains(x.ParentId.Value)))
                    .Select(x => x.Id)
                    .Where(visited.Add)
                    .Take(SubtreeLimit - result.Count)];

                result.UnionWith(frontier);
            }

            return result;
        }
    }
}
