using KleeneStar.Core.WebManager;
using System;
using System.Collections.Generic;
using System.Globalization;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// How far the impact page walks, read from the request the page was asked for.
    /// </summary>
    /// <remarks>
    /// The depth lives in the address rather than in a stored preference, so a reader can hand
    /// somebody the picture they are looking at - "three steps out from this incident" - and the
    /// link says which reading it is. It is resolved in one place because the chooser that sets
    /// it and the canvas that follows it must agree on what the current one is.
    /// </remarks>
    internal static class ObjectImpactDepth
    {
        /// <summary>
        /// The name of the query parameter carrying the depth.
        /// </summary>
        public const string Parameter = "depth";

        /// <summary>
        /// The depths the chooser offers.
        /// </summary>
        /// <remarks>
        /// Not every step between one and the maximum: the difference between four and five is
        /// rarely a different answer, while the difference between one and the whole reachable
        /// set is the question the page exists for. The last entry is the manager's own ceiling,
        /// so the chooser cannot promise a walk the analysis would clamp.
        /// </remarks>
        public static IReadOnlyList<int> Offered { get; } =
        [
            1,
            2,
            ObjectImpactManager.DefaultDepth,
            5,
            ObjectImpactManager.MaximumDepth
        ];

        /// <summary>
        /// Reads the depth the request asks for.
        /// </summary>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The depth, clamped to what the analysis will actually walk.</returns>
        public static int Resolve(IRenderControlContext renderContext)
        {
            var value = renderContext?.Request?.GetParameter(Parameter)?.Value;

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? Math.Clamp(parsed, 1, ObjectImpactManager.MaximumDepth)
                : ObjectImpactManager.DefaultDepth;
        }
    }
}
