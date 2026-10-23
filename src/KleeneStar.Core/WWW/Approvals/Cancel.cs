using KleeneStar.Core.WebControl;
using KleeneStar.Core.WWW.Api._1_.Approvals;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Approvals
{
    /// <summary>
    /// Confirms withdrawing a pending approval (<c>?approval=&lt;id&gt;</c>).
    /// </summary>
    [WebIcon<IconStamp>]
    [Title("kleenestar.core:approval.cancel.title")]
    [Scope<IScopeGeneral>]
    public sealed class Cancel : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Cancel()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var approval = ApprovalApi.Resolve(new ApprovalApi.Payload { Approval = renderContext.Request.GetParameter("approval")?.Value }, out var @object);

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:approval.cancel.title";

            if (approval is null || approval.State != Model.Entities.ApprovalState.Pending)
            {
                return;
            }

            var heading = $"{@object.Key} · {approval.PolicyName}";

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => heading, Format = _ => TypeFormatText.H5 });
            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => "kleenestar.core:approval.cancel.description",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            });

            var form = new ControlModalForm("approval-cancel-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Approvals.Cancel>(),
                RedirectUri = _ => Decide.ObjectUri(@object)
            };

            form.Add(new PresetHiddenInput("approval-cancel-id") { Name = _ => "approval", Value = _ => approval.Id.ToString() });
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:approval.cancel.submit" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
