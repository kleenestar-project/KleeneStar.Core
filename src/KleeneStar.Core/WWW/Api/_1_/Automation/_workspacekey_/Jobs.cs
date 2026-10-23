using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Automation._workspacekey_
{
    /// <summary>
    /// REST endpoint over the automation jobs of a workspace: <c>GET</c> lists the newest
    /// (<c>?state=failed</c> narrows), <c>POST</c> with <c>{ "id" }</c> runs a failed job again.
    /// The URL is <c>/api/1/automation/{workspacekey}/jobs</c>.
    /// </summary>
    /// <remarks>
    /// Running a job again continues after its last finished action: what it already did is
    /// not repeated.
    /// </remarks>
    [Title("kleenestar.core:automation.api.jobs.title")]
    [Cache]
    public sealed class Jobs : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Jobs()
        {
        }

        /// <summary>
        /// Handles <c>GET</c>: the newest jobs of the workspace's rules.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The jobs as JSON.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            AutomationJobState? state = Enum.TryParse<AutomationJobState>(request.GetParameter("state")?.Value, true, out var parsed) ? parsed : null;

            return AutomationApi.Json(AutomationApi.ToDto(CoreHub.AutomationManager.GetJobs(workspace.Id, state, 200)));
        }

        /// <summary>
        /// Handles <c>POST</c>: runs the failed job the body names again.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>204</c>, <c>404</c> for an unknown job, or a refusal for one that has not failed.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Retry(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var id = AutomationApi.ParseId(AutomationApi.Read<AutomationApi.IdPayload>(request)?.Id);
            var job = id is { } jobId ? Model.ModelHub.GetAutomationJob(jobId) : null;
            var rule = job is null ? null : CoreHub.AutomationManager.GetRule(job.RuleId);

            if (job is null || rule?.WorkspaceId != workspace.Id)
            {
                return new ResponseNotFound();
            }

            return CoreHub.AutomationManager.Retry(job.Id) is null
                ? AutomationApi.Refuse("Only a failed job is run again.")
                : new ResponseNoContent();
        }
    }
}
