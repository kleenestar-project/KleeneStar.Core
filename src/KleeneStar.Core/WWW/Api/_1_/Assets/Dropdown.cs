using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Assets
{
    /// <summary>
    /// Provides the items for the assets dropdown in the application header: with no search
    /// term the calling identity's most recently opened assets (newest first); with a
    /// search term a full-text search across the asset-kind objects by summary.
    /// </summary>
    [Title("kleenestar.core:object.kind.assets.label")]
    [Cache]
    public sealed class Dropdown : RestApiObjectKindDropdown
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Dropdown()
        {
        }

        /// <summary>
        /// Gets the kind key listed by this dropdown.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Asset;
    }
}
