using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages files attached to objects.
    /// </summary>
    public sealed class AttachmentManager : IAttachmentManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after an attachment has been added via <see cref="Add"/>.
        /// </summary>
        public event EventHandler<Attachment> AttachmentAdded;

        /// <summary>
        /// Raised after the metadata of an attachment has been changed via
        /// <see cref="SetDescription"/>.
        /// </summary>
        public event EventHandler<Attachment> AttachmentUpdated;

        /// <summary>
        /// Raised after an attachment has been removed via <see cref="Remove"/>.
        /// </summary>
        public event EventHandler<Attachment> AttachmentRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private AttachmentManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns every visible attachment of the object addressed by the supplied
        /// URL-bound object-key parameter, in chronological order (oldest first).
        /// </summary>
        /// <param name="objectKey">The object-key parameter.</param>
        /// <returns>The attachments of the object. The collection may be empty.</returns>
        public IEnumerable<Attachment> GetAttachments(ObjectKeyParameter objectKey)
        {
            ArgumentNullException.ThrowIfNull(objectKey);

            // through the object manager, so a key the caller may not read - by its grants
            // or its security level - has nothing to show
            var obj = CoreHub.ObjectManager.GetObjectByKey(objectKey.Value);
            if (obj is null)
            {
                return [];
            }

            return GetAttachments(obj.Id);
        }

        /// <summary>
        /// Returns every visible attachment of the object with the supplied id, in
        /// chronological order (oldest first).
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The attachments of the object. The collection may be empty.</returns>
        public IEnumerable<Attachment> GetAttachments(Guid objectId)
        {
            return ModelHub.GetAttachmentsByObject(objectId)
                .Where(a => a.State.IsVisible())
                .ToList();
        }

        /// <summary>
        /// Returns the single attachment with the supplied id including its binary
        /// <see cref="Attachment.Content"/>, or <see langword="null"/> when not found.
        /// </summary>
        /// <param name="attachmentId">The attachment id.</param>
        /// <returns>The attachment with its payload, or <see langword="null"/>.</returns>
        public Attachment GetAttachment(Guid attachmentId)
        {
            return ModelHub.GetAttachment(attachmentId);
        }

        /// <summary>
        /// Returns the attachments that satisfy the supplied query. The manager opens its
        /// own DbContext for the call.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching attachments.</returns>
        public IEnumerable<Attachment> GetAttachments(IQuery<Attachment> query)
        {
            return ModelHub.GetAttachments(query);
        }

        /// <summary>
        /// Returns the attachments that satisfy the supplied query, executed inside the
        /// supplied <see cref="IQueryContext"/> (expected to be a <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching attachments.</returns>
        public IEnumerable<Attachment> GetAttachments(IQuery<Attachment> query, IQueryContext context)
        {
            return ModelHub.GetAttachments(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Returns every version of the supplied file name attached to the object, oldest
        /// version first.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <param name="fileName">The file name whose chain is read.</param>
        /// <returns>The versions of the file. The collection may be empty.</returns>
        public IEnumerable<Attachment> GetVersions(Guid objectId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return [];
            }

            return GetAttachments(objectId)
                .Where(a => string.Equals(a.FileName, fileName, StringComparison.Ordinal))
                .OrderBy(a => a.Version)
                .ThenBy(a => a.Created)
                .ToList();
        }

        /// <summary>
        /// Attaches a file to the object. Returns <see langword="null"/> when the object
        /// does not exist or the file name is empty.
        /// </summary>
        /// <remarks>
        /// The name is the identity of a file across its versions, so attaching a name the object
        /// already carries stores the next version of that file rather than a second file; the
        /// number itself is assigned by <see cref="ModelHub.Add"/>, which reads the chain and
        /// writes the row against one context. A new version inherits the description of the one
        /// it supersedes unless the caller supplies its own, because the description says what the
        /// <i>file</i> is - re-uploading it does not make that unknown again.
        /// </remarks>
        /// <param name="objectId">The id of the object the file is attached to.</param>
        /// <param name="fileName">The original file name including its extension.</param>
        /// <param name="contentType">The MIME content type of the file.</param>
        /// <param name="content">The binary payload of the file. The size is derived from it.</param>
        /// <param name="description">An optional human-readable description, or <c>null</c>.</param>
        /// <param name="uploaderId">The id of the uploading identity, or <c>null</c>.</param>
        /// <returns>The persisted attachment, or <see langword="null"/>.</returns>
        public Attachment Add(Guid objectId, string fileName, string contentType, byte[] content, string description, Guid? uploaderId)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            using var db = ModelHub.CreateDbContext();

            if (!db.Objects.AsNoTracking().Any(o => o.Id == objectId))
            {
                return null;
            }

            var attachment = new Attachment
            {
                ObjectId = objectId,
                UploaderId = uploaderId,
                FileName = fileName,
                ContentType = contentType,
                Size = content?.LongLength ?? 0,
                Content = content,
                Description = description ?? GetVersions(objectId, fileName).LastOrDefault()?.Description,
                State = AttachmentState.Active,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            ModelHub.Add(attachment);
            AttachmentAdded?.Invoke(this, attachment);
            Announce();
            TryAddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.attachment.created", CoreHub.ObjectManager.GetObject(objectId));

            return attachment;
        }

        /// <summary>
        /// Changes the human-readable description of an attachment. Raises
        /// <see cref="AttachmentUpdated"/> when a row existed.
        /// </summary>
        /// <remarks>
        /// This is the write path behind the in-place editor of the file surfaces. An earlier
        /// version keeps the description it was given - it is a record of what was - so the edit a
        /// user makes lands on the row they named rather than on the chain.
        /// </remarks>
        /// <param name="attachmentId">The id of the attachment whose description changes.</param>
        /// <param name="description">The new description. An empty or blank value clears it.</param>
        /// <returns>The changed attachment, or <see langword="null"/> when no row matches.</returns>
        public Attachment SetDescription(Guid attachmentId, string description)
        {
            var next = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            // the write goes straight to the column instead of through a loaded entity, so
            // editing a caption never reads the file's payload back out of the database
            var changed = ModelHub.SetAttachmentDescription(attachmentId, next);

            if (changed is null)
            {
                return null;
            }

            AttachmentUpdated?.Invoke(this, changed);
            Announce();

            return changed;
        }

        /// <summary>
        /// Removes the attachment with the supplied id. Raises <see cref="AttachmentRemoved"/>
        /// when a row existed.
        /// </summary>
        /// <param name="attachmentId">The id of the attachment to remove.</param>
        /// <returns><see langword="true"/> when a row existed and was removed.</returns>
        public bool Remove(Guid attachmentId)
        {
            using var db = ModelHub.CreateDbContext();
            var existing = db.Attachments.FirstOrDefault(a => a.Id == attachmentId);

            if (existing is null)
            {
                return false;
            }

            ModelHub.Remove(existing);
            AttachmentRemoved?.Invoke(this, existing);
            Announce();
            TryAddNotification("kleenestar.core:notification.title.deleted", "kleenestar.core:notification.attachment.deleted", CoreHub.ObjectManager.GetObject(existing.ObjectId));

            return true;
        }

        /// <summary>
        /// Announces that the attachments changed, so every file surface currently on screen
        /// re-queries its endpoint.
        /// </summary>
        /// <remarks>
        /// The REST endpoint the file view reads is not the one that wrote the change - an upload
        /// posts to the page route and an edit to the attachment endpoint - so nothing else
        /// announces it. Without this a file another user attached, or a caption they corrected,
        /// stays invisible until the page is loaded again.
        /// </remarks>
        private static void Announce()
        {
            try
            {
                _ = DataChangedNotifier.NotifyAsync<Attachment>(DataChangeOperation.Updated);
            }
            catch
            {
                // the announcement is best-effort; a host without a message queue must not turn a
                // successful write into a failed request
            }
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Emits a UI notification via <see cref="CoreHub.AddNotification"/>, swallowing any
        /// exception so that tests with a partially wired host don't crash.
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
