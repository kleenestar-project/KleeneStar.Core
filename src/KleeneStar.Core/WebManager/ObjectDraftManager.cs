using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the unpublished working copies of the prose attributes of objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The manager is deliberately <b>not</b> wired into the audit log by
    /// <see cref="AuditManager.Connect"/>: the editor saves a draft on every pause in typing, so
    /// subscribing it would bury the installation-wide log under keystroke batches that say
    /// nothing about what was decided. The moment that is worth recording is the publish, and
    /// that travels the ordinary path - <see cref="IObjectManager.Update"/> writes a commit, and
    /// the audit log picks it up from <c>CommitManager.CommitAdded</c> with the exact
    /// before/after of the published text.
    /// </para>
    /// </remarks>
    public sealed class ObjectDraftManager : IObjectDraftManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after a draft has been written by <see cref="Save"/>.
        /// </summary>
        public event EventHandler<ObjectDraft> DraftSaved;

        /// <summary>
        /// Raised after a draft has been dropped by <see cref="Discard"/>.
        /// </summary>
        public event EventHandler<ObjectDraft> DraftDiscarded;

        /// <summary>
        /// Raised after a draft has been published onto its object by <see cref="Publish"/>.
        /// </summary>
        public event EventHandler<ObjectDraft> DraftPublished;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectDraftManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the draft of the supplied object, or <see langword="null"/> when the object
        /// carries no unpublished changes.
        /// </summary>
        /// <param name="objectId">The id of the object whose draft is read.</param>
        /// <returns>The draft, or <see langword="null"/>.</returns>
        public ObjectDraft GetDraft(Guid objectId)
        {
            return objectId == Guid.Empty ? null : ModelHub.GetObjectDraft(objectId);
        }

        /// <summary>
        /// Returns the drafts that satisfy the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching drafts.</returns>
        public IEnumerable<ObjectDraft> GetDrafts(IQuery<ObjectDraft> query)
        {
            return ModelHub.GetObjectDrafts(query);
        }

        /// <summary>
        /// Returns the drafts that satisfy the supplied query, executed inside the supplied
        /// query context.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching drafts.</returns>
        public IEnumerable<ObjectDraft> GetDrafts(IQuery<ObjectDraft> query, IQueryContext context)
        {
            return ModelHub.GetObjectDrafts(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Reports whether the supplied object carries unpublished changes.
        /// </summary>
        /// <param name="objectId">The id of the object to test.</param>
        /// <returns><see langword="true"/> when a draft exists.</returns>
        public bool HasDraft(Guid objectId)
        {
            return GetDraft(objectId) is not null;
        }

        /// <summary>
        /// Returns the prose the editor is to open on: the draft when one exists, the published
        /// values of the object otherwise.
        /// </summary>
        /// <param name="objectId">The id of the object to open.</param>
        /// <returns>The title, the body, whether they came from a draft, and when that draft was
        /// last written.</returns>
        public (string Summary, string Description, bool IsDraft, DateTime? Updated) GetEffective(Guid objectId)
        {
            var @object = CoreHub.ObjectManager.GetObject(objectId);

            if (@object is null)
            {
                return (null, null, false, null);
            }

            var draft = GetDraft(objectId);

            return draft is null
                ? (@object.Summary, @object.Description, false, null)

                // a draft column left null means "unchanged", so the published value stands in
                // for it rather than blanking the field the editor opens on
                : (draft.Summary ?? @object.Summary, draft.Description ?? @object.Description, true, draft.Updated);
        }

        /// <summary>
        /// Writes the supplied prose as the unpublished draft of the object.
        /// </summary>
        /// <param name="objectId">The id of the object being drafted.</param>
        /// <param name="summary">The unpublished title.</param>
        /// <param name="description">The unpublished rich-text body.</param>
        /// <param name="identityId">The identity writing the change.</param>
        /// <returns>The persisted draft, or <see langword="null"/> when the object does not
        /// exist.</returns>
        public ObjectDraft Save(Guid objectId, string summary, string description, Guid identityId)
        {
            if (objectId == Guid.Empty)
            {
                return null;
            }

            var draft = ModelHub.UpsertObjectDraft
            (
                objectId,
                summary,
                description,
                identityId == Guid.Empty ? null : identityId
            );

            if (draft is not null)
            {
                DraftSaved?.Invoke(this, draft);
            }

            return draft;
        }

        /// <summary>
        /// Drops the draft of the object without touching the published text.
        /// </summary>
        /// <param name="objectId">The id of the object whose draft is dropped.</param>
        /// <returns><see langword="true"/> when a draft existed and was dropped.</returns>
        public bool Discard(Guid objectId)
        {
            var draft = GetDraft(objectId);

            if (draft is null || !ModelHub.RemoveObjectDraft(objectId))
            {
                return false;
            }

            DraftDiscarded?.Invoke(this, draft);

            return true;
        }

        /// <summary>
        /// Publishes the draft: copies its prose onto the object as one commit and drops the
        /// draft row.
        /// </summary>
        /// <remarks>
        /// The publish is one commit, opened here so the history says what happened rather than
        /// "updated": <see cref="IObjectManager.Update"/> opens a scope of its own, which nests
        /// into this one and inherits its message. The draft row is dropped only after the
        /// object write returned, so an object that fails to save keeps the text that was
        /// meant for it.
        /// </remarks>
        /// <param name="objectId">The id of the object to publish.</param>
        /// <param name="summary">The title to publish, or <c>null</c> to publish the draft's.</param>
        /// <param name="description">The body to publish, or <c>null</c> to publish the draft's.</param>
        /// <param name="identityId">The publishing identity.</param>
        /// <returns>The published object, or <see langword="null"/> when it does not exist.</returns>
        public Model.Entities.Object Publish(Guid objectId, string summary, string description, Guid identityId)
        {
            var @object = CoreHub.ObjectManager.GetObject(objectId);

            if (@object is null)
            {
                return null;
            }

            var draft = GetDraft(objectId);

            @object.Summary = summary ?? draft?.Summary ?? @object.Summary;
            @object.Description = description ?? draft?.Description ?? @object.Description;

            if (identityId != Guid.Empty)
            {
                @object.UpdaterId = identityId;
            }

            using (CoreHub.CommitManager.BeginCommit
            (
                objectId,
                CommitType.Updated,
                identityId,
                "kleenestar.core:object.draft.commit.published"
            ))
            {
                CoreHub.ObjectManager.Update(@object);
            }

            if (draft is not null && ModelHub.RemoveObjectDraft(objectId))
            {
                DraftPublished?.Invoke(this, draft);
            }

            return @object;
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
