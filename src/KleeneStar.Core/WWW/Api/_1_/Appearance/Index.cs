using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;

namespace KleeneStar.Core.WWW.Api._1_.Appearance
{
    /// <summary>
    /// The adjustments form of the profile's appearance page (<c>/api/1/appearance</c>); the
    /// behaviour is <see cref="RestApiAppearance"/>'s, shared with the portal's endpoint.
    /// </summary>
    [Cache]
    public sealed class Index : RestApiAppearance
    {
    }
}
