using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Automation._workspacekey_
{
    /// <summary>
    /// REST endpoint removing an automation rule of a workspace, with the jobs it left. The URL
    /// is <c>/api/1/automation/{workspacekey}/delete</c>; the body names the rule (<c>{ "id" }</c>),
    /// because the page's confirmation form submits by <c>POST</c>.
    /// </summary>
    [Title("kleenestar.core:automation.api.delete.title")]
    [Cache]
    public sealed class Delete : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Delete()
        {
        }

        /// <summary>
        /// Handles <c>POST</c>: removes the rule the body names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><c>204</c>, or <c>404</c> for a rule of no workspace the route names.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Remove(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var id = AutomationApi.ParseId(AutomationApi.Read<AutomationApi.IdPayload>(request)?.Id);
            var rule = id is { } ruleId ? CoreHub.AutomationManager.GetRule(ruleId) : null;

            if (rule is null || rule.WorkspaceId != workspace.Id)
            {
                return new ResponseNotFound();
            }

            CoreHub.AutomationManager.RemoveRule(rule.Id);

            return new ResponseNoContent();
        }
    }
}
