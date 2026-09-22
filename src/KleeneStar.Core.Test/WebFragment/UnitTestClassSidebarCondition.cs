using KleeneStar.Core.WebFragment.Class;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Pins which links the class administration sidebar offers per kind and renderer: SLA and
    /// calendar only where the kind is measured against service levels, fields and forms only
    /// where the class reads through a structured mask.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestClassSidebarCondition
    {
        private static readonly Guid WorkspaceId = Guid.Parse("7a0d4a52-0b7e-4f55-9d2b-4d4a2f1e0001");

        /// <summary>
        /// Creates a request whose route carries the given parameter.
        /// </summary>
        /// <param name="key">The route segment key.</param>
        /// <param name="value">The segment value.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string key, Guid value)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = $"/kleenestar/class/{value}",
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
                TraceIdentifier = nameof(UnitTestClassSidebarCondition)
            });

            var request = new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;

            request.AddParameter(new WebExpress.WebCore.WebParameter.Parameter(
                key,
                value.ToString(),
                WebExpress.WebCore.WebParameter.ParameterScope.Url));

            return request;
        }

        /// <summary>
        /// Seeds one class of the given kind and renderer into a fresh database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="kind">The kind of the class.</param>
        /// <param name="renderer">The renderer the class names, or null to follow its kind.</param>
        /// <returns>The id of the class.</returns>
        private static Guid SeedClass(string database, string kind, string renderer)
        {
            CoreHubFixture.Initialize(database);

            var workspace = new Workspace { Id = WorkspaceId, Key = "DEV", Name = "Development" };
            var @class = new Model.Entities.Class
            {
                Name = "Subject",
                Kind = kind,
                Renderer = renderer,
                WorkspaceId = workspace.Id,
                Workspace = workspace
            };

            using var db = CoreHubFixture.CreateDbContext(database);
            db.Workspaces.Add(workspace);
            db.Classes.Add(@class);
            db.SaveChanges();

            return @class.Id;
        }

        /// <summary>
        /// Verifies the SLA and calendar links per kind: offered on an issue class, withheld on
        /// the document, blog and asset classes, whose objects nothing waits on.
        /// </summary>
        [Theory]
        [InlineData(ObjectKind.Issue, true)]
        [InlineData(ObjectKind.Document, false)]
        [InlineData(ObjectKind.Blog, false)]
        [InlineData(ObjectKind.Asset, false)]
        public void ServiceLevelLinks_FollowTheKind(string kind, bool offered)
        {
            var classId = SeedClass($"{nameof(ServiceLevelLinks_FollowTheKind)}_{kind}", kind, null);

            Assert.Equal(offered, new ClassServiceLevelCondition().Fulfillment(CreateRequest(ClassIdParameter.Key, classId)));
        }

        /// <summary>
        /// Verifies the field and form links per effective renderer: withheld on a prose class -
        /// a blog always, a document by default - and offered again on a document class switched
        /// to the form renderer, as on the structured kinds.
        /// </summary>
        [Theory]
        [InlineData(ObjectKind.Issue, null, true)]
        [InlineData(ObjectKind.Asset, null, true)]
        [InlineData(ObjectKind.Document, null, false)]
        [InlineData(ObjectKind.Document, ObjectRenderer.Prose, false)]
        [InlineData(ObjectKind.Document, ObjectRenderer.Form, true)]
        [InlineData(ObjectKind.Blog, null, false)]
        [InlineData(ObjectKind.Blog, ObjectRenderer.Form, false)]
        public void StructureLinks_FollowTheEffectiveRenderer(string kind, string renderer, bool offered)
        {
            var classId = SeedClass($"{nameof(StructureLinks_FollowTheEffectiveRenderer)}_{kind}_{renderer}", kind, renderer);

            Assert.Equal(offered, new ClassStructuredRendererCondition().Fulfillment(CreateRequest(ClassIdParameter.Key, classId)));
        }

        /// <summary>
        /// Verifies that a page beneath the class - here a form of it - is answered for the class
        /// the form belongs to, so the sidebar does not change while moving through the class.
        /// </summary>
        [Fact]
        public void Conditions_ResolveTheClassThroughAForm()
        {
            const string database = nameof(Conditions_ResolveTheClassThroughAForm);
            var classId = SeedClass(database, ObjectKind.Document, null);
            var formId = Guid.NewGuid();

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.Forms.Add(new Form { Id = formId, Name = "Standard", FormType = FormType.Default, ClassId = classId });
                db.SaveChanges();
            }

            var request = CreateRequest(FormIdParameter.Key, formId);

            Assert.False(new ClassServiceLevelCondition().Fulfillment(request));
            Assert.False(new ClassStructuredRendererCondition().Fulfillment(request));
        }

        /// <summary>
        /// Verifies that a route whose class cannot be resolved keeps every link, so the
        /// question only ever narrows a sidebar it can answer for.
        /// </summary>
        [Fact]
        public void Conditions_KeepTheLinks_ForAnUnknownClass()
        {
            SeedClass(nameof(Conditions_KeepTheLinks_ForAnUnknownClass), ObjectKind.Blog, null);

            var request = CreateRequest(ClassIdParameter.Key, Guid.NewGuid());

            Assert.True(new ClassServiceLevelCondition().Fulfillment(request));
            Assert.True(new ClassStructuredRendererCondition().Fulfillment(request));
        }
    }
}
