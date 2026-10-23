using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Planning._workspacekey_
{
    /// <summary>
    /// Confirms the removal of a milestone (<c>?milestone=&lt;id&gt;</c>).
    /// </summary>
    [WebIcon<IconTrash>]
    [Title("kleenestar.core:planning.milestone.delete.title")]
    [Scope<IScopeGeneral>]
    public sealed class MilestoneDelete : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(keyParameter?.Value);
            var milestone = Guid.TryParse(renderContext.Request.GetParameter("milestone")?.Value, out var milestoneId)
                ? CoreHub.PlanningManager.GetMilestone(milestoneId)
                : null;

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:planning.milestone.delete.title";

            if (workspace is null || milestone is null || milestone.WorkspaceId != workspace.Id)
            {
                return;
            }

            var name = milestone.Name;

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => name, Format = _ => TypeFormatText.H5 });
            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => "kleenestar.core:planning.milestone.delete.description", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });

            var form = new ControlModalForm("milestone-delete-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Planning._workspacekey_.DeleteMilestone>()?.BindParameters(keyParameter),
                RedirectUri = _ => CoreHub.GetUri<Index>()?.BindParameters(keyParameter)
            };

            form.Add(new PresetHiddenInput("milestone-delete-id") { Name = _ => "id", Value = _ => milestone.Id.ToString() });
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "webexpress.webapp:delete.label" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
