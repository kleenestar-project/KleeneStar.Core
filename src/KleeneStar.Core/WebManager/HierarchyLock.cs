using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Serializes the changes of one workspace's object hierarchy, so the check a move is
    /// validated against and the write that carries it out see the same tree.
    /// </summary>
    /// <remarks>
    /// Without it two moves - A into B and B into A - each pass the cycle check against the
    /// tree before the other one and together persist a cycle. The lock is taken per
    /// workspace, because a parent never lies outside its child's workspace: moves in different
    /// workspaces never wait for each other.
    /// <para>
    /// It is two locks in one. A process-local monitor serializes the threads of this instance
    /// and costs nothing measurable. When the server runs clustered
    /// (<see cref="WebExpress.WebCore.WebCluster.IClusterManager.IsClustered"/>), the cluster
    /// lock named <c>kleenestar.core.hierarchy:{workspace}</c> is taken in addition, so replicas
    /// sharing the database serialize as well - cluster-wide only with a shared state path, the
    /// same caveat the database startup lock carries.
    /// </para>
    /// <para>
    /// The lock is re-entrant on the thread that holds it, so an operation that moves several
    /// objects may hold it around calls that take it again.
    /// </para>
    /// </remarks>
    public static class HierarchyLock
    {
        /// <summary>
        /// The prefix of the cluster lock name; the workspace id follows it.
        /// </summary>
        public const string LockPrefix = "kleenestar.core.hierarchy:";

        /// <summary>
        /// How long the cluster lock holds when the instance holding it dies without releasing it.
        /// </summary>
        public static readonly TimeSpan LockLifetime = TimeSpan.FromMinutes(1);

        /// <summary>
        /// How long a move waits for another move of the same workspace before it gives up.
        /// </summary>
        public static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

        private static readonly ConcurrentDictionary<Guid, object> _gates = new();

        [ThreadStatic]
        private static Dictionary<Guid, int> _held;

        /// <summary>
        /// Takes the hierarchy lock of the supplied workspace and returns the handle that
        /// releases it.
        /// </summary>
        /// <param name="workspaceId">The workspace whose hierarchy is about to change.</param>
        /// <returns>The handle; dispose it to release the lock.</returns>
        /// <exception cref="HierarchyConflictException">
        /// Another move of the workspace held the lock past <see cref="LockTimeout"/>.
        /// </exception>
        public static IDisposable Enter(Guid workspaceId)
        {
            // the handles keep this thread's table: the lock is released where it was taken
            var held = _held ??= [];

            if (held.TryGetValue(workspaceId, out var depth))
            {
                // re-entered on the holding thread: the outer handle releases
                held[workspaceId] = depth + 1;

                return new Release(() => held[workspaceId]--);
            }

            var gate = _gates.GetOrAdd(workspaceId, _ => new object());

            if (!Monitor.TryEnter(gate, LockTimeout))
            {
                throw Busy();
            }

            IDisposable cluster = null;

            try
            {
                var clusterManager = CoreHub.ComponentHub?.ClusterManager;

                if (clusterManager?.IsClustered == true)
                {
                    cluster = clusterManager.Lock(LockPrefix + workspaceId.ToString("N"), LockLifetime, LockTimeout)
                        ?? throw Busy();
                }
            }
            catch
            {
                Monitor.Exit(gate);

                throw;
            }

            held[workspaceId] = 1;

            return new Release(() =>
            {
                if (--held[workspaceId] > 0)
                {
                    return;
                }

                held.Remove(workspaceId);

                try
                {
                    cluster?.Dispose();
                }
                finally
                {
                    Monitor.Exit(gate);
                }
            });
        }

        /// <summary>
        /// Creates the conflict raised when the lock could not be taken in time.
        /// </summary>
        /// <returns>The exception.</returns>
        private static HierarchyConflictException Busy()
        {
            return new HierarchyConflictException("Another change of this workspace's hierarchy is in progress; try again.");
        }

        /// <summary>
        /// Runs an action once, when disposed.
        /// </summary>
        /// <param name="action">The action to run.</param>
        private sealed class Release(Action action) : IDisposable
        {
            private Action _action = action;

            /// <summary>
            /// Runs the action the first time it is called.
            /// </summary>
            public void Dispose()
            {
                Interlocked.Exchange(ref _action, null)?.Invoke();
            }
        }
    }
}
