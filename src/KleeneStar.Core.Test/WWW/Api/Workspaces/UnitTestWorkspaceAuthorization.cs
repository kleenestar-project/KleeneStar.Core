using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.Test.WWW.Api.Workspaces
{
    /// <summary>
    /// Tests that <c>/api/1/workspaces</c> and its permission surface ask the same question the
    /// workspace dialog pages demand an answer to: a mutation on an administered workspace is
    /// refused to a caller outside every granted group with <c>403</c>, an unadministered
    /// workspace is administered by the installation's administrators alone (fail closed), and a
    /// plain create - which is on no chain - is refused only to a caller who is not signed in.
    /// </summary>
    /// <remarks>
    /// The requests are built by hand and carry no body, so a request the gate lets through
    /// runs into the base's own checks - a missing payload, an unknown id - and answers with
    /// something other than <c>403</c>. That is what the cases assert: the gate stands in front
    /// of everything else, and only the gate answers <c>403</c>.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestWorkspaceAuthorization
    {
        private static readonly Guid WorkspaceId = Guid.Parse("A0710000-0000-0000-0000-000000000001");
        private static readonly Guid OutsiderId = Guid.Parse("A0710000-0000-0000-0000-000000000005");
        private static readonly Guid GroupId = Guid.Parse("A0710000-0000-0000-0000-000000000006");

        /// <summary>
        /// Seeds a workspace, a group and an identity outside of it.
        /// </summary>
        /// <param name="database">The name of the isolated in-memory database.</param>
        /// <param name="administered">Whether a grant is written on the workspace, which is what
        /// makes it enforced.</param>
        private static void Seed(string database, bool administered)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-wa", Name = "guarded" });
            db.Groups.Add(new Group { Id = GroupId, Name = "Administrators" });
            db.Identities.Add(new Identity { Id = OutsiderId, Name = "Outsider", UserName = "outsider", Email = "outsider@example.com", PasswordHash = "x" });

            if (administered)
            {
                db.PermissionAssignments.Add(new PermissionAssignment
                {
                    Scope = PermissionScope.Workspace,
                    ScopeId = WorkspaceId.ToString(),
                    GroupId = GroupId,
                    Policy = "workspace_admin_policy",
                    Created = DateTime.UtcNow
                });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Builds a request of the given method addressing the seeded workspace the way the
        /// CRUD endpoint is addressed - through the <c>id</c> query parameter - and, when asked,
        /// in one of the dialog modes.
        /// </summary>
        /// <param name="method">The http method.</param>
        /// <param name="id">The addressed workspace, or null.</param>
        /// <param name="mode">The dialog mode, or null.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string method, Guid? id = null, string mode = null)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = method,
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/workspaces",
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
                TraceIdentifier = nameof(UnitTestWorkspaceAuthorization)
            });

            var context = new WebExpress.WebCore.WebMessage.HttpContext(features, null!);
            var request = context.Request;

            if (id.HasValue)
            {
                request.AddParameter(new Parameter(ParameterId.Key, id.Value.ToString(), ParameterScope.Parameter));
            }

            if (mode is not null)
            {
                request.AddParameter(new Parameter("mode", mode, ParameterScope.Parameter));
            }

            // the permission surface reads the workspace off its route
            request.AddParameter(new Parameter(WorkspaceKeyParameter.Key, "ws-wa", ParameterScope.Url));

            return request;
        }

        /// <summary>
        /// On an administered workspace every mutation, every dialog mode and the permission
        /// surface refuse an outsider with 403 - and nothing else answers 403.
        /// </summary>
        [Fact]
        public void AdministeredWorkspace_RefusesTheOutsider()
        {
            Seed(nameof(AdministeredWorkspace_RefusesTheOutsider), administered: true);

            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Workspaces.Index();
            var permissions = new global::KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_.Permission();

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.IsType<ResponseForbidden>(endpoint.Update(CreateRequest("PUT", WorkspaceId)));
                Assert.IsType<ResponseForbidden>(endpoint.Delete(CreateRequest("DELETE", WorkspaceId)));
                Assert.IsType<ResponseForbidden>(endpoint.Create(CreateRequest("POST", WorkspaceId)));
                Assert.IsType<ResponseForbidden>(endpoint.Retrieve(CreateRequest("GET", WorkspaceId, "edit")));
                Assert.IsType<ResponseForbidden>(endpoint.Retrieve(CreateRequest("GET", WorkspaceId, "delete")));
                Assert.IsType<ResponseForbidden>(endpoint.Retrieve(CreateRequest("GET", WorkspaceId, "clone")));

                Assert.IsType<ResponseForbidden>(permissions.Retrieve(CreateRequest("GET")));
                Assert.IsType<ResponseForbidden>(permissions.Create(CreateRequest("POST")));
                Assert.IsType<ResponseForbidden>(permissions.Update(CreateRequest("PUT")));
                Assert.IsType<ResponseForbidden>(permissions.Delete(CreateRequest("DELETE")));

                // reading a workspace is not what the grants administer
                Assert.IsNotType<ResponseForbidden>(endpoint.Retrieve(CreateRequest("GET", WorkspaceId)));
            }
        }

        /// <summary>
        /// A workspace nobody administered is not administered by everybody: changing it, its
        /// dialogs and its permission surface are the installation's administrators' until a
        /// grant says otherwise, so a caller outside that group - here the hand-built request,
        /// which names nobody - is refused. Reading it is not what the grants administer.
        /// </summary>
        [Fact]
        public void UnadministeredWorkspace_IsAdministeredByTheAdministratorsAlone()
        {
            Seed(nameof(UnadministeredWorkspace_IsAdministeredByTheAdministratorsAlone), administered: false);

            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Workspaces.Index();
            var permissions = new global::KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_.Permission();

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.IsType<ResponseForbidden>(endpoint.Update(CreateRequest("PUT", WorkspaceId)));
                Assert.IsType<ResponseForbidden>(endpoint.Create(CreateRequest("POST", WorkspaceId)));
                Assert.IsType<ResponseForbidden>(endpoint.Retrieve(CreateRequest("GET", WorkspaceId, "edit")));
                Assert.IsType<ResponseForbidden>(permissions.Retrieve(CreateRequest("GET")));

                Assert.IsNotType<ResponseForbidden>(endpoint.Retrieve(CreateRequest("GET", WorkspaceId)));
            }
        }

        /// <summary>
        /// A workspace created from nothing is on no chain, so no grant decides it - but a caller
        /// who is not signed in (the hand-built request names nobody) may not create one. An id
        /// that names no workspace is not an authorization question either - the base answers
        /// it as not found.
        /// </summary>
        [Fact]
        public void CreateAndUnknownId_AreNotAuthorizationQuestions()
        {
            Seed(nameof(CreateAndUnknownId_AreNotAuthorizationQuestions), administered: true);

            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Workspaces.Index();

            using (CoreHub.SessionManager.BeginIdentity(OutsiderId))
            {
                Assert.IsType<ResponseForbidden>(endpoint.Create(CreateRequest("POST")));
                Assert.IsType<ResponseNotFound>(endpoint.Update(CreateRequest("PUT", Guid.NewGuid())));
                Assert.IsType<ResponseNotFound>(endpoint.Delete(CreateRequest("DELETE", Guid.NewGuid())));
            }
        }
    }
}
