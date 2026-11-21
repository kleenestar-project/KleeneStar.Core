using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

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
            // the object itself is read the caller's way: a caller who may not see it is told
            // nothing about it, its children included
            var @object = CoreHub.ObjectManager?.GetObject(objectId);

            return @object is null
                ? new ObjectProgress()
                : GetProgress([@object])[@object.Id];
        }

        /// <summary>
        /// Returns how far each of the supplied objects has come, reading the trees below them
        /// together: one read of the relations and one of the children per level, and one of
        /// the workflow values.
        /// </summary>
        /// <param name="objects">
        /// The objects to measure, already read the caller's way. A caller who may not see an
        /// object must not hand it in.
        /// </param>
        /// <returns>The progress per object id.</returns>
        public IReadOnlyDictionary<Guid, ObjectProgress> GetProgress(IEnumerable<Model.Entities.Object> objects)
        {
            var roots = (objects ?? [])
                .Where(x => x is not null)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            var tree = ProgressTree.Load(roots);

            return roots.ToDictionary(x => x.Id, x => Measure(x, tree, [], 0));
        }

        /// <summary>
        /// Measures one object from the tree read for it.
        /// </summary>
        /// <param name="object">The object to measure.</param>
        /// <param name="tree">The children and own progress of every object below the roots.</param>
        /// <param name="visited">The objects already expanded in this rollup.</param>
        /// <param name="depth">The depth of the object below the root.</param>
        /// <returns>The progress.</returns>
        private static ObjectProgress Measure(Model.Entities.Object @object, ProgressTree tree, HashSet<Guid> visited, int depth)
        {
            var children = depth < MaximumDepth && visited.Add(@object.Id)
                ? tree.Children(@object.Id)
                : [];

            if (children.Count == 0)
            {
                return new ObjectProgress() { Percent = tree.OwnProgress(@object) };
            }

            var contributions = children
                .Select(child =>
                {
                    var percent = Measure(child.Object, tree, visited, depth + 1).Percent;

                    // a child the caller may not read counts, but anonymously: its share of the
                    // percentage and of the summary, never its id, key, title or assignee
                    return child.Visible
                        ? new ObjectProgressContribution(child.Object.Id, child.Object.Key, percent)
                        : new ObjectProgressContribution(Guid.Empty, null, percent, Hidden: true);
                })
                .ToList();

            return new ObjectProgress()
            {
                // the average is rounded rather than truncated, so a tree that is nine tenths
                // done does not report 89 %
                Percent = (int)Math.Round(contributions.Average(x => (double)x.Percent), MidpointRounding.AwayFromZero),
                Aggregated = true,
                Contributions = contributions,
                Completed = contributions.Count(x => x.Percent >= 100),
                Started = contributions.Count(x => x.Percent > 0 && x.Percent < 100),
                Hidden = contributions.Count(x => x.Hidden)
            };
        }

        /// <summary>
        /// The objects below a set of roots, read level by level before anything is measured:
        /// the children of each object, whether the caller may read them, and what the own
        /// progress of each object is computed from.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Every child counts, the ones the caller may not read too.</b> A decomposed order
        /// keeps its sub-tasks where the departments doing them work - another workspace, often
        /// one the requester cannot read - and a rollup over the visible ones alone reported such
        /// an order as done while work on it was still open. Worse, the guard that refuses to
        /// close a parent with unfinished children asked the same rollup, so the parent could be
        /// closed past an open sub-task the person moving it happened not to see.
        /// </para>
        /// <para>
        /// <b>What identifies or describes a hidden child is withheld.</b> It contributes its
        /// share of the percentage and of the done/started/open summary, which is what the parent
        /// is meant to report; its id, key, title, assignee and content never reach the caller
        /// (<see cref="ObjectProgressContribution.Hidden"/>). The children are therefore read
        /// without the caller's narrowing - a read the system performs to answer a question about
        /// the parent - and their visibility is asked separately.
        /// </para>
        /// </remarks>
        private sealed class ProgressTree
        {
            private readonly Dictionary<Guid, List<(Model.Entities.Object Object, bool Visible)>> _children = [];
            private readonly Dictionary<Guid, ObjectBoardClassContext> _contexts = [];
            private Dictionary<Guid, StatusCategory> _categories = [];
            private IReadOnlyDictionary<(Guid ObjectId, Guid FieldId), string> _values = new Dictionary<(Guid ObjectId, Guid FieldId), string>();

            /// <summary>
            /// Reads the trees below the supplied roots.
            /// </summary>
            /// <param name="roots">The objects to measure.</param>
            /// <returns>The tree.</returns>
            public static ProgressTree Load(IReadOnlyList<Model.Entities.Object> roots)
            {
                var tree = new ProgressTree();
                var objects = roots.ToDictionary(x => x.Id);
                var expanded = new HashSet<Guid>();
                var frontier = roots.Select(x => x.Id).ToList();

                // the rollup expands an object only above the depth limit, and a breadth first
                // walk reaches every object at its shallowest depth first, so the levels above
                // the limit are all the rollup can ask for
                for (var depth = 0; depth < MaximumDepth && frontier.Count > 0; depth++)
                {
                    frontier = [.. frontier.Where(expanded.Add)];

                    if (frontier.Count == 0)
                    {
                        break;
                    }

                    var relations = (CoreHub.ObjectRelationManager?.GetRelationsFrom(frontier) ?? [])
                        .Where(x => x is not null && x.Status != RelationStatus.Obsolete)
                        .Where(x => x.TargetObjectId is not null)
                        .Where(x => EffectOf(x.TypeKey) == RelationEffect.AggregatesProgress)
                        .ToList();

                    var targetIds = relations.Select(x => x.TargetObjectId.Value).Distinct().ToList();
                    var (children, visible) = ReadChildren(targetIds);

                    foreach (var relation in relations)
                    {
                        if (!children.TryGetValue(relation.TargetObjectId.Value, out var child))
                        {
                            continue;
                        }

                        if (!tree._children.TryGetValue(relation.SourceObjectId, out var list))
                        {
                            list = [];
                            tree._children[relation.SourceObjectId] = list;
                        }

                        list.Add((child, visible.Contains(child.Id)));
                        objects.TryAdd(child.Id, child);
                    }

                    frontier = [.. children.Keys];
                }

                tree.LoadOwnProgress(objects.Values);

                return tree;
            }

            /// <summary>
            /// Returns the children of an object.
            /// </summary>
            /// <param name="objectId">The aggregating object.</param>
            /// <returns>The children and whether the caller may read each.</returns>
            public IReadOnlyList<(Model.Entities.Object Object, bool Visible)> Children(Guid objectId)
            {
                return _children.TryGetValue(objectId, out var list) ? list : [];
            }

            /// <summary>
            /// Returns the progress an object reports for itself, from the values read for the
            /// tree. It is the reading of <see cref="ObjectProgressManager.OwnProgress"/>.
            /// </summary>
            /// <param name="object">The object.</param>
            /// <returns>The percentage.</returns>
            public int OwnProgress(Model.Entities.Object @object)
            {
                if (!_contexts.TryGetValue(@object.ClassId, out var context) || context is null)
                {
                    return 0;
                }

                var data = context.WorkflowField is not null && _values.TryGetValue((@object.Id, context.WorkflowField.Id), out var value)
                    ? value
                    : null;

                return ObjectBoardProjection.CategoryProgress(ObjectBoardProjection.ResolveCategory(data, context, _categories));
            }

            /// <summary>
            /// Reads the children of one level twice: without the caller's narrowing, since every
            /// child counts, and with it, to learn which of them the caller may read.
            /// </summary>
            /// <param name="ids">The ids of the children.</param>
            /// <returns>The children by id and the ids the caller may read.</returns>
            private static (Dictionary<Guid, Model.Entities.Object> Children, HashSet<Guid> Visible) ReadChildren(IReadOnlyList<Guid> ids)
            {
                var children = new Dictionary<Guid, Model.Entities.Object>();
                var visible = new HashSet<Guid>();

                if (ids.Count == 0 || CoreHub.ObjectManager is null)
                {
                    return (children, visible);
                }

                // a parameterized list may be sent as one parameter per element, and SQL Server
                // accepts at most 2100 of them in a statement
                foreach (var chunk in ids.Chunk(500))
                {
                    var query = new Query<Model.Entities.Object>().Where(x => chunk.Contains(x.Id));

                    using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
                    {
                        foreach (var child in CoreHub.ObjectManager.GetObjects(query))
                        {
                            children.TryAdd(child.Id, child);
                        }
                    }

                    visible.UnionWith(CoreHub.ObjectManager.GetObjects(query).Select(x => x.Id));
                }

                return (children, visible);
            }

            /// <summary>
            /// Reads what the own progress of the supplied objects is computed from: the board
            /// context of each class, the status categories and the workflow values.
            /// </summary>
            /// <param name="objects">Every object of the tree.</param>
            private void LoadOwnProgress(IEnumerable<Model.Entities.Object> objects)
            {
                var list = objects.ToList();

                if (list.Count == 0)
                {
                    return;
                }

                foreach (var group in list.GroupBy(x => x.ClassId))
                {
                    var @class = group.Select(x => x.Class).FirstOrDefault(x => x is not null)
                        ?? CoreHub.ClassManager?.GetClass(group.Key);

                    _contexts[group.Key] = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
                }

                _categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);

                var fieldIds = _contexts.Values
                    .Select(x => x?.WorkflowField?.Id)
                    .OfType<Guid>();

                if (CoreHub.ValueManager is not null)
                {
                    _values = CoreHub.ValueManager.GetData(list.Select(x => x.Id), fieldIds);
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
        internal static int OwnProgress(Model.Entities.Object @object)
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
