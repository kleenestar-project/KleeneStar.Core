using WebExpress.WebApp.WebTheme;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WebTheme
{
    /// <summary>
    /// The default KleeneStar theme. Icons are resolved from the lightweight SVG
    /// set shipped via <c>webexpress.webui.icon.css</c>, which is the only icon
    /// set WebExpress provides.
    /// </summary>
    [Name("kleenestar.core:theme.light.name")]
    [Description("kleenestar.core:theme.light.description")]
    public sealed class LightTheme : IThemeWebApp
    {
    }
}
