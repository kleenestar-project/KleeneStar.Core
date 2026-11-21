using KleeneStar.Model.Entities;
using System;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The outcome of reapplying a historical state, as produced by
    /// <see cref="ICommitManager.RestoreCommit"/>.
    /// </summary>
    /// <remarks>
    /// A restore never rewinds the chain. It reads the state at the requested revision, writes
    /// those values back, and appends a new <see cref="CommitType.Restored"/> commit describing
    /// what it changed — so the fact that a restore happened is itself part of the history.
    /// </remarks>
    public sealed class CommitRestoreResult
    {
        /// <summary>
        /// Gets the id of the object whose state was reapplied.
        /// </summary>
        public Guid ObjectId { get; init; }

        /// <summary>
        /// Gets the key of the object.
        /// </summary>
        public string ObjectKey { get; init; }

        /// <summary>
        /// Gets the revision number whose state was reapplied.
        /// </summary>
        public int RestoredNumber { get; init; }

        /// <summary>
        /// Gets the commit the restore appended, or <c>null</c> when the requested revision
        /// already matched the current state and nothing had to be written.
        /// </summary>
        public Commit Commit { get; init; }

        /// <summary>
        /// Gets whether the restore changed anything.
        /// </summary>
        public bool Changed => Commit is not null;
    }
}
