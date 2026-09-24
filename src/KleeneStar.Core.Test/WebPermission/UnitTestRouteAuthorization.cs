using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebPermission
{
    /// <summary>
    /// Tests what every page of the application demands, read off its id and its route
    /// (<see cref="RouteAuthorization"/>) - the rule the page guard applies to all of them.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestRouteAuthorization
    {
        private static readonly Guid WorkspaceId = Guid.Parse("A0720000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("A0720000-0000-0000-0000-000000000002");
        private static readonly Guid ObjectId = Guid.Parse("A0720000-0000-0000-0000-000000000003");
        private static readonly Guid FieldId = Guid.Parse("A0720000-0000-0000-0000-000000000004");

        /// <summary>
        /// Seeds a workspace with a class, a field of it and an object.
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

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-ra", Name = "routed" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.Fields.Add(new Field { Id = FieldId, Name = "Title", FieldType = FieldType.Text, ClassId = ClassId, State = FieldState.Active });
            db.Objects.Add(new ObjectEntity(ObjectId) { Key = "RA-1", Summary = "routed", WorkspaceId = WorkspaceId, ClassId = ClassId });
            db.SaveChanges();
        }

        /// <summary>
        /// Builds a request whose route carries the supplied segments.
        /// </summary>
        /// <param name="segments">The route parameters, as (key, value).</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(params (string Key, string Value)[] segments)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/",
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
                TraceIdentifier = nameof(UnitTestRouteAuthorization)
            });

            var request = new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;

            foreach (var (key, value) in segments)
            {
                request.AddParameter(new Parameter(key, value, ParameterScope.Url));
            }

            return request;
        }

        /// <summary>
        /// The class page and every page of a class's structure administer the class - the
        /// class is read off the route directly or through the record it names.
        /// </summary>
        [Fact]
        public void TheClassAdministrationDemandsToAdministerTheClass()
        {
            Seed(nameof(TheClassAdministrationDemandsToAdministerTheClass));

            var chain = PageAuthorization.ChainOf(new Class { Id = ClassId, WorkspaceId = WorkspaceId });

            var @class = RouteAuthorization.Resolve("kleenestar.core.www.class._classid_.index", CreateRequest((ClassIdParameter.Key, ClassId.ToString())));
            var field = RouteAuthorization.Resolve("kleenestar.core.www.field._fieldid_.edit", CreateRequest((FieldIdParameter.Key, FieldId.ToString())));
            var classes = RouteAuthorization.Resolve("kleenestar.core.www.classes._workspacekey_.index", CreateRequest((WorkspaceKeyParameter.Key, "ws-ra")));

            Assert.Equal(typeof(ClassUpdatePermission), @class?.Permission);
            Assert.Equal(chain, @class?.Chain);
            Assert.Equal(typeof(ClassUpdatePermission), field?.Permission);
            Assert.Equal(chain, field?.Chain);
            Assert.Equal(typeof(ClassUpdatePermission), classes?.Permission);
            Assert.Equal(PageAuthorization.ChainOf(new Workspace { Id = WorkspaceId }), classes?.Chain);
        }

        /// <summary>
        /// An object's page reads it; its dialogs that change it demand to change it; a
        /// restore demands to restore.
        /// </summary>
        [Fact]
        public void AnObjectPageDemandsWhatThePageDoes()
        {
            Seed(nameof(AnObjectPageDemandsWhatThePageDoes));

            var route = CreateRequest((ObjectKeyParameter.Key, "RA-1"));
            var chain = PageAuthorization.ChainOf(new ObjectEntity(ObjectId) { ClassId = ClassId, WorkspaceId = WorkspaceId });

            Assert.Equal(typeof(ObjectReadPermission), RouteAuthorization.Resolve("kleenestar.core.www.issue._objectkey_.index", route)?.Permission);
            Assert.Equal(chain, RouteAuthorization.Resolve("kleenestar.core.www.issue._objectkey_.index", route)?.Chain);
            Assert.Equal(typeof(ObjectUpdatePermission), RouteAuthorization.Resolve("kleenestar.core.www.issue._objectkey_.edit", route)?.Permission);
            Assert.Equal(typeof(ObjectUpdatePermission), RouteAuthorization.Resolve("kleenestar.core.www.document._objectkey_.edit", route)?.Permission);
            Assert.Equal(typeof(ObjectUpdatePermission), RouteAuthorization.Resolve("kleenestar.core.www.issue._objectkey_.securitylevel", route)?.Permission);
            Assert.Equal(typeof(ObjectRestoreStatePermission), RouteAuthorization.Resolve("kleenestar.core.www.issue._objectkey_.historyrestore", route)?.Permission);
        }

        /// <summary>
        /// A page naming a workspace reads its content; the organize page writes it; the
        /// workspace's own dialogs and the home-page chooser administer it.
        /// </summary>
        [Fact]
        public void AWorkspacePageDemandsWhatThePageDoes()
        {
            Seed(nameof(AWorkspacePageDemandsWhatThePageDoes));

            var route = CreateRequest((WorkspaceKeyParameter.Key, "ws-ra"));

            Assert.Equal(typeof(WorkspaceReadContentPermission), RouteAuthorization.Resolve("kleenestar.core.www.issues._workspacekey_.index", route)?.Permission);
            Assert.Equal(typeof(WorkspaceWriteContentPermission), RouteAuthorization.Resolve("kleenestar.core.www.issues._workspacekey_.organize", route)?.Permission);
            Assert.Equal(typeof(WorkspaceUpdatePermission), RouteAuthorization.Resolve("kleenestar.core.www.workspaces._workspacekey_.edit", route)?.Permission);
            Assert.Equal(typeof(WorkspaceUpdatePermission), RouteAuthorization.Resolve("kleenestar.core.www.workspaces._workspacekey_.permissions", route)?.Permission);
            Assert.Equal(typeof(WorkspaceUpdatePermission), RouteAuthorization.Resolve("kleenestar.core.www.documents._workspacekey_.home", route)?.Permission);
        }

        /// <summary>
        /// A page naming no resource is left to its own guard, and so is every REST endpoint,
        /// which answers a status of its own rather than being redirected.
        /// </summary>
        [Fact]
        public void APageOnNoChainIsNotGuardedHere()
        {
            Seed(nameof(APageOnNoChainIsNotGuardedHere));

            Assert.Null(RouteAuthorization.Resolve("kleenestar.core.www.index", CreateRequest()));
            Assert.Null(RouteAuthorization.Resolve("kleenestar.core.www.settings.groups.index", CreateRequest()));
            Assert.Null(RouteAuthorization.Resolve("kleenestar.core.www.api._1_.objects.index", CreateRequest((ObjectKeyParameter.Key, "RA-1"))));
            Assert.Null(RouteAuthorization.Resolve(null, CreateRequest()));
        }

        /// <summary>
        /// On a workspace nobody administered, the content is open to everybody and the class
        /// page is not - it is the installation's administrators', and an anonymous caller is
        /// none of them. Creating a workspace needs a signed-in caller.
        /// </summary>
        [Fact]
        public void TheClassPageIsClosedToEverybodyButTheAdministrators()
        {
            Seed(nameof(TheClassPageIsClosedToEverybodyButTheAdministrators));

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.True(RouteAuthorization.IsGranted("kleenestar.core.www.issues._workspacekey_.index", CreateRequest((WorkspaceKeyParameter.Key, "ws-ra"))));
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.class._classid_.index", CreateRequest((ClassIdParameter.Key, ClassId.ToString()))));
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.workspaces.add", CreateRequest()));
            }
        }

        /// <summary>
        /// Notifications and dashboards are on no workspace chain; they are for signed-in callers,
        /// and an anonymous one is refused both pages and offered neither menu.
        /// </summary>
        [Fact]
        public void NotificationsAndDashboardsNeedASignedInCaller()
        {
            Seed(nameof(NotificationsAndDashboardsNeedASignedInCaller));

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.notifications.index", CreateRequest()));
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.dashboards.index", CreateRequest()));
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.dashboard._dashboardid_.index", CreateRequest((DashboardIdParameter.Key, Guid.NewGuid().ToString()))));
                Assert.False(ContentVisibility.MayUseDashboards());
            }
        }

        /// <summary>
        /// A kind's header menu is offered while a class of the kind is readable: the seeded
        /// class is an issue class on an unadministered workspace, so issues are offered and a
        /// kind without any class is not; once the workspace is administered without a grant to
        /// the anonymous group, issues are not offered to an anonymous caller either.
        /// </summary>
        [Fact]
        public void AKindMenuIsOfferedOnlyWhileAClassOfTheKindIsReadable()
        {
            var database = nameof(AKindMenuIsOfferedOnlyWhileAClassOfTheKindIsReadable);
            Seed(database);

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.True(ContentVisibility.MayReadAnyOfKind(ObjectKind.Issue));
                Assert.False(ContentVisibility.MayReadAnyOfKind(ObjectKind.Blog));
            }

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Groups.Add(new Group { Id = Guid.Parse("A0720000-0000-0000-0000-00000000000A"), Name = "Editors" });
                db.PermissionAssignments.Add(new PermissionAssignment
                {
                    Scope = PermissionScope.Workspace,
                    ScopeId = WorkspaceId.ToString(),
                    GroupId = Guid.Parse("A0720000-0000-0000-0000-00000000000A"),
                    Policy = "workspace_edit_policy",
                    Created = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            ContentVisibility.Invalidate();

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.False(ContentVisibility.MayReadAnyOfKind(ObjectKind.Issue));
            }
        }

        /// <summary>
        /// The workspace overview - and the menu leading to it - is offered to an anonymous
        /// caller only while a workspace is readable to them: once the only workspace is
        /// administered without a grant to the anonymous group, there is nothing for them on it.
        /// </summary>
        [Fact]
        public void TheOverviewIsOfferedOnlyWhenSomethingIsReadable()
        {
            var database = nameof(TheOverviewIsOfferedOnlyWhenSomethingIsReadable);
            Seed(database);

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.True(RouteAuthorization.MayListWorkspaces(null));
                Assert.True(RouteAuthorization.IsGranted("kleenestar.core.www.workspaces.index", CreateRequest()));
            }

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Groups.Add(new Group { Id = Guid.Parse("A0720000-0000-0000-0000-000000000009"), Name = "Editors" });
                db.PermissionAssignments.Add(new PermissionAssignment
                {
                    Scope = PermissionScope.Workspace,
                    ScopeId = WorkspaceId.ToString(),
                    GroupId = Guid.Parse("A0720000-0000-0000-0000-000000000009"),
                    Policy = "workspace_edit_policy",
                    Created = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            ContentVisibility.Invalidate();

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.False(RouteAuthorization.MayListWorkspaces(null));
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.workspaces.index", CreateRequest()));
            }
        }
    }
}
