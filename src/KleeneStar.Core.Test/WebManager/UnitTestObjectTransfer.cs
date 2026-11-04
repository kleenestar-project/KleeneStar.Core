using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Covers moving objects across workspaces (<c>kleenestar.hierarchy.md</c>, <i>Move Across
    /// Workspaces</i>): the plan's mapping (classes by name or as chosen, dropped fields, states by
    /// name or category, security levels), the run (new keys, aliases, values moved, state mapped,
    /// subtree along, the root at the target root, sprint ended) and the key numbering that never
    /// hands out a former key.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectTransfer
    {
        private static readonly Guid SourceId = Guid.Parse("C0A1B2C3-0501-4000-8000-0000000C0501");
        private static readonly Guid TargetId = Guid.Parse("C0A1B2C3-0502-4000-8000-0000000C0502");
        private static readonly Guid SrcEpicClass = Guid.Parse("C0A1B2C3-0503-4000-8000-0000000C0503");
        private static readonly Guid SrcTaskClass = Guid.Parse("C0A1B2C3-0504-4000-8000-0000000C0504");
        private static readonly Guid DstEpicClass = Guid.Parse("C0A1B2C3-0505-4000-8000-0000000C0505");
        private static readonly Guid DstTaskClass = Guid.Parse("C0A1B2C3-0506-4000-8000-0000000C0506");
        private static readonly Guid DstJobClass = Guid.Parse("C0A1B2C3-0507-4000-8000-0000000C0507");
        private static readonly Guid ToDo = Guid.Parse("C0A1B2C3-0510-4000-8000-0000000C0510");
        private static readonly Guid Doing = Guid.Parse("C0A1B2C3-0511-4000-8000-0000000C0511");
        private static readonly Guid SrcOpen = Guid.Parse("C0A1B2C3-0512-4000-8000-0000000C0512");
        private static readonly Guid SrcWorking = Guid.Parse("C0A1B2C3-0513-4000-8000-0000000C0513");
        private static readonly Guid DstOpen = Guid.Parse("C0A1B2C3-0514-4000-8000-0000000C0514");
        private static readonly Guid DstBusy = Guid.Parse("C0A1B2C3-0515-4000-8000-0000000C0515");
        private static readonly Guid SrcFlow = Guid.Parse("C0A1B2C3-0516-4000-8000-0000000C0516");
        private static readonly Guid DstFlow = Guid.Parse("C0A1B2C3-0517-4000-8000-0000000C0517");
        private static readonly Guid SrcStatusField = Guid.Parse("C0A1B2C3-0518-4000-8000-0000000C0518");
        private static readonly Guid DstStatusField = Guid.Parse("C0A1B2C3-0519-4000-8000-0000000C0519");
        private static readonly Guid SrcNote = Guid.Parse("C0A1B2C3-0520-4000-8000-0000000C0520");
        private static readonly Guid SrcExtra = Guid.Parse("C0A1B2C3-0521-4000-8000-0000000C0521");
        private static readonly Guid DstNote = Guid.Parse("C0A1B2C3-0522-4000-8000-0000000C0522");
        private static readonly Guid SrcLevel = Guid.Parse("C0A1B2C3-0523-4000-8000-0000000C0523");
        private static readonly Guid EpicId = Guid.Parse("C0A1B2C3-0530-4000-8000-0000000C0530");
        private static readonly Guid TaskAId = Guid.Parse("C0A1B2C3-0531-4000-8000-0000000C0531");
        private static readonly Guid TaskBId = Guid.Parse("C0A1B2C3-0532-4000-8000-0000000C0532");
        private static readonly Guid InboxId = Guid.Parse("C0A1B2C3-0533-4000-8000-0000000C0533");
        private static readonly Guid SprintId = Guid.Parse("C0A1B2C3-0534-4000-8000-0000000C0534");

        /// <summary>
        /// Seeds SRC (epic class, task class with Note/Extra/Status, a level) holding
        /// Epic / (task A "Working", task B "Open", classified); DST (epic class, task class with
        /// Note/Status - "Open" and "Busy", same categories - a job class) holding an epic.
        /// </summary>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == SourceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = SourceId, Key = "SRC", Name = "source" });
            db.Workspaces.Add(new Workspace { Id = TargetId, Key = "DST", Name = "target" });
            db.Classes.AddRange
            (
                new Class { Id = SrcEpicClass, Name = "Epic", Kind = ObjectKind.Issue, WorkspaceId = SourceId },
                new Class { Id = SrcTaskClass, Name = "Task", Kind = ObjectKind.Issue, WorkspaceId = SourceId },
                new Class { Id = DstEpicClass, Name = "Epic", Kind = ObjectKind.Issue, WorkspaceId = TargetId },
                new Class { Id = DstTaskClass, Name = "Task", Kind = ObjectKind.Issue, WorkspaceId = TargetId },
                new Class { Id = DstJobClass, Name = "Job", Kind = ObjectKind.Issue, WorkspaceId = TargetId }
            );
            db.StatusCategories.Add(new StatusCategory { Id = ToDo, Name = "ToDo", Color = "#888888", IsDefault = true });
            db.StatusCategories.Add(new StatusCategory { Id = Doing, Name = "InProgress", Color = "#0000ff" });
            db.Statuses.AddRange
            (
                new Status { Id = SrcOpen, Name = "Open", ClassId = SrcTaskClass, CategoryId = ToDo, State = StatusState.Active },
                new Status { Id = SrcWorking, Name = "Working", ClassId = SrcTaskClass, CategoryId = Doing, State = StatusState.Active },
                new Status { Id = DstOpen, Name = "Open", ClassId = DstTaskClass, CategoryId = ToDo, State = StatusState.Active },
                new Status { Id = DstBusy, Name = "Busy", ClassId = DstTaskClass, CategoryId = Doing, State = StatusState.Active }
            );
            db.Workflows.Add(new Workflow { Id = SrcFlow, Name = "src", ClassId = SrcTaskClass, State = WorkflowState.Active, WorkflowStatuses = [new WorkflowStatus { StatusId = SrcOpen, IsStart = true }, new WorkflowStatus { StatusId = SrcWorking }] });
            db.Workflows.Add(new Workflow { Id = DstFlow, Name = "dst", ClassId = DstTaskClass, State = WorkflowState.Active, WorkflowStatuses = [new WorkflowStatus { StatusId = DstOpen, IsStart = true }, new WorkflowStatus { StatusId = DstBusy }] });
            db.Fields.AddRange
            (
                new Field { Id = SrcStatusField, Name = "Status", ClassId = SrcTaskClass, FieldType = FieldType.Workflow, WorkflowId = SrcFlow, State = FieldState.Active },
                new Field { Id = SrcNote, Name = "Note", ClassId = SrcTaskClass, FieldType = FieldType.Text, State = FieldState.Active },
                new Field { Id = SrcExtra, Name = "Extra", ClassId = SrcTaskClass, FieldType = FieldType.Text, State = FieldState.Active },
                new Field { Id = DstStatusField, Name = "Status", ClassId = DstTaskClass, FieldType = FieldType.Workflow, WorkflowId = DstFlow, State = FieldState.Active },
                new Field { Id = DstNote, Name = "note", ClassId = DstTaskClass, FieldType = FieldType.Text, State = FieldState.Active }
            );
            db.SecurityLevels.Add(new SecurityLevel { Id = SrcLevel, Name = "Confidential", ClassId = SrcTaskClass, State = SecurityLevelState.Active });
            db.Sprints.Add(new Sprint { Id = SprintId, Name = "Sprint 1", WorkspaceId = SourceId });

            ObjectEntity Make(Guid id, string key, string summary, Guid classId, string kind, Guid workspace, Guid? parent = null) => new(id)
            {
                Key = key,
                Summary = summary,
                ClassId = classId,
                Kind = kind,
                WorkspaceId = workspace,
                ParentId = parent,
                State = WorkspaceState.Active
            };

            var taskA = Make(TaskAId, "SRC-2", "Task A", SrcTaskClass, ObjectKind.Issue, SourceId, EpicId);
            taskA.SprintId = SprintId;
            var taskB = Make(TaskBId, "SRC-3", "Task B", SrcTaskClass, ObjectKind.Issue, SourceId, EpicId);
            taskB.SecurityLevelId = SrcLevel;

            db.Objects.AddRange
            (
                Make(EpicId, "SRC-1", "Contracts", SrcEpicClass, ObjectKind.Issue, SourceId),
                taskA,
                taskB,
                Make(InboxId, "DST-1", "Inbox", DstEpicClass, ObjectKind.Issue, TargetId)
            );
            db.Values.AddRange
            (
                new Value { Id = Guid.NewGuid(), ObjectId = TaskAId, FieldId = SrcStatusField, Data = "Working" },
                new Value { Id = Guid.NewGuid(), ObjectId = TaskAId, FieldId = SrcNote, Data = "keep" },
                new Value { Id = Guid.NewGuid(), ObjectId = TaskAId, FieldId = SrcExtra, Data = "lost" },
                new Value { Id = Guid.NewGuid(), ObjectId = TaskBId, FieldId = SrcStatusField, Data = "Open" }
            );
            db.SaveChanges();
        }

        private static ObjectEntity Read(Guid id)
        {
            return ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.Id == id)).FirstOrDefault();
        }

        private static TransferOptions Options(bool declassify = false, IReadOnlyDictionary<Guid, Guid> map = null) =>
            new(TargetId, map ?? new Dictionary<Guid, Guid>(), declassify);

        /// <summary>
        /// The plan maps the classes by name, lists the field the target lacks, maps "Working" by
        /// its category onto "Busy", and refuses the classified task until the caller declassifies.
        /// </summary>
        [Fact]
        public void Plan_ShowsTheMapping()
        {
            Seed(nameof(Plan_ShowsTheMapping));

            // the level names no group (closed); visibility is not the question here
            using var unrestricted = CoreHub.SecurityLevelManager.BeginUnrestricted();

            var refused = CoreHub.ObjectOperationManager.PlanTransfer(EpicId, Options(), _ => true, _ => true);

            Assert.Equal("kleenestar.core:transfer.refused.level", refused.RefusalKey);
            Assert.Equal(DstEpicClass, refused.Transfer.Classes.Single(x => x.SourceId == SrcEpicClass).TargetId);
            Assert.Equal(DstTaskClass, refused.Transfer.Classes.Single(x => x.SourceId == SrcTaskClass).TargetId);
            Assert.Contains(refused.Transfer.DroppedFields, x => x.FieldName == "Extra");
            Assert.DoesNotContain(refused.Transfer.DroppedFields, x => x.FieldName == "Note");
            Assert.Contains(refused.Transfer.States, x => x.From == "Working" && x.To == "Busy");
            Assert.Contains(refused.Transfer.States, x => x.From == "Open" && x.To == "Open");

            var plan = CoreHub.ObjectOperationManager.PlanTransfer(EpicId, Options(declassify: true), _ => true, _ => true);
            Assert.False(plan.Refused);
            Assert.Equal(3, plan.Items.Count);

            // a chosen class wins over the name; the job class has no workflow, the states map to none
            var chosen = CoreHub.ObjectOperationManager.PlanTransfer(TaskAId, new TransferOptions(TargetId, new Dictionary<Guid, Guid> { [SrcTaskClass] = DstJobClass }, false), _ => true, _ => true);
            Assert.Equal(DstJobClass, chosen.Transfer.Classes.Single().TargetId);
            Assert.Contains(chosen.Transfer.States, x => x.From == "Working" && x.To is null);

            // the caller must be allowed to create objects there
            Assert.Equal("kleenestar.core:transfer.refused.create",
                CoreHub.ObjectOperationManager.PlanTransfer(TaskAId, Options(), _ => true, id => id != DstTaskClass).RefusalKey);

            // the own workspace is no target
            Assert.Equal("kleenestar.core:transfer.refused.workspace",
                CoreHub.ObjectOperationManager.PlanTransfer(TaskAId, new TransferOptions(SourceId, null, false), _ => true, _ => true).RefusalKey);
        }

        /// <summary>
        /// A classified member the caller cannot see blocks the transfer, like every recursive operation.
        /// </summary>
        [Fact]
        public void Plan_RefusesHiddenMembers()
        {
            Seed(nameof(Plan_RefusesHiddenMembers));

            Assert.Equal("kleenestar.core:operation.refused.hidden",
                CoreHub.ObjectOperationManager.PlanTransfer(EpicId, Options(declassify: true), _ => true, _ => true).RefusalKey);
        }

        /// <summary>
        /// The run moves the epic and its tasks: new keys, the old ones as aliases (which still
        /// find them), values moved by name, the dropped one gone, the state mapped, the epic at the
        /// target root with its tasks below, the sprint ended, the classified task declassified.
        /// </summary>
        [Fact]
        public void Run_MovesTheSubtree()
        {
            Seed(nameof(Run_MovesTheSubtree));

            using var unrestricted = CoreHub.SecurityLevelManager.BeginUnrestricted();

            var run = CoreHub.ObjectOperationManager.StartTransfer(EpicId, Options(declassify: true), null, Guid.Empty, _ => true, _ => true).Operation;

            Assert.NotNull(run);
            Assert.Equal(ObjectOperationState.Completed, run.State);

            var epic = Read(EpicId)!;
            var taskA = Read(TaskAId)!;
            var taskB = Read(TaskBId)!;

            Assert.Equal(TargetId, epic.WorkspaceId);
            Assert.Equal(DstEpicClass, epic.ClassId);
            Assert.Null(epic.ParentId);
            Assert.StartsWith("DST-", epic.Key);
            Assert.Equal(EpicId, taskA.ParentId);
            Assert.Equal(TargetId, taskA.WorkspaceId);
            Assert.Equal(DstTaskClass, taskA.ClassId);
            Assert.Null(taskA.SprintId);
            Assert.Null(taskB.SecurityLevelId);

            Assert.Equal("Busy", CoreHub.ValueManager.GetValue(TaskAId, DstStatusField)?.Data);
            Assert.Equal("keep", CoreHub.ValueManager.GetValue(TaskAId, DstNote)?.Data);
            Assert.DoesNotContain(CoreHub.ValueManager.GetValues(TaskAId), x => x.Data == "lost");

            // the former keys still lead to the objects
            Assert.Equal(TaskAId, CoreHub.ObjectManager.GetObjectByKey("SRC-2")!.Id);
            Assert.Equal(EpicId, CoreHub.ObjectManager.GetObjectByKey("src-1")!.Id);
            Assert.Equal(["SRC-2"], ModelHub.GetObjectKeyAliases(TaskAId));

            // ... and the source numbering never hands one of them out again
            Assert.Equal("SRC-4", CoreHub.ObjectManager.NextObjectKey(SourceId));
        }
    }
}
