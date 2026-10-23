using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebAutomation.Actions;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Automation._workspacekey_
{
    /// <summary>
    /// REST endpoint over the automation rules of a workspace: <c>GET</c> lists them,
    /// <c>POST</c> adds a rule or - with an <c>id</c> - changes one. The URL is
    /// <c>/api/1/automation/{workspacekey}</c>; it binds the segment for its siblings.
    /// </summary>
    /// <remarks>
    /// A rule is validated in full before it is stored - trigger, condition and every line of
    /// its script - and refused with the problems as a readable message, so a rule that could
    /// not run is never saved.
    /// </remarks>
    [Title("kleenestar.core:automation.api.title")]
    [WorkspaceKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Handles <c>GET</c>: the rules of the workspace.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The rules as JSON.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            return AutomationApi.Json(CoreHub.AutomationManager.GetRules(workspace.Id).Select(AutomationApi.ToDto));
        }

        /// <summary>
        /// Handles <c>POST</c>: adds a rule, or changes the one the body names.
        /// </summary>
        /// <param name="request">The request carrying the rule.</param>
        /// <returns>The stored rule, or the problems that kept it from being stored.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Save(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var payload = AutomationApi.Read<AutomationApi.RulePayload>(request);

            if (payload is null)
            {
                return AutomationApi.Refuse("The request body must describe a rule.");
            }

            var existing = AutomationApi.ParseId(payload.Id) is Guid id ? CoreHub.AutomationManager.GetRule(id) : null;

            if (AutomationApi.ParseId(payload.Id) is not null && (existing is null || existing.WorkspaceId != workspace.Id))
            {
                return new ResponseNotFound();
            }

            Guid? runAs = null;

            if (!string.IsNullOrWhiteSpace(payload.RunAs))
            {
                runAs = StandardActions.ResolveIdentity(payload.RunAs)?.Id;

                if (runAs is null)
                {
                    return AutomationApi.Refuse($"Unknown account '{payload.RunAs}' to act as.");
                }
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var rule = existing ?? new AutomationRule
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                CreatedById = callerId == Guid.Empty ? null : callerId,
                Created = DateTime.UtcNow
            };

            rule.Name = payload.Name?.Trim();
            rule.Description = payload.Description;
            rule.State = string.Equals(payload.State, "disabled", StringComparison.OrdinalIgnoreCase)
                ? AutomationRuleState.Disabled
                : AutomationRuleState.Active;
            rule.ClassId = AutomationApi.ParseId(payload.ClassId);
            rule.Trigger = payload.Trigger?.Trim();
            rule.TriggerParameter = string.IsNullOrWhiteSpace(payload.TriggerParameter) ? null : payload.TriggerParameter.Trim();
            rule.Condition = string.IsNullOrWhiteSpace(payload.Condition) ? null : payload.Condition.Trim();
            rule.Actions = payload.Actions?.Replace("\r\n", "\n");
            rule.RunAsId = runAs;

            var problems = CoreHub.AutomationManager.Validate(rule);

            if (problems.Count > 0)
            {
                return AutomationApi.Refuse(string.Join(" ", problems));
            }

            if (existing is null)
            {
                CoreHub.AutomationManager.AddRule(rule);
            }
            else
            {
                CoreHub.AutomationManager.UpdateRule(rule);
            }

            return AutomationApi.Json(AutomationApi.ToDto(rule));
        }
    }
}
