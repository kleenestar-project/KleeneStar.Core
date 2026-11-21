using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.SavedSearches
{
    // The entity type SavedSearch collides with the sibling WWW.SavedSearch namespace;
    // alias it (inside the namespace block) so the bare name binds to the entity.
    using SavedSearch = KleeneStar.Model.Entities.SavedSearch;

    /// <summary>
    /// Backs the advanced-search prompt of the saved-search sidebar table.
    /// </summary>
    [Cache]
    public sealed class Wql : KleeneStarRestApiWqlPrompt<SavedSearch>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Wql()
        {
        }
    }
}
