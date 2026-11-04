using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Moving objects into another workspace (<c>kleenestar.hierarchy.md</c>, <i>Move Across
    /// Workspaces</i>): a transfer is an operation run - planned with a mapping of classes, fields,
    /// workflow states and security levels the caller sees before anything changes, then processed
    /// parents first through the object manager.
    /// </summary>
    /// <remarks>
    /// The object lands at the root of the target workspace, its subtree below it. Per object: a
    /// new key in the target workspace, the old one kept as an alias
    /// (<see cref="ObjectKeyAlias"/>, so links and mentions still resolve); the class mapped; the
    /// field values moved by field name onto the target class (a field it lacks is dropped - the
    /// commit records the removal); the workflow state by name, else by status category, else the
    /// target workflow's entry state; the security level by name, else refused unless the caller
    /// declassifies; sprint membership, milestone and open approvals - all per workspace - are
    /// ended. Relations, comments, attachments, watchers and the history hang off the object's id
    /// and move with it.
    /// </remarks>
    public sealed partial class ObjectOperationManager
    {
        private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        /// <inheritdoc/>
        public ObjectOperationPlan PlanTransfer(Guid rootId, TransferOptions options, Func<ObjectEntity, bool> mayWrite, Func<Guid, bool> mayCreateIn)
        {
            const string kind = ObjectOperationKinds.Transfer;
            mayWrite ??= _ => true;
            mayCreateIn ??= _ => true;

            ObjectOperationPlan Refuse(string key, ObjectEntity root, TransferPlan details, params object[] args) =>
                new(kind, root, [], 0, 0, 0, key, args, null, details);

            var root = ObjectHierarchy.Read(rootId);

            if (root is null)
            {
                return Refuse("kleenestar.core:operation.refused.missing", null, null);
            }

            var target = options is null ? null : CoreHub.WorkspaceManager.GetWorkspace(options.TargetWorkspaceId);

            if (target is null || target.Id == root.WorkspaceId)
            {
                return Refuse("kleenestar.core:transfer.refused.workspace", root, null);
            }

            // the subtree, judged like every recursive operation: hidden or unchangeable members block
            var limit = Settings.RecursiveLimit;
            var below = ObjectHierarchy.GetDescendantIds(rootId);

            if (below.Count > limit)
            {
                return Refuse("kleenestar.core:operation.refused.limit", root, null, below.Count, limit);
            }

            var all = Load(below, unrestricted: true);
            var visible = Load(below, unrestricted: false).Select(x => x.Id).ToHashSet();
            var hidden = all.Count(x => !visible.Contains(x.Id));
            var unwritable = all.Count(x => visible.Contains(x.Id) && !mayWrite(x));

            if (hidden > 0)
            {
                return new(kind, root, [], all.Count, hidden, unwritable, "kleenestar.core:operation.refused.hidden", [hidden]);
            }

            if (unwritable > 0 || !mayWrite(root))
            {
                return new(kind, root, [], all.Count, hidden, unwritable, "kleenestar.core:operation.refused.permission", [unwritable + (mayWrite(root) ? 0 : 1)]);
            }

            var depth = Depths(root, all);
            var items = new List<ObjectEntity> { root };

            items.AddRange(all.OrderBy(x => depth.GetValueOrDefault(x.Id)));

            // the mapping, which the caller sees and may adjust before anything changes
            var details = Map(items, target.Id, options);

            if (details.Classes.Any(x => x.TargetId is null))
            {
                return Refuse("kleenestar.core:transfer.refused.classes", root, details,
                    string.Join(", ", details.Classes.Where(x => x.TargetId is null).Select(x => x.SourceName)));
            }

            var forbidden = details.Classes.FirstOrDefault(x => !mayCreateIn(x.TargetId!.Value));

            if (forbidden is not null)
            {
                return Refuse("kleenestar.core:transfer.refused.create", root, details, forbidden.TargetName);
            }

            var lost = details.Levels.FirstOrDefault(x => x.TargetId is null);

            if (lost is not null && !options.Declassify)
            {
                return Refuse("kleenestar.core:transfer.refused.level", root, details, lost.Name);
            }

            var height = all.Count == 0 ? 0 : all.Max(x => depth.GetValueOrDefault(x.Id));
            // the root lands at the target root: level 1
            if (1 + height > Settings.EffectiveMaxDepth)
            {
                return Refuse("kleenestar.core:transfer.refused.depth", root, details, Settings.EffectiveMaxDepth);
            }

            return new(kind, root, items, all.Count, 0, 0, null, [], null, details);
        }

        /// <inheritdoc/>
        public ObjectOperationStart StartTransfer(Guid rootId, TransferOptions options, string key, Guid identityId, Func<ObjectEntity, bool> mayWrite, Func<Guid, bool> mayCreateIn)
        {
            if (!string.IsNullOrWhiteSpace(key) && ModelHub.GetObjectOperationByKey(key.Trim()) is { } existing)
            {
                return new ObjectOperationStart(existing, null);
            }

            var plan = PlanTransfer(rootId, options, mayWrite, mayCreateIn);

            if (plan.Refused)
            {
                return new ObjectOperationStart(null, plan);
            }

            var now = DateTime.UtcNow;
            var stored = new StoredTransfer
            {
                ClassMap = plan.Transfer.Classes.ToDictionary(x => x.SourceId, x => x.TargetId!.Value),
                Declassify = options.Declassify
            };

            var operation = new ObjectOperation
            {
                Key = string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("N") : key.Trim(),
                Kind = ObjectOperationKinds.Transfer,
                RootId = rootId,
                WorkspaceId = plan.Root.WorkspaceId,
                TargetWorkspaceId = options.TargetWorkspaceId,
                Options = JsonSerializer.Serialize(stored),
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

        /// <summary>
        /// Builds the mapping of a transfer: classes (given, else by name), dropped fields, the
        /// workflow states as they will be mapped, the security levels.
        /// </summary>
        private static TransferPlan Map(IReadOnlyList<ObjectEntity> items, Guid targetWorkspaceId, TransferOptions options)
        {
            var targets = CoreHub.ClassManager.GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, targetWorkspaceId))
                .Where(x => x.State == ClassState.Active && !x.IsAbstract)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var classes = new List<TransferClass>();
            var dropped = new List<TransferField>();
            var states = new List<TransferState>();
            var levels = new List<TransferLevel>();

            foreach (var group in items.GroupBy(x => x.ClassId))
            {
                var source = CoreHub.ClassManager.GetClass(group.Key);

                var candidates = targets;
                var chosen = options?.ClassMap is not null && options.ClassMap.TryGetValue(group.Key, out var wanted)
                    ? candidates.FirstOrDefault(x => x.Id == wanted)
                    : null;

                chosen ??= candidates.FirstOrDefault(x => string.Equals(x.Name?.Trim(), source?.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

                classes.Add(new TransferClass(group.Key, source?.Name ?? group.Key.ToString(), chosen?.Id, chosen?.Name,
                    [.. candidates.Select(x => new TransferCandidate(x.Id, x.Name))]));

                if (chosen is null)
                {
                    continue;
                }

                var sourceFields = Fields(group.Key);
                var targetFields = Fields(chosen.Id);

                foreach (var field in sourceFields.Where(x => x.FieldType != FieldType.Workflow))
                {
                    if (!targetFields.Any(x => x.FieldType != FieldType.Workflow && string.Equals(x.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        dropped.Add(new TransferField(source?.Name, field.Name));
                    }
                }

                var sourceFlow = sourceFields.FirstOrDefault(x => x.FieldType == FieldType.Workflow);

                if (sourceFlow is not null)
                {
                    foreach (var name in group
                        .Select(x => CoreHub.ValueManager.GetValue(x.Id, sourceFlow.Id)?.Data)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        states.Add(new TransferState(source?.Name, name, MapState(sourceFlow, name, targetFields)?.Name));
                    }
                }

                foreach (var levelId in group.Where(x => x.SecurityLevelId.HasValue).Select(x => x.SecurityLevelId!.Value).Distinct())
                {
                    var level = CoreHub.SecurityLevelManager.GetSecurityLevel(levelId);
                    var match = CoreHub.SecurityLevelManager.GetSecurityLevels(new ClassIdParameter(chosen.Id))
                        .FirstOrDefault(x => string.Equals(x.Name, level?.Name, StringComparison.OrdinalIgnoreCase));

                    levels.Add(new TransferLevel(level?.Name ?? levelId.ToString(), match?.Id));
                }
            }

            return new TransferPlan(classes, dropped, states, levels);
        }

        /// <summary>
        /// Moves one object of a transfer run into the target workspace.
        /// </summary>
        private static void Transfer(ObjectOperation operation, ObjectEntity @object)
        {
            var stored = JsonSerializer.Deserialize<StoredTransfer>(operation.Options ?? "{}", _json) ?? new StoredTransfer();
            var targetWorkspaceId = operation.TargetWorkspaceId
                ?? throw new InvalidOperationException("The transfer names no target workspace.");

            // already there: a resumed run that processed this object before the interruption
            if (@object.WorkspaceId == targetWorkspaceId)
            {
                return;
            }

            var targetClass = stored.ClassMap.TryGetValue(@object.ClassId, out var mapped) ? CoreHub.ClassManager.GetClass(mapped) : null;

            if (targetClass is null)
            {
                throw new InvalidOperationException($"The class of {@object.Key} has no counterpart in the target workspace.");
            }

            var targetClassId = targetClass.Id;

            var actor = operation.IdentityId ?? Guid.Empty;
            var oldKey = @object.Key;
            var sourceFields = Fields(@object.ClassId);
            var targetFields = Fields(targetClassId);
            var targetFlow = targetFields.FirstOrDefault(x => x.FieldType == FieldType.Workflow);

            using (CoreHub.CommitManager.BeginCommit(@object.Id, CommitType.Updated, actor))
            {
                foreach (var value in CoreHub.ValueManager.GetValues(@object.Id).ToList())
                {
                    var field = sourceFields.FirstOrDefault(x => x.Id == value.FieldId);
                    Field into = null;
                    var data = value.Data;

                    if (field?.FieldType == FieldType.Workflow)
                    {
                        var state = MapState(field, value.Data, targetFields);

                        into = state is null ? null : targetFlow;
                        data = state?.Name;
                    }
                    else if (field is not null)
                    {
                        into = targetFields.FirstOrDefault(x => x.FieldType != FieldType.Workflow && string.Equals(x.Name, field.Name, StringComparison.OrdinalIgnoreCase));
                    }

                    if (into is null)
                    {
                        CoreHub.ValueManager.Remove(value.Id);
                        continue;
                    }

                    value.FieldId = into.Id;
                    value.Data = data;
                    CoreHub.ValueManager.Update(value);
                }

                // the security level by name among the target class's levels; without one the run
                // only gets here when the caller chose to declassify
                Guid? level = null;

                if (@object.SecurityLevelId is Guid levelId)
                {
                    var name = CoreHub.SecurityLevelManager.GetSecurityLevel(levelId)?.Name;

                    level = CoreHub.SecurityLevelManager.GetSecurityLevels(new ClassIdParameter(targetClassId))
                        .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;

                    if (level is null && !stored.Declassify)
                    {
                        throw new InvalidOperationException($"The security level of {@object.Key} has no counterpart in the target class.");
                    }
                }

                @object.WorkspaceId = targetWorkspaceId;
                @object.ClassId = targetClassId;
                @object.Kind = targetClass.Kind;
                @object.Key = CoreHub.ObjectManager.NextObjectKey(targetWorkspaceId);
                @object.SecurityLevelId = level;
                @object.SprintId = null;
                @object.UpdaterId = operation.IdentityId;
                @object.Updated = DateTime.UtcNow;

                // the root lands at the target root; its subtree stays below it
                if (@object.Id == operation.RootId)
                {
                    @object.ParentId = null;
                }

                CoreHub.ObjectManager.Update(@object);
            }

            ModelHub.AddObjectKeyAlias(oldKey, @object.Id);

            // the path follows the new workspace (the parent was moved before, parents first)
            ObjectPathIndex.Added(@object);

            // what belongs to the source workspace ends: the milestone, the open approvals
            if (ModelHub.GetObjectPlannings([@object.Id]).TryGetValue(@object.Id, out var planning) && planning.MilestoneId.HasValue)
            {
                ModelHub.UpdateObjectPlanning(@object.Id, x => x.MilestoneId = null);
            }

            foreach (var approval in CoreHub.ApprovalManager.GetApprovals(@object.Id).Where(x => x.State == ApprovalState.Pending))
            {
                CoreHub.ApprovalManager.Cancel(approval.Id, actor, administrator: true);
            }
        }

        /// <summary>
        /// Maps a workflow state onto the target class's workflow: by name, else the first state of
        /// the same category, else the workflow's first entry state.
        /// </summary>
        private static Status MapState(Field sourceField, string data, IReadOnlyList<Field> targetFields)
        {
            var targetFlow = targetFields.FirstOrDefault(x => x.FieldType == FieldType.Workflow);

            if (targetFlow?.WorkflowId is not Guid targetWorkflowId || string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            var target = CoreHub.WorkflowManager.GetWorkflowWithStructure(targetWorkflowId);
            var byName = CoreHub.WorkflowManager.ResolveStatus(target, data);

            if (byName is not null)
            {
                return byName;
            }

            var source = sourceField?.WorkflowId is Guid sourceWorkflowId
                ? CoreHub.WorkflowManager.ResolveStatus(CoreHub.WorkflowManager.GetWorkflowWithStructure(sourceWorkflowId), data)
                : null;

            return (source is null ? null : target?.Statuses?.FirstOrDefault(x => x.CategoryId == source.CategoryId && x.State == StatusState.Active))
                ?? CoreHub.WorkflowManager.GetTargetStatuses(target, null).FirstOrDefault();
        }

        /// <summary>
        /// Returns the active fields of a class.
        /// </summary>
        private static IReadOnlyList<Field> Fields(Guid classId)
        {
            return [.. CoreHub.FieldManager.GetFields(new ClassIdParameter(classId)).Where(x => !x.Deprecated && x.State == FieldState.Active)];
        }

        /// <summary>
        /// The options of a transfer as the run keeps them.
        /// </summary>
        private sealed class StoredTransfer
        {
            public Dictionary<Guid, Guid> ClassMap { get; set; } = [];

            public bool Declassify { get; set; }
        }
    }
}
