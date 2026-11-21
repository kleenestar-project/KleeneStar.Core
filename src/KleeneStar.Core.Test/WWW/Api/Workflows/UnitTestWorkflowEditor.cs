using KleeneStar.Core.Test;
using KleeneStar.Model.Entities;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.Test.WWW.Api.Workflows
{
    /// <summary>
    /// Provides unit tests for the state projection of
    /// <see cref="KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkflowEditor
    {
        private static readonly Guid WorkspaceId = Guid.Parse("11112222-3333-4444-5555-666677778888");
        private static readonly Guid ClassId = Guid.Parse("22223333-4444-5555-6666-777788889999");
        private static readonly Guid WorkflowId = Guid.Parse("33334444-5555-6666-7777-88889999AAAA");
        private static readonly Guid CategoryId = Guid.Parse("44445555-6666-7777-8888-9999AAAABBBB");
        private static readonly Guid DraftId = Guid.Parse("55556666-7777-8888-9999-AAAABBBBCCCC");
        private static readonly Guid ReviewId = Guid.Parse("66667777-8888-9999-AAAA-BBBBCCCCDDDD");
        private static readonly Guid DoneId = Guid.Parse("77778888-9999-AAAA-BBBB-CCCCDDDDEEEE");

        /// <summary>
        /// Seeds a linear workflow draft → review → done whose states carry a canvas position and
        /// the entry and end marks on their participation in the workflow.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-we", Name = "main" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.StatusCategories.Add(new StatusCategory { Id = CategoryId, Name = "Open", Color = "#abcdef", IsDefault = true });

            db.Statuses.AddRange(
                new Status
                {
                    Id = DraftId,
                    Name = "Draft",
                    ClassId = ClassId,
                    CategoryId = CategoryId,
                    State = StatusState.Active,
                    Icon = ImageIcon.FromString("/kleenestar/assets/icons/state-new.svg")
                },
                new Status
                {
                    Id = ReviewId,
                    Name = "Review",
                    ClassId = ClassId,
                    CategoryId = CategoryId,
                    State = StatusState.Active
                },
                new Status
                {
                    Id = DoneId,
                    Name = "Done",
                    ClassId = ClassId,
                    CategoryId = CategoryId,
                    State = StatusState.Active
                });

            db.Workflows.Add(new Model.Entities.Workflow
            {
                Id = WorkflowId,
                Name = "Approval",
                ClassId = ClassId,
                State = WorkflowState.Active,
                WorkflowStatuses =
                [
                    new WorkflowStatus { StatusId = DraftId, X = 80, Y = 180, IsStart = true },
                    new WorkflowStatus { StatusId = ReviewId, X = 300, Y = 180 },
                    new WorkflowStatus { StatusId = DoneId, X = 520, Y = 180, IsEnd = true }
                ]
            });

            db.Transitions.AddRange(
                new Transition
                {
                    Name = "submit",
                    WorkflowId = WorkflowId,
                    SourceId = DraftId,
                    TargetId = ReviewId,
                    State = TransitionState.Active
                },
                new Transition
                {
                    Name = "approve",
                    WorkflowId = WorkflowId,
                    SourceId = ReviewId,
                    TargetId = DoneId,
                    State = TransitionState.Active
                });

            db.SaveChanges();
        }

        /// <summary>
        /// Invokes the protected state projection of the endpoint. The endpoint is sealed, so the
        /// projection is reached through its declaring type rather than a test subclass.
        /// </summary>
        /// <param name="context">The query context the projection reads from.</param>
        /// <returns>The projected states.</returns>
        private static IEnumerable<RestApiWorkflowState> RetrieveStates(IQueryContext context)
        {
            var endpoint = new KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor();
            var method = typeof(KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor)
                .GetMethod("RetrieveStates", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return (IEnumerable<RestApiWorkflowState>)method
                .Invoke(endpoint, [WorkflowId.ToString(), context, null]);
        }

        /// <summary>
        /// Invokes the protected update of the endpoint with the given definition.
        /// </summary>
        /// <param name="context">The query context the update writes to.</param>
        /// <param name="workflow">The definition to persist.</param>
        private static void Update(IQueryContext context, RestApiWorkflowResult workflow)
        {
            var endpoint = new KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor();
            var method = typeof(KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method.Invoke(endpoint, [WorkflowId.ToString(), workflow, context, null]);
        }

        /// <summary>
        /// Builds a definition that carries the current states and no transitions, so a caller can
        /// drop or add a state without the transition reconciliation getting in the way.
        /// </summary>
        /// <param name="states">The states the definition should carry.</param>
        /// <returns>The definition.</returns>
        private static RestApiWorkflowResult Definition(params RestApiWorkflowState[] states)
        {
            return new RestApiWorkflowResult()
            {
                Id = WorkflowId.ToString(),
                States = states,
                Transitions = []
            };
        }

        /// <summary>
        /// Verifies that the canvas position stored on the pairing reaches the editor. Without it
        /// the designer re-runs its physics layout on every load and the graph is arranged
        /// differently each time.
        /// </summary>
        [Fact]
        public void RetrieveStates_ProjectsTheStoredCanvasPosition()
        {
            Seed(nameof(RetrieveStates_ProjectsTheStoredCanvasPosition));

            using var db = CoreHubFixture.CreateDbContext(nameof(RetrieveStates_ProjectsTheStoredCanvasPosition));
            var states = RetrieveStates(db).ToList();

            Assert.Equal(3, states.Count);

            var draft = states.Single(s => s.Label == "Draft");
            var review = states.Single(s => s.Label == "Review");

            Assert.Equal(80, draft.X);
            Assert.Equal(180, draft.Y);
            Assert.Equal(300, review.X);
        }

        /// <summary>
        /// Verifies that the entry and end marks are read from the pairing rather than guessed.
        /// The editor computes reachability from them; without them its preflight reports
        /// "no entry state" on every load.
        /// </summary>
        [Fact]
        public void RetrieveStates_ProjectsTheEntryAndEndMarks()
        {
            Seed(nameof(RetrieveStates_ProjectsTheEntryAndEndMarks));

            using var db = CoreHubFixture.CreateDbContext(nameof(RetrieveStates_ProjectsTheEntryAndEndMarks));
            var states = RetrieveStates(db).ToList();

            var draft = states.Single(s => s.Label == "Draft");
            var review = states.Single(s => s.Label == "Review");
            var done = states.Single(s => s.Label == "Done");

            Assert.True(draft.IsStart);
            Assert.False(draft.IsEnd);

            Assert.False(review.IsStart);
            Assert.False(review.IsEnd);

            Assert.False(done.IsStart);
            Assert.True(done.IsEnd);
        }

        /// <summary>
        /// Verifies that a picture-based status symbol travels in <c>Image</c> and not in
        /// <c>Icon</c>. The client renders the two through different SVG elements and treats
        /// <c>Icon</c> as a CSS class, so a URL placed there would render nothing.
        /// </summary>
        [Fact]
        public void RetrieveStates_PutsTheStatusSymbolIntoImage()
        {
            Seed(nameof(RetrieveStates_PutsTheStatusSymbolIntoImage));

            using var db = CoreHubFixture.CreateDbContext(nameof(RetrieveStates_PutsTheStatusSymbolIntoImage));
            var draft = RetrieveStates(db).Single(s => s.Label == "Draft");

            Assert.Equal("/kleenestar/assets/icons/state-new.svg", draft.Image);
            Assert.Null(draft.Icon);
        }

        /// <summary>
        /// Verifies that the category colour reaches the canvas and that a state without a
        /// symbol simply carries none.
        /// </summary>
        [Fact]
        public void RetrieveStates_ProjectsTheCategoryColour()
        {
            Seed(nameof(RetrieveStates_ProjectsTheCategoryColour));

            using var db = CoreHubFixture.CreateDbContext(nameof(RetrieveStates_ProjectsTheCategoryColour));
            var review = RetrieveStates(db).Single(s => s.Label == "Review");

            Assert.Equal("#abcdef", review.BackgroundColor);
            Assert.Null(review.Image);
        }

        /// <summary>
        /// Verifies that a state the editor created is persisted as a status of the workflow's
        /// class, placed in the category nominated as the default. Before this the creation
        /// action on the canvas was silently discarded on the next load.
        /// </summary>
        [Fact]
        public void Update_CreatesAPostedStateInTheDefaultCategory()
        {
            var name = nameof(Update_CreatesAPostedStateInTheDefaultCategory);
            Seed(name);

            using (var db = CoreHubFixture.CreateDbContext(name))
            {
                var existing = RetrieveStates(db)
                    .Select(s => new RestApiWorkflowState { Id = s.Id, Label = s.Label, X = s.X, Y = s.Y, IsStart = s.IsStart, IsEnd = s.IsEnd })
                    .ToList();

                existing.Add(new RestApiWorkflowState { Id = "n_1737", Label = "Rejected", X = 300, Y = 400 });

                Update(db, Definition([.. existing]));
            }

            using var verify = CoreHubFixture.CreateDbContext(name);
            var states = RetrieveStates(verify).ToList();
            var rejected = states.SingleOrDefault(s => s.Label == "Rejected");

            Assert.NotNull(rejected);
            Assert.Equal(300, rejected.X);
            Assert.Equal(400, rejected.Y);
            Assert.Equal("#abcdef", rejected.BackgroundColor);
            Assert.True(Guid.TryParse(rejected.Id, out _));
        }

        /// <summary>
        /// Verifies that a state the editor dropped stops taking part in the workflow while the
        /// status itself survives, because it is defined per class and may be referenced
        /// elsewhere.
        /// </summary>
        [Fact]
        public void Update_DropsAnOmittedStateButKeepsTheStatus()
        {
            var name = nameof(Update_DropsAnOmittedStateButKeepsTheStatus);
            Seed(name);

            using (var db = CoreHubFixture.CreateDbContext(name))
            {
                var kept = RetrieveStates(db)
                    .Where(s => s.Label != "Review")
                    .Select(s => new RestApiWorkflowState { Id = s.Id, Label = s.Label, X = s.X, Y = s.Y, IsStart = s.IsStart, IsEnd = s.IsEnd })
                    .ToArray();

                Update(db, Definition(kept));
            }

            using var verify = CoreHubFixture.CreateDbContext(name);

            Assert.DoesNotContain(RetrieveStates(verify), s => s.Label == "Review");
            Assert.Contains(verify.Statuses, s => s.Name == "Review");
        }

        /// <summary>
        /// Verifies that a state objects currently sit in keeps its participation even when the
        /// editor drops it, because removing it would leave those objects pointing at a state the
        /// workflow no longer knows.
        /// </summary>
        [Fact]
        public void Update_KeepsADroppedStateThatObjectsStillOccupy()
        {
            var name = nameof(Update_KeepsADroppedStateThatObjectsStillOccupy);
            Seed(name);

            var fieldId = Guid.NewGuid();

            using (var arrange = CoreHubFixture.CreateDbContext(name))
            {
                arrange.Fields.Add(new Field
                {
                    Id = fieldId,
                    Name = "Status",
                    ClassId = ClassId,
                    FieldType = FieldType.Workflow,
                    WorkflowId = WorkflowId
                });

                arrange.Values.Add(new Value
                {
                    Id = Guid.NewGuid(),
                    ObjectId = Guid.NewGuid(),
                    FieldId = fieldId,
                    Data = "review"
                });

                arrange.SaveChanges();
            }

            using (var db = CoreHubFixture.CreateDbContext(name))
            {
                var kept = RetrieveStates(db)
                    .Where(s => s.Label != "Review")
                    .Select(s => new RestApiWorkflowState { Id = s.Id, Label = s.Label, X = s.X, Y = s.Y })
                    .ToArray();

                Update(db, Definition(kept));
            }

            using var verify = CoreHubFixture.CreateDbContext(name);

            Assert.Contains(RetrieveStates(verify), s => s.Label == "Review");
        }
    }
}
