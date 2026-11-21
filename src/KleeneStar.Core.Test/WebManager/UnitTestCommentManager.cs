using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.CommentManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCommentManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("E1FB31E4-7C1D-4B11-A8C5-9B3F5A4C8B11");
        private static readonly Guid ClassId = Guid.Parse("F2FC42F5-8D2E-4C22-B9D6-AC4F6B5D9C22");
        private static readonly Guid ObjectId = Guid.Parse("A3FD53F6-9E3F-4D33-CAE7-BD506C6EAD33");
        private static readonly Guid AuthorId = Guid.Parse("B4FE64F7-AF40-4E44-DBF8-CE617D7FBE44");

        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-cm", Name = "workspace" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }
            if (!db.Identities.Any(x => x.Id == AuthorId))
            {
                db.Identities.Add(new Identity { Id = AuthorId, Name = "Test Author", Email = "test@kleenestar.org", PasswordHash = "$test$" });
            }
            if (!db.Objects.Any(x => x.Id == ObjectId))
            {
                db.Objects.Add(new KleeneStar.Model.Entities.Object { Id = ObjectId, Key = "INC-100", Summary = "Test incident", WorkspaceId = WorkspaceId, ClassId = ClassId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Add → GetComment round-trip.
        /// </summary>
        [Fact]
        public void Add_Then_GetComment_RoundTrip()
        {
            Seed(nameof(Add_Then_GetComment_RoundTrip));

            var comment = SampleComment();
            CoreHub.CommentManager.Add(comment);

            var loaded = CoreHub.CommentManager.GetComment(comment.Id);

            Assert.NotNull(loaded);
            Assert.Equal(comment.Content, loaded.Content);
            Assert.NotNull(loaded.Author);
            Assert.Equal("Test Author", loaded.Author.Name);
        }

        /// <summary>
        /// GetComments(Guid) returns all comments for the supplied object id.
        /// </summary>
        [Fact]
        public void GetComments_ByObjectId_ReturnsCommentsForObject()
        {
            Seed(nameof(GetComments_ByObjectId_ReturnsCommentsForObject));

            CoreHub.CommentManager.Add(SampleComment("first"));
            CoreHub.CommentManager.Add(SampleComment("second"));

            var result = CoreHub.CommentManager.GetComments(ObjectId).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Content == "first");
            Assert.Contains(result, c => c.Content == "second");
        }

        /// <summary>
        /// Update changes the body and bumps the state to Edited automatically.
        /// </summary>
        [Fact]
        public void Update_BumpsStateToEdited()
        {
            Seed(nameof(Update_BumpsStateToEdited));

            var comment = SampleComment();
            CoreHub.CommentManager.Add(comment);

            comment.Content = "edited body";
            CoreHub.CommentManager.Update(comment);

            var loaded = CoreHub.CommentManager.GetComment(comment.Id);
            Assert.NotNull(loaded);
            Assert.Equal("edited body", loaded.Content);
            Assert.Equal(CommentState.Edited, loaded.State);
        }

        /// <summary>
        /// SoftDelete sets the state to Deleted and clears the body, but keeps the row.
        /// </summary>
        [Fact]
        public void SoftDelete_ClearsBodyAndKeepsRow()
        {
            Seed(nameof(SoftDelete_ClearsBodyAndKeepsRow));

            var comment = SampleComment("to soft-delete");
            CoreHub.CommentManager.Add(comment);

            CoreHub.CommentManager.SoftDelete(comment.Id);

            var loaded = CoreHub.CommentManager.GetComment(comment.Id);
            Assert.NotNull(loaded);
            Assert.Equal(CommentState.Deleted, loaded.State);
            Assert.Equal(string.Empty, loaded.Content);
            Assert.NotNull(loaded.DeletedAt);
        }

        /// <summary>
        /// Remove hard-deletes the comment and raises the event.
        /// </summary>
        [Fact]
        public void Remove_HardDeletes_RaisesEvent()
        {
            Seed(nameof(Remove_HardDeletes_RaisesEvent));

            var comment = SampleComment();
            CoreHub.CommentManager.Add(comment);

            Comment raised = null;
            CoreHub.CommentManager.CommentRemoved += (_, c) => raised = c;

            CoreHub.CommentManager.Remove(comment.Id);

            Assert.Null(CoreHub.CommentManager.GetComment(comment.Id));
            Assert.NotNull(raised);
            Assert.Equal(comment.Id, raised.Id);
        }

        /// <summary>
        /// Remove of an unknown id is a no-op.
        /// </summary>
        [Fact]
        public void Remove_Unknown_IsNoOp()
        {
            Seed(nameof(Remove_Unknown_IsNoOp));

            CoreHub.CommentManager.Remove(Guid.NewGuid());

            Assert.Empty(CoreHub.CommentManager.GetComments(ObjectId));
        }

        /// <summary>
        /// TogglePin flips IsPinned and returns the new state; second call un-pins.
        /// </summary>
        [Fact]
        public void TogglePin_FlipsState()
        {
            Seed(nameof(TogglePin_FlipsState));

            var comment = SampleComment("pin me");
            CoreHub.CommentManager.Add(comment);

            var first = CoreHub.CommentManager.TogglePin(comment.Id);
            var second = CoreHub.CommentManager.TogglePin(comment.Id);

            Assert.True(first);
            Assert.False(second);
        }

        /// <summary>
        /// TogglePin on an unknown id returns null.
        /// </summary>
        [Fact]
        public void TogglePin_UnknownId_ReturnsNull()
        {
            Seed(nameof(TogglePin_UnknownId_ReturnsNull));

            var result = CoreHub.CommentManager.TogglePin(Guid.NewGuid());

            Assert.Null(result);
        }

        /// <summary>
        /// ToggleLike adds, then removes the like for the same author. Returns the
        /// current list of liker names.
        /// </summary>
        [Fact]
        public void ToggleLike_AddsThenRemoves()
        {
            Seed(nameof(ToggleLike_AddsThenRemoves));

            var comment = SampleComment("like me");
            CoreHub.CommentManager.Add(comment);

            var afterAdd = CoreHub.CommentManager.ToggleLike(comment.Id, AuthorId).ToList();
            var afterRemove = CoreHub.CommentManager.ToggleLike(comment.Id, AuthorId).ToList();

            Assert.Single(afterAdd);
            Assert.Contains("Test Author", afterAdd);
            Assert.Empty(afterRemove);
        }

        /// <summary>
        /// ToggleLike on an unknown comment id returns an empty enumeration.
        /// </summary>
        [Fact]
        public void ToggleLike_UnknownId_ReturnsEmpty()
        {
            Seed(nameof(ToggleLike_UnknownId_ReturnsEmpty));

            var result = CoreHub.CommentManager.ToggleLike(Guid.NewGuid(), AuthorId).ToList();

            Assert.Empty(result);
        }

        /// <summary>
        /// ToggleReaction adds then removes a single emoji reaction. Returns the
        /// current emoji → names map.
        /// </summary>
        [Fact]
        public void ToggleReaction_AddsThenRemoves()
        {
            Seed(nameof(ToggleReaction_AddsThenRemoves));

            var comment = SampleComment("react to me");
            CoreHub.CommentManager.Add(comment);

            var afterAdd = CoreHub.CommentManager.ToggleReaction(comment.Id, AuthorId, "🔥");
            Assert.Single(afterAdd);
            Assert.Contains("🔥", afterAdd.Keys);
            Assert.Contains("Test Author", afterAdd["🔥"]);

            var afterRemove = CoreHub.CommentManager.ToggleReaction(comment.Id, AuthorId, "🔥");
            Assert.Empty(afterRemove);
        }

        /// <summary>
        /// Reply persists with a non-null ParentCommentId.
        /// </summary>
        [Fact]
        public void Reply_StoresParentReference()
        {
            Seed(nameof(Reply_StoresParentReference));

            var parent = SampleComment("parent");
            CoreHub.CommentManager.Add(parent);

            var reply = new Comment
            {
                Id = Guid.NewGuid(),
                ObjectId = ObjectId,
                AuthorId = AuthorId,
                ParentCommentId = parent.Id,
                Content = "child reply",
                State = CommentState.Active
            };
            CoreHub.CommentManager.Add(reply);

            var loaded = CoreHub.CommentManager.GetComment(reply.Id);
            Assert.NotNull(loaded);
            Assert.Equal(parent.Id, loaded.ParentCommentId);
        }

        /// <summary>
        /// SampleComment creates a new comment with the given content or a default body.
        /// </summary>
        /// <param name="content">The content of the comment.</param>
        /// <returns>The sample comment.</returns>
        private static Comment SampleComment(string content = null) => new()
        {
            Id = Guid.NewGuid(),
            ObjectId = ObjectId,
            AuthorId = AuthorId,
            Content = content ?? "sample body",
            State = CommentState.Active
        };
    }
}
