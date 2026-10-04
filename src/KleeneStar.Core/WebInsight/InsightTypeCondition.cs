using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// Decides, for a route that names an insight, whether the insight is of a given type.
    /// </summary>
    /// <remarks>
    /// No page knows about insight types: the insight page carries one fragment per type, each
    /// gated on a condition derived from this one, and the type of the insight the route names
    /// decides which of them draws. A type a plugin adds is a descriptor, a condition and a
    /// fragment; nothing already written is edited. A route that names no insight, or one that
    /// is gone, fulfills no type - every fragment stays away and the page shows its empty frame.
    /// </remarks>
    public abstract class InsightTypeCondition : ICondition
    {
        /// <summary>
        /// Gets the key of the type the condition is fulfilled by.
        /// </summary>
        protected abstract string Type { get; }

        /// <summary>
        /// Determines whether the condition is fulfilled for the request.
        /// </summary>
        /// <param name="request">The request whose route names the insight.</param>
        /// <returns><see langword="true"/> when the insight is of the expected type.</returns>
        public bool Fulfillment(IRequest request)
        {
            return InsightTypeCatalog.IsOfType(Resolve(request), Type);
        }

        /// <summary>
        /// Resolves the insight the route names.
        /// </summary>
        /// <param name="request">The request whose route names the insight.</param>
        /// <returns>The insight, or <see langword="null"/> when the route names none.</returns>
        internal static Model.Entities.Insight Resolve(IRequest request)
        {
            return Guid.TryParse(request?.GetParameter<InsightIdParameter>()?.Value, out var id) && id != Guid.Empty
                ? CoreHub.InsightManager.GetInsight(id)
                : null;
        }
    }

    /// <summary>
    /// Fulfilled when the insight the route names is a dashboard.
    /// </summary>
    public sealed class InsightDashboardCondition : InsightTypeCondition
    {
        /// <inheritdoc/>
        protected override string Type => Model.Entities.Insight.DashboardType;
    }

    /// <summary>
    /// Fulfilled when the insight the route names carries a type nobody registered - typically
    /// one whose plugin was uninstalled - so its page explains why it shows nothing.
    /// </summary>
    public sealed class InsightUnavailableTypeCondition : ICondition
    {
        /// <summary>
        /// Determines whether the condition is fulfilled for the request.
        /// </summary>
        /// <param name="request">The request whose route names the insight.</param>
        /// <returns><see langword="true"/> when the insight exists and its type is not registered.</returns>
        public bool Fulfillment(IRequest request)
        {
            var insight = InsightTypeCondition.Resolve(request);

            return insight is not null && !InsightTypeCatalog.IsRegistered(insight.Type);
        }
    }
}
