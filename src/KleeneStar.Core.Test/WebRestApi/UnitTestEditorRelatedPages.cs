using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests the related pages of the add-on <c>ks-related</c> (<see cref="EditorRelatedPages"/>):
    /// which pages count, how they are weighed and ordered, and how many are answered.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestEditorRelatedPages
    {
        private static readonly Guid WorkspaceId = Guid.Parse("ED180000-0000-4000-8000-000000000001");
        private static readonly Guid PageClassId = Guid.Parse("ED180000-0000-4000-8000-000000000002");
        private static readonly Guid IssueClassId = Guid.Parse("ED180000-0000-4000-8000-000000000003");
        private static readonly Guid ChapterId = Guid.Parse("ED180000-0000-4000-8000-000000000010");
        private static readonly Guid PageId = Guid.Parse("ED180000-0000-4000-8000-000000000011");
        private static readonly Guid RelatedId = Guid.Parse("ED180000-0000-4000-8000-000000000020");

        /// <summary>
        /// Seeds a chapter with the page and two siblings, a child of the page, a page the page
        /// references, a page referencing it, a page sharing its label, a page joined by a
        /// relation, and an issue and an archived page the page references as well.
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

            ObjectEntity Page(Guid id, string key, string summary, Guid? parentId = null, string description = null, string kind = ObjectKind.Document, WorkspaceState state = WorkspaceState.Active) => new(id)
            {
                Key = key,
                Summary = summary,
                Description = description,
                Kind = kind,
                State = state,
                WorkspaceId = WorkspaceId,
                ClassId = kind == ObjectKind.Document ? PageClassId : IssueClassId,
                ParentId = parentId,
                Created = now,
                Updated = now
            };

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "EDR", Name = "related" });
            db.Classes.Add(new Model.Entities.Class { Id = PageClassId, Name = "Page", WorkspaceId = WorkspaceId, Kind = ObjectKind.Document });
            db.Classes.Add(new Model.Entities.Class { Id = IssueClassId, Name = "Ticket", WorkspaceId = WorkspaceId, Kind = ObjectKind.Issue });

            db.Objects.Add(Page(ChapterId, "EDR-1", "Chapter"));
            db.Objects.Add(Page(PageId, "EDR-2", "The page", ChapterId, Document("EDR-6", "EDR-9", "EDR-10", "EDR-6")));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-3", "Sibling b", ChapterId));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-4", "Sibling a", ChapterId));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-5", "Referencing", description: Document("EDR-2")));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-6", "Referenced"));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-7", "Same label"));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-8", "Child", PageId));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-9", "An issue", kind: ObjectKind.Issue));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-10", "Archived", state: WorkspaceState.Archived));
            db.Objects.Add(Page(RelatedId, "EDR-11", "Joined"));
            db.Objects.Add(Page(Guid.NewGuid(), "EDR-12", "Unrelated"));

            var labelled = db.Objects.Local.Single(x => x.Key == "EDR-7").Id;

            db.ObjectTags.Add(new ObjectTag { ObjectId = PageId, Name = "Help", Created = now });
            db.ObjectTags.Add(new ObjectTag { ObjectId = labelled, Name = "help", Created = now });

            db.ObjectRelations.Add(new ObjectRelation
            {
                Id = Guid.NewGuid(),
                TypeKey = "relates",
                SourceObjectId = RelatedId,
                TargetObjectId = PageId,
                Created = now,
                Updated = now
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Returns an editor document naming the supplied keys through object references.
        /// </summary>
        private static string Document(params string[] keys)
        {
            var references = string.Join(",", keys.Select(x => $"{{\"type\":\"addon\",\"attrs\":{{\"name\":\"ks-object\",\"data\":{{\"key\":\"{x}\"}},\"inline\":true,\"container\":false}},\"children\":[]}}"));

            return $"{{\"version\":1,\"doc\":{{\"type\":\"doc\",\"children\":[{{\"type\":\"p\",\"attrs\":{{}},\"children\":[{{\"type\":\"text\",\"text\":\"See \",\"marks\":{{}}}},{references}]}}]}}}}";
        }

        /// <summary>
        /// References first, then relations, referencing pages, the tree and the labels; the
        /// page itself, its children, other kinds, archived and unconnected pages never.
        /// </summary>
        [Fact]
        public void TheMostRelevantPagesComeFirst()
        {
            Seed(nameof(TheMostRelevantPagesComeFirst));

            var page = CoreHub.ObjectManager.GetObject(PageId);
            var related = EditorRelatedPages.Find(page, 50).Select(x => x.Key).ToList();

            Assert.Equal(["EDR-6", "EDR-11", "EDR-5", "EDR-1", "EDR-4", "EDR-3", "EDR-7"], related);
        }

        /// <summary>
        /// The list stops at the number asked for, and a missing page has no related pages.
        /// </summary>
        [Fact]
        public void TheNumberIsBounded()
        {
            Seed(nameof(TheNumberIsBounded));

            var page = CoreHub.ObjectManager.GetObject(PageId);

            Assert.Equal(["EDR-6", "EDR-11", "EDR-5"], EditorRelatedPages.Find(page, 3).Select(x => x.Key));
            Assert.Single(EditorRelatedPages.Find(page, 0));
            Assert.Empty(EditorRelatedPages.Find(null, 5));
            Assert.Equal(5, EditorRelatedPages.DefaultMax);
        }

        /// <summary>
        /// The references of a stored document are read in order, each once; text that is no
        /// document has none.
        /// </summary>
        [Fact]
        public void ReferencesAreReadFromTheDocument()
        {
            Assert.Equal(["EDR-6", "EDR-9"], EditorRelatedPages.References(Document("EDR-6", "EDR-9", "edr-6")));
            Assert.Empty(EditorRelatedPages.References("plain text naming ks-object"));
            Assert.Empty(EditorRelatedPages.References("{ broken ks-object"));
            Assert.Empty(EditorRelatedPages.References(null));
        }
    }
}
