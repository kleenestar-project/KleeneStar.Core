using KleeneStar.Model.Entities;
using KleeneStar.Model.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using TransferEndpoint = global::KleeneStar.Core.WWW.Api._1_.Workspaces.Transfer;

namespace KleeneStar.Core.Test.WWW.Api.Workspaces
{
    /// <summary>
    /// Holds the workspace transfer endpoint to its access rule - an export holds every object of
    /// a workspace, classified ones included, so only the installation's administrators are
    /// answered - and checks how it names its files and words its report.
    /// </summary>
    /// <remarks>
    /// A request built here carries no session, so every call is an outsider's; that an
    /// administrator is answered is verified against a running installation. The transfer
    /// itself is tested on real SQLite files in the model (<c>UnitTestWorkspaceTransfer</c>).
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestWorkspaceTransferEndpoint
    {
        private static readonly Guid OutsiderId = Guid.Parse("F0F0F000-0000-4000-8000-0000000000A1");

        /// <summary>
        /// Seeds an outsider.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (!db.Identities.Any(x => x.Id == OutsiderId))
            {
                db.Identities.Add(new Identity { Id = OutsiderId, Name = "Outsider", UserName = "transfer-outsider", Email = "transfer-outsider@example.com", PasswordHash = "x" });
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Builds a request.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <returns>The request.</returns>
        private static IRequest Request(string method)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = method,
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/workspaces/transfer",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Accept-Language"] = "en" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature { LocalIpAddress = System.Net.IPAddress.Loopback, RemoteIpAddress = System.Net.IPAddress.Loopback });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature { TraceIdentifier = nameof(UnitTestWorkspaceTransferEndpoint) });

            return new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;
        }

        /// <summary>
        /// The export and the import refuse an outsider and an anonymous caller with 403, also
        /// when an identity acts on the call chain.
        /// </summary>
        [Fact]
        public void Transfer_IsTheAdministratorsOnly()
        {
            Seed(nameof(Transfer_IsTheAdministratorsOnly));

            var endpoint = new TransferEndpoint();

            Assert.IsType<ResponseForbidden>(endpoint.Retrieve(Request("GET")));
            Assert.IsType<ResponseForbidden>(endpoint.Start(Request("POST")));

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.IsType<ResponseForbidden>(endpoint.Retrieve(Request("GET")));
                Assert.IsType<ResponseForbidden>(endpoint.Start(Request("POST")));
            }
        }

        /// <summary>
        /// An export of one workspace is named by its key, one of several by the word, and a
        /// character a file name cannot hold is replaced.
        /// </summary>
        [Fact]
        public void FileName_NamesTheWorkspace()
        {
            var single = new WorkspaceTransferReport();
            single.Workspaces.Add(new WorkspaceTransferWorkspace { Id = Guid.NewGuid(), Key = "SD", Name = "Service desk" });

            var several = new WorkspaceTransferReport();
            several.Workspaces.Add(new WorkspaceTransferWorkspace { Id = Guid.NewGuid(), Key = "A" });
            several.Workspaces.Add(new WorkspaceTransferWorkspace { Id = Guid.NewGuid(), Key = "B" });

            var odd = new WorkspaceTransferReport();
            odd.Workspaces.Add(new WorkspaceTransferWorkspace { Id = Guid.NewGuid(), Key = "a/b \"c\"" });

            Assert.Matches("^kleenestar-SD-\\d{8}-\\d{4}\\.zip$", TransferEndpoint.FileName(single));
            Assert.Matches("^kleenestar-workspaces-\\d{8}-\\d{4}\\.zip$", TransferEndpoint.FileName(several));
            Assert.Matches("^kleenestar-a_b__c_-\\d{8}-\\d{4}\\.zip$", TransferEndpoint.FileName(odd));
        }

        /// <summary>
        /// The report lists every workspace with whether it was imported, the tables that had rows
        /// and one line per note.
        /// </summary>
        [Fact]
        public void Report_ListsWorkspacesTablesAndNotes()
        {
            var report = new WorkspaceTransferReport { Succeeded = true, FormatVersion = 1 };
            report.Workspaces.Add(new WorkspaceTransferWorkspace { Id = Guid.NewGuid(), Key = "NEW", Name = "New" });
            report.Workspaces.Add(new WorkspaceTransferWorkspace { Id = Guid.NewGuid(), Key = "OLD", Name = "Old", Skipped = "row-conflict:Key" });
            report.Tables.Add(new WorkspaceTransferTableReport { Name = "Object", Rows = 3, Written = 2, Skipped = 1 });
            report.Tables.Add(new WorkspaceTransferTableReport { Name = "Sprint" });
            report.Note("column-unknown", "Object", "Colour");
            report.Note("column-unknown", "Object", "Colour");

            var json = JsonSerializer.SerializeToElement(TransferEndpoint.Describe(Request("POST"), report, dryRun: true));

            Assert.True(json.GetProperty("succeeded").GetBoolean());
            Assert.True(json.GetProperty("dryRun").GetBoolean());

            var workspaces = json.GetProperty("workspaces").EnumerateArray().ToList();

            Assert.True(workspaces[0].GetProperty("imported").GetBoolean());
            Assert.False(workspaces[1].GetProperty("imported").GetBoolean());
            Assert.False(string.IsNullOrEmpty(workspaces[1].GetProperty("reason").GetString()));

            Assert.Equal("Object", Assert.Single(json.GetProperty("tables").EnumerateArray()).GetProperty("name").GetString());

            // the same note twice is one line
            Assert.Single(json.GetProperty("notes").EnumerateArray());
            Assert.Equal(2, Assert.Single(report.Notes).Count);
        }
    }
}
