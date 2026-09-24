using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Model.Entities;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WWW.Api.Objects
{
    /// <summary>
    /// Tests that a document created while a document is open lands below it in the page tree,
    /// and that nothing else does: the create wizard names the open document whatever is created
    /// from it, so the endpoint decides.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectPlaceBelowOpenDocument
    {
        private static readonly Guid WorkspaceId = Guid.Parse("D0C00000-0000-4000-8000-000000000001");
        private static readonly Guid OtherWorkspaceId = Guid.Parse("D0C00000-0000-4000-8000-000000000002");
        private static readonly Guid PageClassId = Guid.Parse("D0C00000-0000-4000-8000-000000000003");
        private static readonly Guid IssueClassId = Guid.Parse("D0C00000-0000-4000-8000-000000000004");
        private static readonly Guid OtherPageClassId = Guid.Parse("D0C00000-0000-4000-8000-000000000005");
        private static readonly Guid OpenDocumentId = Guid.Parse("D0C00000-0000-4000-8000-000000000006");
        private static readonly Guid OpenIssueId = Guid.Parse("D0C00000-0000-4000-8000-000000000007");

        /// <summary>
        /// Seeds a workspace with a document class, an issue class, an open document and an
        /// open issue, and a second workspace with a document class of its own.
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

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "KB", Name = "knowledge" });
            db.Workspaces.Add(new Workspace { Id = OtherWorkspaceId, Key = "OT", Name = "other" });
            db.Classes.Add(new Class { Id = PageClassId, Name = "Page", WorkspaceId = WorkspaceId, Kind = ObjectKind.Document });
            db.Classes.Add(new Class { Id = IssueClassId, Name = "Ticket", WorkspaceId = WorkspaceId, Kind = ObjectKind.Issue });
            db.Classes.Add(new Class { Id = OtherPageClassId, Name = "Page", WorkspaceId = OtherWorkspaceId, Kind = ObjectKind.Document });
            db.Objects.Add(new ObjectEntity(OpenDocumentId) { Key = "KB-1", Summary = "open", WorkspaceId = WorkspaceId, ClassId = PageClassId, Kind = ObjectKind.Document });
            db.Objects.Add(new ObjectEntity(OpenIssueId) { Key = "KB-2", Summary = "issue", WorkspaceId = WorkspaceId, ClassId = IssueClassId, Kind = ObjectKind.Issue });
            db.SaveChanges();
        }

        /// <summary>
        /// Runs the placement the create endpoint performs on a new object.
        /// </summary>
        /// <param name="object">The object being created.</param>
        /// <param name="openKey">The key the wizard carries, or null.</param>
        private static void Place(ObjectEntity @object, string openKey)
        {
            var payload = new RestApiCrudFormData();

            if (openKey is not null)
            {
                payload[ObjectAddFormFragment.ParentKeyField.ToLowerInvariant()] = openKey;
            }

            typeof(global::KleeneStar.Core.WWW.Api._1_.Objects.Index)
                .GetMethod("PlaceBelowOpenDocument", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [payload, @object]);
        }

        /// <summary>
        /// A document created from an open document of the same workspace becomes its child.
        /// </summary>
        [Fact]
        public void ADocumentLandsBelowTheOpenDocument()
        {
            Seed(nameof(ADocumentLandsBelowTheOpenDocument));

            var created = new ObjectEntity(Guid.NewGuid()) { WorkspaceId = WorkspaceId, ClassId = PageClassId };
            Place(created, "KB-1");

            Assert.Equal(OpenDocumentId, created.ParentId);
        }

        /// <summary>
        /// Everything else stays a root: an issue created while a page is open, a document of
        /// another workspace, a document created while an issue is open, an unknown key and no
        /// key at all.
        /// </summary>
        [Fact]
        public void NothingElseIsPlaced()
        {
            Seed(nameof(NothingElseIsPlaced));

            var issue = new ObjectEntity(Guid.NewGuid()) { WorkspaceId = WorkspaceId, ClassId = IssueClassId };
            var foreign = new ObjectEntity(Guid.NewGuid()) { WorkspaceId = OtherWorkspaceId, ClassId = OtherPageClassId };
            var belowIssue = new ObjectEntity(Guid.NewGuid()) { WorkspaceId = WorkspaceId, ClassId = PageClassId };
            var unknown = new ObjectEntity(Guid.NewGuid()) { WorkspaceId = WorkspaceId, ClassId = PageClassId };
            var none = new ObjectEntity(Guid.NewGuid()) { WorkspaceId = WorkspaceId, ClassId = PageClassId };

            Place(issue, "KB-1");
            Place(foreign, "KB-1");
            Place(belowIssue, "KB-2");
            Place(unknown, "KB-99");
            Place(none, null);

            Assert.All([issue, foreign, belowIssue, unknown, none], x => Assert.Null(x.ParentId));
        }
    }
}
