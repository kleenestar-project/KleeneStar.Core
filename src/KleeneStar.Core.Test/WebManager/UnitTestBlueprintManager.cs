using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests the blueprints: a class following a blueprint receives its statuses, priorities,
    /// workflows and fields as managed copies, adopts what it already had, retires what the
    /// blueprint dropped, and is kept in step when the blueprint changes.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestBlueprintManager
    {
        private static readonly Guid CentralWorkspaceId = Guid.Parse("B1E0A000-0000-4000-8000-000000000001");
        private static readonly Guid DeskWorkspaceId = Guid.Parse("B1E0A000-0000-4000-8000-000000000002");
        private static readonly Guid BlueprintId = Guid.Parse("B1E0A000-0000-4000-8000-000000000003");
        private static readonly Guid FollowerId = Guid.Parse("B1E0A000-0000-4000-8000-000000000004");
        private static readonly Guid OpenId = Guid.Parse("B1E0A000-0000-4000-8000-000000000005");
        private static readonly Guid DoneId = Guid.Parse("B1E0A000-0000-4000-8000-000000000006");
        private static readonly Guid WorkflowId = Guid.Parse("B1E0A000-0000-4000-8000-000000000007");
        private static readonly Guid TodoCategoryId = Guid.Parse("B1E0A000-0000-4000-8000-000000000008");
        private static readonly Guid DoneCategoryId = Guid.Parse("B1E0A000-0000-4000-8000-000000000009");
        private static readonly Guid ImpactFieldId = Guid.Parse("B1E0A000-0000-4000-8000-00000000000A");
        private static readonly Guid StatusFieldId = Guid.Parse("B1E0A000-0000-4000-8000-00000000000B");
        private static readonly Guid LocalImpactFieldId = Guid.Parse("B1E0A000-0000-4000-8000-00000000000C");

        /// <summary>
        /// Seeds a blueprint class with two statuses, a workflow between them, a priority and two
        /// fields, and a follower in another workspace that already has an "Impact" field.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == CentralWorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = CentralWorkspaceId, Key = "BP", Name = "Blueprints" });
            db.Workspaces.Add(new Workspace { Id = DeskWorkspaceId, Key = "DESK", Name = "Desk" });
            db.Classes.Add(new Class { Id = BlueprintId, Name = "Incident", WorkspaceId = CentralWorkspaceId, IsBlueprint = true });
            db.Classes.Add(new Class { Id = FollowerId, Name = "Incident", WorkspaceId = DeskWorkspaceId });

            db.StatusCategories.Add(new StatusCategory { Id = TodoCategoryId, Name = "ToDo" });
            db.StatusCategories.Add(new StatusCategory { Id = DoneCategoryId, Name = "Done" });
            db.Statuses.Add(new Status { Id = OpenId, Name = "Open", ClassId = BlueprintId, CategoryId = TodoCategoryId, State = StatusState.Active });
            db.Statuses.Add(new Status { Id = DoneId, Name = "Done", ClassId = BlueprintId, CategoryId = DoneCategoryId, State = StatusState.Active });

            db.Workflows.Add(new Workflow
            {
                Id = WorkflowId,
                Name = "Lifecycle",
                ClassId = BlueprintId,
                State = WorkflowState.Active,
                WorkflowStatuses =
                [
                    new WorkflowStatus { WorkflowId = WorkflowId, StatusId = OpenId, X = 10, Y = 20, IsStart = true },
                    new WorkflowStatus { WorkflowId = WorkflowId, StatusId = DoneId, X = 200, Y = 20, IsEnd = true }
                ],
                Transitions =
                [
                    new Transition { Id = Guid.NewGuid(), Name = "Resolve", WorkflowId = WorkflowId, SourceId = OpenId, TargetId = DoneId, State = TransitionState.Active, GuardExpression = "assigned" }
                ]
            });

            db.Priorities.Add(new Priority { Id = Guid.NewGuid(), Name = "High", ClassId = BlueprintId, Order = 1, State = PriorityState.Active });

            db.Fields.Add(new Field { Id = StatusFieldId, Name = "Status", ClassId = BlueprintId, FieldType = FieldType.Workflow, WorkflowId = WorkflowId, State = FieldState.Active });
            db.Fields.Add(new Field { Id = ImpactFieldId, Name = "Impact", ClassId = BlueprintId, FieldType = FieldType.Text, Required = true, State = FieldState.Active });
            db.Fields.Add(new Field { Id = LocalImpactFieldId, Name = "impact", ClassId = FollowerId, FieldType = FieldType.Text, State = FieldState.Active });

            db.SaveChanges();
        }

        /// <summary>
        /// Binds the follower to the blueprint.
        /// </summary>
        private static void Bind()
        {
            var follower = CoreHub.ClassManager.GetClass(FollowerId);
            follower.BlueprintId = BlueprintId;
            CoreHub.ClassManager.Update(follower);
        }

        /// <summary>
        /// A follower receives the blueprint's structure as managed copies, its workflow with the
        /// graph translated onto its own statuses and the workflow field pointing at its own
        /// workflow; a field it already had under the same name is adopted, not doubled.
        /// </summary>
        [Fact]
        public void Synchronize_CopiesTheStructure()
        {
            Seed(nameof(Synchronize_CopiesTheStructure));
            Bind();

            CoreHub.BlueprintManager.Synchronize(FollowerId);

            var statuses = CoreHub.StatusManager.GetStatuses(new ClassIdParameter(FollowerId)).ToList();
            Assert.Equal(2, statuses.Count);
            Assert.All(statuses, x => Assert.NotNull(x.BlueprintSourceId));

            var priority = Assert.Single(CoreHub.PriorityManager.GetPriorities(new ClassIdParameter(FollowerId)));
            Assert.Equal("High", priority.Name);

            var workflow = Assert.Single(CoreHub.WorkflowManager.GetWorkflows(new ClassIdParameter(FollowerId)));
            Assert.Equal(WorkflowId, workflow.BlueprintSourceId);

            var structure = CoreHub.WorkflowManager.GetWorkflowWithStructure(workflow.Id);
            Assert.Equal(2, structure.WorkflowStatuses.Count);
            Assert.All(structure.WorkflowStatuses, x => Assert.Contains(statuses, s => s.Id == x.StatusId));

            var transition = Assert.Single(structure.Transitions);
            Assert.Equal("Resolve", transition.Name);
            Assert.Equal("assigned", transition.GuardExpression);
            Assert.Equal(statuses.Single(x => x.Name == "Open").Id, transition.SourceId);

            var fields = CoreHub.FieldManager.GetFields(new ClassIdParameter(FollowerId)).ToList();
            Assert.Equal(2, fields.Count);

            var impact = Assert.Single(fields, x => x.FieldType == FieldType.Text);
            Assert.Equal(LocalImpactFieldId, impact.Id);
            Assert.Equal(ImpactFieldId, impact.BlueprintSourceId);
            Assert.True(impact.Required);

            Assert.Equal(workflow.Id, fields.Single(x => x.FieldType == FieldType.Workflow).WorkflowId);

            // a second run changes nothing
            var again = CoreHub.BlueprintManager.Synchronize(FollowerId);
            Assert.Equal(0, again.Added);
            Assert.Equal(0, again.Retired);
            Assert.Equal(0, again.Updated);
        }

        /// <summary>
        /// A change of the blueprint reaches the follower without anybody synchronizing by hand,
        /// and an element the blueprint drops is retired in the follower rather than deleted.
        /// </summary>
        [Fact]
        public void BlueprintChanges_ReachTheFollower()
        {
            Seed(nameof(BlueprintChanges_ReachTheFollower));
            CoreHub.BlueprintManager.Connect();
            Bind();

            var field = CoreHub.FieldManager.GetField(ImpactFieldId);
            field.Description = "How many users are affected.";
            CoreHub.FieldManager.Update(field);

            var copy = CoreHub.FieldManager.GetFields(new ClassIdParameter(FollowerId)).Single(x => x.BlueprintSourceId == ImpactFieldId);
            Assert.Equal("How many users are affected.", copy.Description);

            CoreHub.FieldManager.Remove(ImpactFieldId);

            Assert.True(CoreHub.FieldManager.GetField(copy.Id).Deprecated);
        }

        /// <summary>
        /// Setting a workspace up from a blueprint workspace creates a follower of every blueprint
        /// class there, and adopts a class of the same name instead of doubling it.
        /// </summary>
        [Fact]
        public void ApplyWorkspace_CreatesOrAdoptsFollowers()
        {
            var database = nameof(ApplyWorkspace_CreatesOrAdoptsFollowers);
            Seed(database);

            var created = CoreHub.BlueprintManager.ApplyWorkspace(CentralWorkspaceId, DeskWorkspaceId);

            Assert.Empty(created);
            Assert.Equal(BlueprintId, CoreHub.ClassManager.GetClass(FollowerId).BlueprintId);

            var fresh = Guid.NewGuid();

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Workspaces.Add(new Workspace { Id = fresh, Key = "NEW", Name = "New" });
                db.SaveChanges();
            }

            var follower = Assert.Single(CoreHub.BlueprintManager.ApplyWorkspace(CentralWorkspaceId, fresh));
            Assert.Equal("Incident", follower.Name);
            Assert.Equal(2, CoreHub.FieldManager.GetFields(new ClassIdParameter(follower.Id)).Count());
        }
    }
}
