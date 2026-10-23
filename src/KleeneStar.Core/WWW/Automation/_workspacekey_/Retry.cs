using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Automation._workspacekey_
{
    /// <summary>
    /// Confirms running a failed automation job (<c>?job=&lt;id&gt;</c>) again.
    /// </summary>
    /// <remarks>
    /// The page shows the error the job failed with, because whoever runs it again should know
    /// what made it fail - and whether that has been dealt with.
    /// </remarks>
    [WebIcon<IconRotate>]
    [Title("kleenestar.core:automation.job.retry.title")]
    [Scope<IScopeGeneral>]
    public sealed class Retry : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Retry()
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
            var job = Guid.TryParse(renderContext.Request.GetParameter("job")?.Value, out var jobId)
                ? ModelHub.GetAutomationJob(jobId)
                : null;
            var rule = job is null ? null : CoreHub.AutomationManager.GetRule(job.RuleId);

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:automation.job.retry.title";

            if (workspace is null || rule is null || rule.WorkspaceId != workspace.Id || job.State != AutomationJobState.Failed)
            {
                return;
            }

            var error = job.LastError ?? string.Empty;
            var name = rule.Name;

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => name, Format = _ => TypeFormatText.H5 });
            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => error, TextColor = _ => new PropertyColorText(TypeColorText.Danger) });
            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => "kleenestar.core:automation.job.retry.description",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            });

            var form = new ControlModalForm("automation-job-retry-form")
            {
                Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Automation._workspacekey_.Jobs>()?.BindParameters(keyParameter),
                RedirectUri = _ => CoreHub.GetUri<Index>()?.BindParameters(keyParameter)
            };

            form.Add(new PresetHiddenInput("automation-job-retry-id") { Name = _ => "id", Value = _ => job.Id.ToString() });
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:automation.job.retry" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
