using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Pins the conditions that decide whether an overview page shows its view or its empty-state
    /// placeholder. The pair per page must be exact complements: if both agreed the page would
    /// render the view and the placeholder at once, if neither did it would render nothing.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestEmptyStateCondition
    {
        /// <summary>
        /// Creates the condition of the given name. The conditions are internal to the plugin, so
        /// they are reached through the public interface they implement.
        /// </summary>
        /// <param name="name">The fully qualified type name of the condition.</param>
        /// <returns>The condition.</returns>
        private static ICondition Condition(string name)
        {
            var type = typeof(CoreHub).Assembly.GetType(name);

            Assert.NotNull(type);

            return (ICondition)Activator.CreateInstance(type!, nonPublic: true)!;
        }

        private static ICondition WorkspaceEmpty()
            => Condition("KleeneStar.Core.WebFragment.Workspace.WorkspaceEmptyStateCondition");

        private static ICondition WorkspaceNotEmpty()
            => Condition("KleeneStar.Core.WebFragment.Workspace.WorkspaceNotEmptyStateCondition");

        private static ICondition ClassEmpty()
            => Condition("KleeneStar.Core.WebFragment.Class.ClassEmptyStateCondition");

        private static ICondition ClassNotEmpty()
            => Condition("KleeneStar.Core.WebFragment.Class.ClassNotEmptyStateCondition");

        /// <summary>
        /// Creates a request that addresses the given workspace through the route.
        /// </summary>
        /// <param name="workspaceKey">The workspace key the route carries.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string workspaceKey)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = $"/kleenestar/classes/{workspaceKey}",
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
                TraceIdentifier = nameof(UnitTestEmptyStateCondition)
            });

            var context = new WebExpress.WebCore.WebMessage.HttpContext(features, null!);
            var request = context.Request;

            request.AddParameter(new WebExpress.WebCore.WebParameter.Parameter(
                WorkspaceKeyParameter.Key,
                workspaceKey,
                WebExpress.WebCore.WebParameter.ParameterScope.Url));

            return request;
        }

        /// <summary>
        /// Verifies that an installation without a workspace reports the empty state, so the
        /// overview points the user at creating one instead of showing a bare view.
        /// </summary>
        [Fact]
        public void WorkspaceCondition_ReportsEmpty_WhenNoWorkspaceExists()
        {
            CoreHubFixture.Initialize(nameof(WorkspaceCondition_ReportsEmpty_WhenNoWorkspaceExists));

            Assert.True(WorkspaceEmpty().Fulfillment(null!));
            Assert.False(WorkspaceNotEmpty().Fulfillment(null!));
        }

        /// <summary>
        /// Verifies that a single workspace flips the pair over to the view.
        /// </summary>
        [Fact]
        public void WorkspaceCondition_ReportsNotEmpty_WhenAWorkspaceExists()
        {
            const string database = nameof(WorkspaceCondition_ReportsNotEmpty_WhenAWorkspaceExists);
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);
            db.Workspaces.Add(new Workspace { Key = "DEV", Name = "Development" });
            db.SaveChanges();

            Assert.False(WorkspaceEmpty().Fulfillment(null!));
            Assert.True(WorkspaceNotEmpty().Fulfillment(null!));
        }

        /// <summary>
        /// Verifies that the class overview reports the empty state for a workspace that has no
        /// class, even though other workspaces do. The question is asked per workspace, because
        /// the page lists the classes of the one the route addresses.
        /// </summary>
        [Fact]
        public void ClassCondition_ReportsEmpty_PerWorkspace()
        {
            const string database = nameof(ClassCondition_ReportsEmpty_PerWorkspace);
            CoreHubFixture.Initialize(database);

            var dev = new Workspace { Key = "DEV", Name = "Development" };
            var ops = new Workspace { Key = "OPS", Name = "Operations" };

            using var db = CoreHubFixture.CreateDbContext(database);
            db.Workspaces.AddRange(dev, ops);
            db.Classes.Add(new Model.Entities.Class { Name = "Issue", WorkspaceId = dev.Id, Workspace = dev });
            db.SaveChanges();

            var populated = CreateRequest("DEV");
            var bare = CreateRequest("OPS");

            Assert.False(ClassEmpty().Fulfillment(populated));
            Assert.True(ClassNotEmpty().Fulfillment(populated));

            Assert.True(ClassEmpty().Fulfillment(bare));
            Assert.False(ClassNotEmpty().Fulfillment(bare));
        }

        /// <summary>
        /// Verifies that a route addressing no existing workspace lands on the empty state rather
        /// than on a view that would have nothing to list.
        /// </summary>
        [Fact]
        public void ClassCondition_ReportsEmpty_ForAnUnknownWorkspace()
        {
            const string database = nameof(ClassCondition_ReportsEmpty_ForAnUnknownWorkspace);
            CoreHubFixture.Initialize(database);

            var dev = new Workspace { Key = "DEV", Name = "Development" };

            using var db = CoreHubFixture.CreateDbContext(database);
            db.Workspaces.Add(dev);
            db.Classes.Add(new Model.Entities.Class { Name = "Issue", WorkspaceId = dev.Id, Workspace = dev });
            db.SaveChanges();

            var unknown = CreateRequest("NOPE");

            Assert.True(ClassEmpty().Fulfillment(unknown));
            Assert.False(ClassNotEmpty().Fulfillment(unknown));
        }
    }
}
