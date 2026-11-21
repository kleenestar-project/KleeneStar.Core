using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests that the table of a kind overview (<see cref="RestApiObjectKindTable"/>) narrows its
    /// rows by the expression of the advanced search. It is a plain <c>IRestApi</c> rather than
    /// a framework table, and it used to read <c>q</c> only, so a WQL search on the issue or
    /// asset overview left the table as it was. The search is tested on its own, because the
    /// whole answer builds its column catalog through the sitemap, which the fixture lacks.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectKindTableWql
    {
        /// <summary>
        /// Verifies which rows the table answers for an expression, for an expression that
        /// does not parse (nothing is narrowed, as on the framework's tables) and for an
        /// expression beside a plain search (the expression takes its place).
        /// </summary>
        /// <param name="wql">The expression, or null for none.</param>
        /// <param name="search">The plain search, or null for none.</param>
        /// <param name="expected">The keys expected, separated by a bar.</param>
        [Theory]
        [InlineData(null, null, "WQL-1|WQL-2|WQL-3")]
        [InlineData("Summary ~ \"server\"", null, "WQL-1|WQL-3")]
        [InlineData("Summary ~ \"server\" and Key = \"WQL-3\"", null, "WQL-3")]
        [InlineData("Summary !~ \"server\"", null, "WQL-2")]
        [InlineData("Summary ~ \"serv*\"", null, "WQL-1|WQL-3")]
        [InlineData("Summary ~ (", null, "WQL-1|WQL-2|WQL-3")]
        [InlineData("Summary ~ \"printer\"", "server", "WQL-2")]
        [InlineData(null, "printer", "WQL-2")]
        public void TableIsNarrowedByTheExpression(string wql, string search, string expected)
        {
            CoreHubFixture.Initialize(nameof(TableIsNarrowedByTheExpression));

            var keys = RestApiObjectKindTable.Search(Objects, CreateRequest(wql, search)).Select(x => x.Key);

            Assert.Equal(expected.Split('|').Order(), keys.Order());
        }

        /// <summary>
        /// Builds a request to the table carrying the expression and the plain search.
        /// </summary>
        /// <param name="wql">The expression, or null for none.</param>
        /// <param name="search">The plain search, or null for none.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string wql, string search)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/api/1/issues/WQL/table",
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
                TraceIdentifier = nameof(UnitTestObjectKindTableWql)
            });

            var request = new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;

            if (wql is not null)
            {
                request.AddParameter(new Parameter("wql", wql, ParameterScope.Parameter));
            }

            if (search is not null)
            {
                request.AddParameter(new Parameter("q", search, ParameterScope.Parameter));
            }

            return request;
        }

        /// <summary>
        /// The objects the table lists.
        /// </summary>
        private static readonly IReadOnlyList<ObjectEntity> Objects =
        [
            Create("WQL-1", "Server down"),
            Create("WQL-2", "Printer jam"),
            Create("WQL-3", "Mail server slow")
        ];

        /// <summary>
        /// Creates an active issue.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="summary">The summary.</param>
        /// <returns>The issue.</returns>
        private static ObjectEntity Create(string key, string summary)
        {
            var now = DateTime.UtcNow;

            return new ObjectEntity(Guid.NewGuid())
            {
                Key = key,
                Summary = summary,
                Kind = ObjectKind.Issue,
                State = WorkspaceState.Active,
                Created = now,
                Updated = now
            };
        }
    }
}
