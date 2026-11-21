using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The base of the endpoints (core and portal, one per application) that keep the caller's
    /// light or dark choice - behind the mode selector of the
    /// appearance page and the dark mode switch of the avatar menu.
    /// </summary>
    /// <remarks>
    /// The framework announces this address on the root of every page
    /// (<c>data-wx-theme-service</c>, see <c>ThemePreferences.RegisterColorScheme</c>), so a
    /// toggle anywhere is stored as the same preference and the next page is rendered in it
    /// from the server - no flash of the other mode. The value arrives reduced to light, dark
    /// or none; a caller who is not signed in stores nothing and keeps the theme's mode on the
    /// next page.
    /// </remarks>
    public abstract class RestApiAppearanceScheme : RestApiColorScheme
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        protected RestApiAppearanceScheme()
        {
        }

        /// <summary>
        /// Returns the caller's mode; without one the theme decides.
        /// </summary>
        /// <param name="context">The query context (unused).</param>
        /// <param name="request">The current request.</param>
        /// <returns>"light", "dark" or <see langword="null"/>.</returns>
        protected override string GetActiveColorScheme(IQueryContext context, IRequest request)
        {
            return CoreHub.AppearanceManager.GetCurrent(request).ColorScheme;
        }

        /// <summary>
        /// Stores the caller's mode.
        /// </summary>
        /// <param name="colorScheme">"light", "dark" or <see langword="null"/>.</param>
        /// <param name="context">The query context (unused).</param>
        /// <param name="request">The current request.</param>
        protected override void PersistSelection(string colorScheme, IQueryContext context, IRequest request)
        {
            CoreHub.AppearanceManager.Update(request, x => x.ColorScheme = colorScheme);
        }
    }
}
