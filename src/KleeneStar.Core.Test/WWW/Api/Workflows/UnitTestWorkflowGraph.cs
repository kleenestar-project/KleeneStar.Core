using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.Test.WWW.Api.Workflows
{
    /// <summary>
    /// Provides unit tests for the graph projection of
    /// <see cref="KleeneStar.Core.WWW.Api._1_.Workflows.Graph"/>, which backs the read-only
    /// workflow view.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkflowGraph
    {
        private static readonly Guid WorkspaceId = Guid.Parse("1111AAAA-2222-3333-4444-555566667777");
        private static readonly Guid ClassId = Guid.Parse("2222AAAA-3333-4444-5555-666677778888");
        private static readonly Guid WorkflowId = Guid.Parse("3333AAAA-4444-5555-6666-777788889999");
        private static readonly Guid CategoryId = Guid.Parse("4444AAAA-5555-6666-7777-88889999AAAA");
        private static readonly Guid DraftId = Guid.Parse("5555AAAA-6666-7777-8888-9999AAAABBBB");
        private static readonly Guid ReviewId = Guid.Parse("6666AAAA-7777-8888-9999-AAAABBBBCCCC");
        private static readonly Guid DoneId = Guid.Parse("7777AAAA-8888-9999-AAAA-BBBBCCCCDDDD");

        /// <summary>
        /// Seeds a linear workflow draft → review → done whose states carry the entry and end marks
        /// on their participation in the workflow.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="laidOut">
        /// Whether the states carry a canvas position, which is what a workflow that has been
        /// through the designer looks like.
        /// </param>
        private static void Seed(string connectionString, bool laidOut = true)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-wg", Name = "main" });
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
                    new WorkflowStatus { StatusId = DraftId, X = laidOut ? 80 : 0, Y = laidOut ? 180 : 0, IsStart = true },
                    new WorkflowStatus { StatusId = ReviewId, X = laidOut ? 300 : 0, Y = laidOut ? 180 : 0 },
                    new WorkflowStatus { StatusId = DoneId, X = laidOut ? 520 : 0, Y = laidOut ? 180 : 0, IsEnd = true }
                ]
            });

            db.Transitions.AddRange(
                new Transition
                {
                    Name = "submit",
                    WorkflowId = WorkflowId,
                    SourceId = DraftId,
                    TargetId = ReviewId,
                    State = TransitionState.Active,
                    Color = "#112233",
                    DashArray = "4,4",
                    Waypoints = [new TransitionWaypoint { X = 190, Y = 120 }]
                },
                new Transition
                {
                    Name = "approve",
                    WorkflowId = WorkflowId,
                    SourceId = ReviewId,
                    TargetId = DoneId,
                    State = TransitionState.Active
                },
                new Transition
                {
                    Name = "retired",
                    WorkflowId = WorkflowId,
                    SourceId = DoneId,
                    TargetId = DraftId,
                    State = TransitionState.Archived
                });

            db.SaveChanges();
        }

        /// <summary>
        /// Invokes the protected node projection of the endpoint. The endpoint is sealed, so the
        /// projection is reached through its declaring type rather than a test subclass.
        /// </summary>
        /// <param name="request">The request addressing the workflow.</param>
        /// <returns>The projected nodes.</returns>
        private static IEnumerable<RestApiGraphNode> RetrieveNodes(IRequest request)
        {
            var endpoint = new KleeneStar.Core.WWW.Api._1_.Workflows.Graph();
            var method = typeof(KleeneStar.Core.WWW.Api._1_.Workflows.Graph)
                .GetMethod("RetrieveNodes", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return (IEnumerable<RestApiGraphNode>)method!.Invoke(endpoint, [request])!;
        }

        /// <summary>
        /// Invokes the protected edge projection of the endpoint.
        /// </summary>
        /// <param name="request">The request addressing the workflow.</param>
        /// <returns>The projected edges.</returns>
        private static IEnumerable<RestApiGraphEdge> RetrieveEdges(IRequest request)
        {
            var endpoint = new KleeneStar.Core.WWW.Api._1_.Workflows.Graph();
            var method = typeof(KleeneStar.Core.WWW.Api._1_.Workflows.Graph)
                .GetMethod("RetrieveEdges", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return (IEnumerable<RestApiGraphEdge>)method!.Invoke(endpoint, [request])!;
        }

        /// <summary>
        /// Creates a mock request that addresses a workflow through the id query parameter, which
        /// is how the viewer names the workflow it loads.
        /// </summary>
        /// <param name="workflowId">The addressed workflow, or null for a request without an id.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string workflowId)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/workflows/graph",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback,
                RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature
            {
                TraceIdentifier = nameof(UnitTestWorkflowGraph)
            });

            var context = new WebExpress.WebCore.WebMessage.HttpContext(features, null!);
            var request = context.Request;

            if (workflowId is not null)
            {
                request.AddParameter(new Parameter(ParameterId.Key, workflowId, ParameterScope.Parameter));
            }

            return request;
        }

        /// <summary>
        /// Verifies that the states of the workflow reach the viewer as its nodes, carrying the
        /// label, the category colour and the canvas position the designer stored.
        /// </summary>
        [Fact]
        public void RetrieveNodes_ProjectsTheStatesOfTheWorkflow()
        {
            Seed(nameof(RetrieveNodes_ProjectsTheStatesOfTheWorkflow));

            var nodes = RetrieveNodes(CreateRequest(WorkflowId.ToString())).ToList();

            Assert.Equal(3, nodes.Count);

            var review = nodes.Single(n => n.Label == "Review");

            Assert.Equal(ReviewId.ToString(), review.Id);
            Assert.Equal("#abcdef", review.BackgroundColor);
            Assert.Equal(300, review.X);
            Assert.Equal(180, review.Y);
        }

        /// <summary>
        /// Verifies that a picture-based status symbol travels in <c>Image</c> and not in
        /// <c>Icon</c>. The client renders the two through different SVG elements and treats
        /// <c>Icon</c> as a CSS class, so a URL placed there would render nothing.
        /// </summary>
        [Fact]
        public void RetrieveNodes_PutsTheStatusSymbolIntoImage()
        {
            Seed(nameof(RetrieveNodes_PutsTheStatusSymbolIntoImage));

            var draft = RetrieveNodes(CreateRequest(WorkflowId.ToString())).Single(n => n.Label == "Draft");

            Assert.Equal("/kleenestar/assets/icons/state-new.svg", draft.Image);
            Assert.Null(draft.Icon);
        }

        /// <summary>
        /// Verifies that the entry and the terminal state are drawn as round terminators while an
        /// intermediate state stays a rectangular step, because the viewer has no notion of an
        /// entry or an end and would otherwise render the state machine as an undirected mesh.
        /// </summary>
        [Fact]
        public void RetrieveNodes_MarksTheEntryAndTerminalStates()
        {
            Seed(nameof(RetrieveNodes_MarksTheEntryAndTerminalStates));

            var nodes = RetrieveNodes(CreateRequest(WorkflowId.ToString())).ToList();

            Assert.Equal("circle", nodes.Single(n => n.Label == "Draft").Shape);
            Assert.Equal("label-below", nodes.Single(n => n.Label == "Draft").Layout);
            Assert.Equal("circle", nodes.Single(n => n.Label == "Done").Shape);
            Assert.Null(nodes.Single(n => n.Label == "Review").Shape);
            Assert.Null(nodes.Single(n => n.Label == "Review").Layout);
        }

        /// <summary>
        /// Verifies that a workflow that has never been laid out delivers no positions at all.
        /// Every state sits at the origin then, and passing that on would stack the nodes on top of
        /// each other instead of handing the placement to the layout simulation.
        /// </summary>
        [Fact]
        public void RetrieveNodes_WithholdsThePositionsOfAWorkflowWithoutALayout()
        {
            Seed(nameof(RetrieveNodes_WithholdsThePositionsOfAWorkflowWithoutALayout), laidOut: false);

            var nodes = RetrieveNodes(CreateRequest(WorkflowId.ToString())).ToList();

            Assert.Equal(3, nodes.Count);
            Assert.All(nodes, n => Assert.Null(n.X));
            Assert.All(nodes, n => Assert.Null(n.Y));
        }

        /// <summary>
        /// Verifies that the active transitions reach the viewer as its edges, carrying their
        /// endpoints, stroke and routing, and that an archived transition is left out.
        /// </summary>
        [Fact]
        public void RetrieveEdges_ProjectsTheActiveTransitions()
        {
            Seed(nameof(RetrieveEdges_ProjectsTheActiveTransitions));

            var edges = RetrieveEdges(CreateRequest(WorkflowId.ToString())).ToList();

            Assert.Equal(2, edges.Count);
            Assert.DoesNotContain(edges, e => e.Label == "retired");

            var submit = edges.Single(e => e.Label == "submit");

            Assert.Equal(DraftId.ToString(), submit.From);
            Assert.Equal(ReviewId.ToString(), submit.To);
            Assert.Equal("#112233", submit.Color);
            Assert.Equal("4,4", submit.DashArray);
            Assert.Equal(190, submit.Waypoints.Single().X);
        }

        /// <summary>
        /// Verifies that a request naming no workflow yields an empty graph rather than the states
        /// of an arbitrary one, which is what the viewer renders as an empty canvas.
        /// </summary>
        [Fact]
        public void Retrieve_WithoutAWorkflowYieldsAnEmptyGraph()
        {
            Seed(nameof(Retrieve_WithoutAWorkflowYieldsAnEmptyGraph));

            Assert.Empty(RetrieveNodes(CreateRequest(null)));
            Assert.Empty(RetrieveEdges(CreateRequest(null)));
            Assert.Empty(RetrieveNodes(CreateRequest(Guid.NewGuid().ToString())));
        }
    }
}
