using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebRelation;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// What changing one object touches: the objects reachable from it along the relations that
    /// carry a workflow effect, and the relations that carry the reach.
    /// </summary>
    /// <remarks>
    /// The result is a graph rather than a list, because the answer to <em>what does changing
    /// this touch</em> is only half the story without <em>how</em>: an object three steps away
    /// is affected for a reason that runs through two others, and a reader who cannot see the
    /// path cannot judge the consequence. <see cref="Nodes"/> therefore carries the distance of
    /// each object and <see cref="Edges"/> the relation every step was taken along.
    /// <para>
    /// The origin is the first node, at distance zero. An object appears once, at the shortest
    /// distance it was reached on, and an object the caller may not see appears not at all -
    /// neither does anything reachable only through it.
    /// </para>
    /// </remarks>
    public sealed class ObjectImpactResult
    {
        /// <summary>
        /// Gets the object the analysis started from.
        /// </summary>
        public Model.Entities.Object Origin { get; init; }

        /// <summary>
        /// Gets the objects the change reaches, the origin first, then in the order they were
        /// reached - which is by distance, because the walk is breadth-first.
        /// </summary>
        public IReadOnlyList<ObjectImpactNode> Nodes { get; init; } = [];

        /// <summary>
        /// Gets the relations the reach runs along, each from the object that carries the
        /// consequence to the object that receives it.
        /// </summary>
        public IReadOnlyList<ObjectImpactEdge> Edges { get; init; } = [];

        /// <summary>
        /// Gets how many steps the walk was allowed to take.
        /// </summary>
        public int Depth { get; init; }

        /// <summary>
        /// Gets a value indicating whether the walk stopped before it had followed everything -
        /// the graph grew past what one reading can say anything with.
        /// </summary>
        /// <remarks>
        /// It is reported rather than hidden because a truncated impact analysis that looks
        /// complete is worse than none: the reader would take the absence of an object as the
        /// statement that it is not affected.
        /// </remarks>
        public bool Truncated { get; init; }
    }

    /// <summary>
    /// One object the change reaches.
    /// </summary>
    /// <param name="Object">The object itself, as the caller is allowed to see it.</param>
    /// <param name="Distance">How many relations away from the origin it stands. The origin is
    /// zero.</param>
    /// <param name="Effect">The effect of the relation the object was reached through, or
    /// <see cref="RelationEffect.None"/> for the origin, which was not reached through
    /// anything.</param>
    public sealed record ObjectImpactNode
    (
        Model.Entities.Object Object,
        int Distance,
        RelationEffect Effect
    );

    /// <summary>
    /// One step of the reach: the relation a consequence travels along.
    /// </summary>
    /// <remarks>
    /// <see cref="From"/> and <see cref="To"/> are the direction the <em>consequence</em> runs
    /// in, which is not always the direction the relation was stored in: a relation that closes
    /// its source with its target is stored source to target and carries its consequence the
    /// other way. The stored ends stay available through <see cref="RelationId"/> for a caller
    /// that has to name the relation itself.
    /// </remarks>
    /// <param name="RelationId">The identity of the stored relation.</param>
    /// <param name="From">The object the consequence starts at.</param>
    /// <param name="To">The object it reaches.</param>
    /// <param name="TypeKey">The key of the relation type, for example <c>blocks</c>.</param>
    /// <param name="Effect">What the relation does, which is why it is part of the analysis at
    /// all.</param>
    public sealed record ObjectImpactEdge
    (
        Guid RelationId,
        Guid From,
        Guid To,
        string TypeKey,
        RelationEffect Effect
    );
}
