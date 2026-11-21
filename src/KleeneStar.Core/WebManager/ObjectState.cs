using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The complete field state of an object at one commit, reconstructed by replaying its
    /// chain from the genesis commit up to that revision.
    /// </summary>
    /// <remarks>
    /// A commit stores only the fields its action touched, so no single commit describes the
    /// object as a whole. Replaying the chain — applying each commit's changes on top of the
    /// previous state — is what makes every revision inspectable without storing a redundant
    /// snapshot per commit.
    /// </remarks>
    public sealed class ObjectState
    {
        /// <summary>
        /// Gets the id of the object the state belongs to.
        /// </summary>
        public Guid ObjectId { get; init; }

        /// <summary>
        /// Gets the key the object carried at that revision, e.g. <c>INC-00123</c>.
        /// </summary>
        public string ObjectKey { get; init; }

        /// <summary>
        /// Gets the id of the commit the state was replayed up to.
        /// </summary>
        public Guid CommitId { get; init; }

        /// <summary>
        /// Gets the 1-based revision number the state was replayed up to.
        /// </summary>
        public int Number { get; init; }

        /// <summary>
        /// Gets the time the commit was appended.
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Gets whether the replayed revision is the head of the chain, i.e. whether this state
        /// equals the object's current <c>Value</c> rows.
        /// </summary>
        public bool IsHead { get; init; }

        /// <summary>
        /// Gets the stable revision reference of the state, e.g. <c>INC-00123#4</c>.
        /// </summary>
        public string Reference => $"{ObjectKey}#{Number}";

        /// <summary>
        /// Gets the fields of the object at that revision, ordered by their display label.
        /// Fields that were never populated do not appear.
        /// </summary>
        public IReadOnlyList<ObjectFieldState> Fields { get; init; } = [];

        /// <summary>
        /// Returns the state of a single field at that revision, or <c>null</c> when the field
        /// had no value.
        /// </summary>
        /// <remarks>
        /// A class field and a system property may carry the same name — a class modelling its
        /// own <c>Description</c> field beside the object's <c>description</c> is common — so the
        /// class field is answered first and the system property only when no field matches. Code
        /// that must not guess addresses the entry by <see cref="ObjectFieldState.Key"/> instead.
        /// </remarks>
        /// <param name="name">The stable name of the field or system property.</param>
        /// <returns>The field state, or <c>null</c>.</returns>
        public ObjectFieldState GetField(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var matches = Fields.Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

            return matches.FirstOrDefault(x => !x.IsSystem) ?? matches.FirstOrDefault();
        }

        /// <summary>
        /// Returns the state of a single attribute by its <see cref="ObjectFieldState.Key"/>.
        /// </summary>
        /// <param name="key">The attribute key.</param>
        /// <returns>The field state, or <c>null</c>.</returns>
        public ObjectFieldState GetByKey(string key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? null
                : Fields.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
