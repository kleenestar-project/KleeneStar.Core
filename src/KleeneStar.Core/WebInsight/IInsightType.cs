using WebExpress.WebCore.WebIcon;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// Describes a type of insight - one kind of user-defined view on the data, such as a
    /// dashboard. The core registers the dashboard type; a plugin contributes another by
    /// registering its own descriptor with <see cref="InsightTypeCatalog.Register"/>, together
    /// with the fragment that draws an insight of that type on its page.
    /// </summary>
    /// <remarks>
    /// The descriptor is the semantic side of the persisted <see cref="Model.Entities.Insight.Type"/>
    /// key: what the type is called, how it is explained in the create dialog and the icon it
    /// is listed under. What an insight of the type shows is not the descriptor's business -
    /// no page knows about insight types; a fragment scoped to the insight page and gated on
    /// <see cref="InsightTypeCondition"/> stands in for each type, the way the reading views of
    /// an object stand in for its renderer.
    /// </remarks>
    public interface IInsightType
    {
        /// <summary>
        /// Gets the key the type is persisted under, for example <c>dashboard</c>. Keys are
        /// compared without case.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the internationalization key of the name the type is offered under.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the internationalization key of the sentence that explains the type.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon the type is listed under.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the position of the type among the others; lower comes first.
        /// </summary>
        int Order { get; }
    }
}
