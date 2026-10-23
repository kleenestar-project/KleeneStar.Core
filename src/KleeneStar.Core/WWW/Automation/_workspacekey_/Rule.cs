using KleeneStar.Core.WebAutomation;
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

namespace KleeneStar.Core.WWW.Automation._workspacekey_
{
    /// <summary>
    /// Writes an automation rule: a new one, or - with <c>?rule=&lt;id&gt;</c> - an existing one.
    /// </summary>
    /// <remarks>
    /// The form posts to <c>/api/1/automation/{workspacekey}</c>, which validates the whole rule
    /// and refuses it with its problems; the actions are a script, one per line, and the syntax
    /// of every registered action is listed beside it - including the ones a plugin added.
    /// </remarks>
    [WebIcon<IconBolt>]
    [Title("kleenestar.core:automation.rule.title")]
    [Scope<IScopeGeneral>]
    public sealed class Rule : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Rule()
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
            var rule = Guid.TryParse(renderContext.Request.GetParameter("rule")?.Value, out var ruleId)
                ? CoreHub.AutomationManager.GetRule(ruleId)
                : null;

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:automation.rule.title";

            if (workspace is null || (rule is not null && rule.WorkspaceId != workspace.Id))
            {
                return;
            }

            var name = new ControlFormItemInputText("automation-rule-name")
            {
                Name = _ => "name",
                Label = _ => "kleenestar.core:automation.rule.name",
                Required = _ => true
            };
            var trigger = new ControlFormItemInputCombo("automation-rule-trigger")
            {
                Name = _ => "trigger",
                Label = _ => "kleenestar.core:automation.rule.trigger"
            };

            foreach (var item in AutomationTriggerCatalog.Triggers)
            {
                var label = Translate(item.Label);
                var key = item.Key;

                trigger.Add(new ControlFormItemInputComboItem() { Value = _ => key, Text = _ => label });
            }

            var parameter = new ControlFormItemInputText("automation-rule-parameter")
            {
                Name = _ => "triggerParameter",
                Label = _ => "kleenestar.core:automation.rule.parameter",
                Help = _ => string.Join(" ", AutomationTriggerCatalog.Triggers
                    .Where(x => x.Parameter is not null)
                    .Select(x => $"{Translate(x.Label)}: {Translate(x.Parameter)}"))
            };
            var scope = new ControlFormItemInputCombo("automation-rule-class")
            {
                Name = _ => "classId",
                Label = _ => "kleenestar.core:automation.rule.class"
            };

            scope.Add(new ControlFormItemInputComboItem() { Value = _ => string.Empty, Text = _ => Translate("kleenestar.core:automation.rule.class.all") });

            foreach (var @class in CoreHub.ClassManager
                .GetClasses(new Query<Model.Entities.Class>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                .Where(x => x.State == Model.Entities.ClassState.Active)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var id = @class.Id.ToString();
                var label = @class.Name;

                scope.Add(new ControlFormItemInputComboItem() { Value = _ => id, Text = _ => label });
            }

            var condition = new ControlFormItemInputText("automation-rule-condition")
            {
                Name = _ => "condition",
                Label = _ => "kleenestar.core:automation.rule.condition",
                Help = _ => "kleenestar.core:automation.rule.condition.help"
            };
            var actions = new ControlFormItemInputText("automation-rule-actions")
            {
                Name = _ => "actions",
                Label = _ => "kleenestar.core:automation.rule.actions",
                Format = _ => TypeEditTextFormat.Multiline,
                Rows = _ => 8,
                Required = _ => true,
                Help = _ => string.Join(" · ", AutomationActionCatalog.All.Select(x => Translate(x.Syntax)))
            };
            var runAs = new ControlFormItemInputText("automation-rule-runas")
            {
                Name = _ => "runAs",
                Label = _ => "kleenestar.core:automation.rule.runas",
                Help = _ => "kleenestar.core:automation.rule.runas.help"
            };
            var state = new ControlFormItemInputCombo("automation-rule-state")
            {
                Name = _ => "state",
                Label = _ => "kleenestar.core:automation.rule.state"
            };

            state.Add
            (
                new ControlFormItemInputComboItem() { Value = _ => "active", Text = _ => Translate("kleenestar.core:automation.rule.state.active") },
                new ControlFormItemInputComboItem() { Value = _ => "disabled", Text = _ => Translate("kleenestar.core:automation.rule.state.disabled") }
            );

            var overview = CoreHub.GetUri<Index>()?.BindParameters(keyParameter);
            var form = new ControlModalForm("automation-rule-form")
            {
                Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Automation._workspacekey_.Index>()?.BindParameters(keyParameter),
                RedirectUri = _ => overview
            };

            form.Add(new PresetHiddenInput("automation-rule-id") { Name = _ => "id", Value = _ => rule?.Id.ToString() ?? string.Empty });
            form.Add(name, trigger, parameter, scope, condition, actions, runAs, state);

            // an existing rule opens with what it says, so a change is made to it rather than
            // to an empty form
            if (rule is not null)
            {
                var runAsName = rule.RunAsId is Guid runAsId ? CoreHub.IdentityManager.GetIdentity(runAsId)?.UserName : null;

                form.Initialize(e =>
                {
                    e.Context.SetValue(name, new ControlFormInputValueString(rule.Name));
                    e.Context.SetValue(trigger, new ControlFormInputValueString(rule.Trigger));
                    e.Context.SetValue(parameter, new ControlFormInputValueString(rule.TriggerParameter));
                    e.Context.SetValue(scope, new ControlFormInputValueString(rule.ClassId?.ToString() ?? string.Empty));
                    e.Context.SetValue(condition, new ControlFormInputValueString(rule.Condition));
                    e.Context.SetValue(actions, new ControlFormInputValueString(rule.Actions));
                    e.Context.SetValue(runAs, new ControlFormInputValueString(runAsName));
                    e.Context.SetValue(state, new ControlFormInputValueString(rule.State == AutomationRuleState.Active ? "active" : "disabled"));
                });
            }

            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:automation.rule.save" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
