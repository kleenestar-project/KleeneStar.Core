using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.ApprovalPolicies._workspacekey_
{
    /// <summary>
    /// Confirms the removal of an approval policy (<c>?policy=&lt;id&gt;</c>).
    /// </summary>
    [WebIcon<IconTrash>]
    [Title("kleenestar.core:approval.policy.delete.title")]
    [Scope<IScopeGeneral>]
    public sealed class Delete : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Delete()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(keyParameter?.Value);
            var policy = Guid.TryParse(renderContext.Request.GetParameter("policy")?.Value, out var policyId)
                ? CoreHub.ApprovalManager.GetPolicy(policyId)
                : null;

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:approval.policy.delete.title";

            if (workspace is null || policy is null || policy.WorkspaceId != workspace.Id)
            {
                return;
            }

            var name = policy.Name;

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => name, Format = _ => TypeFormatText.H5 });
            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => "kleenestar.core:approval.policy.delete.description",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            });

            var form = new ControlModalForm("approval-policy-delete-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.ApprovalPolicies._workspacekey_.Delete>()?.BindParameters(keyParameter),
                RedirectUri = _ => CoreHub.GetUri<Index>()?.BindParameters(keyParameter)
            };

            form.Add(new PresetHiddenInput("approval-policy-delete-id") { Name = _ => "id", Value = _ => policy.Id.ToString() });
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "webexpress.webapp:delete.label" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
