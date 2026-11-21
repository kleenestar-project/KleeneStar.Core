using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WWW.Api.Classes
{
    /// <summary>
    /// Regression tests for the workspace-scoped class selection endpoint used
    /// by template forms.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkspaceClassSelection
    {
        /// <summary>
        /// Verifies that /api/1/classes/{workspacekey} is a real GET selection
        /// endpoint rather than a route-only marker.
        /// </summary>
        [Fact]
        public void Index_IsSelectionEndpoint()
        {
            Assert.True(typeof(RestApiSelection<Model.Entities.Class>).IsAssignableFrom(
                typeof(global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Index)));
        }

        /// <summary>
        /// Verifies that the endpoint returns only classes belonging to the
        /// workspace identified by the route key.
        /// </summary>
        [Fact]
        public void Index_FiltersClassesByWorkspaceKey()
        {
            const string database = nameof(Index_FiltersClassesByWorkspaceKey);
            CoreHubFixture.Initialize(database);

            var dev = new Workspace { Key = "DEV", Name = "Development" };
            var ops = new Workspace { Key = "OPS", Name = "Operations" };
            var devClass = new Model.Entities.Class { Name = "Issue", WorkspaceId = dev.Id, Workspace = dev };
            var opsClass = new Model.Entities.Class { Name = "Incident", WorkspaceId = ops.Id, Workspace = ops };

            using var db = CoreHubFixture.CreateDbContext(database);
            db.Workspaces.AddRange(dev, ops);
            db.Classes.AddRange(devClass, opsClass);
            db.SaveChanges();

            var request = CreateRequest("DEV");
            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Index();
            var retrieveItems = endpoint.GetType().GetMethod(
                "RetrieveItems",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(retrieveItems);

            var result = Assert.IsAssignableFrom<IEnumerable<RestApiSelectionItem>>(
                retrieveItems!.Invoke(endpoint, [new Query<Model.Entities.Class>(), db, request]));
            var items = result.ToList();

            Assert.Contains(items, x => x.Id == devClass.Id && x.Text == devClass.Name);
            Assert.DoesNotContain(items, x => x.Id == opsClass.Id);
        }

        /// <summary>
        /// Creates a mock request for the /api/1/classes/{workspacekey} endpoint.
        /// </summary>
        /// <param name="workspaceKey">
        /// The workspace identifier used to simulate a request context for unit testing.
        /// This allows the routing and parameter resolution logic to be exercised without
        /// requiring a real HTTP pipeline.
        /// </param>
        /// <returns>
        /// A fully constructed IRequest instance containing all relevant HTTP features
        /// needed to test the workspace class selection logic in isolation.
        /// </returns>
        private static IRequest CreateRequest(string workspaceKey)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/classes/DEV",
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
                TraceIdentifier = nameof(UnitTestWorkspaceClassSelection)
            });

            var context = new WebExpress.WebCore.WebMessage.HttpContext(features, null!);
            var request = context.Request;
            request.AddParameter(new WebExpress.WebCore.WebParameter.Parameter(
                WorkspaceKeyParameter.Key,
                workspaceKey,
                WebExpress.WebCore.WebParameter.ParameterScope.Url));

            return request;
        }
    }
}
