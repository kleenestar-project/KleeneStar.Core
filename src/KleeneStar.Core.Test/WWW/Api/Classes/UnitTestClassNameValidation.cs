using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.Test.WWW.Api.Classes
{
    /// <summary>
    /// Tests the gate on the class name: <c>/api/1/classes</c> refuses a class without a name,
    /// a class without a workspace, and a name another class of the same workspace already
    /// carries - and only of the same workspace, because two workspaces may both have an
    /// <c>Invoice</c>. The availability endpoint the dialogs ask while the name is typed is
    /// advice; this is the check every caller passes, including the ones that never open a
    /// dialog.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestClassNameValidation
    {
        private static readonly Guid FinanceId = Guid.Parse("A1A1A1A1-0000-4000-8000-000000000001");
        private static readonly Guid ProcurementId = Guid.Parse("A1A1A1A1-0000-4000-8000-000000000002");
        private static readonly Guid InvoiceId = Guid.Parse("B2B2B2B2-0000-4000-8000-000000000001");
        private static readonly Guid ContractId = Guid.Parse("B2B2B2B2-0000-4000-8000-000000000002");
        private static readonly Guid OrderId = Guid.Parse("B2B2B2B2-0000-4000-8000-000000000003");

        /// <summary>
        /// Seeds two workspaces that share a class name, the seeded shape the rule has to
        /// allow: <c>Invoice</c> in finance and in procurement, plus one class each of its own.
        /// </summary>
        /// <param name="database">The name of the isolated in-memory database.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Workspaces.AddRange(
                new Workspace { Id = FinanceId, Key = "FIN", Name = "Finance" },
                new Workspace { Id = ProcurementId, Key = "PROC", Name = "Procurement" });
            db.Classes.AddRange(
                new Model.Entities.Class { Id = InvoiceId, Name = "Invoice", WorkspaceId = FinanceId },
                new Model.Entities.Class { Id = ContractId, Name = "Contract", WorkspaceId = FinanceId },
                new Model.Entities.Class { Id = OrderId, Name = "Invoice", WorkspaceId = ProcurementId });
            db.SaveChanges();
        }

        /// <summary>
        /// Runs the endpoint's validation the way the CRUD base does before a create, a clone
        /// or an update: with the persisted class where there is one and the payload as the
        /// form parser hands it over, keys lower-cased.
        /// </summary>
        /// <param name="existing">The persisted class, or null for a create.</param>
        /// <param name="request">The request.</param>
        /// <param name="entries">The payload entries.</param>
        /// <returns>The validation result.</returns>
        private static IRestApiValidationResult Validate(Model.Entities.Class existing, IRequest request, params (string Key, object Value)[] entries)
        {
            var payload = new RestApiCrudFormData();

            foreach (var (key, value) in entries)
            {
                payload[key.ToLowerInvariant()] = value;
            }

            var endpoint = new global::KleeneStar.Core.WWW.Api._1_.Classes.Index();
            var validate = endpoint.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(validate);

            return Assert.IsAssignableFrom<IRestApiValidationResult>(validate!.Invoke(endpoint, [existing, payload, request]));
        }

        /// <summary>
        /// Reads a persisted class back, the way the CRUD base hands it to the validation.
        /// </summary>
        /// <param name="database">The name of the isolated in-memory database.</param>
        /// <param name="id">The class.</param>
        /// <returns>The class.</returns>
        private static Model.Entities.Class Load(string database, Guid id)
        {
            using var db = CoreHubFixture.CreateDbContext(database);

            return db.Classes.Single(x => x.Id == id);
        }

        /// <summary>
        /// Builds a request of the given method, optionally addressing a class through the id
        /// query parameter - which is how a clone differs from a create on the wire.
        /// </summary>
        /// <param name="method">The http method.</param>
        /// <param name="id">The addressed class, or null.</param>
        /// <returns>The request.</returns>
        private static IRequest CreateRequest(string method, Guid? id = null)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = method,
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/classes",
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
                TraceIdentifier = nameof(UnitTestClassNameValidation)
            });

            var context = new WebExpress.WebCore.WebMessage.HttpContext(features, null!);
            var request = context.Request;

            if (id.HasValue)
            {
                request.AddParameter(new Parameter(ParameterId.Key, id.Value.ToString(), ParameterScope.Parameter));
            }

            return request;
        }

        /// <summary>
        /// A create that names no workspace is refused for that reason - and for that reason
        /// alone, because a name checked against no workspace would only add a misleading
        /// second complaint.
        /// </summary>
        [Fact]
        public void Create_WithoutWorkspace_IsRefused()
        {
            Seed(nameof(Create_WithoutWorkspace_IsRefused));

            var result = Validate(null, CreateRequest("POST"), ("Name", "Purchase"), ("Kind", "issue"));

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal(nameof(Model.Entities.Class.WorkspaceId), result.Errors.Single().Field);
        }

        /// <summary>
        /// A create that leaves the name out is refused whether or not the payload mentions
        /// it: the entity's own required-check only fires for a field the payload carries.
        /// </summary>
        [Fact]
        public void Create_WithoutName_IsRefused()
        {
            Seed(nameof(Create_WithoutName_IsRefused));

            var result = Validate(null, CreateRequest("POST"), ("WorkspaceId", FinanceId.ToString()), ("Kind", "issue"));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, x => x.Field == nameof(Model.Entities.Class.Name));
        }

        /// <summary>
        /// A name another class of the same workspace carries is taken, whatever its case and
        /// whatever surrounds it: nothing that resolves a class by name tells the spellings
        /// apart.
        /// </summary>
        [Theory]
        [InlineData("Invoice")]
        [InlineData("invoice")]
        [InlineData("  INVOICE ")]
        public void Create_WithTakenName_IsRefused(string name)
        {
            Seed(nameof(Create_WithTakenName_IsRefused) + name.Trim().ToLowerInvariant());

            var result = Validate(null, CreateRequest("POST"), ("WorkspaceId", FinanceId.ToString()), ("Name", name));

            Assert.False(result.IsValid);
            Assert.Single(result.Errors);
            Assert.Equal(nameof(Model.Entities.Class.Name), result.Errors.Single().Field);
        }

        /// <summary>
        /// The same name in another workspace is free: the rule is per workspace, and the seeded
        /// data relies on it.
        /// </summary>
        [Fact]
        public void Create_WithNameTakenElsewhere_IsAccepted()
        {
            Seed(nameof(Create_WithNameTakenElsewhere_IsAccepted));

            var result = Validate(null, CreateRequest("POST"), ("WorkspaceId", ProcurementId.ToString()), ("Name", "Contract"));

            Assert.True(result.IsValid);
        }

        /// <summary>
        /// An update that sends the class's own name back is not a collision: the class being
        /// edited never counts against itself.
        /// </summary>
        [Fact]
        public void Update_KeepingOwnName_IsAccepted()
        {
            const string database = nameof(Update_KeepingOwnName_IsAccepted);
            Seed(database);

            var result = Validate(Load(database, InvoiceId), CreateRequest("PUT"), ("Name", "Invoice"), ("Description", "Bills"));

            Assert.True(result.IsValid);
        }

        /// <summary>
        /// An update that renames the class onto a sibling's name is refused, and one that
        /// leaves the name out is not asked about it - unmentioned means unchanged.
        /// </summary>
        [Fact]
        public void Update_RenamingOntoSibling_IsRefused()
        {
            const string database = nameof(Update_RenamingOntoSibling_IsRefused);
            Seed(database);

            var renamed = Validate(Load(database, InvoiceId), CreateRequest("PUT"), ("Name", "Contract"));
            var untouched = Validate(Load(database, InvoiceId), CreateRequest("PUT"), ("Description", "Bills"));

            Assert.False(renamed.IsValid);
            Assert.Equal(nameof(Model.Entities.Class.Name), renamed.Errors.Single().Field);
            Assert.True(untouched.IsValid);
        }

        /// <summary>
        /// A clone is a new class in the workspace of its original: it competes with every
        /// class there, its original included, and needs no workspace in its payload.
        /// </summary>
        [Fact]
        public void Clone_TakesTheWorkspaceOfTheOriginal_AndCompetesWithIt()
        {
            const string database = nameof(Clone_TakesTheWorkspaceOfTheOriginal_AndCompetesWithIt);
            Seed(database);

            var original = Load(database, InvoiceId);
            var sameName = Validate(original, CreateRequest("POST", InvoiceId), ("Name", "Invoice"));
            var freeName = Validate(original, CreateRequest("POST", InvoiceId), ("Name", "Credit note"));

            Assert.False(sameName.IsValid);
            Assert.Equal(nameof(Model.Entities.Class.Name), sameName.Errors.Single().Field);
            Assert.True(freeName.IsValid);
        }

        /// <summary>
        /// The availability check the dialogs ask answers per workspace and leaves the class
        /// being edited out of the count.
        /// </summary>
        [Fact]
        public void IsAvailable_AnswersPerWorkspace_AndExcludesTheEditedClass()
        {
            Seed(nameof(IsAvailable_AnswersPerWorkspace_AndExcludesTheEditedClass));

            var unique = typeof(global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName);

            Assert.False(global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName.IsAvailable(FinanceId, "invoice", Guid.Empty));
            Assert.True(global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName.IsAvailable(FinanceId, "Invoice", InvoiceId));
            Assert.True(global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName.IsAvailable(ProcurementId, "Contract", Guid.Empty));
            Assert.False(global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName.IsAvailable(FinanceId, "   ", Guid.Empty));
            Assert.Equal("exclude", global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName.ExcludeParameter);
            Assert.True(typeof(RestApiUnique).IsAssignableFrom(unique));
        }
    }
}
