using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Keeps the classes that follow a blueprint in step with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Copies, not references.</b> A follower holds its own rows for every field, status,
    /// priority and workflow it receives, each naming the blueprint element it copies
    /// (<see cref="IBlueprintManaged.BlueprintSourceId"/>). Everything that reads a class - the
    /// forms, the boards, the values of its objects, WQL - therefore works on a follower exactly
    /// as on any other class, and a follower whose blueprint goes away simply keeps what it has.
    /// </para>
    /// <para>
    /// <b>Matching.</b> A copy is found by the element it names; an element the follower had
    /// before it was bound is adopted by name, so binding an existing class to a blueprint does
    /// not double its fields - and the values its objects carry stay where they are. A blueprint
    /// element that is gone is <em>retired</em> in the follower (archived, a field deprecated)
    /// rather than deleted, because objects may still carry values of it.
    /// </para>
    /// <para>
    /// <b>When.</b> A follower is synchronized when it is bound, whenever an element of its
    /// blueprint is added, changed or removed (the structure managers' events, and the workflow
    /// editor after it saved), and at start-up, which heals whatever a crash interrupted - a
    /// synchronization converges, so running it again is always safe. It runs as the system,
    /// without notifications: one change of a blueprint is not announced once per workspace.
    /// </para>
    /// <para>
    /// <b>Local and central.</b> A managed copy is changed in the blueprint, not locally - the
    /// class-structure endpoints refuse it (<see cref="IsManaged"/>). The follower may add fields,
    /// statuses and transitions of its own, which the synchronization leaves alone.
    /// </para>
    /// </remarks>
    public sealed class BlueprintManager : IBlueprintManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly object _gate = new();
        private bool _connected;

        /// <summary>
        /// Whether a synchronization runs on this call chain; the structure events it raises
        /// itself are not taken as changes of a blueprint.
        /// </summary>
        private static readonly AsyncLocal<int> _synchronizing = new();

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private BlueprintManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public IReadOnlyList<Class> GetFollowers(Guid blueprintId)
        {
            return [.. CoreHub.ClassManager
                .GetClasses(new Query<Class>())
                .Where(x => x.BlueprintId == blueprintId && x.Id != blueprintId)];
        }

        /// <inheritdoc/>
        public bool IsManaged(IBlueprintManaged element)
        {
            return element?.BlueprintSourceId is not null;
        }

        /// <inheritdoc/>
        public void SynchronizeFollowers(Guid blueprintId)
        {
            foreach (var follower in GetFollowers(blueprintId))
            {
                try
                {
                    Synchronize(follower.Id);
                }
                catch (Exception ex)
                {
                    // one follower that cannot be synchronized must not keep the others behind;
                    // the next change, or the next start, tries it again
                    _httpServerContext?.Log?.Exception(ex);
                }
            }
        }

        /// <inheritdoc/>
        public BlueprintSyncResult Synchronize(Guid followerId)
        {
            var result = new BlueprintSyncResult();
            var follower = CoreHub.ClassManager.GetClass(followerId);
            var blueprint = follower?.BlueprintId is Guid blueprintId && blueprintId != followerId
                ? CoreHub.ClassManager.GetClass(blueprintId)
                : null;

            if (follower is null || blueprint is null)
            {
                return result;
            }

            _synchronizing.Value++;

            try
            {
                using var silent = CoreHub.BeginSilentNotifications();
                using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

                var statusMap = SynchronizeStatuses(blueprint, follower, result);
                var priorityMap = SynchronizePriorities(blueprint, follower, result);
                var workflowMap = SynchronizeWorkflows(blueprint, follower, statusMap, result);

                SynchronizeFields(blueprint, follower, workflowMap, priorityMap, result);
            }
            finally
            {
                _synchronizing.Value--;
            }

            return result;
        }

        /// <inheritdoc/>
        public IReadOnlyList<Class> ApplyWorkspace(Guid blueprintWorkspaceId, Guid workspaceId)
        {
            var created = new List<Class>();

            if (blueprintWorkspaceId == workspaceId)
            {
                return created;
            }

            var existing = CoreHub.ClassManager
                .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspaceId))
                .ToList();

            foreach (var blueprint in CoreHub.ClassManager
                .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, blueprintWorkspaceId))
                .Where(x => x.IsBlueprint && x.State == ClassState.Active))
            {
                // a class of the same name is adopted rather than doubled - names are unique per
                // workspace, and its objects keep their values
                var follower = existing.FirstOrDefault(x => string.Equals(x.Name, blueprint.Name, StringComparison.OrdinalIgnoreCase));

                if (follower is null)
                {
                    var id = Guid.NewGuid();

                    follower = new Class
                    {
                        Id = id,
                        Name = blueprint.Name,
                        Description = blueprint.Description,
                        Icon = blueprint.Icon,
                        Kind = blueprint.Kind,
                        Renderer = blueprint.Renderer,
                        State = ClassState.Active,
                        PortalVisible = blueprint.PortalVisible,
                        WorkspaceId = workspaceId,
                        BlueprintId = blueprint.Id,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    };

                    CoreHub.ClassManager.Add(follower);
                    created.Add(follower);
                }
                else if (follower.BlueprintId != blueprint.Id)
                {
                    follower.BlueprintId = blueprint.Id;
                    follower.Updated = DateTime.UtcNow;
                    CoreHub.ClassManager.Update(follower);
                }

                Synchronize(follower.Id);
            }

            return created;
        }

        /// <inheritdoc/>
        public void Connect()
        {
            lock (_gate)
            {
                if (_connected)
                {
                    return;
                }

                _connected = true;
            }

            CoreHub.FieldManager.FieldAdded += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.FieldManager.FieldUpdated += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.FieldManager.FieldRemoved += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.StatusManager.StatusAdded += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.StatusManager.StatusUpdated += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.StatusManager.StatusRemoved += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.PriorityManager.PriorityAdded += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.PriorityManager.PriorityUpdated += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.PriorityManager.PriorityRemoved += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.WorkflowManager.WorkflowAdded += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.WorkflowManager.WorkflowUpdated += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.WorkflowManager.WorkflowRemoved += (_, x) => OnStructureChanged(x?.ClassId);
            CoreHub.ClassManager.ClassUpdated += (_, x) => OnClassChanged(x);
            CoreHub.ClassManager.ClassAdded += (_, x) => OnClassChanged(x);
        }

        /// <summary>
        /// Takes a change of a class structure to the followers when the class is a blueprint.
        /// </summary>
        /// <remarks>
        /// Also called by the workflow editor, which writes a workflow's graph without going
        /// through the workflow manager.
        /// </remarks>
        /// <param name="classId">The class whose structure changed.</param>
        public void OnStructureChanged(Guid? classId)
        {
            if (_synchronizing.Value > 0 || classId is not Guid id)
            {
                return;
            }

            try
            {
                if (CoreHub.ClassManager.GetClass(id) is { IsBlueprint: true })
                {
                    SynchronizeFollowers(id);
                }
            }
            catch (Exception ex)
            {
                // the change itself was legitimate and is stored; the followers catch up on the
                // next change or start
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Synchronizes a class that was bound to a blueprint or changed while bound.
        /// </summary>
        /// <param name="class">The class.</param>
        private void OnClassChanged(Class @class)
        {
            if (_synchronizing.Value > 0 || @class?.BlueprintId is null)
            {
                return;
            }

            try
            {
                Synchronize(@class.Id);
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Synchronizes the statuses.
        /// </summary>
        /// <returns>The follower's status per blueprint status.</returns>
        private static Dictionary<Guid, Guid> SynchronizeStatuses(Class blueprint, Class follower, BlueprintSyncResult result)
        {
            var source = CoreHub.StatusManager.GetStatuses(new ClassIdParameter(blueprint.Id)).ToList();
            var target = CoreHub.StatusManager.GetStatuses(new ClassIdParameter(follower.Id)).ToList();
            var map = new Dictionary<Guid, Guid>();

            foreach (var item in source)
            {
                var copy = Match(target, item, x => x.Id, x => x.Name);

                if (copy is null)
                {
                    copy = new Status
                    {
                        Id = Guid.NewGuid(),
                        ClassId = follower.Id,
                        BlueprintSourceId = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        CategoryId = item.CategoryId,
                        State = item.State,
                        Icon = item.Icon,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    };

                    CoreHub.StatusManager.Add(copy);
                    target.Add(copy);
                    result.Added++;
                }
                else if (copy.BlueprintSourceId != item.Id || copy.Name != item.Name || copy.Description != item.Description
                    || copy.CategoryId != item.CategoryId || copy.State != item.State)
                {
                    copy.BlueprintSourceId = item.Id;
                    copy.Name = item.Name;
                    copy.Description = item.Description;
                    copy.CategoryId = item.CategoryId;
                    copy.State = item.State;
                    copy.Icon = item.Icon;
                    copy.Updated = DateTime.UtcNow;
                    CoreHub.StatusManager.Update(copy);
                    result.Updated++;
                }

                map[item.Id] = copy.Id;
            }

            foreach (var orphan in Orphans(target, source, x => x.Id))
            {
                if (orphan.State != StatusState.Archived)
                {
                    orphan.State = StatusState.Archived;
                    orphan.Updated = DateTime.UtcNow;
                    CoreHub.StatusManager.Update(orphan);
                    result.Retired++;
                }
            }

            return map;
        }

        /// <summary>
        /// Synchronizes the priorities.
        /// </summary>
        /// <returns>The follower's priority per blueprint priority.</returns>
        private static Dictionary<Guid, Guid> SynchronizePriorities(Class blueprint, Class follower, BlueprintSyncResult result)
        {
            var source = CoreHub.PriorityManager.GetPriorities(new ClassIdParameter(blueprint.Id)).ToList();
            var target = CoreHub.PriorityManager.GetPriorities(new ClassIdParameter(follower.Id)).ToList();
            var map = new Dictionary<Guid, Guid>();

            foreach (var item in source)
            {
                var copy = Match(target, item, x => x.Id, x => x.Name);

                if (copy is null)
                {
                    copy = new Priority
                    {
                        Id = Guid.NewGuid(),
                        ClassId = follower.Id,
                        BlueprintSourceId = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        Order = item.Order,
                        State = item.State,
                        Icon = item.Icon,
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    };

                    CoreHub.PriorityManager.Add(copy);
                    target.Add(copy);
                    result.Added++;
                }
                else if (copy.BlueprintSourceId != item.Id || copy.Name != item.Name || copy.Description != item.Description
                    || copy.Order != item.Order || copy.State != item.State)
                {
                    copy.BlueprintSourceId = item.Id;
                    copy.Name = item.Name;
                    copy.Description = item.Description;
                    copy.Order = item.Order;
                    copy.State = item.State;
                    copy.Icon = item.Icon;
                    copy.Updated = DateTime.UtcNow;
                    CoreHub.PriorityManager.Update(copy);
                    result.Updated++;
                }

                map[item.Id] = copy.Id;
            }

            foreach (var orphan in Orphans(target, source, x => x.Id))
            {
                if (orphan.State != PriorityState.Archived)
                {
                    orphan.State = PriorityState.Archived;
                    orphan.Updated = DateTime.UtcNow;
                    CoreHub.PriorityManager.Update(orphan);
                    result.Retired++;
                }
            }

            return map;
        }

        /// <summary>
        /// Synchronizes the workflows: their own rows, then their graphs.
        /// </summary>
        /// <returns>The follower's workflow per blueprint workflow.</returns>
        private static Dictionary<Guid, Guid> SynchronizeWorkflows(Class blueprint, Class follower, IReadOnlyDictionary<Guid, Guid> statusMap, BlueprintSyncResult result)
        {
            var source = CoreHub.WorkflowManager.GetWorkflows(new ClassIdParameter(blueprint.Id)).ToList();
            var target = CoreHub.WorkflowManager.GetWorkflows(new ClassIdParameter(follower.Id)).ToList();
            var managedStatuses = CoreHub.StatusManager
                .GetStatuses(new ClassIdParameter(follower.Id))
                .Where(x => x.BlueprintSourceId.HasValue)
                .Select(x => x.Id)
                .ToHashSet();
            var map = new Dictionary<Guid, Guid>();

            foreach (var item in source)
            {
                var copy = Match(target, item, x => x.Id, x => x.Name);

                if (copy is null)
                {
                    copy = new Workflow
                    {
                        Id = Guid.NewGuid(),
                        ClassId = follower.Id,
                        BlueprintSourceId = item.Id,
                        Name = item.Name,
                        Description = item.Description,
                        State = item.State,
                        Icon = item.Icon,
                        WorkflowStatuses = [],
                        Transitions = [],
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    };

                    CoreHub.WorkflowManager.Add(copy);
                    target.Add(copy);
                    result.Added++;
                }
                else if (copy.BlueprintSourceId != item.Id || copy.Name != item.Name || copy.Description != item.Description || copy.State != item.State)
                {
                    copy.BlueprintSourceId = item.Id;
                    copy.Name = item.Name;
                    copy.Description = item.Description;
                    copy.State = item.State;
                    copy.Updated = DateTime.UtcNow;
                    CoreHub.WorkflowManager.Update(copy);
                    result.Updated++;
                }

                var structure = CoreHub.WorkflowManager.GetWorkflowWithStructure(item.Id);

                if (structure is not null)
                {
                    ModelHub.SynchronizeWorkflowGraph(copy.Id, structure, statusMap, managedStatuses);
                }

                map[item.Id] = copy.Id;
            }

            foreach (var orphan in Orphans(target, source, x => x.Id))
            {
                if (orphan.State != WorkflowState.Archived)
                {
                    orphan.State = WorkflowState.Archived;
                    orphan.Updated = DateTime.UtcNow;
                    CoreHub.WorkflowManager.Update(orphan);
                    result.Retired++;
                }
            }

            return map;
        }

        /// <summary>
        /// Synchronizes the fields, translating their references to workflows and priorities
        /// into the follower's copies.
        /// </summary>
        private static void SynchronizeFields
        (
            Class blueprint,
            Class follower,
            IReadOnlyDictionary<Guid, Guid> workflowMap,
            IReadOnlyDictionary<Guid, Guid> priorityMap,
            BlueprintSyncResult result
        )
        {
            var source = CoreHub.FieldManager.GetFields(new ClassIdParameter(blueprint.Id)).ToList();
            var target = CoreHub.FieldManager.GetFields(new ClassIdParameter(follower.Id)).ToList();

            Guid? translate(Guid? id, IReadOnlyDictionary<Guid, Guid> map) =>
                id is Guid value && map.TryGetValue(value, out var mapped) ? mapped : null;

            foreach (var item in source)
            {
                var copy = Match(target, item, x => x.Id, x => x.Name);
                var added = copy is null;

                copy ??= new Field
                {
                    Id = Guid.NewGuid(),
                    ClassId = follower.Id,
                    Created = DateTime.UtcNow
                };

                var before = added ? null : Fingerprint(copy);

                copy.BlueprintSourceId = item.Id;
                copy.Name = item.Name;
                copy.Description = item.Description;
                copy.HelpText = item.HelpText;
                copy.Placeholder = item.Placeholder;
                copy.State = item.State;
                copy.Icon = item.Icon;
                copy.FieldType = item.FieldType;
                copy.Cardinality = item.Cardinality;
                copy.CardinalityMin = item.CardinalityMin;
                copy.CardinalityMax = item.CardinalityMax;
                copy.CardinalityUnlimited = item.CardinalityUnlimited;
                copy.RegexPattern = item.RegexPattern;
                copy.Options = item.Options is null ? null : [.. item.Options];
                copy.WorkflowId = translate(item.WorkflowId, workflowMap);
                copy.DefaultPriorityId = translate(item.DefaultPriorityId, priorityMap);
                copy.SelectedPriorityIds = item.SelectedPriorityIds is null
                    ? null
                    : [.. item.SelectedPriorityIds.Select(x => translate(x, priorityMap)).Where(x => x.HasValue).Select(x => x.Value)];
                copy.ValidationRules = item.ValidationRules is null ? null : [.. item.ValidationRules];
                copy.DefaultSpec = item.DefaultSpec;
                copy.Required = item.Required;
                copy.Unique = item.Unique;
                copy.Deprecated = item.Deprecated;
                copy.AccessModifier = item.AccessModifier;

                if (added)
                {
                    copy.Updated = DateTime.UtcNow;
                    CoreHub.FieldManager.Add(copy);
                    target.Add(copy);
                    result.Added++;
                }
                else if (before != Fingerprint(copy))
                {
                    copy.Updated = DateTime.UtcNow;
                    CoreHub.FieldManager.Update(copy);
                    result.Updated++;
                }
            }

            // a field the blueprint dropped is deprecated rather than deleted: objects still
            // carry values of it, which a deprecated field keeps readable
            foreach (var orphan in Orphans(target, source, x => x.Id))
            {
                if (!orphan.Deprecated)
                {
                    orphan.Deprecated = true;
                    orphan.Updated = DateTime.UtcNow;
                    CoreHub.FieldManager.Update(orphan);
                    result.Retired++;
                }
            }
        }

        /// <summary>
        /// Finds the follower's copy of a blueprint element: the one naming it, else a local
        /// element of the same name, which is adopted.
        /// </summary>
        /// <typeparam name="T">The kind of element.</typeparam>
        /// <param name="target">The follower's elements.</param>
        /// <param name="item">The blueprint element.</param>
        /// <param name="id">How an element is identified.</param>
        /// <param name="name">How an element is named.</param>
        /// <returns>The copy, or <see langword="null"/>.</returns>
        private static T Match<T>(IEnumerable<T> target, T item, Func<T, Guid> id, Func<T, string> name)
            where T : class, IBlueprintManaged
        {
            var sourceId = id(item);

            return target.FirstOrDefault(x => x.BlueprintSourceId == sourceId)
                ?? target.FirstOrDefault(x => x.BlueprintSourceId is null
                    && string.Equals(name(x), name(item), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the managed copies whose blueprint element is gone.
        /// </summary>
        /// <typeparam name="T">The kind of element.</typeparam>
        /// <param name="target">The follower's elements.</param>
        /// <param name="source">The blueprint's elements.</param>
        /// <param name="id">How an element is identified.</param>
        /// <returns>The orphaned copies.</returns>
        private static IEnumerable<T> Orphans<T>(IEnumerable<T> target, IEnumerable<T> source, Func<T, Guid> id)
            where T : class, IBlueprintManaged
        {
            var ids = source.Select(id).ToHashSet();

            return [.. target.Where(x => x.BlueprintSourceId is Guid sourceId && !ids.Contains(sourceId))];
        }

        /// <summary>
        /// Summarizes the definition of a field, so an unchanged copy is not written again.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns>The fingerprint.</returns>
        private static string Fingerprint(Field field)
        {
            return string.Join("\u001f",
                field.BlueprintSourceId, field.Name, field.Description, field.HelpText, field.Placeholder, field.State,
                field.FieldType, field.Cardinality, field.CardinalityMin, field.CardinalityMax, field.CardinalityUnlimited,
                field.RegexPattern, string.Join("\u001e", field.Options ?? []), field.WorkflowId, field.DefaultPriorityId,
                string.Join("\u001e", field.SelectedPriorityIds ?? []), string.Join("\u001e", field.ValidationRules ?? []),
                field.DefaultSpec, field.Required, field.Unique, field.Deprecated, field.AccessModifier);
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
