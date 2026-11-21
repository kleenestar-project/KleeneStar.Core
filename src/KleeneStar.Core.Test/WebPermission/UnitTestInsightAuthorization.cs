using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.Test.WebPermission
{
    /// <summary>
    /// Tests the rules around an insight that are not about one of its tabs: who may create
    /// insights (the installation-wide creation resource), who administers that, which state
    /// change needs archive or restore, and that an insight set to deleted is out of use - not
    /// opened, not listed in the sidebar, and its tab scope answers as if it were missing.
    /// </summary>
    /// <remarks>
    /// The fixtures have no policy registry, so a granted policy carries nothing there: the
    /// cases assert the open reading of an unadministered resource, refusals and fail-closed
    /// rules. A request cannot be signed in here, so the callers act through
    /// <c>BeginIdentity</c> on calls without a request.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestInsightAuthorization
    {
        private static readonly Guid MemberId = Guid.Parse("A0730000-0000-0000-0000-000000000001");
        private static readonly Guid AdministratorId = Guid.Parse("A0730000-0000-0000-0000-000000000002");
        private static readonly Guid CreatorsId = Guid.Parse("A0730000-0000-0000-0000-000000000003");

        /// <summary>
        /// Seeds a member of no group, an administrator and a group of creators the member is
        /// not in.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            if (db.Identities.Any(x => x.Id == MemberId))
            {
                return;
            }

            db.Groups.Add(new Group { Id = CreatorsId, Name = "Creators" });
            db.Identities.Add(new Identity { Id = MemberId, Name = "Member", UserName = "member", Email = "member@example.com", PasswordHash = "x" });

            var administrators = new Group { Id = Group.AdministratorsId, Name = "Admin" };
            db.Groups.Add(administrators);
            db.Identities.Add(new Identity
            {
                Id = AdministratorId,
                Name = "Administrator",
                UserName = "administrator",
                Email = "administrator@example.com",
                PasswordHash = "x",
                GroupMemberships = [new IdentityGroupMembership { Group = administrators }]
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Writes a grant directly, bypassing <c>Assign</c> and its catalog check.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        /// <param name="scopeId">The resource.</param>
        /// <param name="policy">The policy.</param>
        private static void Grant(string database, string scopeId, string policy)
        {
            using var db = CoreHubFixture.CreateDbContext(database);

            db.PermissionAssignments.Add(new PermissionAssignment
            {
                Scope = PermissionScope.Insight,
                ScopeId = scopeId,
                GroupId = CreatorsId,
                Policy = policy,
                Created = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Adds an insight in the given state.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="state">The state.</param>
        /// <returns>The insight.</returns>
        private static Insight AddInsight(string name, InsightState state)
        {
            var insight = new Insight(Guid.NewGuid()) { Name = name, State = state };

            CoreHub.InsightManager.Add(insight);

            return insight;
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
                TraceIdentifier = nameof(UnitTestInsightAuthorization)
            });

            var request = new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;

            foreach (var (key, value) in segments)
            {
                request.AddParameter(new Parameter(key, value, ParameterScope.Url));
            }

            return request;
        }

        /// <summary>
        /// Runs the validation of <c>/api/1/insights</c> the way the CRUD base does, with the
        /// payload keys lower-cased as the form parser hands them over.
        /// </summary>
        /// <param name="existing">The persisted insight.</param>
        /// <param name="entries">The payload entries.</param>
        /// <returns>The validation result.</returns>
        private static IRestApiValidationResult Validate(Insight existing, params (string Key, object Value)[] entries)
        {
            var payload = new RestApiCrudFormData();

            foreach (var (key, value) in entries)
            {
                payload[key.ToLowerInvariant()] = value;
            }

            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Insights.Index();
            var validate = endpoint.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(validate);

            return Assert.IsAssignableFrom<IRestApiValidationResult>(validate!.Invoke(endpoint, [existing, payload, CreateRequest()]));
        }

        /// <summary>
        /// Reads the sidebar's list of insights.
        /// </summary>
        /// <returns>The insights the sidebar lists for the acting caller.</returns>
        private static IReadOnlyList<Insight> SidebarInsights()
        {
            var method = typeof(global::KleeneStar.Core.WebFragment.Insight.InsightSidebarFilterFragment)
                .GetMethod("GetListed", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return Assert.IsAssignableFrom<IReadOnlyList<Insight>>(method!.Invoke(null, []));
        }

        /// <summary>
        /// While nobody administered the creation resource every signed-in caller may create
        /// insights; an anonymous one may not.
        /// </summary>
        [Fact]
        public void CreationIsOpenToSignedInCallersWhileUnadministered()
        {
            Seed(nameof(CreationIsOpenToSignedInCallersWhileUnadministered));

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                Assert.True(ContentAuthorization.MayCreateInsight(null));
            }

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.False(ContentAuthorization.MayCreateInsight(null));
            }
        }

        /// <summary>
        /// One grant on the creation resource narrows creation to the groups it names - a caller
        /// outside them is refused, on the endpoint's rule and the add dialog's alike.
        /// </summary>
        [Fact]
        public void ACreationGrantNarrowsCreationToItsGroups()
        {
            var database = nameof(ACreationGrantNarrowsCreationToItsGroups);
            Seed(database);
            Grant(database, ContentAuthorization.InsightCreationScopeId, "insight_creator_policy");

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                Assert.False(ContentAuthorization.MayCreateInsight(null));
            }

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.insights.add", CreateRequest()));
            }
        }

        /// <summary>
        /// The creation resource is no insight: the readers of per-insight grants pass it by, so
        /// a creation grant hides no insight from anybody.
        /// </summary>
        [Fact]
        public void TheCreationResourceIsNoInsight()
        {
            var database = nameof(TheCreationResourceIsNoInsight);
            Seed(database);
            Grant(database, ContentAuthorization.InsightCreationScopeId, "insight_creator_policy");

            Assert.Empty(CoreHub.PermissionManager.GetRefusedIds(PermissionScope.Insight, MemberId, typeof(InsightReadPermission)));

            var insight = AddInsight("Visible", InsightState.Active);

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                Assert.Contains(SidebarInsights(), x => x.Id == insight.Id);
            }
        }

        /// <summary>
        /// Who may create insights is the installation's business: administering it fails closed
        /// on the administrators group.
        /// </summary>
        [Fact]
        public void AdministeringCreationIsForAdministrators()
        {
            Seed(nameof(AdministeringCreationIsForAdministrators));

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                Assert.False(ContentAuthorization.MayAdministerInsightCreation(null));
            }

            using (CoreHub.SessionManager.BeginIdentity(AdministratorId))
            {
                Assert.True(ContentAuthorization.MayAdministerInsightCreation(null));
            }

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.False(RouteAuthorization.IsGranted("kleenestar.core.www.insights.permission", CreateRequest()));
            }
        }

        /// <summary>
        /// Keeping the state asks nothing; leaving or entering use asks archive or restore, open
        /// on an unadministered insight and refused once the insight's grants leave the caller out.
        /// </summary>
        [Fact]
        public void AStateChangeAsksArchiveOrRestore()
        {
            var database = nameof(AStateChangeAsksArchiveOrRestore);
            Seed(database);

            var insight = AddInsight("Stateful", InsightState.Active);

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                Assert.True(ContentAuthorization.MayChangeInsightState(insight.Id, InsightState.Active, InsightState.Active, null));
                Assert.True(ContentAuthorization.MayChangeInsightState(insight.Id, InsightState.Active, InsightState.Deleted, null));
                Assert.True(ContentAuthorization.MayChangeInsightState(insight.Id, InsightState.Deleted, InsightState.Active, null));
            }

            Grant(database, insight.Id.ToString(), "insight_view_policy");

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                Assert.True(ContentAuthorization.MayChangeInsightState(insight.Id, InsightState.Active, InsightState.Active, null));
                Assert.False(ContentAuthorization.MayChangeInsightState(insight.Id, InsightState.Active, InsightState.Deleted, null));
                Assert.False(ContentAuthorization.MayChangeInsightState(insight.Id, InsightState.Deleted, InsightState.Active, null));
            }
        }

        /// <summary>
        /// The endpoint refuses a state change the caller may not make on the state field - read
        /// from the id the selection posts as well as from the name - and leaves an unchanged
        /// state alone.
        /// </summary>
        [Fact]
        public void TheEndpointRefusesAStateChangeOnTheField()
        {
            Seed(nameof(TheEndpointRefusesAStateChangeOnTheField));

            var insight = AddInsight("Guarded", InsightState.Active);

            // the request signs nobody in, so every change of state is refused
            var byId = Validate(insight, ("State", InsightState.Deleted.Id().ToString()));
            var byName = Validate(insight, ("State", "Deleted"));
            var unchanged = Validate(insight, ("State", InsightState.Active.Id().ToString()));

            Assert.False(byId.IsValid);
            Assert.False(byName.IsValid);
            Assert.True(unchanged.IsValid);
        }

        /// <summary>
        /// An insight set to deleted is out of use: its tab scope answers as if it were missing,
        /// nobody may read or arrange it, and the sidebar does not list it.
        /// </summary>
        [Fact]
        public void ADeletedInsightIsOutOfUse()
        {
            Seed(nameof(ADeletedInsightIsOutOfUse));

            var active = AddInsight("In use", InsightState.Active);
            var deleted = AddInsight("Set aside", InsightState.Deleted);

            Assert.True(InsightScope.IsInUse(active));
            Assert.False(InsightScope.IsInUse(deleted));
            Assert.Equal(active.Id, InsightScope.Resolve(CreateRequest((InsightIdParameter.Key, active.Id.ToString())))?.Id);
            Assert.Null(InsightScope.Resolve(CreateRequest((InsightIdParameter.Key, deleted.Id.ToString()))));

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                var listed = SidebarInsights();

                Assert.Contains(listed, x => x.Id == active.Id);
                Assert.DoesNotContain(listed, x => x.Id == deleted.Id);
            }
        }

        /// <summary>
        /// The sidebar lists only what the caller may read: an insight whose grants leave the
        /// caller out is not named there.
        /// </summary>
        [Fact]
        public void TheSidebarListsOnlyReadableInsights()
        {
            var database = nameof(TheSidebarListsOnlyReadableInsights);
            Seed(database);

            var open = AddInsight("Open", InsightState.Active);
            var closed = AddInsight("Closed", InsightState.Active);
            Grant(database, closed.Id.ToString(), "insight_view_policy");

            using (CoreHub.SessionManager.BeginIdentity(MemberId))
            {
                var listed = SidebarInsights();

                Assert.Contains(listed, x => x.Id == open.Id);
                Assert.DoesNotContain(listed, x => x.Id == closed.Id);
            }

            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                Assert.Empty(SidebarInsights());
            }
        }
    }
}
