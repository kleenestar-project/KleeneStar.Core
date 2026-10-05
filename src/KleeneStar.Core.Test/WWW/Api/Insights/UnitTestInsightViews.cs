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
    /// Tests the tabs of an insight: the open catalog of tab types and its mapping onto the tab
    /// templates, and the gate of <c>/api/1/insights</c> on the query that selects the objects.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestInsightViews
    {
        /// <summary>
        /// A tab type a plugin might register.
        /// </summary>
        private sealed class TimelineViewType : IInsightViewType
        {
            public string Key => "Timeline";
            public string Label => "timeline";
            public string Description => "timeline";
            public IIcon Icon => null;
            public int Order => 99;
            public Type Template => typeof(UnitTestInsightViews);
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
                TraceIdentifier = nameof(UnitTestInsightViews)
            });

            return new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;
        }

        /// <summary>
        /// The core ships the seven tab types under the keys the seeder and the migration name,
        /// and the default - the first tab of a new insight - cannot be unregistered.
        /// </summary>
        [Fact]
        public void Catalog_ShipsTheCoreTypes()
        {
            Assert.Equal
            (
                [
                    InsightViewTypes.Objects, InsightViewTypes.Dashboard, InsightViewTypes.Kanban,
                    InsightViewTypes.Scrum, InsightViewTypes.Gantt, InsightViewTypes.Calendar,
                    InsightViewTypes.Reports
                ],
                InsightViewTypeCatalog.Types.Select(x => InsightViewTypeCatalog.Normalize(x.Key)).Where(x => x != "timeline")
            );

            Assert.Equal(InsightViewTypes.Objects, InsightViewTypeCatalog.Default);
            Assert.False(InsightViewTypeCatalog.Unregister(InsightViewTypes.Objects));
            Assert.True(InsightViewTypeCatalog.IsRegistered(" Reports "));
        }

        /// <summary>
        /// Every core type names a template of its own, and the id the client reports for a
        /// template leads back to its type - the tab endpoint depends on both directions.
        /// </summary>
        [Fact]
        public void Catalog_MapsTemplatesBothWays()
        {
            var types = InsightViewTypeCatalog.Types.ToList();

            Assert.Equal(types.Count, types.Select(x => x.Template).Distinct().Count());

            foreach (var type in types)
            {
                var templateId = InsightViewTypeCatalog.TemplateId(type);

                Assert.False(string.IsNullOrWhiteSpace(templateId));
                Assert.Same(type, InsightViewTypeCatalog.FromTemplateId(templateId.ToUpperInvariant()));
            }

            Assert.Null(InsightViewTypeCatalog.FromTemplateId("no-such-template"));
        }

        /// <summary>
        /// A type a plugin registers is offered behind the core's and found ignoring case;
        /// unregistering takes it away.
        /// </summary>
        [Fact]
        public void Catalog_RegistersAndUnregistersAType()
        {
            var version = InsightViewTypeCatalog.Version;

            InsightViewTypeCatalog.Register(new TimelineViewType());

            try
            {
                Assert.NotEqual(version, InsightViewTypeCatalog.Version);
                Assert.True(InsightViewTypeCatalog.IsRegistered("timeline"));
                Assert.Equal("Timeline", InsightViewTypeCatalog.Types.Last().Key);
            }
            finally
            {
                Assert.True(InsightViewTypeCatalog.Unregister("TIMELINE"));
            }

            Assert.False(InsightViewTypeCatalog.IsRegistered("timeline"));
        }

        /// <summary>
        /// A type without a template could never be drawn, so the catalog refuses it.
        /// </summary>
        [Fact]
        public void Catalog_RefusesATypeWithoutTemplate()
        {
            Assert.Throws<ArgumentException>(() => InsightViewTypeCatalog.Register(new TemplatelessViewType()));
            Assert.False(InsightViewTypeCatalog.IsRegistered("broken"));
        }

        /// <summary>
        /// A tab type that names no template.
        /// </summary>
        private sealed class TemplatelessViewType : IInsightViewType
        {
            public string Key => "broken";
            public string Label => "broken";
            public string Description => "broken";
            public IIcon Icon => null;
            public int Order => 0;
            public Type Template => null;
        }

        /// <summary>
        /// A blank query and one that compiles are accepted.
        /// </summary>
        [Fact]
        public void Query_BlankOrValid_IsAccepted()
        {
            CoreHubFixture.Initialize(nameof(Query_BlankOrValid_IsAccepted));

            Assert.True(Validate(null, ("Name", "Ops")).IsValid);
            Assert.True(Validate(null, ("Name", "Ops"), ("Query", "")).IsValid);
            Assert.True(Validate(null, ("Name", "Ops"), ("Query", "Summary ~ \"incident\"")).IsValid);
            Assert.True(Validate(null, ("Name", "Ops"), ("Query", "Workspace.Key = \"SD\"")).IsValid);
        }

        /// <summary>
        /// A query that does not compile is refused on the query field, on a create and on an
        /// update alike - stored, it would leave every tab of the insight empty.
        /// </summary>
        [Fact]
        public void Query_Invalid_IsRefused()
        {
            CoreHubFixture.Initialize(nameof(Query_Invalid_IsRefused));

            var create = Validate(null, ("Name", "Ops"), ("Query", "Summary ~~ ("));
            var update = Validate(new Insight { Name = "Ops" }, ("Query", "NoSuchAttribute = 1"));

            Assert.False(create.IsValid);
            Assert.False(update.IsValid);
        }

        /// <summary>
        /// A blank query selects every object, a broken one selects none - an insight must not
        /// widen to everything when its filter stops compiling.
        /// </summary>
        [Fact]
        public void Scope_BlankSelectsAll_BrokenSelectsNone()
        {
            var visible = new KleeneStar.Model.Entities.Object { Summary = "Printer down", Key = "SD-1" };

            Assert.Null(InsightScope.Predicate(new Insight { Query = " " }));
            Assert.False(InsightScope.Predicate(new Insight { Query = "Summary ~~ (" })!.Compile()(visible));
            Assert.True(InsightScope.Predicate(new Insight { Query = "Summary ~ \"printer\"" })!.Compile()(visible));
        }
    }
}
