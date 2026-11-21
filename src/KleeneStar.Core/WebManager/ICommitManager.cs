using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for the exclusive owner of the object commit chains — the
    /// append-only history from which every past state of an object can be reconstructed.
    /// </summary>
    /// <remarks>
    /// The manager owns both halves of a change: it appends the commit describing it and writes
    /// the <see cref="Value"/> rows it produced, in one transaction. That is what guarantees the
    /// invariant the whole concept rests on — the head of the chain and the object's current
    /// values can never diverge. No mutation path may write a value row past this manager; the
    /// <see cref="IValueManager"/> routes every write through it.
    /// <para>
    /// Recording is driven by <see cref="BeginCommit"/>: the scope collects everything one user
    /// action did to one object and closes it into a single commit. A value written with no
    /// scope open still lands in the history, as a commit of its own.
    /// </para>
    /// </remarks>
    public interface ICommitManager : IComponentManager
    {
        /// <summary>
        /// Raised after a commit has been appended to an object's chain.
        /// </summary>
        event EventHandler<Commit> CommitAdded;

        /// <summary>
        /// Raised after a historical state has been reapplied as a new commit.
        /// </summary>
        event EventHandler<CommitRestoreResult> CommitRestored;

        /// <summary>
        /// Raised after a difference between two revisions has been computed.
        /// </summary>
        event EventHandler<CommitDiff> CommitDiffed;

        /// <summary>
        /// Gets every commit the manager holds, newest first. Intended for administrative and
        /// diagnostic surfaces; object-scoped callers use <see cref="GetHistory"/>.
        /// </summary>
        IEnumerable<Commit> Commits { get; }

        /// <summary>
        /// Opens the unit of work one action forms on one object. Every change reported while
        /// the scope is open joins the same commit, and the value rows written inside it are
        /// persisted together with that commit when the scope closes.
        /// </summary>
        /// <remarks>
        /// Opening a scope for an object that already has one joins the existing scope, so a
        /// manager may open one for its own operation without knowing what the caller did.
        /// </remarks>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="type">The action being recorded.</param>
        /// <param name="identityId">The identity performing the action, or <see cref="Guid.Empty"/>.</param>
        /// <param name="message">An optional message describing the intent of the change.</param>
        /// <returns>The scope. Dispose it to write, or abort it to discard.</returns>
        ICommitScope BeginCommit(Guid objectId, CommitType type, Guid identityId, string message = null);

        /// <summary>
        /// Appends a commit to an object's chain directly, without a scope. Used by callers that
        /// already know the complete set of changes.
        /// </summary>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="type">The action being recorded.</param>
        /// <param name="changes">The field modifications the action produced. May be empty.</param>
        /// <param name="identityId">The identity performing the action, or <see cref="Guid.Empty"/>.</param>
        /// <param name="message">An optional message describing the intent of the change.</param>
        /// <returns>The appended commit, or <c>null</c> when the object does not exist.</returns>
        Commit AddCommit(Guid objectId, CommitType type, IEnumerable<Change> changes, Guid identityId, string message = null);

        /// <summary>
        /// Records a single field modification against the ambient scope of the object, or — when
        /// no scope is open for it — as a commit of its own.
        /// </summary>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="change">The field modification.</param>
        /// <param name="type">The action to record when no scope is open.</param>
        /// <param name="identityId">The identity performing the action, or <see cref="Guid.Empty"/>.</param>
        void Record(Guid objectId, Change change, CommitType type, Guid identityId);

        /// <summary>
        /// Returns the commit chain of an object, newest first.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The chain, newest first. Empty when the object has no history.</returns>
        IEnumerable<Commit> GetHistory(Guid objectId);

        /// <summary>
        /// Returns the commit chain of an object filtered by a query, newest first.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching commits.</returns>
        IEnumerable<Commit> GetCommits(IQuery<Commit> query);

        /// <summary>
        /// Returns the commit chain of an object filtered by a query, executed inside the
        /// supplied query context.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching commits.</returns>
        IEnumerable<Commit> GetCommits(IQuery<Commit> query, IQueryContext context);

        /// <summary>
        /// Returns one revision of an object's chain.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="number">The 1-based revision number.</param>
        /// <returns>The commit, or <c>null</c> when the chain has no such revision.</returns>
        Commit GetCommit(Guid objectId, int number);

        /// <summary>
        /// Returns a commit by its unique identifier.
        /// </summary>
        /// <param name="commitId">The commit id.</param>
        /// <returns>The commit, or <c>null</c> when no commit matches.</returns>
        Commit GetCommit(Guid commitId);

        /// <summary>
        /// Returns the head of an object's chain — the commit whose state matches the object's
        /// current values.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The head commit, or <c>null</c> when the object has no history.</returns>
        Commit GetHead(Guid objectId);

        /// <summary>
        /// Returns the complete field state of an object at one revision, reconstructed by
        /// replaying its chain up to that commit.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="number">The 1-based revision number.</param>
        /// <returns>The replayed state, or <c>null</c> when the chain has no such revision.</returns>
        ObjectState GetStateAt(Guid objectId, int number);

        /// <summary>
        /// Returns the aggregated field difference between two revisions of an object and raises
        /// <see cref="CommitDiffed"/>.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="from">The revision the comparison starts at.</param>
        /// <param name="to">The revision the comparison ends at.</param>
        /// <returns>The difference, or <c>null</c> when either revision does not exist.</returns>
        CommitDiff DiffCommits(Guid objectId, int from, int to);

        /// <summary>
        /// Reapplies the field values an object held at one revision and appends the resulting
        /// <see cref="CommitType.Restored"/> commit, preserving the append-only chain.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="number">The 1-based revision number whose state is reapplied.</param>
        /// <param name="identityId">The identity performing the restore.</param>
        /// <returns>
        /// The outcome, or <c>null</c> when the object or the revision does not exist. A restore
        /// of the head changes nothing and reports <see cref="CommitRestoreResult.Changed"/> as
        /// <see langword="false"/>.
        /// </returns>
        CommitRestoreResult RestoreCommit(Guid objectId, int number, Guid identityId);
    }
}
