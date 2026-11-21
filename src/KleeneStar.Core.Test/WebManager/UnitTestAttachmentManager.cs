using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.AttachmentManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestAttachmentManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C7A1E9D2-3F46-4B58-9A0C-1D2E3F4A5B61");
        private static readonly Guid ClassId = Guid.Parse("D8B2FAE3-4057-4C69-AB1D-2E3F4A5B6C72");
        private static readonly Guid ObjectId = Guid.Parse("E9C30BF4-5168-4D7A-BC2E-3F4A5B6C7D83");
        private static readonly Guid UploaderId = Guid.Parse("FAD41CF5-6279-4E8B-CD3F-4A5B6C7D8E94");

        private const string ObjectKey = "INC-200";

        /// <summary>
        /// Wires the hub to an isolated in-memory database and seeds the workspace, class,
        /// uploader identity, and the object the attachments hang off.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-am", Name = "workspace" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.Identities.Any(x => x.Id == UploaderId))
            {
                db.Identities.Add(new Identity { Id = UploaderId, Name = "Test Uploader", Email = "uploader@kleenestar.org", PasswordHash = "$test$" });
            }
            if (!db.Objects.Any(x => x.Id == ObjectId))
            {
                db.Objects.Add(new KleeneStar.Model.Entities.Object { Id = ObjectId, Key = ObjectKey, Summary = "Test incident", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Add persists the file and GetAttachment returns the full row including its payload.
        /// </summary>
        [Fact]
        public void Add_Then_GetAttachment_RoundTrip()
        {
            Seed(nameof(Add_Then_GetAttachment_RoundTrip));

            var content = SampleContent();
            var added = CoreHub.AttachmentManager.Add(ObjectId, "report.pdf", "application/pdf", content, "monthly report", UploaderId);

            Assert.NotNull(added);

            var loaded = CoreHub.AttachmentManager.GetAttachment(added.Id);

            Assert.NotNull(loaded);
            Assert.Equal("report.pdf", loaded.FileName);
            Assert.Equal("application/pdf", loaded.ContentType);
            Assert.Equal("monthly report", loaded.Description);
            Assert.Equal(AttachmentState.Active, loaded.State);
            Assert.Equal(UploaderId, loaded.UploaderId);
            Assert.Equal(content, loaded.Content);
        }

        /// <summary>
        /// Add derives <see cref="Attachment.Size"/> from the supplied content length.
        /// </summary>
        [Fact]
        public void Add_ComputesSizeFromContent()
        {
            Seed(nameof(Add_ComputesSizeFromContent));

            var content = SampleContent();
            var added = CoreHub.AttachmentManager.Add(ObjectId, "data.bin", "application/octet-stream", content, null, null);

            Assert.NotNull(added);
            Assert.Equal(content.LongLength, added.Size);
        }

        /// <summary>
        /// Add rejects an empty or whitespace file name and persists nothing.
        /// </summary>
        /// <param name="fileName">The invalid file name under test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Add_EmptyFileName_ReturnsNull(string fileName)
        {
            Seed(nameof(Add_EmptyFileName_ReturnsNull));

            var result = CoreHub.AttachmentManager.Add(ObjectId, fileName, "text/plain", SampleContent(), null, null);

            Assert.Null(result);
            Assert.Empty(CoreHub.AttachmentManager.GetAttachments(ObjectId));
        }

        /// <summary>
        /// Add against an unknown object returns null instead of persisting an orphan.
        /// </summary>
        [Fact]
        public void Add_UnknownObject_ReturnsNull()
        {
            Seed(nameof(Add_UnknownObject_ReturnsNull));

            var result = CoreHub.AttachmentManager.Add(Guid.NewGuid(), "orphan.txt", "text/plain", SampleContent(), null, null);

            Assert.Null(result);
        }

        /// <summary>
        /// Add raises the <see cref="KleeneStar.Core.WebManager.AttachmentManager.AttachmentAdded"/> event.
        /// </summary>
        [Fact]
        public void Add_RaisesAttachmentAddedEvent()
        {
            Seed(nameof(Add_RaisesAttachmentAddedEvent));

            Attachment raised = null;
            CoreHub.AttachmentManager.AttachmentAdded += (_, a) => raised = a;

            var added = CoreHub.AttachmentManager.Add(ObjectId, "evented.txt", "text/plain", SampleContent(), null, null);

            Assert.NotNull(raised);
            Assert.Equal(added.Id, raised.Id);
        }

        /// <summary>
        /// GetAttachments(Guid) returns every visible attachment of the object.
        /// </summary>
        [Fact]
        public void GetAttachments_ByObjectId_ReturnsAllAttachments()
        {
            Seed(nameof(GetAttachments_ByObjectId_ReturnsAllAttachments));

            CoreHub.AttachmentManager.Add(ObjectId, "a.txt", "text/plain", SampleContent(), null, null);
            CoreHub.AttachmentManager.Add(ObjectId, "b.txt", "text/plain", SampleContent(), null, null);

            var result = CoreHub.AttachmentManager.GetAttachments(ObjectId).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, a => a.FileName == "a.txt");
            Assert.Contains(result, a => a.FileName == "b.txt");
        }

        /// <summary>
        /// GetAttachments hides soft-deleted rows (<see cref="AttachmentState.Deleted"/>).
        /// </summary>
        [Fact]
        public void GetAttachments_ExcludesSoftDeleted()
        {
            Seed(nameof(GetAttachments_ExcludesSoftDeleted));

            CoreHub.AttachmentManager.Add(ObjectId, "visible.txt", "text/plain", SampleContent(), null, null);
            InsertAttachment(nameof(GetAttachments_ExcludesSoftDeleted), "deleted.txt", AttachmentState.Deleted);

            var result = CoreHub.AttachmentManager.GetAttachments(ObjectId).ToList();

            Assert.Single(result);
            Assert.Equal("visible.txt", result[0].FileName);
        }

        /// <summary>
        /// GetAttachments still lists quarantined rows; only deleted rows are hidden.
        /// </summary>
        [Fact]
        public void GetAttachments_IncludesQuarantined()
        {
            Seed(nameof(GetAttachments_IncludesQuarantined));

            InsertAttachment(nameof(GetAttachments_IncludesQuarantined), "scanning.txt", AttachmentState.Quarantined);

            var result = CoreHub.AttachmentManager.GetAttachments(ObjectId).ToList();

            Assert.Single(result);
            Assert.Equal(AttachmentState.Quarantined, result[0].State);
        }

        /// <summary>
        /// GetAttachments(ObjectKeyParameter) resolves the object by its key and returns its files.
        /// </summary>
        [Fact]
        public void GetAttachments_ByObjectKey_ResolvesObject()
        {
            Seed(nameof(GetAttachments_ByObjectKey_ResolvesObject));

            CoreHub.AttachmentManager.Add(ObjectId, "keyed.txt", "text/plain", SampleContent(), null, null);

            var result = CoreHub.AttachmentManager.GetAttachments(new ObjectKeyParameter(ObjectKey)).ToList();

            Assert.Single(result);
            Assert.Equal("keyed.txt", result[0].FileName);
        }

        /// <summary>
        /// GetAttachments(ObjectKeyParameter) returns an empty list for an unknown key.
        /// </summary>
        [Fact]
        public void GetAttachments_ByObjectKey_UnknownKey_ReturnsEmpty()
        {
            Seed(nameof(GetAttachments_ByObjectKey_UnknownKey_ReturnsEmpty));

            var result = CoreHub.AttachmentManager.GetAttachments(new ObjectKeyParameter("does-not-exist"));

            Assert.Empty(result);
        }

        /// <summary>
        /// Remove hard-deletes the row, returns true, and raises the removal event.
        /// </summary>
        [Fact]
        public void Remove_ExistingAttachment_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_ExistingAttachment_DeletesAndRaisesEvent));

            var added = CoreHub.AttachmentManager.Add(ObjectId, "to-remove.txt", "text/plain", SampleContent(), null, null);

            Attachment raised = null;
            CoreHub.AttachmentManager.AttachmentRemoved += (_, a) => raised = a;

            var removed = CoreHub.AttachmentManager.Remove(added.Id);

            Assert.True(removed);
            Assert.Null(CoreHub.AttachmentManager.GetAttachment(added.Id));
            Assert.NotNull(raised);
            Assert.Equal(added.Id, raised.Id);
        }

        /// <summary>
        /// Remove of an unknown id returns false and is a no-op.
        /// </summary>
        [Fact]
        public void Remove_UnknownAttachment_ReturnsFalse()
        {
            Seed(nameof(Remove_UnknownAttachment_ReturnsFalse));

            var removed = CoreHub.AttachmentManager.Remove(Guid.NewGuid());

            Assert.False(removed);
        }

        /// <summary>
        /// The first upload of a name is version 1, and every further upload of the same name
        /// continues that chain instead of starting a second file.
        /// </summary>
        [Fact]
        public void Add_SameFileName_ContinuesTheVersionChain()
        {
            Seed(nameof(Add_SameFileName_ContinuesTheVersionChain));

            var first = CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), "first cut", null);
            var second = CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), null, null);
            var third = CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), null, null);

            Assert.Equal(1, first.Version);
            Assert.Equal(2, second.Version);
            Assert.Equal(3, third.Version);

            var versions = CoreHub.AttachmentManager.GetVersions(ObjectId, "plan.pdf").ToList();

            Assert.Equal(3, versions.Count);
            Assert.Equal([1, 2, 3], versions.Select(x => x.Version));
        }

        /// <summary>
        /// A different name is a different file, so it starts its own chain at version 1.
        /// </summary>
        [Fact]
        public void Add_DifferentFileName_StartsItsOwnChain()
        {
            Seed(nameof(Add_DifferentFileName_StartsItsOwnChain));

            CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), null, null);
            var other = CoreHub.AttachmentManager.Add(ObjectId, "budget.xlsx", "text/csv", SampleContent(), null, null);

            Assert.Equal(1, other.Version);
            Assert.Single(CoreHub.AttachmentManager.GetVersions(ObjectId, "budget.xlsx"));
        }

        /// <summary>
        /// A new version inherits the description of the version it supersedes, because the
        /// description says what the file is - re-uploading it does not make that unknown again.
        /// An explicit description still wins.
        /// </summary>
        [Fact]
        public void Add_NewVersion_InheritsTheDescription()
        {
            Seed(nameof(Add_NewVersion_InheritsTheDescription));

            CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), "rollback plan", null);

            var inherited = CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), null, null);
            var explicitly = CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), "revised plan", null);

            Assert.Equal("rollback plan", inherited.Description);
            Assert.Equal("revised plan", explicitly.Description);
        }

        /// <summary>
        /// SetDescription persists the new text, raises <c>AttachmentUpdated</c> and leaves the
        /// other versions of the file alone.
        /// </summary>
        [Fact]
        public void SetDescription_WritesOnlyTheNamedVersion()
        {
            Seed(nameof(SetDescription_WritesOnlyTheNamedVersion));

            var first = CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), "first cut", null);
            var second = CoreHub.AttachmentManager.Add(ObjectId, "plan.pdf", "application/pdf", SampleContent(), null, null);

            Attachment raised = null;
            CoreHub.AttachmentManager.AttachmentUpdated += (_, a) => raised = a;

            var changed = CoreHub.AttachmentManager.SetDescription(second.Id, "  approved by the board  ");

            Assert.NotNull(changed);
            Assert.Equal("approved by the board", changed.Description);

            var reloaded = CoreHub.AttachmentManager.GetAttachment(second.Id);

            Assert.Equal("approved by the board", reloaded.Description);
            Assert.Equal("first cut", CoreHub.AttachmentManager.GetAttachment(first.Id).Description);

            // the caption is written without the payload passing through, so the file itself has
            // to be exactly what it was
            Assert.Equal(SampleContent(), reloaded.Content);
            Assert.Equal(2, reloaded.Version);
            Assert.NotNull(raised);
            Assert.Equal(second.Id, raised.Id);
        }

        /// <summary>
        /// A blank description clears the text rather than storing whitespace, so the file
        /// surfaces fall back to the placeholder of their editor.
        /// </summary>
        [Fact]
        public void SetDescription_Blank_ClearsTheDescription()
        {
            Seed(nameof(SetDescription_Blank_ClearsTheDescription));

            var added = CoreHub.AttachmentManager.Add(ObjectId, "notes.txt", "text/plain", SampleContent(), "draft", null);

            var changed = CoreHub.AttachmentManager.SetDescription(added.Id, "   ");

            Assert.NotNull(changed);
            Assert.Null(changed.Description);
            Assert.Null(CoreHub.AttachmentManager.GetAttachment(added.Id).Description);
        }

        /// <summary>
        /// SetDescription of an unknown id returns null and is a no-op.
        /// </summary>
        [Fact]
        public void SetDescription_UnknownAttachment_ReturnsNull()
        {
            Seed(nameof(SetDescription_UnknownAttachment_ReturnsNull));

            Assert.Null(CoreHub.AttachmentManager.SetDescription(Guid.NewGuid(), "anything"));
        }

        /// <summary>
        /// Inserts an attachment row directly with the requested state, bypassing the manager
        /// (which only ever creates active rows), so visibility filtering can be exercised.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="fileName">The file name of the inserted attachment.</param>
        /// <param name="state">The lifecycle state to assign.</param>
        private static void InsertAttachment(string connectionString, string fileName, AttachmentState state)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(),
                ObjectId = ObjectId,
                FileName = fileName,
                ContentType = "text/plain",
                Size = 0,
                State = state,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Returns a small deterministic binary payload for use as attachment content.
        /// </summary>
        /// <returns>A non-empty byte array.</returns>
        private static byte[] SampleContent() => [1, 2, 3, 4, 5];
    }
}
