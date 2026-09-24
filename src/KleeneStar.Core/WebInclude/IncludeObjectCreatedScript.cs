using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes the script that opens an object once it has been created, on every page of the
    /// core application: the create button stands in the header of every page, so any page can
    /// be the one a new object has to replace.
    /// </summary>
    [Asset("/assets/js/objectcreated.js")]
    public sealed class IncludeObjectCreatedScript : IInclude
    {
    }
}
