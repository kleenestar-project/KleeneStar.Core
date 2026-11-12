using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Runs the recursive operations on the object hierarchy (see <see cref="IObjectOperationManager"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every object goes through the object manager</b> - a recursive delete is one
    /// <see cref="IObjectManager.Remove"/> per object, children first, so every object gets its
    /// commit, its audit event and its automation trigger, and no foreign key is hit. The run
    /// (<see cref="ObjectOperation"/>, its objects as <see cref="ObjectOperationItem"/>) is written
    /// before the first object is touched, every object is marked when done, and a run a crash
    /// interrupted is resumed by the worker - what is done stays done.
    /// </para>
    /// <para>
    /// <b>Hidden members block.</b> Archive, restore, delete and dissolve are refused - with the
    /// numbers, before anything changes - when the subtree holds an object the caller may not see
    /// or may not change: destroying what one cannot see is the one thing an anonymous count
    /// already set the precedent against. A clone copies what the caller sees.
    /// </para>
    /// <para>
    /// A run of up to <see cref="HierarchySettings.InlineLimit"/> objects is processed in the
    /// request; a larger one waits for <see cref="WebMaintenance.BackgroundWorker"/>, which calls
    /// <see cref="ProcessDue"/>. Processing holds the workspace's <see cref="HierarchyLock"/>, acts
    /// as the identity that started the run, reads unrestricted (the plan already judged the
    /// caller) and silences the per-object notifications.
    /// </para>
    /// </remarks>
    public sealed partial class ObjectOperationManager : IObjectOperationManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// How long a running run may go without progress before the worker takes it over: a
        /// request that processes one inline is never interrupted by it.
        /// </summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private ObjectOperationManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        private static HierarchySettings Settings => CoreHub.HierarchySettings ?? new HierarchySettings();

        /// <inheritdoc/>
        public ObjectOperationPlan Plan(string kind, Guid rootId, string mode, Func<ObjectEntity, bool> mayWrite)
        {
            kind = kind?.Trim().ToLowerInvariant();
            mayWrite ??= _ => true;

            ObjectOperationPlan Refuse(string key, ObjectEntity root, params object[] args) =>
                new(kind, root, [], 0, 0, 0, key, args);

            if (kind is not (ObjectOperationKinds.Archive or ObjectOperationKinds.Restore or ObjectOperationKinds.Delete
                or ObjectOperationKinds.Dissolve or ObjectOperationKinds.Clone))
            {
                return Refuse("kleenestar.core:operation.refused.kind", null);
            }

            var root = ObjectHierarchy.Read(rootId);

            if (root is null)
            {
                return Refuse("kleenestar.core:operation.refused.missing", null);
            }

            // the subtree as the tree knows it (unrestricted, bounded), then as the caller sees it
            var limit = Settings.RecursiveLimit;
            var below = ObjectHierarchy.GetDescendantIds(rootId);

            if (below.Count > limit)
            {
                return Refuse("kleenestar.core:operation.refused.limit", root, below.Count, limit);
            }

            var all = Load(below, unrestricted: true);
            var visible = Load(below, unrestricted: false).Select(x => x.Id).ToHashSet();
            var hidden = all.Count(x => !visible.Contains(x.Id));
            var unwritable = all.Count(x => visible.Contains(x.Id) && !mayWrite(x));
            var depth = Depths(root, all);

            if (kind != ObjectOperationKinds.Clone)
            {
                if (hidden > 0)
                {
                    return new(kind, root, [], all.Count, hidden, unwritable, "kleenestar.core:operation.refused.hidden", [hidden]);
                }

                if (unwritable > 0 || !mayWrite(root))
                {
                    return new(kind, root, [], all.Count, hidden, unwritable, "kleenestar.core:operation.refused.permission", [unwritable + (mayWrite(root) ? 0 : 1)]);
                }
            }
            else if (!mayWrite(root))
            {
                return new(kind, root, [], all.Count, hidden, unwritable, "kleenestar.core:operation.refused.permission", [1]);
            }

            var deepestFirst = all.Append(root).OrderByDescending(x => depth.GetValueOrDefault(x.Id)).ToList();
            List<ObjectEntity> items;
            Guid? reference = null;

            switch (kind)
            {
                case ObjectOperationKinds.Archive:
                    items = [.. deepestFirst.Where(x => x.State == WorkspaceState.Active)];

                    if (items.Count == 0)
                    {
                        return new(kind, root, [], all.Count, 0, 0, "kleenestar.core:operation.refused.archived", []);
                    }

                    break;

                case ObjectOperationKinds.Restore:
                    {
                        if (root.ParentId is Guid parentId && ObjectHierarchy.Read(parentId) is { } parent
                            && parent.State == WorkspaceState.Archived)
                        {
                            return new(kind, root, [], all.Count, 0, 0, "kleenestar.core:operation.refused.parentarchived", [parent.Summary ?? parent.Key]);
                        }

                        // exactly what the last archive run of this root archived, parents first;
                        // without such a run, the root alone
                        var archive = ModelHub.GetLatestObjectOperation(rootId, ObjectOperationKinds.Archive);
                        var byId = all.Append(root).ToDictionary(x => x.Id);

                        items = archive is null
                            ? [root]
                            : [.. ModelHub.GetObjectOperationItems(archive.Id)
                                .Where(x => x.Done)
                                .OrderByDescending(x => x.Position)
                                .Select(x => byId.GetValueOrDefault(x.ObjectId))
                                .Where(x => x is not null)];

                        items = [.. items.Where(x => x.State == WorkspaceState.Archived)];
                        reference = archive?.Id;

                        if (items.Count == 0)
                        {
                            return new(kind, root, [], all.Count, 0, 0, "kleenestar.core:operation.refused.active", []);
                        }

                        break;
                    }

                case ObjectOperationKinds.Delete:
                    items = deepestFirst;
                    break;

                case ObjectOperationKinds.Dissolve:
                    {
                        // the children move up to the root's parent, then the root goes
                        var children = all.Where(x => x.ParentId == rootId).ToList();

                        items = [.. children.OrderBy(x => x.Summary, StringComparer.CurrentCultureIgnoreCase), root];
                        break;
                    }

                default:
                    {
                        // clone: what the caller sees, parents before children; a visible object
                        // below a hidden one has no copy to hang under and is left out
                        mode = NormalizeMode(mode);

                        var candidates = mode == "root"
                            ? []
                            : all.Where(x => visible.Contains(x.Id));

                        var chosen = new HashSet<Guid> { rootId };
                        var ordered = new List<ObjectEntity> { root };

                        foreach (var candidate in candidates.OrderBy(x => depth.GetValueOrDefault(x.Id)))
                        {
                            if (candidate.ParentId is Guid parentId && chosen.Contains(parentId))
                            {
                                chosen.Add(candidate.Id);
                                ordered.Add(candidate);
                            }
                        }

                        items = ordered;
                        break;
                    }
            }

            return new(kind, root, items, all.Count, hidden, unwritable, null, [], reference);
        }

        /// <inheritdoc/>
        public ObjectOperationStart Start(string kind, Guid rootId, string mode, string key, Guid identityId, Func<ObjectEntity, bool> mayWrite)
        {
            if (!string.IsNullOrWhiteSpace(key) && ModelHub.GetObjectOperationByKey(key.Trim()) is { } existing)
            {
                return new ObjectOperationStart(existing, null);
            }

            var plan = Plan(kind, rootId, mode, mayWrite);

            if (plan.Refused)
            {
                return new ObjectOperationStart(null, plan);
            }

            var now = DateTime.UtcNow;
            var operation = new ObjectOperation
            {
                Key = string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("N") : key.Trim(),
                Kind = plan.Kind,
                Mode = plan.Kind == ObjectOperationKinds.Clone ? NormalizeMode(mode) : null,
                RootId = rootId,
                WorkspaceId = plan.Root.WorkspaceId,
                ReferenceId = plan.ReferenceId,
                State = ObjectOperationState.Pending,
                Total = plan.Items.Count,
                IdentityId = identityId == Guid.Empty ? null : identityId,
                Created = now,
                Updated = now
            };

            ModelHub.AddObjectOperation(operation, plan.Items.Select(x => x.Id));

            if (plan.Items.Count <= Settings.InlineLimit)
            {
                Process(operation, int.MaxValue, background: false);
            }

            return new ObjectOperationStart(ModelHub.GetObjectOperation(operation.Id) ?? operation, plan);
        }

        /// <inheritdoc/>
        public ObjectOperation GetOperation(string idOrKey)
        {
            if (string.IsNullOrWhiteSpace(idOrKey))
            {
                return null;
            }

            return Guid.TryParse(idOrKey, out var id)
                ? ModelHub.GetObjectOperation(id) ?? ModelHub.GetObjectOperationByKey(idOrKey.Trim())
                : ModelHub.GetObjectOperationByKey(idOrKey.Trim());
        }

        /// <inheritdoc/>
        public int ProcessDue(int budget = 500)
        {
            var processed = 0;
            var now = DateTime.UtcNow;

            foreach (var operation in ModelHub.GetOpenObjectOperations(20))
            {
                if (processed >= budget)
                {
                    break;
                }

                // a running run belongs to whoever runs it until it stops making progress
                if (operation.State == ObjectOperationState.Running && now - operation.Updated < StaleAfter)
                {
                    continue;
                }

                processed += Process(operation, budget - processed, background: true);
            }

            return processed;
        }

        /// <summary>
        /// Processes the open objects of a run, at most <paramref name="budget"/>.
        /// </summary>
        /// <param name="operation">The run.</param>
        /// <param name="budget">The most objects.</param>
        /// <param name="background">Whether the worker runs it - its end is then reported in the
        /// notification center of the identity that started it, which is no longer watching.</param>
        /// <returns>The number of objects processed.</returns>
        private int Process(ObjectOperation operation, int budget, bool background)
        {
            var processed = 0;

            using var notices = ObjectChangeNotices.Batch();
            using var hierarchy = HierarchyLock.Enter(operation.WorkspaceId);

            // a transfer changes the target workspace's tree too
            using var targetHierarchy = operation.TargetWorkspaceId is Guid targetWorkspace && targetWorkspace != operation.WorkspaceId
                ? HierarchyLock.Enter(targetWorkspace)
                : null;
            using var identity = CoreHub.SessionManager?.BeginIdentity(operation.IdentityId ?? Guid.Empty);
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();
            using var silent = CoreHub.BeginSilentNotifications();

            // read again under the lock: another worker may have finished it meanwhile
            operation = ModelHub.GetObjectOperation(operation.Id) ?? operation;

            if (operation.State is ObjectOperationState.Completed or ObjectOperationState.Failed)
            {
                return 0;
            }

            operation.State = ObjectOperationState.Running;
            operation.Updated = DateTime.UtcNow;
            ModelHub.UpdateObjectOperation(operation);

            var items = ModelHub.GetObjectOperationItems(operation.Id);
            var clones = items.Where(x => x.ResultId.HasValue).ToDictionary(x => x.ObjectId, x => x.ResultId.Value);
            var root = ObjectHierarchy.Read(operation.RootId);

            foreach (var item in items.Where(x => !x.Done))
            {
                if (processed >= budget)
                {
                    return processed;
                }

                try
                {
                    var result = Execute(operation, item, root, clones);

                    if (result is Guid copy)
                    {
                        clones[item.ObjectId] = copy;

                        if (item.ObjectId == operation.RootId)
                        {
                            operation.ReferenceId = copy;
                        }
                    }

                    ModelHub.MarkObjectOperationItem(operation.Id, item.ObjectId, result);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    operation.State = ObjectOperationState.Failed;
                    operation.Error = Describe(ex);
                    operation.Updated = DateTime.UtcNow;
                    operation.Finished = operation.Updated;
                    ModelHub.UpdateObjectOperation(operation);

                    if (background)
                    {
                        Summarize(operation, root);
                    }

                    return processed;
                }

                processed++;
                operation.Done++;
                operation.Updated = DateTime.UtcNow;

                if (operation.Done % 20 == 0)
                {
                    ModelHub.UpdateObjectOperation(operation);
                }
            }

            operation.State = ObjectOperationState.Completed;
            operation.Updated = DateTime.UtcNow;
            operation.Finished = operation.Updated;
            ModelHub.UpdateObjectOperation(operation);

            if (background)
            {
                Summarize(operation, root);
            }

            return processed;
        }

        /// <summary>
        /// Records one summary in the notification center of the identity that started a run the
        /// worker finished: what was done, how much, and where to look - instead of a notification
        /// per object, which the run silenced.
        /// </summary>
        /// <param name="operation">The finished run.</param>
        /// <param name="root">The root as it was read when processing began.</param>
        private static void Summarize(ObjectOperation operation, ObjectEntity root)
        {
            try
            {
                var name = string.IsNullOrWhiteSpace(root?.Summary) ? root?.Key : root.Summary.Trim();
                var subject = $"{name} · {operation.Done.ToString(CultureInfo.InvariantCulture)}/{operation.Total.ToString(CultureInfo.InvariantCulture)}";

                // where to look: the copy of a clone, the parent of what is gone, else the root
                var target = operation.Kind switch
                {
                    ObjectOperationKinds.Clone when operation.ReferenceId is Guid copy => ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.Id == copy)).FirstOrDefault(),
                    ObjectOperationKinds.Delete or ObjectOperationKinds.Dissolve when operation.State == ObjectOperationState.Completed =>
                        root?.ParentId is Guid parentId ? ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.Id == parentId)).FirstOrDefault() : null,
                    _ => ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.Id == operation.RootId)).FirstOrDefault()
                };

                string uri = null;

                try
                {
                    uri = target is null ? null : global::KleeneStar.Core.CoreHub.ObjectKindManager.ResolveDetailUri(target)?.ToString();
                }
                catch
                {
                    // no address without the sitemap (tests); the entry stands without a link
                }

                CoreHub.NotificationCenterManager?.Record
                (
                    operation.State == ObjectOperationState.Completed
                        ? "kleenestar.core:operation.notification.completed"
                        : "kleenestar.core:operation.notification.failed",
                    "kleenestar.core:operation.notification." + operation.Kind,
                    subject,
                    uri
                );
            }
            catch
            {
                // the summary is a courtesy; the run's state is the record
            }
        }

        /// <summary>
        /// Processes one object of a run.
        /// </summary>
        /// <returns>The copy a clone produced, else <see langword="null"/>.</returns>
        private static Guid? Execute(ObjectOperation operation, ObjectOperationItem item, ObjectEntity root, IDictionary<Guid, Guid> clones)
        {
            var @object = CoreHub.ObjectManager.GetObject(item.ObjectId);
            var actor = operation.IdentityId;

            switch (operation.Kind)
            {
                case ObjectOperationKinds.Archive:
                case ObjectOperationKinds.Restore:
                    {
                        var target = operation.Kind == ObjectOperationKinds.Archive ? WorkspaceState.Archived : WorkspaceState.Active;

                        // gone or already there is the outcome the run wants (a retry, a hand)
                        if (@object is null || @object.State == target)
                        {
                            return null;
                        }

                        @object.State = target;
                        @object.UpdaterId = actor;
                        @object.Updated = DateTime.UtcNow;
                        CoreHub.ObjectManager.Update(@object);

                        return null;
                    }

                case ObjectOperationKinds.Delete:
                    if (@object is not null)
                    {
                        CoreHub.ObjectManager.Remove(@object.Id);
                    }

                    return null;

                case ObjectOperationKinds.Dissolve:
                    if (@object is null)
                    {
                        return null;
                    }

                    if (@object.Id == operation.RootId)
                    {
                        CoreHub.ObjectManager.Remove(@object.Id);
                    }
                    else if (@object.ParentId == operation.RootId)
                    {
                        CoreHub.ObjectManager.SetParent(@object.Id, root?.ParentId);
                    }

                    return null;

                case ObjectOperationKinds.Transfer:
                    if (@object is not null)
                    {
                        Transfer(operation, @object);
                    }

                    return null;

                default:
                    return @object is null ? null : Clone(operation, @object, clones);
            }
        }

        /// <summary>
        /// Copies one object - its attributes and its field values - under the copy of its parent
        /// (the root under the original's parent).
        /// </summary>
        private static Guid Clone(ObjectOperation operation, ObjectEntity source, IDictionary<Guid, Guid> clones)
        {
            var isRoot = source.Id == operation.RootId;
            Guid? parentId = isRoot
                ? source.ParentId
                : source.ParentId is Guid parent && clones.TryGetValue(parent, out var copiedParent) ? copiedParent : null;

            var id = Guid.NewGuid();
            var copy = new ObjectEntity(id)
            {
                Key = CoreHub.ObjectManager.NextObjectKey(source.WorkspaceId),
                Summary = source.Summary,
                Description = source.Description,
                ClassId = source.ClassId,
                Kind = source.Kind,
                WorkspaceId = source.WorkspaceId,
                ParentId = parentId,
                State = WorkspaceState.Active,
                SecurityLevelId = source.SecurityLevelId,
                AssigneeId = source.AssigneeId,
                StoryPoints = source.StoryPoints,
                Icon = source.Icon,
                CreatorId = operation.IdentityId,
                UpdaterId = operation.IdentityId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            using (CoreHub.CommitManager.BeginCommit(id, CommitType.Created, operation.IdentityId ?? Guid.Empty))
            {
                CoreHub.ObjectManager.Add(copy);

                foreach (var value in CoreHub.ValueManager.GetValues(source.Id).ToList())
                {
                    CoreHub.ValueManager.Add(new Value { Id = Guid.NewGuid(), ObjectId = id, FieldId = value.FieldId, Data = value.Data });
                }
            }

            return id;
        }

        /// <summary>
        /// Loads objects by id, unrestricted or narrowed to the caller.
        /// </summary>
        private static List<ObjectEntity> Load(IEnumerable<Guid> ids, bool unrestricted)
        {
            var result = new List<ObjectEntity>();

            foreach (var chunk in ids.Chunk(500))
            {
                var query = new Query<ObjectEntity>().Where(x => chunk.Contains(x.Id));

                result.AddRange(unrestricted ? ModelHub.GetObjects(query) : CoreHub.ObjectManager.GetObjects(query));
            }

            return result;
        }

        /// <summary>
        /// Answers the depth of every object below the root (the root is 0).
        /// </summary>
        private static Dictionary<Guid, int> Depths(ObjectEntity root, IReadOnlyCollection<ObjectEntity> below)
        {
            var parents = below.ToDictionary(x => x.Id, x => x.ParentId);
            var depth = new Dictionary<Guid, int> { [root.Id] = 0 };

            int Of(Guid id, int guard)
            {
                if (depth.TryGetValue(id, out var known))
                {
                    return known;
                }

                var result = guard > 64 || !parents.TryGetValue(id, out var parent) || parent is not Guid parentId
                    ? 64
                    : Of(parentId, guard + 1) + 1;

                depth[id] = result;

                return result;
            }

            foreach (var id in parents.Keys)
            {
                Of(id, 0);
            }

            return depth;
        }

        /// <summary>
        /// Translates in the language of the request being served (a run processed inline), else
        /// in the installation's (a run the worker processes).
        /// </summary>
        private static string Translate(string key)
        {
            var request = WebEx.CurrentRequest;

            return request is null ? I18N.Translate(key) : I18N.Translate(request, key);
        }

        /// <summary>
        /// Normalizes the clone mode (default: contents).
        /// </summary>
        private static string NormalizeMode(string mode)
        {
            // "container" is the former name of the root alone
            return mode?.Trim().ToLowerInvariant() switch
            {
                "root" or "container" => "root",
                _ => "contents"
            };
        }

        /// <summary>
        /// Describes why an object could not be processed, in the installation's language.
        /// </summary>
        private static string Describe(Exception ex)
        {
            return ex.Message;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
