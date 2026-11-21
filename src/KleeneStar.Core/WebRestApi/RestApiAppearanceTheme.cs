using System;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The base of the endpoints (core and portal, one per application) that keep the color
    /// theme the caller picked - behind the framework's theme
    /// selector on the appearance page of the profile and on the system's theme settings page.
    /// </summary>
    /// <remarks>
    /// The framework lists the themes registered for the application (the core's own and those
    /// of plugins such as <c>KleeneStar.Theme</c>) and reloads the page after a pick, because a
    /// theme is a stylesheet the server links. The hooks only move the id in and out of
    /// <see cref="WebManager.IAppearanceManager"/>; an id that names no theme of this
    /// application is refused, and a caller who is not signed in stores nothing.
    /// </remarks>
    public abstract class RestApiAppearanceTheme : RestApiTheme
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        protected RestApiAppearanceTheme()
        {
        }

        /// <summary>
        /// Returns the caller's theme, else the application's default, so the selector shows
        /// the theme the page is drawn in.
        /// </summary>
        /// <param name="context">The query context (unused).</param>
        /// <param name="request">The current request.</param>
        /// <returns>The theme id.</returns>
        protected override string GetActiveThemeId(IQueryContext context, IRequest request)
        {
            var themeId = CoreHub.AppearanceManager.GetCurrent(request).Theme;

            return CoreHub.AppearanceManager.IsRegistered(themeId, request)
                ? themeId
                : request?.ApplicationContext?.DefaultTheme?.ThemeId?.ToString();
        }

        /// <summary>
        /// Stores the picked theme; picking the application's default clears the choice, so a
        /// later change of the default reaches this caller too.
        /// </summary>
        /// <param name="themeId">The theme id, or <see langword="null"/> to clear.</param>
        /// <param name="context">The query context (unused).</param>
        /// <param name="request">The current request.</param>
        protected override void PersistSelection(string themeId, IQueryContext context, IRequest request)
        {
            if (!string.IsNullOrEmpty(themeId) && !CoreHub.AppearanceManager.IsRegistered(themeId, request))
            {
                throw new ArgumentException("The selected theme is not registered for this application.", nameof(themeId));
            }

            var isDefault = string.Equals
            (
                themeId,
                request?.ApplicationContext?.DefaultTheme?.ThemeId?.ToString(),
                StringComparison.OrdinalIgnoreCase
            );

            CoreHub.AppearanceManager.Update(request, x => x.Theme = isDefault ? null : themeId);
        }
    }
}
