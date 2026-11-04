using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes <c>Assets/js/transfer.js</c> (<c>kleenestar.core.Transfer</c>) on the object
    /// detail pages, whose overflow menu offers <i>Move to another workspace…</i>
    /// (<c>ObjectTransferMoreFragment</c>).
    /// </summary>
    [Asset("/assets/js/transfer.js")]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    public sealed class IncludeTransferScript : IInclude
    {
    }
}
