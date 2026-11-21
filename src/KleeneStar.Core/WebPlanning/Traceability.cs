using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebPlanning
{
    /// <summary>
    /// An object reached by a trace.
    /// </summary>
    /// <param name="Object">The object.</param>
    /// <param name="Distance">The hops from the origin.</param>
    /// <param name="Upstream">Whether it lies upstream - the origin is the target of the
    /// relations that lead to it - or downstream.</param>
    /// <param name="Via">The label of the relation it was reached by, read in the direction of the trace.</param>
    /// <param name="ParentId">The object it was reached from.</param>
    public sealed record TraceNode(ObjectEntity Object, int Distance, bool Upstream, string Via, Guid ParentId);

    /// <summary>
    /// The result of a trace.
    /// </summary>
    /// <param name="Origin">The object traced from.</param>
    /// <param name="Nodes">The objects reached, nearest first.</param>
    /// <param name="Hidden">Relations leading to objects the caller may not see; the trace stops there.</param>
    /// <param name="Truncated">Whether the trace stopped at its limit.</param>
    public sealed record TraceResult(ObjectEntity Origin, IReadOnlyList<TraceNode> Nodes, int Hidden, bool Truncated);

    /// <summary>
    /// Follows the relations of an object both ways - from a requirement down to the tasks that
    /// implement it, the changes that deliver them and the release that ships them, and from a
    /// release back up to why it exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A trace keeps its direction: downstream follows relations from source to target,
    /// upstream from target to source. Without that, a requirement would reach every sibling
    /// requirement of a shared release and the trace would say nothing. The relation types are
    /// whatever the installation uses - the trace needs no special ones.
    /// </para>
    /// <para>
    /// Objects are read as the caller may see them; a relation to an object the caller may not
    /// see is counted and not followed, so the trace never tells what lies behind it.
    /// </para>
    /// </remarks>
    public static class Traceability
    {
        /// <summary>
        /// The hops a trace follows by default.
        /// </summary>
        public const int DefaultDepth = 4;

        /// <summary>
        /// Traces an object.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <param name="depth">The hops to follow each way.</param>
        /// <param name="limit">The most objects to reach.</param>
        /// <returns>The trace, or <see langword="null"/> for an object the caller may not see.</returns>
        public static TraceResult Trace(Guid objectId, int depth = DefaultDepth, int limit = 200)
        {
            var origin = CoreHub.ObjectManager.GetObject(objectId);

            if (origin is null)
            {
                return null;
            }

            var nodes = new List<TraceNode>();
            var seen = new HashSet<Guid> { origin.Id };
            var hidden = 0;
            var truncated = false;

            foreach (var upstream in new[] { false, true })
            {
                var frontier = new List<Guid> { origin.Id };

                for (var distance = 1; distance <= depth && frontier.Count > 0 && !truncated; distance++)
                {
                    var next = new List<Guid>();

                    foreach (var current in frontier)
                    {
                        foreach (var relation in CoreHub.ObjectRelationManager.GetRelations(current).Where(x => x.Status == RelationStatus.Active && x.TargetObjectId is not null))
                        {
                            var outgoing = relation.SourceObjectId == current;

                            if (outgoing == upstream)
                            {
                                continue;
                            }

                            var neighbourId = outgoing ? relation.TargetObjectId.Value : relation.SourceObjectId;

                            if (!seen.Add(neighbourId))
                            {
                                continue;
                            }

                            var neighbour = CoreHub.ObjectManager.GetObject(neighbourId);

                            if (neighbour is null)
                            {
                                hidden++;
                                continue;
                            }

                            if (nodes.Count >= limit)
                            {
                                truncated = true;
                                break;
                            }

                            nodes.Add(new TraceNode(neighbour, distance, upstream, LabelOf(relation.TypeKey, outgoing), current));
                            next.Add(neighbourId);
                        }
                    }

                    frontier = next;
                }
            }

            return new TraceResult(origin, nodes, hidden, truncated);
        }

        /// <summary>
        /// Returns the label of a relation read in the direction of the trace.
        /// </summary>
        /// <param name="typeKey">The relation type.</param>
        /// <param name="outgoing">Whether the trace runs from source to target.</param>
        /// <returns>The label, or the type key.</returns>
        private static string LabelOf(string typeKey, bool outgoing)
        {
            var type = RelationRegistry.GetType(typeKey);

            return (outgoing ? type?.Label : type?.InverseLabel ?? type?.Label) ?? typeKey;
        }
    }
}
