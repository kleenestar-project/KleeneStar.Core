using KleeneStar.Model.Entities;
using System;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The value of a single field of an object at one commit, as produced by replaying the
    /// commit chain.
    /// </summary>
    public sealed class ObjectFieldState
    {
        /// <summary>
        /// Gets the stable name of the attribute: the <see cref="Field.Name"/> of a class field,
        /// or the lower-case name of a system property of the object.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets the id of the class field, or <c>null</c> when the attribute is a system property
        /// of the object or the field has since been deleted.
        /// </summary>
        public Guid? FieldId { get; init; }

        /// <summary>
        /// Gets the label the attribute is shown under. Resolves to the field definition when it
        /// still exists, and falls back to the recorded name otherwise, so a deleted field keeps
        /// a readable entry in the history.
        /// </summary>
        public string Label { get; init; }

        /// <summary>
        /// Gets the serialized value at that revision, or <c>null</c> when the attribute was
        /// cleared.
        /// </summary>
        public string Value { get; init; }

        /// <summary>
        /// Gets whether the attribute is a system property of the object (summary, assignee, …)
        /// rather than a class field.
        /// </summary>
        public bool IsSystem => !FieldId.HasValue;

        /// <summary>
        /// Gets the identity of the attribute inside a state: the field id for a class field, and
        /// the prefixed name for a system property.
        /// </summary>
        /// <remarks>
        /// The name alone will not do. A class may model a field called <c>Description</c> beside
        /// the object's own <c>description</c>, and the seeded classes do exactly that — keying
        /// a replay by name would let one silently overwrite the other and the reconstructed
        /// state would be missing a field it had.
        /// </remarks>
        public string Key => FieldId.HasValue
            ? FieldId.Value.ToString()
            : string.Concat("system:", (Name ?? string.Empty).ToLowerInvariant());
    }
}
