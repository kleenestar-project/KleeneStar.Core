using KleeneStar.Core.WebRestApi;
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
    /// Rolls the progress of an object up from the objects it aggregates, and falls back to the
    /// progress of its own workflow state where it aggregates nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What counts as a child</b> is the relation model's answer, not a second hierarchy:
    /// the targets of the relations this object is the <em>source</em> of, whose type declares
    /// <see cref="RelationEffect.AggregatesProgress"/> - "the progress of the targets is
    /// aggregated into the source". Which relation that is remains a decision of whoever runs
    /// the installation; the shipped catalog calls it <c>parent</c>, and nothing here knows that
    /// name. An obsolete relation counts nothing, and an external address has no progress to
    /// count at all.
    /// </para>
    /// <para>
    /// <b>Every child counts once and equally.</b> The obvious alternative - weighting by
    /// estimate, by story points, by remaining effort - needs a number the model does not
    /// require any class to carry, and a rollup that silently ignored the classes without it
    /// would report a different figure for the same tree depending on how it was modelled. An
    /// unweighted average is the honest reading of what is known: <em>this many of these are
    /// done</em>.
    /// </para>
    /// <para>
    /// <b>A child that aggregates is asked for its own rollup</b>, so a tree is summarised from
    /// the bottom up and a parent of parents says something about the leaves. The recursion is
    /// bounded by a visited set - two objects may aggregate each other, which is a mistake
    /// somebody made rather than a case to refuse - and by a depth, past which a child counts
    /// with its own state instead of its subtree.
    /// </para>
    /// <para>
    /// <b>Nothing is stored.</b> The percentage is a statement about the children as they are
    /// now; a copy on the parent would be wrong from the next transition onwards without
    /// anything saying so, and the parent's own commit history would fill with changes nobody
    /// made.
    /// </para>
    /// </remarks>
    public sealed class ObjectProgressManager : IObjectProgressManager
    {
        /// <summary>
        /// How deep the rollup follows children before it stops asking them what their own
        /// children did.
        /// </summary>
        /// <remarks>
        /// A tree deeper than this is a modelling accident rather than a plan, and the reading
        /// stays sensible either way: at the limit a child counts with the progress of its own
        /// state, which is what every object answered before this manager existed.
        /// </remarks>
        public const int MaximumDepth = 10;

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectProgressManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns how far the supplied object has come.
        /// </summary>
        /// <param name="objectId">The object to measure.</param>
        /// <returns>The progress.</returns>
        public ObjectProgress GetProgress(Guid objectId)
        {
            return Measure(objectId, [], 0);
        }

        /// <summary>
        /// Measures one object, rolling up where it aggregates.
        /// </summary>
        /// <param name="objectId">The object to measure.</param>
        /// <param name="visited">The objects already on this branch of the walk.</param>
        /// <param name="depth">How many parents were followed to get here.</param>
        /// <returns>The progress.</returns>
        private static ObjectProgress Measure(Guid objectId, HashSet<Guid> visited, int depth)
        {
            var @object = CoreHub.ObjectManager?.GetObject(objectId);

            if (@object is null)
            {
                return new ObjectProgress();
            }

            var children = depth < MaximumDepth && visited.Add(objectId)
                ? Children(objectId).ToList()
                : [];

            if (children.Count == 0)
            {
                return new ObjectProgress() { Percent = OwnProgress(@object) };
            }

            var contributions = children
                .Select(child => new ObjectProgressContribution(child.Id, child.Key, Measure(child.Id, visited, depth + 1).Percent))
                .ToList();

            return new ObjectProgress()
            {
                // the average is rounded rather than truncated, so a tree that is nine tenths
                // done does not report 89 %
                Percent = (int)Math.Round(contributions.Average(x => (double)x.Percent), MidpointRounding.AwayFromZero),
                Aggregated = true,
                Contributions = contributions,
                Completed = contributions.Count(x => x.Percent >= 100)
            };
        }

        /// <summary>
        /// Returns the objects whose progress the supplied object reports.
        /// </summary>
        /// <param name="objectId">The aggregating object.</param>
        /// <returns>The children, as the caller is allowed to see them.</returns>
        private static IEnumerable<Model.Entities.Object> Children(Guid objectId)
        {
            var relations = CoreHub.ObjectRelationManager?.GetRelations(objectId) ?? [];

            foreach (var relation in relations)
            {
                if (relation is null || relation.Status == RelationStatus.Obsolete)
                {
                    continue;
                }

                // the aggregating end is the source: "the progress of the targets is aggregated
                // into the source", so a relation this object is the target of reports upwards
                // rather than downwards and is none of its business here
                if (relation.SourceObjectId != objectId || relation.TargetObjectId is not Guid targetId)
                {
                    continue;
                }

                if (EffectOf(relation.TypeKey) != RelationEffect.AggregatesProgress)
                {
                    continue;
                }

                // a child the caller may not see is not counted, which is the same reading the
                // object lists give it: it does not exist for this request, and counting it
                // would leak its state through an average
                var child = CoreHub.ObjectManager?.GetObject(targetId);

                if (child is not null)
                {
                    yield return child;
                }
            }
        }

        /// <summary>
        /// Returns the progress an object reports for itself: the one its workflow state stands
        /// for.
        /// </summary>
        /// <remarks>
        /// This is the reading the plan views have always used, and it is deliberately the same
        /// one - a parent bar and a child bar in the same chart may not measure differently.
        /// </remarks>
        /// <param name="object">The object.</param>
        /// <returns>The percentage.</returns>
        private static int OwnProgress(Model.Entities.Object @object)
        {
            var @class = @object.Class ?? CoreHub.ClassManager?.GetClass(@object.ClassId);

            if (@class is null)
            {
                return 0;
            }

            var context = ObjectBoardProjection.BuildClassContext(@class);
            var categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);
            var category = ObjectBoardProjection.ResolveCategory(@object.Id, context, categories);

            return ObjectBoardProjection.CategoryProgress(category);
        }

        /// <summary>
        /// Returns the effect a relation carries, read from the published catalog rather than
        /// from the stored row, so a type an administrator changed takes effect at once.
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
