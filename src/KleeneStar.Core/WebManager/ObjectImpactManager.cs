using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Answers what changing an object touches: a breadth-first walk of the relations that carry
    /// a workflow effect, in the direction that effect runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which relations propagate.</b> Only the ones whose type declares an effect. A relation
    /// that merely references or resembles states that two records have something to do with
    /// each other; it says nothing about one governing the other, and following it would answer
    /// <em>what is this connected to</em> - which the relation surface already answers - instead
    /// of <em>what does changing this touch</em>. The three effects are read from the published
    /// catalog rather than from the stored row, so a type an administrator re-declared takes
    /// effect at once, exactly as it does in the workflow guard.
    /// </para>
    /// <para>
    /// <b>Which way each one propagates</b> is the part that cannot be guessed, and it is not the
    /// same for all three. It is taken from what the rules already implement
    /// (<see cref="ObjectRelationWorkflowRules"/>), because an analysis that disagreed with the
    /// guard would predict consequences the system then refuses to have:
    /// </para>
    /// <list type="table">
    /// <item>
    /// <term><see cref="RelationEffect.BlocksCompletion"/></term>
    /// <description>the source blocks the target, so the consequence runs source → target: while
    /// the blocker is open, what it blocks cannot be finished.</description>
    /// </item>
    /// <item>
    /// <term><see cref="RelationEffect.ClosesItem"/></term>
    /// <description>the source is closed with its target, so the consequence runs target →
    /// source: closing the original settles the duplicate.</description>
    /// </item>
    /// <item>
    /// <term><see cref="RelationEffect.AggregatesProgress"/></term>
    /// <description>the source rolls up its targets, so the consequence runs target → source:
    /// moving a child moves what the parent reports.</description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>What the walk refuses to do.</b> It does not follow an obsolete relation (kept for the
    /// history, and history propagates nothing), it does not leave the installation (an external
    /// address has no state a change could touch), it does not visit an object twice, and it does
    /// not read an object the caller may not see - which also ends every path that would have run
    /// through it, because a chain learned through a record one may not open is that record,
    /// spelled differently.
    /// </para>
    /// </remarks>
    public sealed class ObjectImpactManager : IObjectImpactManager
    {
        /// <summary>
        /// How many relations the analysis follows when the caller names no depth.
        /// </summary>
        /// <remarks>
        /// Three steps is what a reader can still follow in a picture and still a chain rather
        /// than a neighbourhood: the thing changed, what it governs, and what that governs in
        /// turn. Deeper is available on request and rarely tells a different story, because a
        /// relation graph widens much faster than it deepens.
        /// </remarks>
        public const int DefaultDepth = 3;

        /// <summary>
        /// The deepest walk the manager performs, whatever it is asked for.
        /// </summary>
        public const int MaximumDepth = 10;

        /// <summary>
        /// How many objects one analysis may answer with.
        /// </summary>
        /// <remarks>
        /// A hub object - the release everything is filed under - reaches most of a workspace
        /// within two steps, and a picture of it says nothing that a list of the workspace would
        /// not have said. The budget stops the walk at the point where the answer has stopped
        /// being one, and the result says that it did.
        /// </remarks>
        public const int NodeBudget = 250;

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectImpactManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Walks the relations of the supplied object and answers what a change to it reaches.
        /// </summary>
        /// <param name="objectId">The object a change would be made to.</param>
        /// <param name="depth">How many relations to follow.</param>
        /// <returns>The reachable objects and the relations they were reached through.</returns>
        public ObjectImpactResult Analyze(Guid objectId, int depth = DefaultDepth)
        {
            var limit = Math.Clamp(depth, 1, MaximumDepth);
            var origin = CoreHub.ObjectManager?.GetObject(objectId);

            if (origin is null)
            {
                return new ObjectImpactResult() { Depth = limit };
            }

            var nodes = new List<ObjectImpactNode>() { new(origin, 0, RelationEffect.None) };
            var edges = new List<ObjectImpactEdge>();
            var visited = new HashSet<Guid>() { origin.Id };
            var frontier = new Queue<(Model.Entities.Object Object, int Distance)>();
            var truncated = false;

            frontier.Enqueue((origin, 0));

            while (frontier.Count > 0)
            {
                var (current, distance) = frontier.Dequeue();

                if (distance >= limit)
                {
                    continue;
                }

                foreach (var step in StepsFrom(current.Id))
                {
                    // the relation is part of the answer even when it leads somewhere already
                    // known: the reader is looking at a graph, and an edge that was left out
                    // because its far end was reached earlier would make a cycle look like a
                    // chain
                    var neighbour = visited.Contains(step.To)
                        ? nodes.FirstOrDefault(x => x.Object.Id == step.To)?.Object
                        : Reach(step.To);

                    if (neighbour is null)
                    {
                        continue;
                    }

                    edges.Add(new ObjectImpactEdge(step.RelationId, current.Id, neighbour.Id, step.TypeKey, step.Effect));

                    if (!visited.Add(neighbour.Id))
                    {
                        continue;
                    }

                    if (nodes.Count >= NodeBudget)
                    {
                        truncated = true;
                        break;
                    }

                    nodes.Add(new ObjectImpactNode(neighbour, distance + 1, step.Effect));
                    frontier.Enqueue((neighbour, distance + 1));
                }

                if (truncated)
                {
                    break;
                }
            }

            return new ObjectImpactResult()
            {
                Origin = origin,
                Nodes = nodes,
                Edges = edges,
                Depth = limit,
                Truncated = truncated
            };
        }

        /// <summary>
        /// Returns the steps a consequence can take from the supplied object: one per relation
        /// that carries an effect away from it.
        /// </summary>
        /// <remarks>
        /// A relation the object stands at the wrong end of yields nothing - the consequence of
        /// that one runs towards the object rather than away from it, and is answered when the
        /// analysis is run for the other end.
        /// </remarks>
        /// <param name="objectId">The object being left.</param>
        /// <returns>The steps, each naming the relation it belongs to.</returns>
        private static IEnumerable<(Guid RelationId, Guid To, string TypeKey, RelationEffect Effect)> StepsFrom(Guid objectId)
        {
            var relations = CoreHub.ObjectRelationManager?.GetRelations(objectId) ?? [];

            foreach (var relation in relations)
            {
                if (relation is null || relation.Status == RelationStatus.Obsolete)
                {
                    continue;
                }

                // an external address is an end, not a station: it has no state a change could
                // touch and no relations of its own to carry one further
                if (relation.TargetObjectId is not Guid targetId)
                {
                    continue;
                }

                var effect = EffectOf(relation.TypeKey);
                var isSource = relation.SourceObjectId == objectId;
                var carries = effect switch
                {
                    // the blocker is the source, so what it holds back is downstream of it
                    RelationEffect.BlocksCompletion => isSource,

                    // the follower is the source, so it is downstream of the target it follows
                    RelationEffect.ClosesItem => !isSource,

                    // the parent is the source and reports what its targets do
                    RelationEffect.AggregatesProgress => !isSource,

                    _ => false
                };

                if (!carries)
                {
                    continue;
                }

                yield return
                (
                    relation.Id,
                    isSource ? targetId : relation.SourceObjectId,
                    relation.TypeKey,
                    effect
                );
            }
        }

        /// <summary>
        /// Reads the object at the far end of a step, as the caller is allowed to see it.
        /// </summary>
        /// <param name="objectId">The object to read.</param>
        /// <returns>The object, or <see langword="null"/> when it is gone or classified beyond
        /// the caller's clearance.</returns>
        private static Model.Entities.Object Reach(Guid objectId)
        {
            return CoreHub.ObjectManager?.GetObject(objectId);
        }

        /// <summary>
        /// Returns the effect a relation carries, read from the published catalog rather than
        /// from the stored row.
        /// </summary>
        /// <param name="typeKey">The key of the relation type.</param>
        /// <returns>The effect, or <see cref="RelationEffect.None"/> for an unknown type.</returns>
        private static RelationEffect EffectOf(string typeKey)
        {
            return RelationRegistry.GetType(typeKey)?.Effect ?? RelationEffect.None;
        }

        /// <summary>
        /// Releases resources held by this manager.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
