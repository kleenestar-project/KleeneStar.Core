using KleeneStar.Core.WWW.Api._1_.Automation;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Planning._workspacekey_
{
    /// <summary>
    /// Removes a milestone of a workspace: <c>POST /api/1/planning/{workspacekey}/deletemilestone</c>
    /// with <c>{ id }</c>. Its objects stay, without a milestone.
    /// </summary>
    [Title("kleenestar.core:planning.api.deletemilestone.title")]
    [Cache]
    public sealed class DeleteMilestone : IRestApi
    {
        /// <summary>
        /// Handles <c>POST</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>204</c> or <c>404</c>.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Remove(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var id = AutomationApi.ParseId(AutomationApi.Read<AutomationApi.IdPayload>(request)?.Id);
            var milestone = id is { } milestoneId ? CoreHub.PlanningManager.GetMilestone(milestoneId) : null;

            if (milestone is null || milestone.WorkspaceId != workspace.Id)
            {
                return new ResponseNotFound();
            }

            CoreHub.PlanningManager.RemoveMilestone(milestone.Id);

            return new ResponseNoContent();
        }
    }
}
