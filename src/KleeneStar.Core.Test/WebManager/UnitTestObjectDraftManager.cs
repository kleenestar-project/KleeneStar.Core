using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.ObjectDraftManager"/> - the
    /// unpublished working copy the prose editor writes into while the reading view keeps
    /// showing the last published text.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectDraftManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("A1B2C3D4-0001-4001-8001-000000000001");
        private static readonly Guid ClassId = Guid.Parse("A1B2C3D4-0002-4002-8002-000000000002");
        private static readonly Guid ObjectId = Guid.Parse("A1B2C3D4-0003-4003-8003-000000000003");
        private static readonly Guid AuthorId = Guid.Parse("A1B2C3D4-0004-4004-8004-000000000004");

        private const string PublishedSummary = "published title";
        private const string PublishedBody = "<p>published body</p>";

        /// <summary>
        /// Seeds one published document and the identity that edits it.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-dr", Name = "workspace" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Page", WorkspaceId = WorkspaceId });
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
                Key = "DR-1",
                Summary = PublishedSummary,
                Description = PublishedBody,
                Kind = ObjectKind.Document,
                WorkspaceId = WorkspaceId,
                ClassId = ClassId
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Without a draft the editor opens on the published text and says so.
        /// </summary>
        [Fact]
        public void GetEffective_WithoutDraft_ReturnsPublished()
        {
            Seed(nameof(GetEffective_WithoutDraft_ReturnsPublished));

            var (summary, description, isDraft, updated) = CoreHub.ObjectDraftManager.GetEffective(ObjectId);

            Assert.Equal(PublishedSummary, summary);
            Assert.Equal(PublishedBody, description);
            Assert.False(isDraft);
            Assert.Null(updated);
            Assert.False(CoreHub.ObjectDraftManager.HasDraft(ObjectId));
        }

        /// <summary>
        /// Saving a draft leaves the published object untouched — the readers keep seeing what
        /// was published — while the editor opens on the draft.
        /// </summary>
        [Fact]
        public void Save_LeavesPublishedTextUntouched_ButIsWhatEditingLoads()
        {
            Seed(nameof(Save_LeavesPublishedTextUntouched_ButIsWhatEditingLoads));

            var draft = CoreHub.ObjectDraftManager.Save(ObjectId, "work in progress", "<p>draft body</p>", AuthorId);

            Assert.NotNull(draft);
            Assert.Equal(AuthorId, draft.UpdaterId);

            var published = CoreHub.ObjectManager.GetObject(ObjectId);
            Assert.Equal(PublishedSummary, published.Summary);
            Assert.Equal(PublishedBody, published.Description);

            var (summary, description, isDraft, updated) = CoreHub.ObjectDraftManager.GetEffective(ObjectId);
            Assert.Equal("work in progress", summary);
            Assert.Equal("<p>draft body</p>", description);
            Assert.True(isDraft);
            Assert.NotNull(updated);
        }

        /// <summary>
        /// A draft is the shared working copy of the object, so a second save overwrites the
        /// first rather than opening a second draft — the unique index makes that a hard rule.
        /// </summary>
        [Fact]
        public void Save_Twice_KeepsOneDraft()
        {
            Seed(nameof(Save_Twice_KeepsOneDraft));

            var first = CoreHub.ObjectDraftManager.Save(ObjectId, "one", "<p>one</p>", AuthorId);
            var second = CoreHub.ObjectDraftManager.Save(ObjectId, "two", "<p>two</p>", Guid.Empty);

            Assert.Equal(first.Id, second.Id);
            Assert.Equal("two", CoreHub.ObjectDraftManager.GetDraft(ObjectId).Summary);
            Assert.Null(CoreHub.ObjectDraftManager.GetDraft(ObjectId).UpdaterId);
        }

        /// <summary>
        /// Publishing copies the draft onto the object, drops the draft, and appends exactly one
        /// commit — the history begins where publishing ends, not where typing does.
        /// </summary>
        [Fact]
        public void Publish_AppliesDraft_DropsIt_AndWritesOneCommit()
        {
            Seed(nameof(Publish_AppliesDraft_DropsIt_AndWritesOneCommit));

            var before = CoreHub.CommitManager.GetHistory(ObjectId).Count();

            CoreHub.ObjectDraftManager.Save(ObjectId, "final title", "<p>final body</p>", AuthorId);
            CoreHub.ObjectDraftManager.Publish(ObjectId, null, null, AuthorId);

            var published = CoreHub.ObjectManager.GetObject(ObjectId);
            Assert.Equal("final title", published.Summary);
            Assert.Equal("<p>final body</p>", published.Description);

            Assert.False(CoreHub.ObjectDraftManager.HasDraft(ObjectId));
            Assert.Equal(before + 1, CoreHub.CommitManager.GetHistory(ObjectId).Count());
        }

        /// <summary>
        /// Publishing what the editor is showing wins over what the last autosave stored: the
        /// payload carries everything typed since, and publishing something the author is not
        /// looking at would be the one outcome they cannot predict.
        /// </summary>
        [Fact]
        public void Publish_PrefersSubmittedTextOverDraft()
        {
            Seed(nameof(Publish_PrefersSubmittedTextOverDraft));

            CoreHub.ObjectDraftManager.Save(ObjectId, "autosaved", "<p>autosaved</p>", AuthorId);
            CoreHub.ObjectDraftManager.Publish(ObjectId, "typed since", "<p>typed since</p>", AuthorId);

            var published = CoreHub.ObjectManager.GetObject(ObjectId);
            Assert.Equal("typed since", published.Summary);
            Assert.Equal("<p>typed since</p>", published.Description);
            Assert.False(CoreHub.ObjectDraftManager.HasDraft(ObjectId));
        }

        /// <summary>
        /// Publishing without any draft still lands — a publish that arrives before the first
        /// autosave must not be lost.
        /// </summary>
        [Fact]
        public void Publish_WithoutDraft_StillPublishes()
        {
            Seed(nameof(Publish_WithoutDraft_StillPublishes));

            CoreHub.ObjectDraftManager.Publish(ObjectId, "straight through", "<p>straight through</p>", AuthorId);

            var published = CoreHub.ObjectManager.GetObject(ObjectId);
            Assert.Equal("straight through", published.Summary);
            Assert.Equal("<p>straight through</p>", published.Description);
        }

        /// <summary>
        /// Discarding drops the unpublished changes and leaves the published text as it stands.
        /// </summary>
        [Fact]
        public void Discard_DropsDraft_AndKeepsPublishedText()
        {
            Seed(nameof(Discard_DropsDraft_AndKeepsPublishedText));

            CoreHub.ObjectDraftManager.Save(ObjectId, "abandoned", "<p>abandoned</p>", AuthorId);

            Assert.True(CoreHub.ObjectDraftManager.Discard(ObjectId));
            Assert.False(CoreHub.ObjectDraftManager.Discard(ObjectId));

            var published = CoreHub.ObjectManager.GetObject(ObjectId);
            Assert.Equal(PublishedSummary, published.Summary);
            Assert.Equal(PublishedBody, published.Description);

            var (summary, _, isDraft, _) = CoreHub.ObjectDraftManager.GetEffective(ObjectId);
            Assert.Equal(PublishedSummary, summary);
            Assert.False(isDraft);
        }

        /// <summary>
        /// A draft column left null means "unchanged", so the published value stands in for it
        /// rather than blanking the field the editor opens on.
        /// </summary>
        [Fact]
        public void GetEffective_PartialDraft_FallsBackToPublished()
        {
            Seed(nameof(GetEffective_PartialDraft_FallsBackToPublished));

            CoreHub.ObjectDraftManager.Save(ObjectId, null, "<p>body only</p>", AuthorId);

            var (summary, description, isDraft, _) = CoreHub.ObjectDraftManager.GetEffective(ObjectId);

            Assert.Equal(PublishedSummary, summary);
            Assert.Equal("<p>body only</p>", description);
            Assert.True(isDraft);
        }

        /// <summary>
        /// An unknown object has no draft and cannot be given one.
        /// </summary>
        [Fact]
        public void Save_UnknownObject_ReturnsNull()
        {
            Seed(nameof(Save_UnknownObject_ReturnsNull));

            Assert.Null(CoreHub.ObjectDraftManager.Save(Guid.NewGuid(), "x", "y", AuthorId));
            Assert.Null(CoreHub.ObjectDraftManager.Save(Guid.Empty, "x", "y", AuthorId));
            Assert.Null(CoreHub.ObjectDraftManager.Publish(Guid.NewGuid(), "x", "y", AuthorId));
        }

        /// <summary>
        /// The draft follows its object into deletion. The rule is a database cascade rather
        /// than manager code, so what is asserted here is the configured delete behaviour - the
        /// in-memory provider the tests run on enforces no referential integrity of its own, so
        /// observing the deletion would prove nothing about the shipped schema.
        /// </summary>
        [Fact]
        public void DraftCascadesWithItsObject()
        {
            Seed(nameof(DraftCascadesWithItsObject));

            using var db = CoreHubFixture.CreateDbContext(nameof(DraftCascadesWithItsObject));

            var foreignKey = db.Model
                .FindEntityType(typeof(ObjectDraft))
                .GetForeignKeys()
                .Single(x => x.PrincipalEntityType.ClrType == typeof(ObjectEntity));

            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        }

        /// <summary>
        /// A draft is the shared working copy of one object, so the schema refuses a second row
        /// for the same object rather than leaving the manager to be careful.
        /// </summary>
        [Fact]
        public void DraftIsUniquePerObject()
        {
            Seed(nameof(DraftIsUniquePerObject));

            using var db = CoreHubFixture.CreateDbContext(nameof(DraftIsUniquePerObject));

            var index = db.Model
                .FindEntityType(typeof(ObjectDraft))
                .GetIndexes()
                .Single(x => x.Properties.Count == 1 && x.Properties[0].Name == nameof(ObjectDraft.ObjectId));

            Assert.True(index.IsUnique);
        }
        /// <summary>
        /// Verifies that a normalized empty workbook replaces published content when a draft resumes.
        /// </summary>
        [Fact]
        public void Save_EmptyWorkbook_ResumesEmptyBody()
        {
            // arrange
            Seed(nameof(Save_EmptyWorkbook_ResumesEmptyBody));
            Assert.True(global::KleeneStar.Core.WebControl.WorkbookText.TryNormalize("", out var empty));

            // act
            CoreHub.ObjectDraftManager.Save(ObjectId, "empty draft", empty, AuthorId);
            var effective = CoreHub.ObjectDraftManager.GetEffective(ObjectId);

            // validation
            Assert.True(effective.IsDraft);
            Assert.True(global::KleeneStar.Core.WebControl.WorkbookText.Read(effective.Description)?.IsEmpty);
            Assert.Equal(PublishedBody, CoreHub.ObjectManager.GetObject(ObjectId).Description);
        }

        /// <summary>
        /// Verifies that publishing an empty workbook cannot resurrect an older draft body.
        /// </summary>
        /// <param name="hasDraft">Whether an older draft exists before publication.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Publish_EmptyWorkbook_ReplacesPreviousContent(bool hasDraft)
        {
            // arrange
            Seed(nameof(Publish_EmptyWorkbook_ReplacesPreviousContent) + hasDraft);
            if (hasDraft)
            {
                CoreHub.ObjectDraftManager.Save(ObjectId, "older draft", PublishedBody, AuthorId);
            }
            Assert.True(global::KleeneStar.Core.WebControl.WorkbookText.TryNormalize("", out var empty));

            // act
            var published = CoreHub.ObjectDraftManager.Publish(ObjectId, "empty workbook", empty, AuthorId);

            // validation
            Assert.True(global::KleeneStar.Core.WebControl.WorkbookText.Read(published.Description)?.IsEmpty);
            Assert.True(global::KleeneStar.Core.WebControl.WorkbookText.Read(CoreHub.ObjectManager.GetObject(ObjectId).Description)?.IsEmpty);
            Assert.False(CoreHub.ObjectDraftManager.HasDraft(ObjectId));
        }
    }
}
