using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes <c>Assets/js/kleenestar.core.kanban.transition.js</c> (<c>kleenestar.core.KanbanTransition</c>) on
    /// the pages that show a Kanban board: a card dropped into a state whose transition has a
    /// form opens that form in a dialog instead of moving at once
    /// (<see cref="WebRestApi.RestApiObjectKindKanban"/>).
    /// </summary>
    [Asset("/assets/js/kleenestar.core.kanban.transition.js")]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Insight._insightid_.Index>]
    public sealed class IncludeKanbanTransitionScript : IInclude
    {
    }
}
