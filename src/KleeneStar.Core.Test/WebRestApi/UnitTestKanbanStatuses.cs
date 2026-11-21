using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using System.Runtime.CompilerServices;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using IssueKanban = KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.Kanban;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Provides unit tests for the workflow statuses of the object Kanban board: the catalog
    /// it offers, what a saved column stores, and the drop that moves a card through the
    /// workflow.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestKanbanStatuses
    {
        private static readonly Guid WorkspaceId = Guid.Parse("CB000000-0000-4000-8000-000000000001");
        private static readonly Guid IncidentId = Guid.Parse("CB000000-0000-4000-8000-000000000002");
        private static readonly Guid ProblemId = Guid.Parse("CB000000-0000-4000-8000-000000000003");
        private static readonly Guid NoteId = Guid.Parse("CB000000-0000-4000-8000-000000000004");
        private static readonly Guid ToDoId = Guid.Parse("CB000000-0000-4000-8000-000000000005");
        private static readonly Guid DoingId = Guid.Parse("CB000000-0000-4000-8000-000000000006");
        private static readonly Guid DoneCategoryId = Guid.Parse("CB000000-0000-4000-8000-000000000007");
        private static readonly Guid IncidentFieldId = Guid.Parse("CB000000-0000-4000-8000-000000000008");
        private static readonly Guid CardId = Guid.Parse("CB000000-0000-4000-8000-000000000009");

        /// <summary>
        /// Seeds two issue classes sharing the names <c>New</c> and <c>Done</c> but not their
        /// middle state (<c>In Progress</c> against <c>Analysis</c>), a third issue class
        /// without a workflow, and one incident in <c>New</c>.
        /// </summary>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-kb", Name = "board" });
            db.StatusCategories.AddRange
            (
                new StatusCategory { Id = ToDoId, Name = "ToDo", IsDefault = true },
                new StatusCategory { Id = DoingId, Name = "InProgress" },
                new StatusCategory { Id = DoneCategoryId, Name = "Done" }
            );

            AddClass(db, IncidentId, "Incident", "In Progress", IncidentFieldId);
            AddClass(db, ProblemId, "Problem", "Analysis", Guid.NewGuid());
            db.Classes.Add(new Class { Id = NoteId, Name = "Note", WorkspaceId = WorkspaceId, Kind = ObjectKind.Issue });

            db.Objects.Add(new Model.Entities.Object
            {
                Id = CardId,
                Key = "INC-1",
                Summary = "Printer on fire",
                WorkspaceId = WorkspaceId,
                ClassId = IncidentId,
                Kind = ObjectKind.Issue
            });
            db.Values.Add(new Value { Id = Guid.NewGuid(), ObjectId = CardId, FieldId = IncidentFieldId, Data = "New" });

            db.SaveChanges();
        }

        /// <summary>
        /// Adds an issue class with the lifecycle New -> middle -> Done.
        /// </summary>
        private static void AddClass(Model.KleeneStarDbContext db, Guid classId, string name, string middle, Guid fieldId)
        {
            var workflowId = Guid.NewGuid();
            var newId = Guid.NewGuid();
            var middleId = Guid.NewGuid();
            var doneId = Guid.NewGuid();

            db.Classes.Add(new Class { Id = classId, Name = name, WorkspaceId = WorkspaceId, Kind = ObjectKind.Issue });
            db.Statuses.AddRange
            (
                new Status { Id = newId, Name = "New", ClassId = classId, CategoryId = ToDoId, State = StatusState.Active },
                new Status { Id = middleId, Name = middle, ClassId = classId, CategoryId = DoingId, State = StatusState.Active },
                new Status { Id = doneId, Name = "Done", ClassId = classId, CategoryId = DoneCategoryId, State = StatusState.Active }
            );
            db.Workflows.Add(new Workflow
            {
                Id = workflowId,
                Name = name + " lifecycle",
                ClassId = classId,
                State = WorkflowState.Active,
                WorkflowStatuses =
                [
                    new WorkflowStatus { StatusId = newId, IsStart = true },
                    new WorkflowStatus { StatusId = middleId },
                    new WorkflowStatus { StatusId = doneId, IsEnd = true }
                ]
            });
            db.Transitions.AddRange
            (
                new Transition { Name = "Start", WorkflowId = workflowId, SourceId = newId, TargetId = middleId, State = TransitionState.Active },
                new Transition { Name = "Finish", WorkflowId = workflowId, SourceId = middleId, TargetId = doneId, State = TransitionState.Active }
            );
            db.Fields.Add(new Field
            {
                Id = fieldId,
                Name = "Status",
                ClassId = classId,
                FieldType = FieldType.Workflow,
                WorkflowId = workflowId,
                State = FieldState.Active
            });
        }

        /// <summary>
        /// Tests that the catalog merges statuses of the same name across classes and orders
        /// them by category.
        /// </summary>
        [Fact]
        public void CatalogMergesStatusesByNameInCategoryOrder()
        {
            Seed(nameof(CatalogMergesStatusesByNameInCategoryOrder));

            var catalog = KanbanStatusCatalog.Build(WorkspaceId, ObjectKind.Issue);

            Assert.False(catalog.IsEmpty);
            Assert.Equal(["new", "inprogress", "analysis", "done"], catalog.Statuses.Select(x => x.Id));
            Assert.Equal("In Progress", catalog.Statuses.Single(x => x.Id == "inprogress").Label);
            Assert.Equal(["inprogress", "analysis"], catalog.KeysOfCategory(DoingId));
            Assert.Empty(catalog.KeysOfCategory(null));
        }

        /// <summary>
        /// Tests that a class without a workflow contributes no statuses, and that a board whose
        /// classes have none offers an empty catalog.
        /// </summary>
        [Fact]
        public void ClassWithoutWorkflowOffersNothing()
        {
            Seed(nameof(ClassWithoutWorkflowOffersNothing));

            var catalog = KanbanStatusCatalog.Build(WorkspaceId, ObjectKind.Issue);

            Assert.Null(catalog.WorkflowOf(NoteId).Workflow);
            Assert.NotNull(catalog.WorkflowOf(IncidentId).Workflow);
            Assert.True(KanbanStatusCatalog.Build(WorkspaceId, ObjectKind.Asset).IsEmpty);
        }

        /// <summary>
        /// Tests that the stored form tells a column that follows its category from one that
        /// was explicitly emptied.
        /// </summary>
        [Fact]
        public void StoredStatusesDistinguishFollowingFromEmpty()
        {
            Assert.Null(KanbanStatusCatalog.Parse(null));
            Assert.Empty(KanbanStatusCatalog.Parse(""));
            Assert.Equal(["new", "done"], KanbanStatusCatalog.Parse("new, done,new"));
            Assert.Equal("new,done", KanbanStatusCatalog.Format(["new", "done", "new", " "]));
            Assert.Equal("", KanbanStatusCatalog.Format([]));
        }

        /// <summary>
        /// Tests what a saved column stores: nothing new while the board echoes what it showed,
        /// the list once it differs, nothing for a column the board just added.
        /// </summary>
        [Fact]
        public void SavedColumnStoresOnlyAChangedAssignment()
        {
            Seed(nameof(SavedColumnStoresOnlyAChangedAssignment));

            var catalog = KanbanStatusCatalog.Build(WorkspaceId, ObjectKind.Issue);
            var column = new KanbanBoardColumn { CategoryId = DoingId };
            var shown = new Dictionary<Guid, IReadOnlyList<string>> { [column.Id] = ["inprogress", "analysis"] };

            Assert.Null(Store(["analysis", "inprogress"], column, DoingId, shown, catalog));
            Assert.Equal("analysis", Store(["analysis"], column, DoingId, shown, catalog));
            Assert.Equal("", Store([], column, DoingId, shown, catalog));
            Assert.Null(Store(null, column, DoingId, shown, catalog));
            Assert.Null(Store([], null, DoingId, shown, catalog));
            Assert.Equal("done", Store(["done", "unknown"], column, DoingId, shown, catalog));
        }

        /// <summary>
        /// Tests that a drop into a status the workflow reaches moves the card there.
        /// </summary>
        [Fact]
        public void DropExecutesTheTransition()
        {
            Seed(nameof(DropExecutesTheTransition));

            Move("inprogress");

            Assert.Equal("inprogress", CurrentKey());
        }

        /// <summary>
        /// Tests that a drop into a status no transition reaches is refused and leaves the card
        /// where it was.
        /// </summary>
        [Fact]
        public void DropIntoAnUnreachableStatusIsRefused()
        {
            Seed(nameof(DropIntoAnUnreachableStatusIsRefused));

            var error = Assert.Throws<TargetInvocationException>(() => Move("done"));

            // the board shows a refusal's message where the card snaps back
            var refusal = Assert.IsType<RestApiRefusal>(error.InnerException);
            Assert.Contains("object.property.workflow.transition.notallowed", refusal.Message);
            Assert.Equal("new", CurrentKey());
        }

        /// <summary>
        /// Tests that a drop naming the card's own status - a reorder within the column - does
        /// not touch the workflow.
        /// </summary>
        [Fact]
        public void ReorderKeepsTheStatus()
        {
            Seed(nameof(ReorderKeepsTheStatus));

            Move("new");

            Assert.Equal("new", CurrentKey());
        }

        /// <summary>
        /// Reads the status key the card is in.
        /// </summary>
        private static string CurrentKey()
        {
            var catalog = KanbanStatusCatalog.Build(WorkspaceId, ObjectKind.Issue);
            var (field, workflow) = catalog.WorkflowOf(IncidentId);
            var data = CoreHub.ValueManager.GetValue(CardId, field.Id)?.Data;

            return KanbanStatusCatalog.Key(CoreHub.WorkflowManager.ResolveStatus(workflow, data));
        }

        /// <summary>
        /// Drops the card into a status through the issue board's endpoint.
        /// </summary>
        private static void Move(string statusKey)
        {
            // the constructor resolves routes through a sitemap the fixture does not wire; the
            // drop needs none of it
            var endpoint = (IssueKanban)RuntimeHelpers.GetUninitializedObject(typeof(IssueKanban));
            var method = typeof(RestApiObjectKindKanban).GetMethod("MoveCard", BindingFlags.NonPublic | BindingFlags.Instance);
            var move = new RestApiKanbanMove { CardId = CardId.ToString(), ColumnId = DoingId.ToString(), StatusId = statusKey };

            method.Invoke(endpoint, [move, CreateRequest()]);
        }

        /// <summary>
        /// Invokes the private rule deciding what a saved column stores.
        /// </summary>
        private static string Store(IEnumerable<string> submitted, KanbanBoardColumn existing, Guid? categoryId, IReadOnlyDictionary<Guid, IReadOnlyList<string>> shown, KanbanStatusCatalog catalog)
        {
            var method = typeof(RestApiObjectKindKanban).GetMethod("ResolveStoredStatuses", BindingFlags.NonPublic | BindingFlags.Static);

            return (string)method.Invoke(null, [submitted, existing, categoryId, shown, catalog]);
        }

        /// <summary>
        /// Builds an anonymous board request.
        /// </summary>
        private static IRequest CreateRequest()
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "PUT",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/objects/ws-kb/kanban",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Accept-Language"] = "en" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback,
                RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature
            {
                TraceIdentifier = nameof(UnitTestKanbanStatuses)
            });

            return new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;
        }
    }
}
