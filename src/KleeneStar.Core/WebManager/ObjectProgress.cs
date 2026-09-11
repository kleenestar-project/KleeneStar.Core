using System;
using System.Collections.Generic;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// How far an object has come: the percentage, and where the number is from.
    /// </summary>
    /// <remarks>
    /// The two are reported together because they are read differently. <em>50 % because this
    /// object is in progress</em> is a statement about one record and is as coarse as its
    /// workflow; <em>50 % because three of six children are done</em> is a measurement, and the
    /// reader who sees the second wants to know how many things it counted.
    /// </remarks>
    public sealed record ObjectProgress
    {
        /// <summary>
        /// Gets how far along the object is, between 0 and 100.
        /// </summary>
        public int Percent { get; init; }

        /// <summary>
        /// Gets a value indicating whether the percentage was rolled up from other objects
        /// rather than read off this one's own state.
        /// </summary>
        public bool Aggregated { get; init; }

        /// <summary>
        /// Gets the objects the percentage was rolled up from, empty when it was not.
        /// </summary>
        /// <remarks>
        /// Only the direct children are listed - what a child itself counted is already in its
        /// own percentage, and repeating its children here would report the same work twice.
        /// </remarks>
        public IReadOnlyList<ObjectProgressContribution> Contributions { get; init; } = [];

        /// <summary>
        /// Gets how many of the contributions are finished, which is the count a caption reads
        /// as <em>3 of 6</em>.
        /// </summary>
        public int Completed { get; init; }
    }

    /// <summary>
    /// One object a rolled-up percentage counted.
    /// </summary>
    /// <param name="ObjectId">The child object.</param>
    /// <param name="Key">Its key, so a caller can name it without reading it again.</param>
    /// <param name="Percent">How far along it is - its own aggregate where it has children of
    /// its own.</param>
    public sealed record ObjectProgressContribution(Guid ObjectId, string Key, int Percent);
}
