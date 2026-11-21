using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Shared base for the conditions that gate the objects sidebar links: the link of a
    /// kind is rendered only where the workspace has a class of that kind.
    /// </summary>
    /// <remarks>
    /// A concrete subclass names one kind. The subclasses exist because a condition is
    /// bound through <c>[Condition&lt;T&gt;]</c>, which takes a type rather than a value —
    /// they carry no logic of their own.
    /// </remarks>
    internal abstract class ObjectKindConfiguredCondition : ICondition
    {
        /// <summary>
        /// Gets the kind key the condition asks about.
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Determines whether the addressed workspace has at least one active class of the
        /// kind.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>True when the kind is configured in the workspace.</returns>
        public bool Fulfillment(IRequest request)
        {
            return ObjectKindScope.IsConfigured(request, Kind);
        }
    }

    /// <summary>
    /// Gates the sidebar link of the issue kind.
    /// </summary>
    internal sealed class IssueKindConfiguredCondition : ObjectKindConfiguredCondition
    {
        /// <inheritdoc/>
        protected override string Kind => Model.Entities.ObjectKind.Issue;
    }

    /// <summary>
    /// Gates the sidebar link of the asset kind.
    /// </summary>
    internal sealed class AssetKindConfiguredCondition : ObjectKindConfiguredCondition
    {
        /// <inheritdoc/>
        protected override string Kind => Model.Entities.ObjectKind.Asset;
    }

    /// <summary>
    /// Gates the sidebar link of the document kind.
    /// </summary>
    internal sealed class DocumentKindConfiguredCondition : ObjectKindConfiguredCondition
    {
        /// <inheritdoc/>
        protected override string Kind => Model.Entities.ObjectKind.Document;
    }

    /// <summary>
    /// Gates the sidebar link of the blog kind.
    /// </summary>
    internal sealed class BlogKindConfiguredCondition : ObjectKindConfiguredCondition
    {
        /// <inheritdoc/>
        protected override string Kind => Model.Entities.ObjectKind.Blog;
    }
}
