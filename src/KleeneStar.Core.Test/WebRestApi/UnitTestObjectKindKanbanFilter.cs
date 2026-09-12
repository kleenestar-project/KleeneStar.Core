using KleeneStar.Core.Test;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests the board filter of the object Kanban
    /// (<see cref="KleeneStar.Core.WebRestApi.RestApiObjectKindKanban"/>, through the issue
    /// endpoint): the stored WQL expression narrows the cards, the lanes and the "Other"
    /// catch-all alike, a filter on the request previews over the stored one, and a filter
    /// that does not compile is refused at the moment it is written.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectKindKanbanFilter
    {
        private static readonly Guid WorkspaceId = Guid.Parse("CB000000-0000-0000-0000-000000000001");
        private static readonly Guid BugClassId = Guid.Parse("CB000000-0000-0000-0000-000000000002");
        private static readonly Guid TaskClassId = Guid.Parse("CB000000-0000-0000-0000-000000000003");
        private static readonly Guid CategoryId = Guid.Parse("CB000000-0000-0000-0000-000000000004");
        private static readonly Guid MailBugId = Guid.Parse("CB000000-0000-0000-0000-000000000005");
        private static readonly Guid PrinterBugId = Guid.Parse("CB000000-0000-0000-0000-000000000006");
        private static readonly Guid MailTaskId = Guid.Parse("CB000000-0000-0000-0000-000000000007");
        private static readonly Guid ArchivedMailId = Guid.Parse("CB000000-0000-0000-0000-000000000008");

        private const string WorkspaceKey = "KB";

        /// <summary>
        /// Seeds a workspace with two issue classes and three active issues - two about mail,
        /// one about a printer - plus an archived one about mail that no board shows.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = WorkspaceKey, Name = "Kanban", State = WorkspaceState.Active });
            db.Classes.AddRange
            (
                new Class { Id = BugClassId, Name = "Bug", WorkspaceId = WorkspaceId, State = ClassState.Active, Kind = ObjectKind.Issue },
                new Class { Id = TaskClassId, Name = "Task", WorkspaceId = WorkspaceId, State = ClassState.Active, Kind = ObjectKind.Issue }
            );
            db.StatusCategories.Add(new StatusCategory { Id = CategoryId, Name = "ToDo", Color = "#abcdef", IsDefault = true });

            db.Objects.AddRange
            (
                Issue(MailBugId, "KB-1", "Outlook not receiving mail", BugClassId),
                Issue(PrinterBugId, "KB-2", "Printer prints stripes", BugClassId),
                Issue(MailTaskId, "KB-3", "Migrate mail archive", TaskClassId),
                Issue(ArchivedMailId, "KB-4", "Old mail relay", BugClassId, WorkspaceState.Archived)
            );

            db.SaveChanges();
        }

        /// <summary>
        /// Builds an issue of the workspace.
        /// </summary>
        private static ObjectEntity Issue(Guid id, string key, string summary, Guid classId, WorkspaceState state = WorkspaceState.Active)
        {
            return new ObjectEntity(id)
            {
                Key = key,
                Summary = summary,
                WorkspaceId = WorkspaceId,
                ClassId = classId,
                Kind = ObjectKind.Issue,
                State = state,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Builds a request addressing the workspace's board, optionally carrying a filter of
        /// its own the way the settings dialog previews one.
        /// </summary>
        /// <param name="wql">The filter on the request, or <see langword="null"/>.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string wql = null)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/objects/" + WorkspaceKey + "/kanban",
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
                TraceIdentifier = nameof(UnitTestObjectKindKanbanFilter)
            });

            var context = new WebExpress.WebCore.WebMessage.HttpContext(features, null!);
            var request = context.Request;

            request.AddParameter(new Parameter(WorkspaceKeyParameter.Key, WorkspaceKey, ParameterScope.Url));

            if (wql is not null)
            {
                request.AddParameter(new Parameter("wql", wql, ParameterScope.Parameter));
            }

            return request;
        }

        /// <summary>
        /// Invokes a protected member of the sealed issue Kanban endpoint, unwrapping the
        /// reflection envelope so a case can assert on what the endpoint throws.
        /// </summary>
        private static T Invoke<T>(string name, params object[] args)
        {
            var endpoint = new KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.Kanban();
            var method = typeof(KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.Kanban)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

            Assert.NotNull(method);

            try
            {
                return (T)method.Invoke(endpoint, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Reads the cards of the board as the endpoint answers them.
        /// </summary>
        private static List<string> Cards(string connectionString, string wql = null)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            return [.. Invoke<IEnumerable<RestApiKanbanCard>>("RetrieveCards", new Query<ObjectEntity>(), db, CreateRequest(wql))
                .Select(x => x.Id)
                .OrderBy(x => x)];
        }

        /// <summary>
        /// Reads the swimlanes of the board as the endpoint answers them.
        /// </summary>
        private static List<string> Swimlanes(string wql = null)
        {
            return [.. Invoke<IEnumerable<RestApiKanbanSwimlane>>("RetrieveSwimlanes", CreateRequest(wql)).Select(x => x.Label)];
        }

        /// <summary>
        /// Stores a filter through the settings dialog's path.
        /// </summary>
        private static void Settings(string filter)
        {
            Invoke<object>("UpdateSettings", new RestApiDashboardLayout { Action = "settings", Filter = filter }, CreateRequest());
        }

        /// <summary>
        /// Reads the stored filter.
        /// </summary>
        private static string StoredFilter()
        {
            return CoreHub.KanbanBoardManager.GetBoard(WorkspaceId, ObjectKind.Issue)?.Filter;
        }

        /// <summary>
        /// Without a filter the board shows every active issue of the workspace, in one lane
        /// per populated class; the archived one is never a card.
        /// </summary>
        [Fact]
        public void WithoutAFilter_TheBoardShowsEveryActiveIssue()
        {
            Seed(nameof(WithoutAFilter_TheBoardShowsEveryActiveIssue));

            var cards = Cards(nameof(WithoutAFilter_TheBoardShowsEveryActiveIssue));

            Assert.Equal(3, cards.Count);
            Assert.DoesNotContain(ArchivedMailId.ToString(), cards);
            Assert.Equal(["Bug", "Task"], Swimlanes());
        }

        /// <summary>
        /// A stored filter narrows the cards to what it matches, and the lanes with them: a
        /// class none of whose issues match is not a lane.
        /// </summary>
        [Fact]
        public void AStoredFilter_NarrowsTheCardsAndTheLanes()
        {
            Seed(nameof(AStoredFilter_NarrowsTheCardsAndTheLanes));

            Settings("Summary ~ \"mail\"");

            Assert.Equal("Summary ~ \"mail\"", StoredFilter());

            var cards = Cards(nameof(AStoredFilter_NarrowsTheCardsAndTheLanes));

            Assert.Equal([MailBugId.ToString(), MailTaskId.ToString()], cards);
            Assert.Equal(["Bug", "Task"], Swimlanes());

            Settings("Key = \"KB-2\"");

            Assert.Equal([PrinterBugId.ToString()], Cards(nameof(AStoredFilter_NarrowsTheCardsAndTheLanes)));
            Assert.Equal(["Bug"], Swimlanes());
        }

        /// <summary>
        /// A filter nothing satisfies empties the board rather than falling back to everything.
        /// </summary>
        [Fact]
        public void AFilterNothingMatches_EmptiesTheBoard()
        {
            Seed(nameof(AFilterNothingMatches_EmptiesTheBoard));

            Settings("Key = \"KB-99\"");

            Assert.Empty(Cards(nameof(AFilterNothingMatches_EmptiesTheBoard)));
            Assert.Empty(Swimlanes());
        }

        /// <summary>
        /// A filter the request carries previews over the stored one, so the settings dialog
        /// shows what it is about to store; the stored filter is untouched by that.
        /// </summary>
        [Fact]
        public void AFilterOnTheRequest_WinsOverTheStoredOne()
        {
            Seed(nameof(AFilterOnTheRequest_WinsOverTheStoredOne));

            Settings("Key = \"KB-1\"");

            Assert.Equal([PrinterBugId.ToString()], Cards(nameof(AFilterOnTheRequest_WinsOverTheStoredOne), "Key = \"KB-2\""));
            Assert.Equal(["Bug"], Swimlanes("Key = \"KB-2\""));
            Assert.Equal("Key = \"KB-1\"", StoredFilter());

            // a blank one on the request means "nothing of my own": the stored filter applies
            Assert.Equal([MailBugId.ToString()], Cards(nameof(AFilterOnTheRequest_WinsOverTheStoredOne), "  "));
        }

        /// <summary>
        /// A blank filter clears the stored one, and surrounding whitespace is not stored.
        /// </summary>
        [Fact]
        public void UpdateSettings_ClearsOnBlankAndTrims()
        {
            Seed(nameof(UpdateSettings_ClearsOnBlankAndTrims));

            Settings("  Key = \"KB-3\"  ");
            Assert.Equal("Key = \"KB-3\"", StoredFilter());

            Settings("   ");
            Assert.Null(StoredFilter());
            Assert.Equal(3, Cards(nameof(UpdateSettings_ClearsOnBlankAndTrims)).Count);

            Settings("Key = \"KB-3\"");
            Settings(null);
            Assert.Null(StoredFilter());
        }

        /// <summary>
        /// A filter that does not compile is refused when it is written - with the reason - and
        /// the filter that was stored before stays.
        /// </summary>
        [Fact]
        public void UpdateSettings_RefusesAFilterThatDoesNotCompile()
        {
            Seed(nameof(UpdateSettings_RefusesAFilterThatDoesNotCompile));

            Settings("Key = \"KB-1\"");

            var syntax = Assert.Throws<ArgumentException>(() => Settings("=== not a query ==="));
            Assert.Contains("WQL", syntax.Message);

            var unknown = Assert.Throws<ArgumentException>(() => Settings("NoSuchAttribute = \"x\""));
            Assert.Contains("WQL", unknown.Message);

            Assert.Equal("Key = \"KB-1\"", StoredFilter());
        }

        /// <summary>
        /// A stored filter that no longer compiles - written before the check existed, or
        /// against an attribute that is gone - narrows nothing rather than taking the board
        /// down.
        /// </summary>
        [Fact]
        public void AStoredFilterThatDoesNotCompile_IsIgnored()
        {
            Seed(nameof(AStoredFilterThatDoesNotCompile_IsIgnored));

            var board = CoreHub.KanbanBoardManager.EnsureBoard(WorkspaceId, ObjectKind.Issue);
            CoreHub.KanbanBoardManager.SetFilter(board.Id, "=== not a query ===");

            Assert.Equal(3, Cards(nameof(AStoredFilterThatDoesNotCompile_IsIgnored)).Count);
            Assert.Equal(["Bug", "Task"], Swimlanes());
        }

        /// <summary>
        /// On a customized board the "Other" lane exists for an unconfigured class only while
        /// one of that class's issues is on the board - a filter that leaves none removes the
        /// lane too.
        /// </summary>
        [Fact]
        public void ACustomizedBoard_ShowsTheOtherLaneOnlyForFilteredIssues()
        {
            Seed(nameof(ACustomizedBoard_ShowsTheOtherLaneOnlyForFilteredIssues));

            var board = CoreHub.KanbanBoardManager.EnsureBoard(WorkspaceId, ObjectKind.Issue);

            CoreHub.KanbanBoardManager.SetSwimlanes(board.Id,
            [
                new KanbanBoardSwimlane { BoardId = board.Id, Name = "Bugs", ClassId = BugClassId, Position = 0 }
            ]);

            // the task is unconfigured, so the catch-all lane is offered
            Assert.Equal(["Bugs", "Other"], Swimlanes());

            // ...until the filter leaves no task on the board
            Settings("Key = \"KB-1\"");

            Assert.Equal(["Bugs"], Swimlanes());
            Assert.Equal([MailBugId.ToString()], Cards(nameof(ACustomizedBoard_ShowsTheOtherLaneOnlyForFilteredIssues)));

            // ...and again when it leaves only the task
            Settings("Key = \"KB-3\"");

            Assert.Equal(["Bugs", "Other"], Swimlanes());
        }

        /// <summary>
        /// An issue whose text attribute is empty does not match a text operator and does not
        /// fail the board either.
        /// </summary>
        [Fact]
        public void AnIssueWithoutASummary_IsSkippedByATextFilter()
        {
            Seed(nameof(AnIssueWithoutASummary_IsSkippedByATextFilter));

            using (var db = CoreHubFixture.CreateDbContext(nameof(AnIssueWithoutASummary_IsSkippedByATextFilter)))
            {
                var task = db.Objects.First(x => x.Id == MailTaskId);
                task.Summary = null;
                db.SaveChanges();
            }

            Settings("Summary ~ \"mail\"");

            Assert.Equal([MailBugId.ToString()], Cards(nameof(AnIssueWithoutASummary_IsSkippedByATextFilter)));
        }
    }
}
