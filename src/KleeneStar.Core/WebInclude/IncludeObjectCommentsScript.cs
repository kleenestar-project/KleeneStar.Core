using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes the script that keeps the comment count of the docked thread in step, on the
    /// pages that carry the dock (<see cref="WebFragment.Object.ObjectCommentDockFragment"/>).
    /// </summary>
    [Asset("/assets/js/objectcomments.js")]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    public sealed class IncludeObjectCommentsScript : IInclude
    {
    }
}
