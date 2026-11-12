using KleeneStar.Core.WebInsight;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The tab types an insight can show - the lookup behind the persisted
    /// <see cref="Model.Entities.InsightView.Type"/> key. A stored key nobody provides any more
    /// is kept; the tab control leaves it out and it returns as soon as the type does.
    /// </summary>
    public interface IInsightViewTypeManager : IComponentManager
    {
        /// <summary>
        /// Gets a number that changes whenever the set of types does.
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Gets the types, ordered by <see cref="IInsightViewType.Order"/> and then by key.
        /// </summary>
        IEnumerable<IInsightViewType> Types { get; }

        /// <summary>
        /// Registers a type that is no type of a plugin. A key that is present is taken over.
        /// </summary>
        /// <param name="type">The type. Must not be null and must carry a key and a template.</param>
        void Register(IInsightViewType type);

        /// <summary>
        /// Removes a registered type, so it can no longer be added as a tab. The default type
        /// cannot be removed, only replaced - it is the first tab of a new insight.
        /// </summary>
        /// <param name="key">The key of the type.</param>
        /// <returns><see langword="true"/> when a type was removed.</returns>
        bool Unregister(string key);

        /// <summary>
        /// Returns the type under a key.
        /// </summary>
        /// <param name="key">The key, compared without case.</param>
        /// <returns>The type, or <see langword="null"/>.</returns>
        IInsightViewType Get(string key);

        /// <summary>
        /// Determines whether a type is known under a key.
        /// </summary>
        /// <param name="key">The key, compared without case.</param>
        /// <returns><see langword="true"/> when the key names a known type.</returns>
        bool IsRegistered(string key);

        /// <summary>
        /// Returns the type whose tab template the client reported.
        /// </summary>
        /// <param name="templateId">The template id, compared without case.</param>
        /// <returns>The type, or <see langword="null"/> when no known type uses that template.</returns>
        IInsightViewType FromTemplateId(string templateId);
    }
}
