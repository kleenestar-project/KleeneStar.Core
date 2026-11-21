using System;
using System.Collections.Generic;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The result of walking the hash chain of the audit log: how much was checked, and where -
    /// if anywhere - it stopped adding up.
    /// </summary>
    /// <remarks>
    /// The verification is deliberately anchored at the first event it was asked to check rather
    /// than always at the genesis event. A log that has been pruned no longer has its genesis,
    /// and refusing to verify a pruned log would make retention and integrity mutually
    /// exclusive. What is checked is that every event from the anchor onwards seals correctly
    /// onto the one before it - which detects any edit, deletion, insertion or reordering inside
    /// the verified range.
    /// <para>
    /// What it cannot detect is a chain rewritten wholesale from the anchor. Guarding against
    /// that needs a hash kept where the installation cannot reach it; <see cref="HeadHash"/> is
    /// what an operator copies off-box for exactly that purpose.
    /// </para>
    /// </remarks>
    public sealed class AuditVerification
    {
        /// <summary>
        /// Gets whether every event in the verified range seals correctly onto its predecessor.
        /// </summary>
        public bool IsIntact => BrokenAt is null && MissingSequences.Count == 0;

        /// <summary>
        /// Gets the position the verification started at.
        /// </summary>
        public long FromSequence { get; init; }

        /// <summary>
        /// Gets the position the verification ended at.
        /// </summary>
        public long ToSequence { get; init; }

        /// <summary>
        /// Gets the number of events checked.
        /// </summary>
        public int Checked { get; init; }

        /// <summary>
        /// Gets the position of the first event whose seal did not match, or <see langword="null"/>
        /// when the range is intact. Everything before it is verified; everything from it on is
        /// unverifiable, because a broken link makes every hash after it meaningless.
        /// </summary>
        public long? BrokenAt { get; init; }

        /// <summary>
        /// Gets the positions the log skipped inside the verified range. A gap means events were
        /// deleted, since the sequence is assigned without gaps.
        /// </summary>
        public IReadOnlyList<long> MissingSequences { get; init; } = [];

        /// <summary>
        /// Gets the hash of the last event in the verified range. An operator who keeps a copy
        /// of this outside the installation can later prove the whole range preceding it was not
        /// rewritten.
        /// </summary>
        public string HeadHash { get; init; }

        /// <summary>
        /// Gets the moment the verification ran, in UTC.
        /// </summary>
        public DateTime Verified { get; init; } = DateTime.UtcNow;
    }
}
