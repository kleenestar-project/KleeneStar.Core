using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.WorkflowManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkflowManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("EE445566-7788-99AA-BBCC-DD00112233EE");
        private static readonly Guid ClassId = Guid.Parse("FF556677-8899-AABB-CCDD-EE11223344FF");

        /// <summary>
        /// Seeds the in-memory database with the workspace and class that workflows attach to.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-wf", Name = "main" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the workflow and that <c>GetWorkflow</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetWorkflow_RoundTrip()
        {
            Seed(nameof(Add_Then_GetWorkflow_RoundTrip));

            var workflow = Sample("Default");
            CoreHub.WorkflowManager.Add(workflow);

            var loaded = CoreHub.WorkflowManager.GetWorkflow(workflow.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Default", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>GetWorkflows(ClassIdParameter)</c> returns only workflows
        /// attached to the supplied class.
        /// </summary>
        [Fact]
        public void GetWorkflows_ByClassId_ReturnsWorkflowsForClass()
        {
            Seed(nameof(GetWorkflows_ByClassId_ReturnsWorkflowsForClass));

            CoreHub.WorkflowManager.Add(Sample("Alpha"));
            CoreHub.WorkflowManager.Add(Sample("Beta"));

            var result = CoreHub.WorkflowManager.GetWorkflows(new ClassIdParameter(ClassId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, w => w.Name == "Alpha");
            Assert.Contains(result, w => w.Name == "Beta");
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var workflow = Sample("Initial");
            CoreHub.WorkflowManager.Add(workflow);

            workflow.Name = "Renamed";
            CoreHub.WorkflowManager.Update(workflow);

            var loaded = CoreHub.WorkflowManager.GetWorkflow(workflow.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the workflow and raises the
        /// <see cref="KleeneStar.Core.WebManager.IWorkflowManager.WorkflowRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var workflow = Sample("DeleteMe");
            CoreHub.WorkflowManager.Add(workflow);

            Workflow raised = null;
            CoreHub.WorkflowManager.WorkflowRemoved += (_, w) => raised = w;

            CoreHub.WorkflowManager.Remove(workflow.Id);

            Assert.Null(CoreHub.WorkflowManager.GetWorkflow(workflow.Id));
            Assert.NotNull(raised);
            Assert.Equal(workflow.Id, raised.Id);
        }

        /// <summary>
        /// Creates a sample <see cref="Workflow"/> attached to the seeded class.
        /// </summary>
        /// <param name="name">The workflow name.</param>
        /// <returns>The sample workflow.</returns>
        private static Workflow Sample(string name) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClassId = ClassId,
            State = WorkflowState.Active
        };
    }
}
