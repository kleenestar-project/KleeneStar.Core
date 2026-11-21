using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents a condition that determines whether the class overview of the workspace
    /// addressed by the route has something to show.
    /// </summary>
    /// <remarks>
    /// It is the exact complement of <see cref="ClassEmptyStateCondition"/> and is derived from it
    /// rather than repeating the query, so the two can never disagree and leave the page with both
    /// the view and the empty-state placeholder, or with neither.
    /// </remarks>
    internal class ClassNotEmptyStateCondition : ICondition
    {
        private readonly ClassEmptyStateCondition _empty = new();

        /// <summary>
        /// Determines whether the addressed workspace has at least one class.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>True when the overview has a class to list.</returns>
        public bool Fulfillment(IRequest request)
        {
            return !_empty.Fulfillment(request);
        }
    }
}
