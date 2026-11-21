using KleeneStar.Core.WebApproval;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebRestApi;
using System;
using System.Linq;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Approvals
{
    /// <summary>
    /// Requests an approval of an object (<c>?object=KEY&amp;policy=&lt;id&gt;</c>), with a note to
    /// the approvers.
    /// </summary>
    [WebIcon<IconStamp>]
    [Title("kleenestar.core:approval.request.title")]
    [Scope<IScopeGeneral>]
    public sealed class Request : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Request()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(renderContext.Request.GetParameter("object")?.Value ?? string.Empty);
            var policy = Guid.TryParse(renderContext.Request.GetParameter("policy")?.Value, out var policyId)
                ? CoreHub.ApprovalManager.GetPolicy(policyId)
                : null;

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:approval.request.title";

            if (@object is null || policy is null || policy.ClassId != @object.ClassId || !ContentAuthorization.MayWrite(@object, renderContext.Request))
            {
                return;
            }

            var heading = $"{@object.Key} · {@object.Summary}";
            var steps = string.Join(" → ", ApprovalSteps.Parse(policy.Steps).Select(x => $"{x.Name} ({string.Join(", ", x.Approvers)})"));
            var name = policy.Name;

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => heading, Format = _ => TypeFormatText.H5 });
            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => name });
            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => steps, TextColor = _ => new PropertyColorText(TypeColorText.Secondary), Format = _ => TypeFormatText.Small });

            var note = new ControlFormItemInputText("approval-request-note")
            {
                Name = _ => "reason",
                Label = _ => "kleenestar.core:approval.request.note",
                Format = _ => TypeEditTextFormat.Multiline,
                Rows = _ => 3
            };
            var form = new ControlModalForm("approval-request-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Approvals.Index>(),
                RedirectUri = _ => Decide.ObjectUri(@object)
            };

            form.Add(new PresetHiddenInput("approval-request-object") { Name = _ => "object", Value = _ => @object.Key });
            form.Add(new PresetHiddenInput("approval-request-policy") { Name = _ => "policy", Value = _ => policy.Id.ToString() });
            form.Add(note);
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:approval.request.submit" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
