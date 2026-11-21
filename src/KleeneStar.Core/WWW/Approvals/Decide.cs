using KleeneStar.Core.WebControl;
using KleeneStar.Core.WWW.Api._1_.Approvals;
using System;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Approvals
{
    /// <summary>
    /// The decision of an approver (<c>?approval=&lt;id&gt;</c>): what is to be approved, what
    /// was decided before, and the verdict with its reason.
    /// </summary>
    /// <remarks>
    /// The page shows the form only to an approver of the step the approval awaits; the
    /// endpoint checks the same again.
    /// </remarks>
    [WebIcon<IconStamp>]
    [Title("kleenestar.core:approval.decide.title")]
    [Scope<IScopeGeneral>]
    public sealed class Decide : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Decide()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            string Translate(string key) => I18N.Translate(renderContext, key);

            var approval = ApprovalApi.Resolve(new ApprovalApi.Payload { Approval = renderContext.Request.GetParameter("approval")?.Value }, out var @object);

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:approval.decide.title";

            if (approval is null)
            {
                return;
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
            var steps = CoreHub.ApprovalManager.StepsOf(approval);
            var culture = renderContext.Request.Culture ?? CultureInfo.InvariantCulture;
            var heading = $"{@object.Key} · {@object.Summary}";
            var policy = $"{approval.PolicyName} - {Translate("kleenestar.core:approval.state." + approval.State.ToString().ToLowerInvariant())}";

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => heading, Format = _ => TypeFormatText.H5 });
            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => policy, TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });

            if (!string.IsNullOrWhiteSpace(approval.Note))
            {
                var note = $"{ApprovalApi.NameOf(approval.RequestedById)}: {approval.Note}";

                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => note });
            }

            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                var marker = !step.Applies ? "–" : index < approval.CurrentStep || approval.State != Model.Entities.ApprovalState.Pending ? "✓" : index == approval.CurrentStep ? "▶" : "·";
                var line = $"{marker} {step.Name} ({step.Approvers})";

                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => line, Format = _ => TypeFormatText.Small });

                foreach (var decision in approval.Decisions.Where(x => x.Step == index).OrderBy(x => x.Decided))
                {
                    var text = $"    {ApprovalApi.NameOf(decision.IdentityId)} · {Translate("kleenestar.core:approval.verdict." + decision.Verdict.ToString().ToLowerInvariant())} · {decision.Decided.ToLocalTime().ToString("g", culture)}{(decision.Reason is null ? string.Empty : " · " + decision.Reason)}";

                    visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => text, Format = _ => TypeFormatText.Small, TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });
                }
            }

            if (!CoreHub.ApprovalManager.IsApprover(approval, callerId))
            {
                visualTree.Content.MainPanel.AddPrimary(new ControlText()
                {
                    Text = _ => "kleenestar.core:approval.decide.notyours",
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
                });

                return;
            }

            var verdict = new ControlFormItemInputCombo("approval-verdict")
            {
                Name = _ => "verdict",
                Label = _ => "kleenestar.core:approval.decide.verdict"
            };

            verdict.Add
            (
                new ControlFormItemInputComboItem() { Value = _ => "approve", Text = _ => Translate("kleenestar.core:approval.verdict.approve") },
                new ControlFormItemInputComboItem() { Value = _ => "reject", Text = _ => Translate("kleenestar.core:approval.verdict.reject") }
            );

            var reason = new ControlFormItemInputText("approval-reason")
            {
                Name = _ => "reason",
                Label = _ => "kleenestar.core:approval.decide.reason",
                Format = _ => TypeEditTextFormat.Multiline,
                Rows = _ => 3,
                Help = _ => "kleenestar.core:approval.decide.reason.help"
            };
            var form = new ControlModalForm("approval-decide-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Approvals.Decide>(),
                RedirectUri = _ => ObjectUri(@object)
            };

            form.Add(new PresetHiddenInput("approval-decide-id") { Name = _ => "approval", Value = _ => approval.Id.ToString() });
            form.Add(verdict, reason);
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:approval.decide.submit" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }

        /// <summary>
        /// Returns the page of an object, where its approval card shows the outcome.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The address.</returns>
        internal static WebExpress.WebCore.WebUri.IUri ObjectUri(Model.Entities.Object @object)
        {
            return CoreHub.GetUri<Issue._objectkey_.Index>()?.BindParameters(new WebParameter.ObjectKeyParameter(@object.Key));
        }
    }
}
