using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes <c>Assets/js/assignee.js</c> (<c>kleenestar.core.Assignee</c>) on the issue and
    /// asset pages, whose people card offers <i>assign to me</i> and <i>unassign</i>
    /// (<see cref="WebFragment.Object.ObjectPropertyPeopleCardFragment"/>): the action changes
    /// the assignee through <c>PUT /api/1/assignee/{objectkey}</c> without reloading the page.
    /// </summary>
    [Asset("/assets/js/assignee.js")]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    public sealed class IncludeAssigneeScript : IInclude
    {
    }
}
