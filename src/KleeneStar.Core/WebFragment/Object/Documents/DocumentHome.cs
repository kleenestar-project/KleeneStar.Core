using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Object.Documents
{
    /// <summary>
    /// Resolves the home page of the workspace on the document overview and decides whether the
    /// caller may edit it there.
    /// </summary>
    /// <remarks>
    /// The home page is the dedicated document of the workspace
    /// (<see cref="WebManager.IWorkspaceManager.EnsureHome"/>). It has no detail page and is read
    /// and edited on the document overview. The reading view, the edit button, the editor and the
    /// reload meta use this class, so they resolve the same document.
    /// </remarks>
    public static class DocumentHome
    {
        /// <summary>
        /// Resolves the home page of the workspace the request's route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The home page, or <see langword="null"/> when the route names no workspace,
        /// the workspace has none, or the caller may not read it.</returns>
        public static Model.Entities.Object Resolve(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = string.IsNullOrWhiteSpace(key) ? null : CoreHub.WorkspaceManager.GetWorkspaceByKey(key);

            return workspace is null ? null : CoreHub.WorkspaceManager.GetHome(workspace.Id);
        }

        /// <summary>
        /// Determines whether the caller may edit the home page on the overview.
        /// </summary>
        /// <remarks>
        /// Requires a signed-in caller (drafts and publications have an author),
        /// <see cref="WebRestApi.ContentAuthorization.MayWrite"/> on the object (the rule of the
        /// publication endpoint), and a class with the effective renderer prose, because the page
        /// is edited in the prose editor.
        /// </remarks>
        /// <param name="home">The home page.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the edit is offered.</returns>
        public static bool MayEdit(Model.Entities.Object home, IRequest request)
        {
            if (home is null || CoreHub.SessionManager.GetCurrentIdentityId(request) == Guid.Empty)
            {
                return false;
            }

            var @class = CoreHub.ClassManager.GetClass(home.ClassId);

            return CoreHub.ObjectRendererManager.IsRenderedAs(@class, ObjectRenderer.Prose)
                && WebRestApi.ContentAuthorization.MayWrite(home, request);
        }
    }
}
