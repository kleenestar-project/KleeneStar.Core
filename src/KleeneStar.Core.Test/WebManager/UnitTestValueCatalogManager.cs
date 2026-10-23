using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System.Net;
using System.Text;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Tests the central value catalogs: versions only for real changes, restorable; bound
    /// fields follow the catalog unless pinned; query catalogs read their objects; external
    /// catalogs read another system and keep their values when it is down.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestValueCatalogManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("E0E0E000-0000-4000-8000-000000000001");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("E0E0E000-0000-4000-8000-000000000002");
        private static readonly Guid ClassId = Guid.Parse("E0E0E000-0000-4000-8000-000000000003");
        private static readonly Guid ServerClassId = Guid.Parse("E0E0E000-0000-4000-8000-000000000004");
        private static readonly Guid LocationFieldId = Guid.Parse("E0E0E000-0000-4000-8000-000000000005");
        private static readonly Guid PinnedFieldId = Guid.Parse("E0E0E000-0000-4000-8000-000000000006");
        private static readonly Guid TextFieldId = Guid.Parse("E0E0E000-0000-4000-8000-000000000007");
        private static readonly Guid ForeignFieldId = Guid.Parse("E0E0E000-0000-4000-8000-000000000008");

        /// <summary>
        /// Seeds two workspaces, a ticket class with selection and text fields, and a server class.
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

            var foreignClass = Guid.NewGuid();

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "SD", Name = "Service desk" });
            db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "HR", Name = "People" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.Classes.Add(new Class { Id = ServerClassId, Name = "Server", WorkspaceId = WorkspaceId });
            db.Classes.Add(new Class { Id = foreignClass, Name = "Case", WorkspaceId = OtherWorkspaceId });
            db.Fields.Add(new Field { Id = LocationFieldId, Name = "Location", ClassId = ClassId, FieldType = FieldType.Selection, State = FieldState.Active });
            db.Fields.Add(new Field { Id = PinnedFieldId, Name = "Old location", ClassId = ClassId, FieldType = FieldType.Selection, State = FieldState.Active });
            db.Fields.Add(new Field { Id = TextFieldId, Name = "Notes", ClassId = ClassId, FieldType = FieldType.Text, State = FieldState.Active });
            db.Fields.Add(new Field { Id = ForeignFieldId, Name = "Site", ClassId = foreignClass, FieldType = FieldType.Selection, State = FieldState.Active });
            db.SaveChanges();
        }

        /// <summary>
        /// Adds a catalog.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="configure">Changes it before it is stored.</param>
        /// <returns>The catalog.</returns>
        private static ValueCatalog Catalog(ValueCatalogSource source, Action<ValueCatalog> configure = null)
        {
            var catalog = new ValueCatalog { Name = "catalog " + Guid.NewGuid().ToString("N")[..6], WorkspaceId = WorkspaceId, Source = source, State = ValueCatalogState.Active };

            configure?.Invoke(catalog);
            CoreHub.ValueCatalogManager.SaveCatalog(catalog);

            return catalog;
        }

        /// <summary>
        /// Returns the options of a field.
        /// </summary>
        /// <param name="fieldId">The field.</param>
        /// <returns>The options.</returns>
        private static List<string> Options(Guid fieldId)
        {
            return CoreHub.FieldManager.GetField(fieldId).Options;
        }

        /// <summary>
        /// A version is written only for a real change, says what changed, and an earlier one
        /// comes back as a new version.
        /// </summary>
        [Fact]
        public void Publish_VersionsRealChangesOnly()
        {
            Seed(nameof(Publish_VersionsRealChangesOnly));

            var catalog = Catalog(ValueCatalogSource.Static);

            var first = CoreHub.ValueCatalogManager.Publish(catalog.Id, [new("Berlin"), new("Munich"), new(" Munich "), new("")], "typed", Guid.Empty);
            Assert.Equal(1, first.Number);
            Assert.Equal(["Berlin", "Munich"], CoreHub.ValueCatalogManager.GetValues(catalog.Id).Select(x => x.Value));

            Assert.Null(CoreHub.ValueCatalogManager.Publish(catalog.Id, [new("Berlin"), new("Munich")], "same", Guid.Empty));

            var second = CoreHub.ValueCatalogManager.Publish(catalog.Id, [new("Berlin"), new("Hamburg")], "typed", Guid.Empty);
            Assert.Equal((2, 1, 1), (second.Number, second.Added, second.Removed));

            var restored = CoreHub.ValueCatalogManager.Restore(catalog.Id, 1, Guid.Empty);
            Assert.Equal(3, restored.Number);
            Assert.Equal(["Berlin", "Munich"], CoreHub.ValueCatalogManager.GetValues(catalog.Id).Select(x => x.Value));
            Assert.Equal(["Berlin", "Hamburg"], CoreHub.ValueCatalogManager.GetValues(catalog.Id, 2).Select(x => x.Value));
            Assert.Equal([3, 2, 1], CoreHub.ValueCatalogManager.GetVersions(catalog.Id).Select(x => x.Number));
        }

        /// <summary>
        /// A bound field gets the catalog's values and follows it; a pinned one keeps its version;
        /// a text field and a field of another workspace cannot be bound.
        /// </summary>
        [Fact]
        public void Binding_PassesValuesToFields()
        {
            Seed(nameof(Binding_PassesValuesToFields));

            var catalog = Catalog(ValueCatalogSource.Static);

            CoreHub.ValueCatalogManager.Publish(catalog.Id, [new("Berlin"), new("Munich")], null, Guid.Empty);
            CoreHub.ValueCatalogManager.Bind(LocationFieldId, catalog.Id, null);
            CoreHub.ValueCatalogManager.Bind(PinnedFieldId, catalog.Id, 1);

            Assert.Equal(["Berlin", "Munich"], Options(LocationFieldId));

            CoreHub.ValueCatalogManager.Publish(catalog.Id, [new("Berlin"), new("Munich"), new("Vienna")], null, Guid.Empty);

            Assert.Equal(["Berlin", "Munich", "Vienna"], Options(LocationFieldId));
            Assert.Equal(["Berlin", "Munich"], Options(PinnedFieldId));
            Assert.Equal(2, CoreHub.ValueCatalogManager.GetBindings(catalog.Id).Count);

            Assert.Throws<ArgumentException>(() => CoreHub.ValueCatalogManager.Bind(TextFieldId, catalog.Id, null));
            Assert.Throws<ArgumentException>(() => CoreHub.ValueCatalogManager.Bind(ForeignFieldId, catalog.Id, null));
            Assert.Throws<ArgumentException>(() => CoreHub.ValueCatalogManager.Bind(LocationFieldId, catalog.Id, 9));

            // an installation-wide catalog serves every workspace
            var global = Catalog(ValueCatalogSource.Static, x => x.WorkspaceId = null);
            CoreHub.ValueCatalogManager.Publish(global.Id, [new("EU"), new("US")], null, Guid.Empty);
            CoreHub.ValueCatalogManager.Bind(ForeignFieldId, global.Id, null);
            Assert.Equal(["EU", "US"], Options(ForeignFieldId));
        }

        /// <summary>
        /// A query catalog lists the objects its WQL selects; a refresh that finds nothing new
        /// writes nothing.
        /// </summary>
        [Fact]
        public void QueryCatalog_ReadsItsObjects()
        {
            var database = nameof(QueryCatalog_ReadsItsObjects);
            Seed(database);

            void Server(string name)
            {
                using var db = CoreHubFixture.CreateDbContext(database);

                db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "SRV-" + name, Summary = name, WorkspaceId = WorkspaceId, ClassId = ServerClassId });
                db.SaveChanges();
            }

            Server("web01");
            Server("db01");

            var catalog = Catalog(ValueCatalogSource.Query, x =>
            {
                x.Query = "Summary ~ \"01\"";
                x.ValueAttribute = "summary";
            });

            Assert.Equal(1, CoreHub.ValueCatalogManager.Refresh(catalog.Id).Number);
            Assert.Equal(["db01", "web01"], CoreHub.ValueCatalogManager.GetValues(catalog.Id).Select(x => x.Value));
            Assert.Null(CoreHub.ValueCatalogManager.Refresh(catalog.Id));

            Server("app01");
            Assert.Equal(2, CoreHub.ValueCatalogManager.Refresh(catalog.Id).Number);
            Assert.NotNull(CoreHub.ValueCatalogManager.GetCatalog(catalog.Id).LastRefresh);
        }

        /// <summary>
        /// An external catalog reads another system; when that fails, the values stay and the
        /// reason is kept. The scheduler refreshes what is due.
        /// </summary>
        [Fact]
        public void ExternalCatalog_KeepsValuesWhenTheSourceFails()
        {
            Seed(nameof(ExternalCatalog_KeepsValuesWhenTheSourceFails));

            var source = new SourceHandler { Body = """[{"value":"CC-100","label":"Finance"},{"value":"CC-200","label":"IT"}]""" };
            var manager = (ValueCatalogManager)CoreHub.ValueCatalogManager;
            var previous = manager.Transport;

            manager.Transport = source;

            try
            {
                var catalog = Catalog(ValueCatalogSource.External, x =>
                {
                    x.Url = "https://erp.example.com/costcenters";
                    x.RefreshMinutes = 60;
                });

                Assert.Equal(1, CoreHub.ValueCatalogManager.RefreshDue(DateTime.UtcNow));
                Assert.Equal([new CatalogValue("CC-100", "Finance"), new CatalogValue("CC-200", "IT")], CoreHub.ValueCatalogManager.GetValues(catalog.Id));
                Assert.Equal(0, CoreHub.ValueCatalogManager.RefreshDue(DateTime.UtcNow.AddMinutes(30)));

                source.Status = HttpStatusCode.BadGateway;
                Assert.Null(CoreHub.ValueCatalogManager.Refresh(catalog.Id));

                var stored = CoreHub.ValueCatalogManager.GetCatalog(catalog.Id);
                Assert.Contains("502", stored.LastError, StringComparison.Ordinal);
                Assert.Equal(1, stored.CurrentVersion);
                Assert.Equal(2, CoreHub.ValueCatalogManager.GetValues(catalog.Id).Count);
            }
            finally
            {
                manager.Transport = previous;
            }
        }

        /// <summary>
        /// The external formats: strings, objects, a wrapped list, and text lines or CSV.
        /// </summary>
        [Fact]
        public void ParseExternal_ReadsTheCommonFormats()
        {
            static string[] Values(string text) => [.. ValueCatalogManager.ParseExternal(Encoding.UTF8.GetBytes(text)).Select(x => x.Value)];

            Assert.Equal(["a", "b"], Values("""["a","b"]"""));
            Assert.Equal(["1", "2"], Values("""{"values":[{"id":1},{"id":2}]}"""));
            Assert.Equal(["Berlin", "Munich"], Values("Berlin;DE\r\nMunich;DE\r\n"));
        }

        /// <summary>
        /// A catalog is refused with its problems.
        /// </summary>
        [Fact]
        public void Validate_ReportsProblems()
        {
            Seed(nameof(Validate_ReportsProblems));

            Assert.NotEmpty(CoreHub.ValueCatalogManager.Validate(new ValueCatalog { Name = "q", WorkspaceId = WorkspaceId, Source = ValueCatalogSource.Query, Query = "Summary ~~", ValueAttribute = "summary" }));
            Assert.NotEmpty(CoreHub.ValueCatalogManager.Validate(new ValueCatalog { Name = "e", WorkspaceId = WorkspaceId, Source = ValueCatalogSource.External, Url = "file:///etc/passwd" }));
            Assert.NotEmpty(CoreHub.ValueCatalogManager.Validate(new ValueCatalog { Name = "r", WorkspaceId = WorkspaceId, Source = ValueCatalogSource.External, Url = "https://x.example.com", RefreshMinutes = 1 }));
            Assert.Empty(CoreHub.ValueCatalogManager.Validate(new ValueCatalog { Name = "ok", WorkspaceId = WorkspaceId, Source = ValueCatalogSource.Static }));
        }

        /// <summary>
        /// Answers external reads with a set status and body.
        /// </summary>
        private sealed class SourceHandler : HttpMessageHandler
        {
            /// <summary>Gets or sets the status.</summary>
            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            /// <summary>Gets or sets the body.</summary>
            public string Body { get; set; }

            /// <inheritdoc/>
            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return new HttpResponseMessage(Status) { Content = new StringContent(Body ?? string.Empty, Encoding.UTF8, "application/json") };
            }

            /// <inheritdoc/>
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Send(request, cancellationToken));
            }
        }
    }
}
