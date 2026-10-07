using KleeneStar.Core.WebParameter;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Kanban endpoint of the Scrum sprint tab: the issues of the workspace's <em>active</em>
    /// sprint as a board grouped by workflow status, shown below the sprint burn-down. The
    /// board logic lives in
    /// <see cref="global::KleeneStar.Core.WebRestApi.RestApiObjectKindKanban"/>; this
    /// endpoint scopes it to the issue kind and to the active sprint. It is an independent
    /// sibling of the workspace Kanban endpoint (not a subclass), so both keep their own
    /// route.
    /// </summary>
    [Title("kleenestar.core:object.view.scrum.sprint.title")]
    [Cache]
    public sealed class ScrumSprintKanban : global::KleeneStar.Core.WebRestApi.RestApiObjectKindKanban
    {
        /// <summary>
        /// Gets the object kind the board is scoped to: issues.
        /// </summary>
        protected override string Kind => Model.Entities.ObjectKind.Issue;

        /// <summary>
        /// Gets the Scrum tab type that owns the sprint board.
        /// </summary>
        protected override Model.Entities.ObjectViewType BoardViewType => Model.Entities.ObjectViewType.ScrumSprint;

        /// <summary>
        /// Scopes the board to the workspace's active sprint. When no sprint is active the
        /// sentinel <see cref="Guid.Empty"/> is returned so the board renders empty (no
        /// object is committed to the empty sprint) rather than falling back to the whole
        /// workspace.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The active sprint id, or <see cref="Guid.Empty"/> when none is active.</returns>
        protected override Guid? ResolveSprint(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);

            if (workspace is null)
            {
                return Guid.Empty;
            }

            return CoreHub.SprintManager.GetActiveSprint(workspace.Id)?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// Applies the scrum view's query surface: the search term of the <c>q</c> parameter
        /// and the personal-scope quickfilter chips (assigned to me, starred) of the <c>f</c>
        /// parameter. With neither set the board is unchanged.
        /// </summary>
        /// <remarks>
        /// The search rides along in this override because it is the only in-memory hook the
        /// board base offers, and the board must honour the same header controls the backlog
        /// does. Both narrowings are delegated to
        /// <see cref="ScrumProjection"/>, so the board and the backlog cannot drift apart on
        /// what a chip or a search term means.
        /// </remarks>
        /// <param name="objects">The candidate objects.</param>
        /// <param name="request">The request carrying the query surface and the caller.</param>
        /// <returns>The filtered objects.</returns>
        protected override IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request)
        {
            objects = ScrumProjection.ApplySearch(objects, request);
            objects = ScrumProjection.ApplyQuickfilter(objects, request);

            return objects;
        }
    }
}

