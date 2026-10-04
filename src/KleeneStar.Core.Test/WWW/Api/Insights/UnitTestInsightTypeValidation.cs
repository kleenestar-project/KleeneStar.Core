using KleeneStar.Core.WebInsight;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.Test.WWW.Api.Insights
{
    /// <summary>
    /// Tests the insight type: the open catalog the core registers the dashboard in, and the
    /// gate of <c>/api/1/insights</c> - a new insight takes a registered type (the dashboard
    /// when it names none), and an existing one keeps the type it was created with.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestInsightTypeValidation
    {
        /// <summary>
        /// A type a plugin might register, for the tests that need a second one.
        /// </summary>
        private sealed class CalendarInsightType : IInsightType
        {
            public string Key => "Calendar";
            public string Label => "calendar";
            public string Description => "calendar";
            public IIcon Icon => null;
            public int Order => 1;
        }

        /// <summary>
        /// Runs the endpoint's validation the way the CRUD base does, with the payload keys
        /// lower-cased as the form parser hands them over.
        /// </summary>
        /// <param name="existing">The persisted insight, or null for a create.</param>
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
        /// Builds a request carrying a culture, which the messages are translated in.
        /// </summary>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest()
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "POST",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/insights",
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
                TraceIdentifier = nameof(UnitTestInsightTypeValidation)
            });

            return new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;
        }

        /// <summary>
        /// The core ships the dashboard, it is the default, and it cannot be unregistered.
        /// </summary>
        [Fact]
        public void Catalog_ShipsTheDashboard()
        {
            Assert.Equal(Insight.DashboardType, InsightTypeCatalog.Default);
            Assert.True(InsightTypeCatalog.IsRegistered("dashboard"));
            Assert.True(InsightTypeCatalog.IsRegistered(" Dashboard "));
            Assert.False(InsightTypeCatalog.Unregister("dashboard"));
            Assert.True(InsightTypeCatalog.IsRegistered("dashboard"));
        }

        /// <summary>
        /// A registered type is offered and found ignoring case; unregistering takes it away.
        /// </summary>
        [Fact]
        public void Catalog_RegistersAndUnregistersAType()
        {
            var version = InsightTypeCatalog.Version;

            InsightTypeCatalog.Register(new CalendarInsightType());

            try
            {
                Assert.NotEqual(version, InsightTypeCatalog.Version);
                Assert.True(InsightTypeCatalog.IsRegistered("calendar"));
                Assert.Equal(["dashboard", "Calendar"], InsightTypeCatalog.Types.Select(x => x.Key));
                Assert.True(InsightTypeCatalog.IsOfType(new Insight { Type = "CALENDAR" }, "calendar"));
            }
            finally
            {
                Assert.True(InsightTypeCatalog.Unregister("calendar"));
            }

            Assert.False(InsightTypeCatalog.IsRegistered("calendar"));
        }

        /// <summary>
        /// A create may name a registered type, or none at all.
        /// </summary>
        [Fact]
        public void Create_WithRegisteredOrNoType_IsAccepted()
        {
            CoreHubFixture.Initialize(nameof(Create_WithRegisteredOrNoType_IsAccepted));

            Assert.True(Validate(null, ("Name", "Ops"), ("Type", "dashboard")).IsValid);
            Assert.True(Validate(null, ("Name", "Ops")).IsValid);
            Assert.True(Validate(null, ("Name", "Ops"), ("Type", "")).IsValid);
        }

        /// <summary>
        /// A create naming a type nobody registered is refused on the type field.
        /// </summary>
        [Fact]
        public void Create_WithUnknownType_IsRefused()
        {
            CoreHubFixture.Initialize(nameof(Create_WithUnknownType_IsRefused));

            var result = Validate(null, ("Name", "Ops"), ("Type", "gantt"));

            Assert.False(result.IsValid);
            Assert.Equal(nameof(Insight.Type), Assert.Single(result.Errors).Field);
        }

        /// <summary>
        /// An update keeps the type: naming the same one is fine, naming another is refused.
        /// </summary>
        [Fact]
        public void Update_CannotChangeTheType()
        {
            CoreHubFixture.Initialize(nameof(Update_CannotChangeTheType));

            var existing = new Insight { Name = "Ops", Type = Insight.DashboardType };

            Assert.True(Validate(existing, ("Name", "Ops"), ("Type", "Dashboard")).IsValid);

            InsightTypeCatalog.Register(new CalendarInsightType());

            try
            {
                var result = Validate(existing, ("Type", "calendar"));

                Assert.False(result.IsValid);
                Assert.Equal(nameof(Insight.Type), Assert.Single(result.Errors).Field);
            }
            finally
            {
                InsightTypeCatalog.Unregister("calendar");
            }
        }
    }
}
