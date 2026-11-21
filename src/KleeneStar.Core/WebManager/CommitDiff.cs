using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The aggregated field difference between two revisions of the same object, as produced by
    /// <see cref="ICommitManager.DiffCommits"/>.
    /// </summary>
    /// <remarks>
    /// The difference is computed over the replayed states rather than by concatenating the
    /// commits in between: a field that was changed and changed back appears in two commits but
    /// carries no difference between the two revisions, and only the state comparison says so.
    /// </remarks>
    public sealed class CommitDiff
    {
        /// <summary>
        /// Gets the id of the object the two revisions belong to.
        /// </summary>
        public Guid ObjectId { get; init; }

        /// <summary>
        /// Gets the key of the object.
        /// </summary>
        public string ObjectKey { get; init; }

        /// <summary>
        /// Gets the revision number the comparison starts at.
        /// </summary>
        public int From { get; init; }

        /// <summary>
        /// Gets the revision number the comparison ends at.
        /// </summary>
        public int To { get; init; }

        /// <summary>
        /// Gets the fields that differ between the two revisions, each carrying the value at
        /// <see cref="From"/> as its old value and the value at <see cref="To"/> as its new one.
        /// The entries are transient and never persisted.
        /// </summary>
        public IReadOnlyList<Change> Changes { get; init; } = [];
    }
}
