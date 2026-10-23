using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Work
{
    /// <summary>
    /// Removes a worklog: <c>POST /api/1/work/deleteworklog</c> with <c>{ id }</c> - by its author,
    /// or by an administrator of the object's workspace.
    /// </summary>
    [Title("kleenestar.core:planning.api.deleteworklog.title")]
    [Cache]
    public sealed class DeleteWorklog : IRestApi
    {
        /// <summary>
        /// Handles <c>POST</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>204</c>, <c>404</c>, or why it was not removed.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Remove(IRequest request)
        {
            var payload = AutomationApi.Read<WorkApi.Payload>(request);
            var worklog = Guid.TryParse(payload?.Id, out var id) ? CoreHub.PlanningManager.GetWorklog(id) : null;
            var @object = worklog is null ? null : CoreHub.ObjectManager.GetObject(worklog.ObjectId);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            try
            {
                CoreHub.PlanningManager.RemoveWorklog(worklog.Id, CoreHub.SessionManager.GetCurrentIdentityId(request), ContentAuthorization.MayAdministerWorkspace(@object.WorkspaceId, request));

                return new ResponseNoContent();
            }
            catch (PlanningRefusedException refusal)
            {
                return WorkApi.Refuse(refusal);
            }
        }
    }
}
