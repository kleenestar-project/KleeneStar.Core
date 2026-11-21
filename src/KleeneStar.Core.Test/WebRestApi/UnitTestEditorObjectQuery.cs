using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests the filters behind the editor's object add-ons (<see cref="EditorObjectQuery"/>):
    /// the columns a filter narrows by, the WQL condition applied as the rows are read, the
    /// "mine" filter for nobody, and the child pages of a document.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestEditorObjectQuery
    {
        private static readonly Guid WorkspaceId = Guid.Parse("ED170000-0000-4000-8000-000000000001");
        private static readonly Guid IssueClassId = Guid.Parse("ED170000-0000-4000-8000-000000000002");
        private static readonly Guid PageClassId = Guid.Parse("ED170000-0000-4000-8000-000000000003");
        private static readonly Guid ParentId = Guid.Parse("ED170000-0000-4000-8000-000000000010");

        /// <summary>
        /// Seeds a workspace with three issues and a page with two child pages.
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

            var now = DateTime.UtcNow;

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "EDQ", Name = "editor" });
            db.Classes.Add(new Model.Entities.Class { Id = IssueClassId, Name = "Ticket", WorkspaceId = WorkspaceId, Kind = ObjectKind.Issue });
            db.Classes.Add(new Model.Entities.Class { Id = PageClassId, Name = "Page", WorkspaceId = WorkspaceId, Kind = ObjectKind.Document });

            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "EDQ-1", Summary = "mail down", Kind = ObjectKind.Issue, WorkspaceId = WorkspaceId, ClassId = IssueClassId, Created = now, Updated = now.AddMinutes(-3) });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "EDQ-2", Summary = "printer jam", Kind = ObjectKind.Issue, WorkspaceId = WorkspaceId, ClassId = IssueClassId, Created = now, Updated = now.AddMinutes(-2) });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "EDQ-3", Summary = "mail slow", Kind = ObjectKind.Issue, WorkspaceId = WorkspaceId, ClassId = IssueClassId, Created = now, Updated = now.AddMinutes(-1) });
            db.Objects.Add(new ObjectEntity(ParentId) { Key = "EDQ-10", Summary = "Handbook", Kind = ObjectKind.Document, WorkspaceId = WorkspaceId, ClassId = PageClassId, Created = now, Updated = now });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "EDQ-12", Summary = "Setup", Kind = ObjectKind.Document, WorkspaceId = WorkspaceId, ClassId = PageClassId, ParentId = ParentId, Created = now, Updated = now });
            db.Objects.Add(new ObjectEntity(Guid.NewGuid()) { Key = "EDQ-11", Summary = "Basics", Kind = ObjectKind.Document, WorkspaceId = WorkspaceId, ClassId = PageClassId, ParentId = ParentId, Created = now, Updated = now });
            db.SaveChanges();
        }

        /// <summary>
        /// Workspace, class and kind narrow the query; the objects come changed last first,
        /// the total counts all of them and the items stop at the maximum.
        /// </summary>
        [Fact]
        public void ColumnsNarrowAndTheNewestComeFirst()
        {
            Seed(nameof(ColumnsNarrowAndTheNewestComeFirst));

            var result = EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "EDQ", Class: "ticket", Kind: "issue", Max: 2), Guid.Empty);

            Assert.Equal(3, result.Total);
            Assert.False(result.Truncated);
            Assert.Equal(["EDQ-3", "EDQ-2"], result.Items.Select(x => x.Object.Key));
        }

        /// <summary>
        /// "any" and blank mean every kind - not the default kind, which is what
        /// <c>ObjectKind.Normalize</c> would make of blank.
        /// </summary>
        [Fact]
        public void AnyKindMeansEveryKind()
        {
            Seed(nameof(AnyKindMeansEveryKind));

            Assert.Equal(6, EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "EDQ", Kind: "any", Max: 50), Guid.Empty).Total);
            Assert.Equal(6, EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "EDQ", Max: 50), Guid.Empty).Total);
            Assert.Equal(3, EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "EDQ", Kind: "document", Max: 50), Guid.Empty).Total);
        }

        /// <summary>
        /// A WQL condition is applied to the rows as they are read.
        /// </summary>
        [Fact]
        public void WqlNarrowsTheRows()
        {
            Seed(nameof(WqlNarrowsTheRows));

            var result = EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "EDQ", Wql: "Summary ~ \"mail\"", Max: 50), Guid.Empty);

            Assert.Equal(["EDQ-3", "EDQ-1"], result.Items.Select(x => x.Object.Key));
        }

        /// <summary>
        /// A workspace or class that does not exist selects nothing rather than everything, and
        /// "assigned to the reader" selects nothing for nobody.
        /// </summary>
        [Fact]
        public void UnknownNamesAndNobodySelectNothing()
        {
            Seed(nameof(UnknownNamesAndNobodySelectNothing));

            Assert.Equal(0, EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "NOPE"), Guid.Empty).Total);
            Assert.Equal(0, EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "EDQ", Class: "Nope"), Guid.Empty).Total);
            Assert.Equal(0, EditorObjectQuery.Run(new EditorObjectFilter(Workspace: "EDQ", Mine: true), Guid.Empty).Total);
        }

        /// <summary>
        /// The child pages of a document are the objects whose parent it is, by summary.
        /// </summary>
        [Fact]
        public void ChildrenAreTheDocumentsBelowBySummary()
        {
            Seed(nameof(ChildrenAreTheDocumentsBelowBySummary));

            var parent = CoreHub.ObjectManager.GetObject(ParentId);

            Assert.Equal(["Basics", "Setup"], EditorObjectQuery.Children(parent, 10).Select(x => x.Summary));
            Assert.Empty(EditorObjectQuery.Children(null, 10));
        }

        /// <summary>
        /// A condition that does not parse is refused with the parser's reason.
        /// </summary>
        [Fact]
        public void AnInvalidConditionIsRefused()
        {
            Assert.True(EditorObjectQuery.TryValidate(null, out _));
            Assert.False(EditorObjectQuery.TryValidate("Summary ~~~", out var error));
            Assert.False(string.IsNullOrWhiteSpace(error));
        }
    }
}
