using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Appearance
{
    /// <summary>
    /// The light or dark choice of the core application (<c>/api/1/appearance/scheme</c>); the
    /// behaviour is <see cref="RestApiAppearanceScheme"/>'s.
    /// </summary>
    [Title("kleenestar.core:profile.appearance.scheme.label")]
    public sealed class Scheme : RestApiAppearanceScheme
    {
    }
}
