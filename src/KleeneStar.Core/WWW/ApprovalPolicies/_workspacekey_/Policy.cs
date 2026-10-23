using KleeneStar.Core.WebApproval;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.ApprovalPolicies._workspacekey_
{
    /// <summary>
    /// Writes an approval policy: a new one, one started from a template
    /// (<c>?template=cab</c>), or - with <c>?policy=&lt;id&gt;</c> - an existing one.
    /// </summary>
    /// <remarks>
    /// The form posts to <c>/api/1/approvalpolicies/{workspacekey}</c>, which validates the
    /// whole policy. The states are offered for every class of the workspace and checked
    /// against the chosen class on saving.
    /// </remarks>
    [WebIcon<IconStamp>]
    [Title("kleenestar.core:approval.policy.title")]
    [Scope<IScopeGeneral>]
    public sealed class Policy : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Policy()
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

            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(keyParameter?.Value);
            var policy = Guid.TryParse(renderContext.Request.GetParameter("policy")?.Value, out var policyId)
                ? CoreHub.ApprovalManager.GetPolicy(policyId)
                : null;
            var template = ApprovalTemplates.Get(renderContext.Request.GetParameter("template")?.Value);

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:approval.policy.title";

            if (workspace is null || (policy is not null && policy.WorkspaceId != workspace.Id))
            {
                return;
            }

            if (template is not null && policy is null)
            {
                var description = Translate(template.Description);

                visualTree.Content.MainPanel.AddPrimary(new ControlText()
                {
                    Text = _ => description,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
                });
            }

            var classes = CoreHub.ClassManager
                .GetClasses(new Query<Model.Entities.Class>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                .Where(x => x.State == Model.Entities.ClassState.Active)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var name = new ControlFormItemInputText("approval-policy-name")
            {
                Name = _ => "name",
                Label = _ => "kleenestar.core:approval.policy.name",
                Required = _ => true
            };
            var description2 = new ControlFormItemInputText("approval-policy-description")
            {
                Name = _ => "description",
                Label = _ => "kleenestar.core:approval.policy.description"
            };
            var @class = new ControlFormItemInputCombo("approval-policy-class")
            {
                Name = _ => "classId",
                Label = _ => "kleenestar.core:approval.policy.class"
            };

            foreach (var item in classes)
            {
                var id = item.Id.ToString();
                var label = item.Name;

                @class.Add(new ControlFormItemInputComboItem() { Value = _ => id, Text = _ => label });
            }

            var steps = new ControlFormItemInputText("approval-policy-steps")
            {
                Name = _ => "steps",
                Label = _ => "kleenestar.core:approval.policy.steps",
                Format = _ => TypeEditTextFormat.Multiline,
                Rows = _ => 6,
                Required = _ => true,
                Help = _ => "kleenestar.core:approval.policy.steps.help"
            };

            ControlFormItemInputCombo StatusCombo(string id, string field, string label)
            {
                var combo = new ControlFormItemInputCombo(id) { Name = _ => field, Label = _ => label };

                combo.Add(new ControlFormItemInputComboItem() { Value = _ => string.Empty, Text = _ => Translate("kleenestar.core:approval.policy.status.none") });

                foreach (var item in classes)
                {
                    foreach (var status in CoreHub.StatusManager.GetStatuses(new ClassIdParameter(item.Id)).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
                    {
                        var value = status.Id.ToString();
                        var text = $"{item.Name} · {status.Name}";

                        combo.Add(new ControlFormItemInputComboItem() { Value = _ => value, Text = _ => text });
                    }
                }

                return combo;
            }

            var requestStatus = StatusCombo("approval-policy-request", "requestStatusId", "kleenestar.core:approval.policy.request");
            var approvedStatus = StatusCombo("approval-policy-approved", "approvedStatusId", "kleenestar.core:approval.policy.approved");
            var rejectedStatus = StatusCombo("approval-policy-rejected", "rejectedStatusId", "kleenestar.core:approval.policy.rejected");
            var selfApproval = new ControlFormItemInputCombo("approval-policy-self")
            {
                Name = _ => "allowSelfApproval",
                Label = _ => "kleenestar.core:approval.policy.self"
            };

            selfApproval.Add
            (
                new ControlFormItemInputComboItem() { Value = _ => "false", Text = _ => Translate("kleenestar.core:approval.policy.self.no") },
                new ControlFormItemInputComboItem() { Value = _ => "true", Text = _ => Translate("kleenestar.core:approval.policy.self.yes") }
            );

            var state = new ControlFormItemInputCombo("approval-policy-state")
            {
                Name = _ => "state",
                Label = _ => "kleenestar.core:approval.policy.state"
            };

            state.Add
            (
                new ControlFormItemInputComboItem() { Value = _ => "active", Text = _ => Translate("kleenestar.core:approval.policy.state.active") },
                new ControlFormItemInputComboItem() { Value = _ => "disabled", Text = _ => Translate("kleenestar.core:approval.policy.state.disabled") }
            );

            var form = new ControlModalForm("approval-policy-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.ApprovalPolicies._workspacekey_.Index>()?.BindParameters(keyParameter),
                RedirectUri = _ => CoreHub.GetUri<Index>()?.BindParameters(keyParameter)
            };

            form.Add(new PresetHiddenInput("approval-policy-id") { Name = _ => "id", Value = _ => policy?.Id.ToString() ?? string.Empty });
            form.Add(name, description2, @class, steps, requestStatus, approvedStatus, rejectedStatus, selfApproval, state);

            if (policy is not null || template is not null)
            {
                form.Initialize(e =>
                {
                    if (policy is null)
                    {
                        e.Context.SetValue(name, new ControlFormInputValueString(Translate(template.Label)));
                        e.Context.SetValue(steps, new ControlFormInputValueString(template.Steps));

                        return;
                    }

                    e.Context.SetValue(name, new ControlFormInputValueString(policy.Name));
                    e.Context.SetValue(description2, new ControlFormInputValueString(policy.Description));
                    e.Context.SetValue(@class, new ControlFormInputValueString(policy.ClassId.ToString()));
                    e.Context.SetValue(steps, new ControlFormInputValueString(policy.Steps));
                    e.Context.SetValue(requestStatus, new ControlFormInputValueString(policy.RequestStatusId?.ToString() ?? string.Empty));
                    e.Context.SetValue(approvedStatus, new ControlFormInputValueString(policy.ApprovedStatusId?.ToString() ?? string.Empty));
                    e.Context.SetValue(rejectedStatus, new ControlFormInputValueString(policy.RejectedStatusId?.ToString() ?? string.Empty));
                    e.Context.SetValue(selfApproval, new ControlFormInputValueString(policy.AllowSelfApproval ? "true" : "false"));
                    e.Context.SetValue(state, new ControlFormInputValueString(policy.State == ApprovalPolicyState.Active ? "active" : "disabled"));
                });
            }

            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:approval.policy.save" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
