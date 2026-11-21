using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// The stylesheets the KleeneStar plugin contributes to every page of its applications.
    /// </summary>
    /// <remarks>
    /// The include manager discovers these per plugin, so a sheet named here is served from the
    /// plugin's own embedded assets and is loaded after the WebExpress ones - which is what lets
    /// it set the rhythm of a view the framework controls only the frame of.
    /// </remarks>
    [Asset("/assets/css/kleenestar.css")]
    public sealed class IncludeStyleSheet : IInclude
    {
    }
}
