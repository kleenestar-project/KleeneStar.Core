using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the lifecycle of <see cref="Comment"/> entities attached to
    /// <see cref="Object"/>s. Supports both hard deletion (<see cref="Remove(Guid)"/>)
    /// and soft deletion (<see cref="SoftDelete(Guid)"/>); the latter is the default
    /// path used by the REST endpoint so that reply threads remain navigable.
    /// </summary>
    public sealed class CommentManager : ICommentManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after a comment has been added via <see cref="Add(Comment)"/>.
        /// </summary>
        public event EventHandler<Comment> CommentAdded;

        /// <summary>
        /// Raised after a comment has been updated via <see cref="Update(Comment)"/>
        /// or <see cref="SoftDelete(Guid)"/>.
        /// </summary>
        public event EventHandler<Comment> CommentUpdated;

        /// <summary>
        /// Raised after a comment has been hard-removed via <see cref="Remove(Guid)"/>.
        /// </summary>
        public event EventHandler<Comment> CommentRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private CommentManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the comment identified by the supplied id.
        /// </summary>
        /// <param name="commentId">The comment id.</param>
        /// <returns>The comment, or <c>null</c> when no entry matches.</returns>
        public Comment GetComment(Guid commentId)
        {
            var query = new Query<Comment>()
                .Where(x => x.Id == commentId)
                .WithPaging(0, 1);

            return ModelHub.GetComments(query).FirstOrDefault();
        }

        /// <summary>
        /// Returns the comment identified by the supplied URL-bound id parameter.
        /// </summary>
        /// <param name="commentId">The id parameter parsed from the URL path.</param>
        /// <returns>The comment, or <c>null</c> when no entry matches.</returns>
        public Comment GetComment(CommentIdParameter commentId)
        {
            ArgumentNullException.ThrowIfNull(commentId);

            var guid = Guid.TryParse(commentId.Value, out var id) ? id : Guid.Empty;

            return GetComment(guid);
        }

        /// <summary>
        /// Returns every comment attached to the object addressed by the supplied
        /// URL-bound object-key parameter, in chronological order (oldest first).
        /// </summary>
        /// <param name="objectKey">The object-key parameter.</param>
        /// <returns>The comments attached to the object. The collection may be empty.</returns>
        public IEnumerable<Comment> GetComments(ObjectKeyParameter objectKey)
        {
            ArgumentNullException.ThrowIfNull(objectKey);

            // through the object manager, so a key the caller may not read - by its grants
            // or its security level - has nothing to show
            var obj = CoreHub.ObjectManager.GetObjectByKey(objectKey.Value);
            if (obj is null)
            {
                return [];
            }

            // the thread a caller reads is the one the audience rule leaves them
            return WebPermission.CommentAudience.Filter(obj, GetComments(obj.Id)).ToList();
        }

        /// <summary>
        /// Returns every comment attached to the object with the supplied id, in
        /// chronological order (oldest first).
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The comments attached to the object. The collection may be empty.</returns>
        public IEnumerable<Comment> GetComments(Guid objectId)
        {
            var query = new Query<Comment>()
                .WhereEquals(x => x.ObjectId, objectId);

            return ModelHub.GetComments(query).OrderBy(c => c.Created).ToList();
        }

        /// <summary>
        /// Returns the comments that satisfy the supplied query. The manager opens its
        /// own DbContext for the call.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching comments.</returns>
        public IEnumerable<Comment> GetComments(IQuery<Comment> query)
        {
            return ModelHub.GetComments(query);
        }

        /// <summary>
        /// Returns the comments that satisfy the supplied query, executed inside the
        /// supplied <see cref="IQueryContext"/> (expected to be a
        /// <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching comments.</returns>
        public IEnumerable<Comment> GetComments(IQuery<Comment> query, IQueryContext context)
        {
            return ModelHub.GetComments(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds the supplied comment to the database, raises <see cref="CommentAdded"/>,
        /// and emits a UI notification. Returns the manager instance to allow chaining.
        /// </summary>
        /// <param name="comment">The comment to add.</param>
        /// <returns>The current manager instance.</returns>
        public ICommentManager Add(Comment comment)
        {
            ArgumentNullException.ThrowIfNull(comment);

            ModelHub.Add(comment);
            CommentAdded?.Invoke(this, comment);
            TryAddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.comment.created", CoreHub.ObjectManager.GetObject(comment.ObjectId));

            return this;
        }

        /// <summary>
        /// Persists the supplied comment's content and state changes. If the content
        /// changed, the state is bumped to <see cref="CommentState.Edited"/> so the
        /// UI can render an "(edited)" marker. Raises <see cref="CommentUpdated"/>.
        /// </summary>
        /// <param name="comment">The comment to update.</param>
        /// <returns>The current manager instance.</returns>
        public ICommentManager Update(Comment comment)
        {
            ArgumentNullException.ThrowIfNull(comment);

            // bump to "Edited" so the badge shows even if the caller forgot to set it
            if (comment.State == CommentState.Active)
            {
                comment.State = CommentState.Edited;
            }

            ModelHub.Update(comment);
            CommentUpdated?.Invoke(this, comment);
            TryAddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.comment.updated", CoreHub.ObjectManager.GetObject(comment.ObjectId));

            return this;
        }

        /// <summary>
        /// Soft-deletes the comment identified by the supplied id: sets its
        /// <see cref="Comment.State"/> to <see cref="CommentState.Deleted"/>, blanks
        /// the content (so the deleted text is not retained), and records the
        /// deletion timestamp. Raises <see cref="CommentUpdated"/>. No-op when the
        /// comment does not exist.
        /// </summary>
        /// <param name="commentId">The comment id.</param>
        /// <returns>The current manager instance.</returns>
        public ICommentManager SoftDelete(Guid commentId)
        {
            var existing = GetComment(commentId);
            if (existing is null)
            {
                return this;
            }

            existing.State = CommentState.Deleted;
            existing.DeletedAt = DateTime.UtcNow;
            existing.Content = string.Empty;

            ModelHub.Update(existing);
            CommentUpdated?.Invoke(this, existing);
            TryAddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.comment.deleted", CoreHub.ObjectManager.GetObject(existing.ObjectId));

            return this;
        }

        /// <summary>
        /// Hard-removes the comment identified by the supplied id. Raises
        /// <see cref="CommentRemoved"/>. Use <see cref="SoftDelete(Guid)"/> instead
        /// when the comment may have replies.
        /// </summary>
        /// <param name="commentId">The comment id.</param>
        /// <returns>The current manager instance.</returns>
        public ICommentManager Remove(Guid commentId)
        {
            var existing = GetComment(commentId);

            if (existing is not null)
            {
                ModelHub.Remove(existing);
                CommentRemoved?.Invoke(this, existing);
            }

            return this;
        }

        /// <summary>
        /// Toggles the <see cref="Comment.IsPinned"/> flag of the comment with the
        /// supplied id.
        /// </summary>
        /// <param name="commentId">The id of the comment to pin / unpin.</param>
        /// <returns>The new pin state, or <c>null</c> when no comment matches.</returns>
        public bool? TogglePin(Guid commentId)
        {
            using var db = ModelHub.CreateDbContext();
            var comment = db.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment is null)
            {
                return null;
            }

            comment.IsPinned = !comment.IsPinned;
            comment.Updated = DateTime.UtcNow;
            db.SaveChanges();

            CommentUpdated?.Invoke(this, comment);
            TryAddNotification("kleenestar.core:notification.title.pinned", "kleenestar.core:notification.comment.pinned", CoreHub.ObjectManager.GetObject(comment.ObjectId));

            return comment.IsPinned;
        }

        /// <summary>
        /// Toggles a like on the comment for the supplied identity. Returns the names of
        /// every identity that currently likes the comment.
        /// </summary>
        /// <param name="commentId">The comment id.</param>
        /// <param name="authorId">The identity authoring the like toggle.</param>
        /// <returns>The current likers.</returns>
        public IEnumerable<string> ToggleLike(Guid commentId, Guid authorId)
        {
            using var db = ModelHub.CreateDbContext();
            var comment = db.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment is null)
            {
                return [];
            }

            var existing = db.CommentLikes
                .FirstOrDefault(l => l.CommentId == commentId && l.AuthorId == authorId);

            if (existing is null)
            {
                db.CommentLikes.Add(new CommentLike
                {
                    CommentId = commentId,
                    AuthorId = authorId,
                    Created = DateTime.UtcNow
                });
            }
            else
            {
                db.CommentLikes.Remove(existing);
            }

            db.SaveChanges();

            CommentUpdated?.Invoke(this, comment);

            return db.CommentLikes
                .Include(l => l.Author)
                .Where(l => l.CommentId == commentId)
                .OrderBy(l => l.Created)
                .Select(l => l.Author.Name)
                .ToList();
        }

        /// <summary>
        /// Toggles an emoji reaction on the comment for the supplied identity. Returns
        /// the full reaction map after the toggle.
        /// </summary>
        /// <param name="commentId">The comment id.</param>
        /// <param name="authorId">The identity authoring the reaction toggle.</param>
        /// <param name="emoji">The emoji to toggle.</param>
        /// <returns>The current reaction map.</returns>
        public IDictionary<string, IEnumerable<string>> ToggleReaction(Guid commentId, Guid authorId, string emoji)
        {
            if (string.IsNullOrEmpty(emoji))
            {
                return new Dictionary<string, IEnumerable<string>>();
            }

            using var db = ModelHub.CreateDbContext();
            var comment = db.Comments.FirstOrDefault(c => c.Id == commentId);
            if (comment is null)
            {
                return new Dictionary<string, IEnumerable<string>>();
            }

            var existing = db.CommentReactions
                .FirstOrDefault(r => r.CommentId == commentId && r.AuthorId == authorId && r.Emoji == emoji);

            if (existing is null)
            {
                db.CommentReactions.Add(new CommentReaction
                {
                    CommentId = commentId,
                    AuthorId = authorId,
                    Emoji = emoji,
                    Created = DateTime.UtcNow
                });
            }
            else
            {
                db.CommentReactions.Remove(existing);
            }

            db.SaveChanges();

            CommentUpdated?.Invoke(this, comment);

            var reactions = db.CommentReactions
                .Include(r => r.Author)
                .Where(r => r.CommentId == commentId)
                .OrderBy(r => r.Created)
                .ToList();

            return reactions
                .GroupBy(r => r.Emoji)
                .ToDictionary(
                    g => g.Key,
                    g => (IEnumerable<string>)g.Select(r => r.Author.Name).ToList());
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Emits a UI notification via <see cref="CoreHub.AddNotification"/>, swallowing
        /// any exception so that tests with a partially wired host don't crash.
        /// </summary>
        /// <param name="titleKey">The i18n key of the notification title.</param>
        /// <param name="messageKey">The i18n key of the notification message.</param>
        private static void TryAddNotification(string titleKey, string messageKey, object subject)
        {
            try
            {
                CoreHub.AddNotification(titleKey, messageKey, subject);
            }
            catch
            {
                // notification is best-effort; ignore failures from incomplete host state
            }
        }
    }
}
