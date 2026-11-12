using KleeneStar.Core.WebTheme;
using System;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebTheme;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Keeps the appearance every identity chose for itself - theme, light or dark, accent,
    /// font size, density, motion - and answers it for the page being rendered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The themes themselves are not this manager's: WebExpress discovers them in the plugins
    /// (<c>IThemeManager</c>), so a plugin such as <c>KleeneStar.Theme</c> adds its themes
    /// without the core knowing it exists. The manager only remembers which of them a person
    /// picked, and hands that to the framework's <c>ThemePreferences</c>, which is what the
    /// framework's settings pages, its theme selector and its dark mode switch ask.
    /// </para>
    /// <para>
    /// The choice belongs to the signed-in identity, so it follows the person across browsers.
    /// A caller who is not signed in keeps the installation's defaults and cannot store a
    /// choice.
    /// </para>
    /// </remarks>
    public interface IAppearanceManager : IComponentManager
    {
        /// <summary>
        /// The scope of the identity's key/value preferences the record is kept under.
        /// </summary>
        const string Scope = "appearance";

        /// <summary>
        /// The key the record is kept under.
        /// </summary>
        const string Key = "preference";

        /// <summary>
        /// Raised after an identity's appearance has been written.
        /// </summary>
        event EventHandler<AppearancePreference> AppearanceChanged;

        /// <summary>
        /// Returns the appearance an identity chose.
        /// </summary>
        /// <param name="identityId">The identity.</param>
        /// <returns>The record; an empty one when nothing was chosen or nobody is named.</returns>
        AppearancePreference Get(Guid identityId);

        /// <summary>
        /// Returns the appearance of the caller of a request, read once per request.
        /// </summary>
        /// <param name="request">The request, or <see langword="null"/> for the ambient one.</param>
        /// <returns>The record; an empty one for a caller who is not signed in.</returns>
        AppearancePreference GetCurrent(IRequest request);

        /// <summary>
        /// Writes an identity's appearance; a record without any choice removes the stored one.
        /// </summary>
        /// <remarks>
        /// Every value is normalized before it is kept (<see cref="AppearanceOptions"/>), so
        /// what is stored is always something a stylesheet may carry.
        /// </remarks>
        /// <param name="identityId">The owner; <see cref="Guid.Empty"/> stores nothing.</param>
        /// <param name="preference">The complete record.</param>
        void Save(Guid identityId, AppearancePreference preference);

        /// <summary>
        /// Changes the appearance of the caller of a request and keeps every other choice.
        /// </summary>
        /// <remarks>
        /// Read, change and write happen under one gate per identity (process and, when
        /// clustered, cluster-wide), and the base is the stored record read inside it - never
        /// the copy cached beside the request - so two concurrent changes of the same person
        /// (theme here, mode there) both survive.
        /// </remarks>
        /// <param name="request">The request.</param>
        /// <param name="change">The change applied to the stored record; it touches only what it changes.</param>
        /// <returns>Whether a signed-in caller was found to store it for.</returns>
        bool Update(IRequest request, Action<AppearancePreference> change);

        /// <summary>
        /// Resolves the theme the caller picked, provided it is registered for the application
        /// being rendered - a theme whose plugin is gone, or one of another application, is
        /// no choice and the application's default applies.
        /// </summary>
        /// <param name="renderContext">The request and application being rendered.</param>
        /// <returns>The theme, or <see langword="null"/>.</returns>
        IThemeContext ResolveTheme(IRenderContext renderContext);

        /// <summary>
        /// Tells whether a theme id names a theme registered for an application.
        /// </summary>
        /// <param name="themeId">The theme id.</param>
        /// <param name="request">The request whose application is asked.</param>
        /// <returns>Whether the theme can be used there.</returns>
        bool IsRegistered(string themeId, IRequest request);
    }
}
