using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Model.Entities;
using WebExpress.WebUI.WebSpreadsheet;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Follows a document whose class was switched to the spreadsheet renderer through its whole
    /// life - opened, autosaved, published, opened again - along the steps
    /// <c>/api/1/drafts/{objectkey}</c> and <c>/api/1/prose</c> compose, and checks that the text
    /// it carried before is neither lost on the way nor missing from the history.
    /// </summary>
    /// <remarks>
    /// The endpoints themselves resolve their caller from the request's credential, which a unit
    /// test has none of; the steps below are exactly what they run once the caller may write.
    /// </remarks>
    [Collection("NonParallelTests")]
    public class UnitTestSpreadsheetDocumentLifecycle
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C5D6E7F8-0001-4001-8001-000000000001");
        private static readonly Guid ClassId = Guid.Parse("C5D6E7F8-0002-4002-8002-000000000002");
        private static readonly Guid ObjectId = Guid.Parse("C5D6E7F8-0003-4003-8003-000000000003");
        private static readonly Guid AuthorId = Guid.Parse("C5D6E7F8-0004-4004-8004-000000000004");

        /// <summary>
        /// The prose the document carried while its class still rendered it as text.
        /// </summary>
        private const string Prose = """
            {"version":1,"doc":{"type":"doc","id":"n1","children":[{"type":"p","id":"n2","children":[{"type":"text","text":"Quarterly budget.","marks":{}}]},{"type":"p","id":"n3","children":[{"type":"text","text":"Rent is due monthly.","marks":{}}]}]}}
            """;

        /// <summary>
        /// Seeds a prose document in a class rendered as a spreadsheet.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-sl", Name = "workspace" });
            db.Classes.Add(new Class
            {
                Id = ClassId,
                Name = "Budget",
                WorkspaceId = WorkspaceId,
                Kind = ObjectKind.Document,
                Renderer = ObjectRenderer.Spreadsheet
            });
            db.Identities.Add(new Identity
            {
                Id = AuthorId,
                Name = "Autor",
                UserName = "autor",
                Email = "autor@example.test",
                PasswordHash = "x",
                State = IdentityState.Active
            });
            db.Objects.Add(new ObjectEntity
            {
                Id = ObjectId,
                Key = "SL-1",
                Summary = "Budget",
                Description = Prose,
                Kind = ObjectKind.Document,
                WorkspaceId = WorkspaceId,
                ClassId = ClassId
            });

            db.SaveChanges();
        }

        /// <summary>
        /// What the dialog opens on, as both endpoints answer it.
        /// </summary>
        /// <returns>The workbook JSON.</returns>
        private static string Open()
        {
            var (_, description, _, _) = CoreHub.ObjectDraftManager.GetEffective(ObjectId);

            return WorkbookText.ForEditor(description);
        }

        /// <summary>
        /// The autosave of the dialog as the draft endpoint stores it.
        /// </summary>
        /// <param name="body">The posted workbook.</param>
        /// <returns>True when the body was accepted.</returns>
        private static bool Autosave(string body)
        {
            if (!WorkbookText.TryNormalize(body, out var normalized))
            {
                return false;
            }

            CoreHub.ObjectDraftManager.Save(ObjectId, "Budget", normalized, AuthorId);

            return true;
        }

        /// <summary>
        /// The publication of the dialog as the prose endpoint performs it.
        /// </summary>
        /// <param name="body">The posted workbook.</param>
        /// <returns>True when the body was accepted.</returns>
        private static bool Publish(string body)
        {
            if (!WorkbookText.TryNormalize(body, out var normalized))
            {
                return false;
            }

            CoreHub.ObjectDraftManager.Publish(ObjectId, "Budget", normalized, AuthorId);

            return true;
        }

        /// <summary>
        /// Verifies the whole round: the switched text opens as rows, an edit is autosaved as a
        /// draft while readers keep the text, a refused body changes nothing, the publication
        /// makes the workbook the document, the next opening resumes on it, and the history
        /// keeps the text it replaced.
        /// </summary>
        [Fact]
        public void SwitchedProse_SurvivesOpenAutosavePublishAndReload()
        {
            // arrange
            Seed(nameof(SwitchedProse_SurvivesOpenAutosavePublishAndReload));
            Assert.True(ObjectRendererCatalog.IsRenderedAs(CoreHub.ClassManager.GetClass(ClassId), ObjectRenderer.Spreadsheet));

            // act: open
            var opened = SpreadsheetWorkbook.Parse(Open());

            // validation: the text is in the sheet, not behind an empty one
            Assert.Equal("Quarterly budget.", opened.Sheets[0].GetValue("A1"));
            Assert.Equal("Rent is due monthly.", opened.Sheets[0].GetValue("A2"));

            // act: the author adds a figure and the dialog autosaves
            opened.Sheets[0].SetValue("B2", 900);
            Assert.True(Autosave(opened.ToJson()));

            // validation: readers still see the published text, the editor resumes the draft
            Assert.Equal(Prose, CoreHub.ObjectManager.GetObject(ObjectId).Description);
            Assert.Equal(900d, Convert.ToDouble(SpreadsheetWorkbook.Parse(Open()).Sheets[0].GetValue("B2")));

            // act / validation: bodies that are no workbook are refused and change nothing
            var draftBefore = CoreHub.ObjectDraftManager.GetDraft(ObjectId).Description;
            Assert.False(Autosave(Prose));
            Assert.False(Autosave("{\"sheets\":[" + new string(' ', WorkbookText.MaxLength) + "]}"));
            Assert.False(Publish("[]"));
            Assert.Equal(draftBefore, CoreHub.ObjectDraftManager.GetDraft(ObjectId).Description);
            Assert.Equal(Prose, CoreHub.ObjectManager.GetObject(ObjectId).Description);

            // act: publish what the dialog shows
            Assert.True(Publish(Open()));

            // validation: the workbook is the document, the draft is gone, a reload resumes it
            var published = CoreHub.ObjectManager.GetObject(ObjectId).Description;
            Assert.True(WorkbookText.IsWorkbook(published));
            Assert.False(CoreHub.ObjectDraftManager.HasDraft(ObjectId));
            Assert.Equal(published, Open());
            Assert.Equal("Quarterly budget. Rent is due monthly. 900", ProseText.ToPlainText(published));

            // validation: the publication is one commit that remembers the replaced text
            var commit = CoreHub.CommitManager.GetHistory(ObjectId).First();
            Assert.Contains(commit.Changes, change => change.OldValue == Prose && change.NewValue == published);
        }

        /// <summary>
        /// Verifies that a document emptied of every cell is published as the empty workbook and
        /// opens as one, rather than falling back to the text it had before the switch.
        /// </summary>
        [Fact]
        public void ClearedWorkbook_PublishesEmptyAndReopensEmpty()
        {
            // arrange
            Seed(nameof(ClearedWorkbook_PublishesEmptyAndReopensEmpty));

            // act
            Assert.True(Publish(""));

            // validation
            Assert.True(WorkbookText.Read(Open())?.IsEmpty);
            Assert.True(ProseText.IsEmpty(CoreHub.ObjectManager.GetObject(ObjectId).Description));
        }
    }
}
