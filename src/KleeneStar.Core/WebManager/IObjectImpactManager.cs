using System;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Answers what changing an object touches, by walking the relations of the installation
    /// transitively rather than one hop at a time.
    /// </summary>
    /// <remarks>
    /// The relation model already says which relations have consequences and in which direction
    /// they run: an <see cref="WebExpress.WebApp.WebRelation.RelationEffect"/> on the type is
    /// what the workflow asks before it refuses a move
    /// (<see cref="ObjectRelationWorkflowRules"/>). This manager asks the same question of the
    /// whole neighbourhood instead of of one object, which is all impact analysis is: the guard
    /// answers <em>may I close this</em>, and the analysis answers <em>what happens if I do</em>.
    /// <para>
    /// It reads and never writes. Nothing about an object changes by being analysed, and the
    /// analysis is therefore free to be run from a page render, a tooltip or an endpoint.
    /// </para>
    /// </remarks>
    public interface IObjectImpactManager : IComponentManager
    {
        /// <summary>
        /// Walks the relations of the supplied object and answers what a change to it reaches.
        /// </summary>
        /// <remarks>
        /// Only relations that carry an effect are followed - an informational relation states
        /// that two records have something to do with each other, not that one governs the
        /// other - and only in the direction the effect runs. Obsolete relations carry nothing:
        /// they are kept for the history, and history does not propagate a change made today.
        /// <para>
        /// The walk is bounded three ways, because a relation graph has no natural end: by
        /// <paramref name="depth"/>, by a visited set (two objects may be each other's
        /// duplicate), and by a node budget the result reports as
        /// <see cref="ObjectImpactResult.Truncated"/>.
        /// </para>
        /// </remarks>
        /// <param name="objectId">The object a change would be made to.</param>
        /// <param name="depth">How many relations to follow. Clamped to a sensible range; the
        /// default is what a reader can still take in.</param>
        /// <returns>The reachable objects and the relations they were reached through. An object
        /// that does not exist, or that the caller may not see, answers an empty result rather
        /// than an error - the same reading a list of objects gives it.</returns>
        ObjectImpactResult Analyze(Guid objectId, int depth = ObjectImpactManager.DefaultDepth);
    }
}
