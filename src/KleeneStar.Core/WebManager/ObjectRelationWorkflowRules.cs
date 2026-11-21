using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Evaluates what the relations of an object mean for its workflow: which of them refuse a
    /// move into a closing state, and which objects follow it once it reaches one.
    /// </summary>
    /// <remarks>
    /// The rules are stated once here rather than inside <see cref="WorkflowManager"/>, because
    /// they belong to the relation model: the workflow only has to ask two questions - may this
    /// object close, and what closes with it - without knowing the semantics of any particular
    /// relation an administrator invented. What answers them is the
    /// <see cref="RelationEffect"/> declared on the relation type.
    /// <para>
    /// <b>Which end an effect constrains.</b> WebExpress ships the effects with two descriptions
    /// that do not agree: the XML comment on <see cref="RelationEffect.BlocksCompletion"/> says
    /// the <i>source</i> cannot close while the target is open, while the description of its own
    /// shipped <c>blocks</c> relation - the sentence an administrator actually reads when picking
    /// the effect - says "the target cannot be completed while this item is open". The type
    /// descriptions are self-consistent across all three effects and match the labels
    /// (<c>blocks</c> / <c>is blocked by</c>), so they are what is implemented here: <b>the
    /// source blocks, the target is blocked</b>. Implementing the enum comment instead would make
    /// the shipped catalog mean the opposite of what its own labels say.
    /// </para>
    /// </remarks>
    internal static class ObjectRelationWorkflowRules
    {
        /// <summary>
        /// The name of the status category that counts as closing. It is the same collapsed
        /// reading the boards use, so "done" means one thing across the application.
        /// </summary>
        private const string ClosingCategory = "done";

        /// <summary>
        /// Returns the objects that refuse the move of <paramref name="objectId"/> into
        /// <paramref name="target"/>: those that block it and are themselves still open.
        /// </summary>
        /// <remarks>
        /// Only a move into a closing state can be blocked. Every other move is unaffected, which
        /// is what lets a blocked object still be worked on - it simply cannot be finished.
        /// <para>
        /// An obsolete relation no longer states anything, so it does not block; a relation whose
        /// type was meanwhile deleted carries no effect and does not block either.
        /// </para>
        /// </remarks>
        /// <param name="objectId">The object being moved.</param>
        /// <param name="target">The state it is being moved to.</param>
        /// <returns>The keys of the blocking objects, empty when nothing blocks the move.</returns>
        public static IReadOnlyList<string> FindBlockers(Guid objectId, Status target)
        {
            if (!IsClosing(target))
            {
                return [];
            }

            var cache = new Dictionary<Guid, bool>();

            return
            [
                .. CoreHub.ObjectRelationManager
                    .GetRelations(objectId)
                    .Where(x => x.Status != RelationStatus.Obsolete)

                    // the blocking end is the source: "A blocks B" constrains B, so the object
                    // being moved has to be the target for the relation to hold it back
                    .Where(x => x.TargetObjectId == objectId)
                    .Where(x => EffectOf(x.TypeKey) == RelationEffect.BlocksCompletion)
                    .Select(x => x.SourceObject)
                    .Where(x => x is not null)
                    .Where(x => !IsClosed(x, cache))
                    .Select(x => x.Key)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            ];
        }

        /// <summary>
        /// Returns the objects that follow <paramref name="objectId"/> into a closing state: the
        /// sources of the relations that declare they are closed with their target.
        /// </summary>
        /// <remarks>
        /// This is how a duplicate follows its original. Only objects that are not already closed
        /// are answered, so a repeated transition does not re-close what is settled.
        /// </remarks>
        /// <param name="objectId">The object that reached a closing state.</param>
        /// <returns>The ids of the objects to close with it.</returns>
        public static IReadOnlyList<Guid> FindFollowers(Guid objectId)
        {
            var cache = new Dictionary<Guid, bool>();

            return
            [
                .. CoreHub.ObjectRelationManager
                    .GetRelations(objectId)
                    .Where(x => x.Status != RelationStatus.Obsolete)

                    // "this item ... is closed with the target": the source follows, so the
                    // object that just closed has to be the target
                    .Where(x => x.TargetObjectId == objectId)
                    .Where(x => EffectOf(x.TypeKey) == RelationEffect.ClosesItem)
                    .Select(x => x.SourceObject)
                    .Where(x => x is not null && !IsClosed(x, cache))
                    .Select(x => x.Id)
                    .Distinct()
            ];
        }

        /// <summary>
        /// Determines whether a state finishes an object, which is the only kind of move the
        /// relations of an object can refuse.
        /// </summary>
        /// <param name="status">The state, may be absent.</param>
        /// <returns><see langword="true"/> when the state belongs to the closing category.</returns>
        public static bool IsClosing(Status status)
        {
            if (status is null)
            {
                return false;
            }

            var category = CoreHub.StatusManager
                .GetStatusCategories(new WebExpress.WebIndex.Queries.Query<StatusCategory>())
                .FirstOrDefault(x => x.Id == status.CategoryId);

            return Normalize(category?.Name) == ClosingCategory;
        }

        /// <summary>
        /// Returns the workflow effect a relation carries, read from the published catalog rather
        /// than from the stored row, so a type an administrator changed takes effect at once.
        /// </summary>
        /// <param name="typeKey">The key of the relation type.</param>
        /// <returns>The effect, or <see cref="RelationEffect.None"/> for an unknown type.</returns>
        private static RelationEffect EffectOf(string typeKey)
        {
            return RelationRegistry.GetType(typeKey)?.Effect ?? RelationEffect.None;
        }

        /// <summary>
        /// Determines whether an object has reached a closing state, resolved from the workflow
        /// field of its class exactly as the boards resolve it.
        /// </summary>
        /// <remarks>
        /// An object whose class models no workflow at all can never be open in the sense this
        /// rule means, so it is treated as closed and blocks nothing - otherwise linking a
        /// document to a task would make the task unfinishable forever.
        /// </remarks>
        /// <param name="object">The object to inspect.</param>
        /// <param name="cache">Memoizes the answer per object within one evaluation.</param>
        /// <returns><see langword="true"/> when the object is closed or models no workflow.</returns>
        private static bool IsClosed(ObjectEntity @object, IDictionary<Guid, bool> cache)
        {
            if (cache.TryGetValue(@object.Id, out var known))
            {
                return known;
            }

            var closed = ResolveClosed(@object);
            cache[@object.Id] = closed;

            return closed;
        }

        /// <summary>
        /// Reads the workflow state of an object and decides whether it counts as closed.
        /// </summary>
        /// <param name="object">The object to inspect.</param>
        /// <returns><see langword="true"/> when the object is closed or models no workflow.</returns>
        private static bool ResolveClosed(ObjectEntity @object)
        {
            var @class = @object.Class ?? CoreHub.ClassManager.GetClass(@object.ClassId);

            if (@class is null)
            {
                return true;
            }

            var context = ObjectBoardProjection.BuildClassContext(@class);

            if (context.WorkflowField is null)
            {
                return true;
            }

            var categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);
            var category = ObjectBoardProjection.ResolveCategory(@object.Id, context, categories);

            // an object that has not entered its state machine yet carries no category; it is
            // open, because nothing has finished it
            return Normalize(category?.Name) == ClosingCategory;
        }

        /// <summary>
        /// Returns the closing state an object may be moved to from where it stands, or
        /// <see langword="null"/> when its workflow offers none.
        /// </summary>
        /// <remarks>
        /// The follower is moved along a transition its workflow actually declares, never by
        /// writing a state the state machine forbids. A workflow that offers no reachable closing
        /// state simply keeps its object where it is.
        /// </remarks>
        /// <param name="objectId">The object to move.</param>
        /// <param name="fieldId">Receives the workflow field carrying the state.</param>
        /// <returns>The reachable closing state, or <see langword="null"/>.</returns>
        public static Status FindClosingTarget(Guid objectId, out Guid fieldId)
        {
            fieldId = Guid.Empty;

            // the follower is somebody else's record and may well be classified above the
            // caller; a relation that closes it has to close it either way
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            var @object = CoreHub.ObjectManager.GetObject(objectId);
            var @class = @object is null ? null : CoreHub.ClassManager.GetClass(@object.ClassId);

            if (@class is null)
            {
                return null;
            }

            var field = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(@class.Id))
                .FirstOrDefault(x => x.FieldType == FieldType.Workflow && x.WorkflowId.HasValue);

            if (field is null)
            {
                return null;
            }

            fieldId = field.Id;

            // the state machine has to be walked, so the structure-loading overload is the
            // one that answers which states are reachable from here
            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);
            var current = CoreHub.WorkflowManager.ResolveStatus(workflow, CoreHub.ValueManager.GetValue(objectId, field.Id)?.Data);

            return CoreHub.WorkflowManager
                .GetTargetStatuses(workflow, current)
                .FirstOrDefault(IsClosing);
        }

        /// <summary>
        /// Collapses a name for comparison, so "In Progress" and "InProgress" read alike.
        /// </summary>
        /// <param name="value">The name, may be absent.</param>
        /// <returns>The collapsed, lower-case name.</returns>
        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }
    }
}
