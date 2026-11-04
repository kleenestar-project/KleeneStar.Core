using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Covers the recursive operations on the object hierarchy (<c>kleenestar.hierarchy.md</c>): the
    /// path index (maintained on every parent write, rebuilt from the parent relation, consistent
    /// after random moves), archive with contents and its exact restore, recursive delete,
    /// dissolve, clone alone or with contents, hidden members block, the WQL predicate
    /// <c>within(…)</c> and the change notices.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectOperations
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C0A1B2C3-0201-4000-8000-0000000C0201");
        private static readonly Guid EpicClassId = Guid.Parse("C0A1B2C3-0202-4000-8000-0000000C0202");
        private static readonly Guid TaskClassId = Guid.Parse("C0A1B2C3-0203-4000-8000-0000000C0203");
        private static readonly Guid FieldId = Guid.Parse("C0A1B2C3-0204-4000-8000-0000000C0204");
        private static readonly Guid LevelId = Guid.Parse("C0A1B2C3-0205-4000-8000-0000000C0205");
        private static readonly Guid RootId = Guid.Parse("C0A1B2C3-0210-4000-8000-0000000C0210");
        private static readonly Guid SubId = Guid.Parse("C0A1B2C3-0211-4000-8000-0000000C0211");
        private static readonly Guid DeepId = Guid.Parse("C0A1B2C3-0212-4000-8000-0000000C0212");
        private static readonly Guid TaskAId = Guid.Parse("C0A1B2C3-0220-4000-8000-0000000C0220");
        private static readonly Guid TaskBId = Guid.Parse("C0A1B2C3-0221-4000-8000-0000000C0221");
        private static readonly Guid TaskCId = Guid.Parse("C0A1B2C3-0222-4000-8000-0000000C0222");
        private static readonly Guid OtherId = Guid.Parse("C0A1B2C3-0230-4000-8000-0000000C0230");
        private static readonly Guid TwinId = Guid.Parse("C0A1B2C3-0231-4000-8000-0000000C0231");

        /// <summary>
        /// Seeds epics and tasks: Root / (Sub / (Deep / task C), task A, task B (archived on its
        /// own)), Other and Twin at the root.
        /// </summary>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "OPS3", Name = "operations" });
            db.Classes.AddRange
            (
                new Class { Id = EpicClassId, Name = "Epic", Kind = ObjectKind.Issue, WorkspaceId = WorkspaceId },
                new Class { Id = TaskClassId, Name = "Task", Kind = ObjectKind.Issue, WorkspaceId = WorkspaceId }
            );
            db.Fields.Add(new Field { Id = FieldId, Name = "Note", ClassId = TaskClassId, FieldType = FieldType.Text, State = FieldState.Active });
            db.SecurityLevels.Add(new SecurityLevel { Id = LevelId, Name = "Closed", ClassId = TaskClassId, State = SecurityLevelState.Active });

            ObjectEntity Make(Guid id, string key, string summary, Guid classId, string kind, Guid? parent = null, WorkspaceState state = WorkspaceState.Active) => new(id)
            {
                Key = key,
                Summary = summary,
                ClassId = classId,
                Kind = kind,
                WorkspaceId = WorkspaceId,
                ParentId = parent,
                State = state
            };

            db.Objects.AddRange
            (
                Make(RootId, "OPS3-1", "Root", EpicClassId, ObjectKind.Issue),
                Make(SubId, "OPS3-2", "Sub", EpicClassId, ObjectKind.Issue, RootId),
                Make(DeepId, "OPS3-3", "Deep", EpicClassId, ObjectKind.Issue, SubId),
                Make(TaskAId, "OPS3-4", "Task A", TaskClassId, ObjectKind.Issue, RootId),
                Make(TaskBId, "OPS3-5", "Task B", TaskClassId, ObjectKind.Issue, RootId, WorkspaceState.Archived),
                Make(TaskCId, "OPS3-6", "Task C", TaskClassId, ObjectKind.Issue, DeepId),
                Make(OtherId, "OPS3-7", "Other", EpicClassId, ObjectKind.Issue),
                Make(TwinId, "OPS3-8", "Sub", EpicClassId, ObjectKind.Issue)
            );
            db.Values.Add(new Value { Id = Guid.NewGuid(), ObjectId = TaskCId, FieldId = FieldId, Data = "keep me" });
            db.SaveChanges();
        }

        private static ObjectEntity Read(Guid id)
        {
            return ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.Id == id)).FirstOrDefault();
        }

        /// <summary>
        /// The rebuild writes a path per object from the parent relation and turns the index on;
        /// subtree, height and ancestors then come from it and agree with the walk.
        /// </summary>
        [Fact]
        public void PathIndex_RebuildAnswersLikeTheWalk()
        {
            Seed(nameof(PathIndex_RebuildAnswersLikeTheWalk));

            var walked = ObjectHierarchy.GetDescendantIds(RootId).ToHashSet();

            Assert.False(ObjectPathIndex.Ready);
            Assert.True(ObjectPathIndex.Rebuild() > 0);
            Assert.True(ObjectPathIndex.Ready);

            Assert.Equal($"/{RootId:N}/{SubId:N}/", ModelHub.GetObjectPath(DeepId)!.Path);
            Assert.Equal(2, ModelHub.GetObjectPath(DeepId)!.Depth);
            Assert.Equal(walked, ObjectHierarchy.GetDescendantIds(RootId).ToHashSet());
            Assert.Equal(5, ObjectPathIndex.Count(RootId));
            Assert.Equal(3, ObjectPathIndex.Height(RootId));
            Assert.Equal([RootId, SubId, DeepId], ObjectPathIndex.Ancestors(TaskCId)!);

            // a second rebuild finds nothing to write
            Assert.Equal(0, ObjectPathIndex.Rebuild());
        }

        /// <summary>
        /// After a random sequence of valid moves (and some refused ones) every stored path equals
        /// the path derived from the parent relation - the property the index stands on.
        /// </summary>
        [Fact]
        public void PathIndex_StaysConsistentUnderRandomMoves()
        {
            Seed(nameof(PathIndex_StaysConsistentUnderRandomMoves));
            ObjectPathIndex.Rebuild();

            var random = new Random(4711);
            var targets = new[] { RootId, SubId, DeepId, OtherId };
            var movable = new[] { SubId, DeepId, TaskAId, TaskCId, OtherId };
            var moved = 0;

            for (var i = 0; i < 120; i++)
            {
                var node = movable[random.Next(movable.Length)];
                Guid? target = random.Next(5) == 0 ? null : targets[random.Next(targets.Length)];

                try
                {
                    CoreHub.ObjectManager.SetParent(node, target);
                    moved++;
                }
                catch (InvalidOperationException)
                {
                    // a cycle, the same parent, a name taken: refused, nothing written
                }

                Assert.True(ObjectPathIndex.Ready);
            }

            Assert.True(moved > 20);

            var stored = ModelHub.GetAllObjectPaths().ToDictionary(x => x.ObjectId);
            var parents = ModelHub.GetObjectHierarchy().Where(x => x.WorkspaceId == WorkspaceId).ToDictionary(x => x.Id, x => x.ParentId);

            foreach (var id in parents.Keys)
            {
                var chain = new List<Guid>();

                for (var cursor = parents[id]; cursor is Guid p; cursor = parents[p])
                {
                    chain.Insert(0, p);
                }

                Assert.Equal("/" + string.Concat(chain.Select(x => x.ToString("N") + "/")), stored[id].Path);
                Assert.Equal(chain.Count, stored[id].Depth);
            }
        }

        /// <summary>
        /// Archive runs over the subtree children first and records what it archived; the restore
        /// brings back exactly those - task B, archived on its own before, stays archived.
        /// </summary>
        [Fact]
        public void ArchiveAndRestore_AreExact()
        {
            Seed(nameof(ArchiveAndRestore_AreExact));

            var archive = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Archive, RootId, null, "arc-1", Guid.Empty, _ => true);

            Assert.NotNull(archive.Operation);
            Assert.Equal(ObjectOperationState.Completed, archive.Operation.State);
            Assert.Equal(5, archive.Operation.Total);
            Assert.All(new[] { RootId, SubId, DeepId, TaskAId, TaskCId }, x => Assert.Equal(WorkspaceState.Archived, Read(x)!.State));

            // the same key answers the same run instead of starting another
            Assert.Equal(archive.Operation.Id, CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Archive, RootId, null, "arc-1", Guid.Empty, _ => true).Operation.Id);

            // a child cannot be restored on its own below the archived root
            Assert.True(CoreHub.ObjectOperationManager.Plan(ObjectOperationKinds.Restore, SubId, null, _ => true).Refused);

            var restore = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Restore, RootId, null, null, Guid.Empty, _ => true);

            Assert.Equal(ObjectOperationState.Completed, restore.Operation.State);
            Assert.Equal(archive.Operation.Id, restore.Operation.ReferenceId);
            Assert.All(new[] { RootId, SubId, DeepId, TaskAId, TaskCId }, x => Assert.Equal(WorkspaceState.Active, Read(x)!.State));
            Assert.Equal(WorkspaceState.Archived, Read(TaskBId)!.State);
        }

        /// <summary>
        /// A subtree holding an object the caller may not see, or may not change, is refused as a
        /// whole and nothing changes.
        /// </summary>
        [Fact]
        public void HiddenAndUnchangeableMembers_Block()
        {
            var database = nameof(HiddenAndUnchangeableMembers_Block);
            Seed(database);

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Objects.Single(x => x.Id == TaskCId).SecurityLevelId = LevelId;
                db.SaveChanges();
            }

            using (CoreHub.SessionManager.BeginIdentity(Guid.NewGuid()))
            {
                var refused = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Delete, RootId, null, null, Guid.Empty, _ => true);

                Assert.Null(refused.Operation);
                Assert.Equal("kleenestar.core:operation.refused.hidden", refused.Plan.RefusalKey);
                Assert.Equal(1, refused.Plan.Hidden);
            }

            Assert.NotNull(Read(TaskCId));

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Objects.Single(x => x.Id == TaskCId).SecurityLevelId = null;
                db.SaveChanges();
            }

            var unchangeable = CoreHub.ObjectOperationManager.Plan(ObjectOperationKinds.Archive, RootId, null, x => x.Id != TaskAId);

            Assert.Equal("kleenestar.core:operation.refused.permission", unchangeable.RefusalKey);
            Assert.Equal(WorkspaceState.Active, Read(TaskAId)!.State);
        }

        /// <summary>
        /// A recursive delete removes the subtree children first; the object beside it stays.
        /// </summary>
        [Fact]
        public void Delete_RemovesTheSubtree()
        {
            Seed(nameof(Delete_RemovesTheSubtree));

            var run = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Delete, SubId, null, null, Guid.Empty, _ => true);

            Assert.Equal(ObjectOperationState.Completed, run.Operation.State);
            Assert.Equal(3, run.Operation.Done);
            Assert.Null(Read(SubId));
            Assert.Null(Read(DeepId));
            Assert.Null(Read(TaskCId));
            Assert.NotNull(Read(RootId));
            Assert.NotNull(Read(TaskAId));
        }

        /// <summary>
        /// Dissolving moves the direct children one level up and deletes the object - of any kind.
        /// </summary>
        [Fact]
        public void Dissolve_MovesTheContentsUp()
        {
            Seed(nameof(Dissolve_MovesTheContentsUp));

            var run = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Dissolve, SubId, null, null, Guid.Empty, _ => true);

            Assert.Equal(ObjectOperationState.Completed, run.Operation.State);
            Assert.Null(Read(SubId));
            Assert.Equal(RootId, Read(DeepId)!.ParentId);
            Assert.Equal(DeepId, Read(TaskCId)!.ParentId);

            // a task with children dissolves like an epic: its children land at the root
            var root = CoreHub.ObjectManager.SetParent(TaskCId, TaskAId);
            Assert.NotNull(root);
            Assert.Equal(ObjectOperationState.Completed, CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Dissolve, TaskAId, null, null, Guid.Empty, _ => true).Operation.State);
            Assert.Equal(RootId, Read(TaskCId)!.ParentId);
        }

        /// <summary>
        /// A clone copies the object alone or with everything below it - beside the original, with
        /// new keys and the field values of what it copies.
        /// </summary>
        [Fact]
        public void Clone_CopiesByMode()
        {
            Seed(nameof(Clone_CopiesByMode));

            ObjectEntity Copy(ObjectOperation run) => Read(run.ReferenceId!.Value)!;
            List<ObjectEntity> Below(Guid id) => [.. ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.ParentId == id))];

            var alone = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Clone, SubId, "root", null, Guid.Empty, _ => true).Operation;
            Assert.Equal(1, alone.Done);
            Assert.Equal(RootId, Copy(alone).ParentId);
            Assert.Equal("Sub", Copy(alone).Summary);
            Assert.NotEqual("OPS3-2", Copy(alone).Key);
            Assert.Empty(Below(Copy(alone).Id));

            var contents = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Clone, SubId, "contents", null, Guid.Empty, _ => true).Operation;
            Assert.Equal(3, contents.Done);
            var taskCopy = Assert.Single(Below(Assert.Single(Below(Copy(contents).Id)).Id));
            Assert.NotEqual("OPS3-6", taskCopy.Key);
            Assert.Equal("keep me", CoreHub.ValueManager.GetValue(taskCopy.Id, FieldId)?.Data);
        }

        /// <summary>
        /// <c>within("KEY")</c> expands to the subtree before parsing and filters like any WQL;
        /// <c>not within</c> selects the rest, an unknown key nothing.
        /// </summary>
        [Fact]
        public void Within_ExpandsToTheSubtree()
        {
            Seed(nameof(Within_ExpandsToTheSubtree));

            Assert.Equal("Summary = \"x\"", WqlWithin.Expand("Summary = \"x\""));
            Assert.Contains("Id not in (", WqlWithin.Expand("not within('OPS3-2')", _ => [SubId]));

            var all = ModelHub.GetObjects(new Query<ObjectEntity>().Where(x => x.WorkspaceId == WorkspaceId)).ToList();

            var within = WqlFilter.Apply("within(\"OPS3-2\")", all).Select(x => x.Id).ToHashSet();
            Assert.Equal(new HashSet<Guid> { DeepId, TaskCId }, within);

            var tasks = WqlFilter.Apply("within(\"OPS3-1\") and Summary ~ \"task\"", all).Select(x => x.Id).ToHashSet();
            Assert.Equal(new HashSet<Guid> { TaskAId, TaskBId, TaskCId }, tasks);

            var outside = WqlFilter.Apply("not within(\"OPS3-1\")", all).Select(x => x.Id).ToHashSet();
            Assert.Equal(new HashSet<Guid> { RootId, OtherId, TwinId }, outside);

            Assert.Empty(WqlFilter.Apply("within(\"NOPE-1\")", all));
            Assert.True(WqlFilter.TryValidate<ObjectEntity>("within(\"OPS3-1\")", out _));
        }

        /// <summary>
        /// Every object change is announced to the other open pages, and a run announces once
        /// however many objects it touches.
        /// </summary>
        [Fact]
        public void ChangeNotices_FoldARunIntoOne()
        {
            Seed(nameof(ChangeNotices_FoldARunIntoOne));

            var sent = new List<Guid?>();
            var previous = ObjectChangeNotices.Transport;
            ObjectChangeNotices.Transport = sent.Add;

            try
            {
                CoreHub.ObjectManager.SetParent(TaskAId, OtherId);
                Assert.Contains((Guid?)TaskAId, sent);

                sent.Clear();
                var run = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Archive, RootId, null, null, Guid.Empty, _ => true);

                Assert.Equal(ObjectOperationState.Completed, run.Operation.State);
                Assert.True(run.Operation.Done >= 4);
                Assert.Single(sent);

                // nested batches still announce once, when the outermost closes
                sent.Clear();
                using (ObjectChangeNotices.Batch())
                {
                    using (ObjectChangeNotices.Batch())
                    {
                        ObjectChangeNotices.Notify(TaskAId);
                    }

                    ObjectChangeNotices.Notify(TaskBId);
                    Assert.Empty(sent);
                }

                Assert.Single(sent);
            }
            finally
            {
                ObjectChangeNotices.Transport = previous;
            }
        }

        /// <summary>
        /// A run too large for the request waits for the worker, which records one summary in the
        /// notification center of the identity that started it; an inline run records none.
        /// </summary>
        [Fact]
        public void BackgroundRun_RecordsOneSummary()
        {
            var database = nameof(BackgroundRun_RecordsOneSummary);
            Seed(database);

            var owner = Guid.NewGuid();

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Identities.Add(new Identity { Id = owner, Name = "Starter", UserName = "starter-" + owner.ToString("N")[..6], Email = owner.ToString("N") + "@example.com", PasswordHash = "x" });
                db.SaveChanges();
            }

            var property = typeof(CoreHub).GetProperty(nameof(CoreHub.HierarchySettings))!;
            var previous = CoreHub.HierarchySettings;

            property.SetValue(null, new HierarchySettings { InlineLimit = 1 });

            try
            {
                var run = CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Archive, RootId, null, null, owner, _ => true).Operation;

                Assert.Equal(ObjectOperationState.Pending, run.State);
                Assert.True(CoreHub.ObjectOperationManager.ProcessDue() >= 4);
                Assert.Equal(ObjectOperationState.Completed, CoreHub.ObjectOperationManager.GetOperation(run.Id.ToString())!.State);
            }
            finally
            {
                property.SetValue(null, previous);
            }

            using var check = CoreHubFixture.CreateDbContext(database);
            var summary = Assert.Single(check.UserNotifications.Where(x => x.OwnerId == owner));

            Assert.Equal("kleenestar.core:operation.notification.completed", summary.TitleKey);
            Assert.Equal("kleenestar.core:operation.notification.archive", summary.MessageKey);
            Assert.StartsWith("Root · ", summary.Subject);

            // an inline run (the restore of the same, within the default limit) records none
            CoreHub.ObjectOperationManager.Start(ObjectOperationKinds.Restore, RootId, null, null, owner, _ => true);

            using var again = CoreHubFixture.CreateDbContext(database);
            Assert.Single(again.UserNotifications.Where(x => x.OwnerId == owner));
        }
    }
}
