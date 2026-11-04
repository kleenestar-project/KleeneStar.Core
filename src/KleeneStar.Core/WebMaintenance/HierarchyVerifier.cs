using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.WebLog;

namespace KleeneStar.Core.WebMaintenance
{
    /// <summary>
    /// The outcome of one run of the <see cref="HierarchyVerifier"/>.
    /// </summary>
    /// <param name="Objects">The number of objects inspected.</param>
    /// <param name="Cycles">The number of parent cycles found.</param>
    /// <param name="Repaired">The ids of the objects detached to break them.</param>
    /// <param name="TooDeep">The number of objects deeper than the configured limit (reported only).</param>
    /// <param name="CrossWorkspace">The number of objects whose parent lies in another workspace (reported only).</param>
    public sealed record HierarchyVerification(int Objects, int Cycles, IReadOnlyList<Guid> Repaired, int TooDeep, int CrossWorkspace);

    /// <summary>
    /// Checks the object hierarchy once per start and repairs what would otherwise make it
    /// wrong for good: parent cycles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The moves are guarded now (<see cref="ObjectManager.SetParent(Guid, Guid?)"/> under the
    /// <see cref="HierarchyLock"/>), but data written before - or by hand, or by an import -
    /// may carry a cycle. The readers survive one (every walk keeps a visited set), but the
    /// objects of a cycle hang below nothing reachable: no tree shows them, and no move can
    /// take any of them out, since every target inside the cycle is "a descendant".
    /// </para>
    /// <para>
    /// A cycle is broken by detaching <b>one</b> member to the root of its workspace: the one
    /// changed last, because the most recent change is the likeliest to have closed it. The
    /// repair is an ordinary update through the object manager, so it lands in the object's
    /// history and in the audit log - attributed to nobody, since nobody did it.
    /// </para>
    /// <para>
    /// Objects deeper than <see cref="HierarchySettings.MaxDepth"/> and parents in another
    /// workspace are only reported: both still form a tree every surface can show, and
    /// flattening somebody's structure because a limit changed would destroy more than it
    /// protects. New moves cannot produce either.
    /// </para>
    /// <para>
    /// It reads four columns of every object once (<c>ModelHub.GetObjectHierarchy</c>) and
    /// writes only for a cycle, so on healthy data it costs one query.
    /// </para>
    /// </remarks>
    public static class HierarchyVerifier
    {
        /// <summary>
        /// Checks the hierarchy and breaks every parent cycle.
        /// </summary>
        /// <param name="log">The log to report to, or <c>null</c>.</param>
        /// <returns>What was found and repaired.</returns>
        public static HierarchyVerification Run(ILog log)
        {
            var links = ModelHub.GetObjectHierarchy().ToDictionary(x => x.Id);
            var cycles = FindCycles(links.ToDictionary(x => x.Key, x => x.Value.ParentId));
            var repaired = new List<Guid>();

            foreach (var cycle in cycles)
            {
                var victim = cycle
                    .OrderByDescending(id => links[id].Updated)
                    .ThenBy(id => id)
                    .First();

                bool detached;

                try
                {
                    detached = Detach(victim, links[victim].WorkspaceId);
                }
                catch (InvalidOperationException ex)
                {
                    // e.g. a container whose name is taken at the root: report and go on, one
                    // cycle that cannot be broken here must not keep the others in place
                    log?.Warning(string.Format
                    (
                        CultureInfo.InvariantCulture,
                        "Object hierarchy: could not break the parent cycle at object {0}: {1}",
                        victim,
                        ex.Message
                    ));

                    continue;
                }

                if (detached)
                {
                    repaired.Add(victim);

                    var updated = links[victim];
                    links[victim] = (updated.Id, null, updated.WorkspaceId, updated.Updated);

                    log?.Warning(string.Format
                    (
                        CultureInfo.InvariantCulture,
                        "Object hierarchy: broke a parent cycle of {0} object(s) by moving object {1} to the root of its workspace.",
                        cycle.Count,
                        victim
                    ));
                }
            }

            var maxDepth = (CoreHub.HierarchySettings ?? new HierarchySettings()).EffectiveMaxDepth;
            var tooDeep = CountTooDeep(links.ToDictionary(x => x.Key, x => x.Value.ParentId), maxDepth);
            var crossWorkspace = links.Values.Count(x => x.ParentId is Guid parentId
                && links.TryGetValue(parentId, out var parent)
                && parent.WorkspaceId != x.WorkspaceId);

            if (tooDeep > 0)
            {
                log?.Warning(string.Format
                (
                    CultureInfo.InvariantCulture,
                    "Object hierarchy: {0} object(s) sit deeper than the configured limit of {1} levels; they stay where they are, but nothing can be moved below them.",
                    tooDeep,
                    maxDepth
                ));
            }

            if (crossWorkspace > 0)
            {
                log?.Warning(string.Format
                (
                    CultureInfo.InvariantCulture,
                    "Object hierarchy: {0} object(s) have a parent in another workspace; such links are no longer created.",
                    crossWorkspace
                ));
            }

            return new HierarchyVerification(links.Count, cycles.Count, repaired, tooDeep, crossWorkspace);
        }

        /// <summary>
        /// Finds every parent cycle of the supplied relation.
        /// </summary>
        /// <param name="parents">The parent of every object (<c>null</c> for a root).</param>
        /// <returns>The cycles, each as the ids of its members.</returns>
        public static IReadOnlyList<IReadOnlyList<Guid>> FindCycles(IReadOnlyDictionary<Guid, Guid?> parents)
        {
            // 1 = on the path being followed, 2 = settled (its chain was followed to the end)
            var state = new Dictionary<Guid, int>();
            var cycles = new List<IReadOnlyList<Guid>>();

            foreach (var start in parents.Keys)
            {
                if (state.ContainsKey(start))
                {
                    continue;
                }

                var path = new List<Guid>();
                Guid? current = start;

                while (current is Guid id && parents.ContainsKey(id) && !state.ContainsKey(id))
                {
                    state[id] = 1;
                    path.Add(id);
                    current = parents[id];
                }

                // the chain ran into itself: everything from that object on is the cycle
                if (current is Guid hit && state.TryGetValue(hit, out var mark) && mark == 1)
                {
                    var from = path.IndexOf(hit);
                    cycles.Add(path.GetRange(from, path.Count - from));
                }

                foreach (var id in path)
                {
                    state[id] = 2;
                }
            }

            return cycles;
        }

        /// <summary>
        /// Counts the objects that sit deeper than the supplied limit, a root being level one.
        /// </summary>
        /// <param name="parents">The parent of every object, free of cycles.</param>
        /// <param name="maxDepth">The deepest permitted level.</param>
        /// <returns>The number of objects below the limit.</returns>
        private static int CountTooDeep(IReadOnlyDictionary<Guid, Guid?> parents, int maxDepth)
        {
            var depths = new Dictionary<Guid, int>();
            var count = 0;

            foreach (var start in parents.Keys)
            {
                var chain = new List<Guid>();
                Guid? current = start;
                var known = 0;

                while (current is Guid id && parents.ContainsKey(id))
                {
                    if (depths.TryGetValue(id, out known))
                    {
                        break;
                    }

                    if (chain.Count > parents.Count)
                    {
                        // a cycle the repair could not break; it was reported already
                        known = 0;
                        chain.Clear();
                        break;
                    }

                    chain.Add(id);
                    current = parents[id];
                    known = 0;
                }

                for (var i = chain.Count - 1; i >= 0; i--)
                {
                    known++;
                    depths[chain[i]] = known;

                    if (known > maxDepth)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Moves an object to the root of its workspace as an act of the system.
        /// </summary>
        /// <param name="objectId">The object to detach.</param>
        /// <param name="workspaceId">Its workspace, whose hierarchy lock is taken.</param>
        /// <returns><see langword="true"/> when the object was detached.</returns>
        private static bool Detach(Guid objectId, Guid workspaceId)
        {
            // the repair is the system's act: it reads every object, and it is attributed to
            // nobody rather than to whoever changed the object last
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();
            using var nobody = CoreHub.SessionManager?.BeginIdentity(Guid.Empty);
            using var hierarchy = HierarchyLock.Enter(workspaceId);

            var @object = CoreHub.ObjectManager.GetObject(objectId);

            if (@object?.ParentId is null)
            {
                return false;
            }

            @object.ParentId = null;
            @object.UpdaterId = null;
            @object.Updated = DateTime.UtcNow;

            CoreHub.ObjectManager.Update(@object);

            return true;
        }
    }
}
