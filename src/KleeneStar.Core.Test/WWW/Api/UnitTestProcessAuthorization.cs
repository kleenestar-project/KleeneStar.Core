using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WWW.Api
{
    /// <summary>
    /// Holds the endpoints of automation, integrations, approvals, planning and catalogs to the
    /// access rules: the configuration of a workspace is its administrators' - anybody else is
    /// refused with 403 - and the records of an object (approvals, worklogs) are reached through
    /// the object, so a caller who may not read it is told nothing (404).
    /// </summary>
    /// <remarks>
    /// An endpoint reads its caller from the request's session; a request built here carries
    /// none, so every call is an outsider's - with or without an identity acting on the call
    /// chain, which a request deliberately does not inherit. That the administrators are
    /// answered is verified against a running installation (see the documentation).
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestProcessAuthorization
    {
        private static readonly Guid WorkspaceId = Guid.Parse("F0F0F000-0000-4000-8000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("F0F0F000-0000-4000-8000-000000000002");
        private static readonly Guid ObjectId = Guid.Parse("F0F0F000-0000-4000-8000-000000000003");
        private static readonly Guid AdminId = Guid.Parse("F0F0F000-0000-4000-8000-000000000004");
        private static readonly Guid OutsiderId = Guid.Parse("F0F0F000-0000-4000-8000-000000000005");
        private static readonly Guid GroupId = Guid.Parse("F0F0F000-0000-4000-8000-000000000006");
        private const string Key = "ws-pa";

        /// <summary>
        /// Seeds a workspace administered by a group with one member, an outsider, and an object.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            var group = new Group { Id = GroupId, Name = "Workspace admins" };
            var admin = new Identity { Id = AdminId, Name = "Admin", UserName = "wsadmin", Email = "wsadmin@example.com", PasswordHash = "x" };

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = Key, Name = "guarded" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.Objects.Add(new ObjectEntity(ObjectId) { Key = "PA-1", Summary = "Guarded", WorkspaceId = WorkspaceId, ClassId = ClassId });
            db.Groups.Add(group);
            db.Identities.Add(admin);
            db.Identities.Add(new Identity { Id = OutsiderId, Name = "Outsider", UserName = "outsider", Email = "outsider@example.com", PasswordHash = "x" });
            db.Set<IdentityGroupMembership>().Add(new IdentityGroupMembership { Identity = admin, Group = group });
            db.PermissionAssignments.Add(new PermissionAssignment
            {
                Scope = PermissionScope.Workspace,
                ScopeId = WorkspaceId.ToString(),
                GroupId = GroupId,
                Policy = "workspace_admin_policy",
                Created = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        /// <summary>
        /// Builds a GET request on the workspace's route.
        /// </summary>
        /// <param name="query">Further parameters.</param>
        /// <returns>The request.</returns>
        private static IRequest Request(params (string Name, string Value)[] query)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Accept-Language"] = "en" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature { LocalIpAddress = System.Net.IPAddress.Loopback, RemoteIpAddress = System.Net.IPAddress.Loopback });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature { TraceIdentifier = nameof(UnitTestProcessAuthorization) });

            var request = new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;

            request.AddParameter(new Parameter(WorkspaceKeyParameter.Key, Key, ParameterScope.Url));

            foreach (var (name, value) in query)
            {
                request.AddParameter(new Parameter(name, value, ParameterScope.Parameter));
            }

            return request;
        }

        /// <summary>
        /// Returns the configuration endpoints of a workspace.
        /// </summary>
        /// <returns>The endpoint calls.</returns>
        private static IEnumerable<(string Name, Func<IRequest, IResponse> Call)> Configuration()
        {
            yield return ("automation", new global::KleeneStar.Core.WWW.Api._1_.Automation._workspacekey_.Index().Retrieve);
            yield return ("automation jobs", new global::KleeneStar.Core.WWW.Api._1_.Automation._workspacekey_.Jobs().Retrieve);
            yield return ("webhooks", new global::KleeneStar.Core.WWW.Api._1_.Webhooks._workspacekey_.Index().Retrieve);
            yield return ("approval policies", new global::KleeneStar.Core.WWW.Api._1_.ApprovalPolicies._workspacekey_.Index().Retrieve);
            yield return ("planning", new global::KleeneStar.Core.WWW.Api._1_.Planning._workspacekey_.Index().Retrieve);
            yield return ("catalogs", new global::KleeneStar.Core.WWW.Api._1_.Catalogs._workspacekey_.Index().Retrieve);
        }

        /// <summary>
        /// The configuration endpoints refuse an outsider and an anonymous caller with 403.
        /// </summary>
        [Fact]
        public void Configuration_IsTheAdministratorsOnly()
        {
            Seed(nameof(Configuration_IsTheAdministratorsOnly));

            foreach (var (name, call) in Configuration())
            {
                using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
                {
                    Assert.True(call(Request()) is ResponseForbidden, $"{name} answers an outsider.");
                }

                Assert.True(call(Request()) is ResponseForbidden, $"{name} answers an anonymous caller.");

                // an identity acting on the call chain does not sign a request in
                using (CoreHub.SessionManager.BeginIdentity(AdminId))
                {
                    Assert.True(call(Request()) is ResponseForbidden, $"{name} takes an acting identity for a session.");
                }
            }
        }

        /// <summary>
        /// The approvals and worklogs of an object are not found for a caller who may not read it.
        /// </summary>
        [Fact]
        public void ObjectRecords_AreHiddenWithTheirObject()
        {
            Seed(nameof(ObjectRecords_AreHiddenWithTheirObject));

            var approvals = new global::KleeneStar.Core.WWW.Api._1_.Approvals.Index();
            var worklogs = new global::KleeneStar.Core.WWW.Api._1_.Work.Worklogs();

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.IsType<ResponseNotFound>(approvals.Retrieve(Request(("object", "PA-1"))));
                Assert.IsType<ResponseNotFound>(worklogs.Retrieve(Request(("object", "PA-1"))));
            }

        }

        /// <summary>
        /// Nobody signed in has a capacity to read.
        /// </summary>
        [Fact]
        public void Capacity_NeedsASignedInPerson()
        {
            Seed(nameof(Capacity_NeedsASignedInPerson));

            Assert.IsType<ResponseUnauthorized>(new global::KleeneStar.Core.WWW.Api._1_.Work.Capacity().Retrieve(Request()));
        }
    }
}
