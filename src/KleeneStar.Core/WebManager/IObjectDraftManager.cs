using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the unpublished working copies (<see cref="ObjectDraft"/>) of the prose
    /// attributes of objects: the text the editor of a document or a blog post keeps writing
    /// while the reading view keeps showing the last published version.
    /// </summary>
    /// <remarks>
    /// The manager owns the whole life cycle of a draft - it is opened by the first
    /// <see cref="Save"/>, survives an abandoned editor, is what <see cref="GetEffective"/>
    /// hands the editor when it opens again, and ends either in <see cref="Publish"/> (copied
    /// onto the object as a commit) or in <see cref="Discard"/> (dropped, published text
    /// untouched).
    /// </remarks>
    public interface IObjectDraftManager : IComponentManager
    {
        /// <summary>
        /// Raised after a draft has been written by <see cref="Save"/>.
        /// </summary>
        event EventHandler<ObjectDraft> DraftSaved;

        /// <summary>
        /// Raised after a draft has been dropped by <see cref="Discard"/>.
        /// </summary>
        event EventHandler<ObjectDraft> DraftDiscarded;

        /// <summary>
        /// Raised after a draft has been published onto its object by <see cref="Publish"/>.
        /// </summary>
        event EventHandler<ObjectDraft> DraftPublished;

        /// <summary>
        /// Returns the draft of the supplied object, or <see langword="null"/> when the object
        /// carries no unpublished changes.
        /// </summary>
        /// <param name="objectId">The id of the object whose draft is read.</param>
        /// <returns>The draft, or <see langword="null"/>.</returns>
        ObjectDraft GetDraft(Guid objectId);

        /// <summary>
        /// Returns the drafts that satisfy the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching drafts.</returns>
        IEnumerable<ObjectDraft> GetDrafts(IQuery<ObjectDraft> query);

        /// <summary>
        /// Returns the drafts that satisfy the supplied query, executed inside the supplied
        /// query context.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching drafts.</returns>
        IEnumerable<ObjectDraft> GetDrafts(IQuery<ObjectDraft> query, IQueryContext context);

        /// <summary>
        /// Reports whether the supplied object carries unpublished changes.
        /// </summary>
        /// <param name="objectId">The id of the object to test.</param>
        /// <returns><see langword="true"/> when a draft exists.</returns>
        bool HasDraft(Guid objectId);

        /// <summary>
        /// Returns the prose the editor is to open on: the draft when one exists, the published
        /// values of the object otherwise. This is the single place that decides "editing loads
        /// the draft", so no caller has to fall back by hand.
        /// </summary>
        /// <param name="objectId">The id of the object to open.</param>
        /// <returns>The title, the body, whether they came from a draft, and when that draft was
        /// last written; all <c>null</c>/<c>false</c> when the object does not exist.</returns>
        (string Summary, string Description, bool IsDraft, DateTime? Updated) GetEffective(Guid objectId);

        /// <summary>
        /// Writes the supplied prose as the unpublished draft of the object, opening the draft
        /// on the first call and overwriting it on every later one. The published object is not
        /// touched and no commit is written - a draft is not a revision.
        /// </summary>
        /// <param name="objectId">The id of the object being drafted.</param>
        /// <param name="summary">The unpublished title.</param>
        /// <param name="description">The unpublished rich-text body.</param>
        /// <param name="identityId">The identity writing the change, or
        /// <see cref="Guid.Empty"/> when unauthenticated.</param>
        /// <returns>The persisted draft, or <see langword="null"/> when the object does not
        /// exist.</returns>
        ObjectDraft Save(Guid objectId, string summary, string description, Guid identityId);

        /// <summary>
        /// Drops the draft of the object without touching the published text.
        /// </summary>
        /// <param name="objectId">The id of the object whose draft is dropped.</param>
        /// <returns><see langword="true"/> when a draft existed and was dropped.</returns>
        bool Discard(Guid objectId);

        /// <summary>
        /// Publishes the draft: copies its prose onto the object as one commit and drops the
        /// draft row. When the object carries no draft the supplied values are published
        /// directly, so a publish that arrives before the first autosave still lands.
        /// </summary>
        /// <param name="objectId">The id of the object to publish.</param>
        /// <param name="summary">The title to publish, or <c>null</c> to publish what the draft
        /// holds.</param>
        /// <param name="description">The body to publish, or <c>null</c> to publish what the
        /// draft holds.</param>
        /// <param name="identityId">The publishing identity, or <see cref="Guid.Empty"/>.</param>
        /// <returns>The published object, or <see langword="null"/> when it does not exist.</returns>
        Model.Entities.Object Publish(Guid objectId, string summary, string description, Guid identityId);
    }
}
