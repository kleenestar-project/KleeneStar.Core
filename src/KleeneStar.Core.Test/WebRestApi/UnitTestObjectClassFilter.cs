using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests <see cref="ObjectClassFilter"/>, which narrows a kind overview to the class picked in
    /// the asset sidebar's class tree and to every class inheriting from it.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectClassFilter
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C1A55000-0000-4000-8000-000000000001");
        private static readonly Guid HardwareId = Guid.Parse("C1A55000-0000-4000-8000-000000000002");
        private static readonly Guid ServerId = Guid.Parse("C1A55000-0000-4000-8000-000000000003");
        private static readonly Guid BladeId = Guid.Parse("C1A55000-0000-4000-8000-000000000004");
        private static readonly Guid SoftwareId = Guid.Parse("C1A55000-0000-4000-8000-000000000005");

        /// <summary>
        /// Builds a class inheriting from another one.
        /// </summary>
        private static Model.Entities.Class Derived(Guid id, Guid? baseId) => new()
        {
            Id = id,
            Name = id.ToString(),
            InheritedId = baseId,
            WorkspaceId = WorkspaceId,
            Kind = ObjectKind.Asset
        };

        /// <summary>
        /// Verifies that the lineage holds the class and its descendants at every depth, but
        /// neither its base class nor a sibling.
        /// </summary>
        [Fact]
        public void LineageFollowsInheritanceDownwards()
        {
            var classes = new[]
            {
                Derived(HardwareId, null),
                Derived(ServerId, HardwareId),
                Derived(BladeId, ServerId),
                Derived(SoftwareId, null)
            };

            Assert.True(ObjectClassFilter.Lineage(HardwareId, classes).SetEquals([HardwareId, ServerId, BladeId]));
            Assert.True(ObjectClassFilter.Lineage(ServerId, classes).SetEquals([ServerId, BladeId]));
            Assert.True(ObjectClassFilter.Lineage(SoftwareId, classes).SetEquals([SoftwareId]));
        }

        /// <summary>
        /// Verifies that an inheritance cycle, which older data may carry, ends the walk instead
        /// of looping forever.
        /// </summary>
        [Fact]
        public void LineageSurvivesACycle()
        {
            var classes = new[]
            {
                Derived(HardwareId, BladeId),
                Derived(ServerId, HardwareId),
                Derived(BladeId, ServerId)
            };

            Assert.Equal(3, ObjectClassFilter.Lineage(ServerId, classes).Count);
        }

        /// <summary>
        /// Verifies that the carried address names the class of the page and is left alone when
        /// the page names none.
        /// </summary>
        [Fact]
        public void CarryAppendsTheClassOfThePage()
        {
            Assert.Equal($"/api/1/assets/IT/table?class={ServerId}", ObjectClassFilter.Carry("/api/1/assets/IT/table", CreateRequest(ServerId.ToString())));
            Assert.Equal($"/api/1/assets/IT/table?v=a&class={ServerId}", ObjectClassFilter.Carry("/api/1/assets/IT/table?v=a", CreateRequest(ServerId.ToString())));
            Assert.Equal("/api/1/assets/IT/table", ObjectClassFilter.Carry("/api/1/assets/IT/table", CreateRequest(null)));
            Assert.Equal("/api/1/assets/IT/table", ObjectClassFilter.Carry("/api/1/assets/IT/table", CreateRequest("not-a-guid")));
        }

        /// <summary>
        /// Verifies that a query is narrowed to the objects of the named class and its
        /// descendants, left alone without a class, and emptied for a class that does not exist.
        /// </summary>
        [Fact]
        public void ApplyNarrowsToTheLineageOfTheNamedClass()
        {
            const string database = nameof(ApplyNarrowsToTheLineageOfTheNamedClass);
            CoreHubFixture.Initialize(database);

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                var now = DateTime.UtcNow;

                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "CLS", Name = "classes" });
                db.Classes.Add(Derived(HardwareId, null));
                db.Classes.Add(Derived(ServerId, HardwareId));
                db.Classes.Add(Derived(BladeId, ServerId));
                db.Classes.Add(Derived(SoftwareId, null));
                db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "CLS-1", Summary = "rack server", Kind = ObjectKind.Asset, WorkspaceId = WorkspaceId, ClassId = ServerId, Created = now, Updated = now });
                db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "CLS-2", Summary = "blade", Kind = ObjectKind.Asset, WorkspaceId = WorkspaceId, ClassId = BladeId, Created = now, Updated = now });
                db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "CLS-3", Summary = "office suite", Kind = ObjectKind.Asset, WorkspaceId = WorkspaceId, ClassId = SoftwareId, Created = now, Updated = now });
                db.SaveChanges();
            }

            string[] Keys(string classParameter) => [.. CoreHub.ObjectManager
                .GetObjects(ObjectClassFilter.Apply(new Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, WorkspaceId), CreateRequest(classParameter)))
                .Select(x => x.Key)
                .OrderBy(x => x)];

            // the abstract base gathers the objects of its descendants
            Assert.Equal(["CLS-1", "CLS-2"], Keys(HardwareId.ToString()));
            Assert.Equal(["CLS-2"], Keys(BladeId.ToString()));
            Assert.Equal(["CLS-3"], Keys(SoftwareId.ToString()));
            Assert.Equal(["CLS-1", "CLS-2", "CLS-3"], Keys(null));
            Assert.Empty(Keys(Guid.NewGuid().ToString()));
        }

        /// <summary>
        /// Builds a request to the asset overview carrying the class parameter.
        /// </summary>
        /// <param name="classParameter">The value of the class parameter, or null for none.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string classParameter)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/assets/CLS",
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
                TraceIdentifier = nameof(UnitTestObjectClassFilter)
            });

            var request = new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;

            request.AddParameter(new WebExpress.WebCore.WebParameter.Parameter(
                WorkspaceKeyParameter.Key,
                "CLS",
                WebExpress.WebCore.WebParameter.ParameterScope.Url));

            if (classParameter is not null)
            {
                request.AddParameter(new WebExpress.WebCore.WebParameter.Parameter(
                    ObjectClassFilter.Parameter,
                    classParameter,
                    WebExpress.WebCore.WebParameter.ParameterScope.Parameter));
            }

            return request;
        }
    }
}
