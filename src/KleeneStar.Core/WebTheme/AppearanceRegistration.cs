using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebTheme;
using WebExpress.WebCore.WebApplication;

namespace KleeneStar.Core.WebTheme
{
    /// <summary>
    /// Hands the appearance every identity chose to the framework's theme preferences of one
    /// application - the core's and the portal's, each with endpoints of its own.
    /// </summary>
    /// <remarks>
    /// The framework asks the registration on its own pages (the settings pages apply the theme,
    /// the system's theme page offers the selectors) and on every page for the light or dark mark
    /// of the root, whose address also tells the dark mode switch where to store a toggle. The
    /// endpoints are named by type because an endpoint belongs to one application and
    /// <c>ThemePreferences</c> builds their address in it. The record behind them is one per
    /// identity (<see cref="WebManager.IAppearanceManager"/>), so a theme picked on one side is in
    /// use on the other wherever it is registered there too. The other pages are switched by a
    /// <see cref="WebFragment.Appearance.AppearanceFragmentBase"/> of the application.
    /// </remarks>
    public static class AppearanceRegistration
    {
        /// <summary>
        /// Registers the appearance of an application.
        /// </summary>
        /// <typeparam name="TThemeEndpoint">The application's theme endpoint.</typeparam>
        /// <typeparam name="TSchemeEndpoint">The application's light or dark endpoint.</typeparam>
        /// <param name="applicationContext">The application.</param>
        public static void Register<TThemeEndpoint, TSchemeEndpoint>(IApplicationContext applicationContext)
            where TThemeEndpoint : RestApiAppearanceTheme
            where TSchemeEndpoint : RestApiAppearanceScheme
        {
            ThemePreferences.Register
            (
                applicationContext,
                renderContext => CoreHub.AppearanceManager.ResolveTheme(renderContext),
                () => new ControlDataSelectionTheme("ks-appearance-theme").DataService<TThemeEndpoint>()
            );

            ThemePreferences.RegisterColorScheme<TSchemeEndpoint>
            (
                applicationContext,
                renderContext => CoreHub.AppearanceManager.GetCurrent(renderContext?.Request).ColorScheme
            );
        }

        /// <summary>
        /// Releases the registration of an application when it is disposed.
        /// </summary>
        /// <param name="applicationContext">The application.</param>
        public static void Remove(IApplicationContext applicationContext)
        {
            if (applicationContext is not null)
            {
                ThemePreferences.Remove(applicationContext);
            }
        }
    }
}
