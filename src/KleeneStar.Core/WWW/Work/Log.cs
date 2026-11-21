using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Core.WebRestApi;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Work
{
    /// <summary>
    /// Logs work on an object (<c>?object=KEY</c>), and lists the caller's own worklogs on it -
    /// every worklog for an administrator of the workspace.
    /// </summary>
    [WebIcon<IconClock>]
    [Title("kleenestar.core:planning.log.title")]
    [Scope<IScopeGeneral>]
    public sealed class Log : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            string Translate(string key) => I18N.Translate(renderContext, key);

            var @object = CoreHub.ObjectManager.GetObjectByKey(renderContext.Request.GetParameter("object")?.Value ?? string.Empty);

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:planning.log.title";

            if (@object is null)
            {
                return;
            }

            var heading = $"{@object.Key} · {@object.Summary}";
            var estimate = CoreHub.PlanningManager.GetEstimate(@object.Id);
            var figures = string.Format(Translate("kleenestar.core:planning.card.figures"), Durations.Format(estimate.Original), Durations.Format(estimate.Logged), Durations.Format(estimate.Remaining));

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => heading, Format = _ => TypeFormatText.H5 });
            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => figures, TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });

            if (!CoreHub.PlanningManager.IsTimeTracking(@object.WorkspaceId))
            {
                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => "kleenestar.core:planning.log.off", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });

                return;
            }

            if (ContentAuthorization.MayWrite(@object, renderContext.Request))
            {
                var started = new ControlFormItemInputText("worklog-started")
                {
                    Name = _ => "started",
                    Label = _ => "kleenestar.core:planning.log.started",
                    Help = _ => "kleenestar.core:planning.log.started.help"
                };
                var duration = new ControlFormItemInputText("worklog-duration")
                {
                    Name = _ => "duration",
                    Label = _ => "kleenestar.core:planning.log.duration",
                    Help = _ => "kleenestar.core:planning.duration.help",
                    Required = _ => true
                };
                var remaining = new ControlFormItemInputText("worklog-remaining")
                {
                    Name = _ => "remaining",
                    Label = _ => "kleenestar.core:planning.log.remaining",
                    Help = _ => "kleenestar.core:planning.log.remaining.help"
                };
                var comment = new ControlFormItemInputText("worklog-comment")
                {
                    Name = _ => "comment",
                    Label = _ => "kleenestar.core:planning.log.comment",
                    Format = _ => TypeEditTextFormat.Multiline,
                    Rows = _ => 3
                };
                var form = new ControlModalForm("worklog-form")
                {
                    Uri = _ => CoreHub.GetUri<Api._1_.Work.Worklogs>(),
                    RedirectUri = _ => Approvals.Decide.ObjectUri(@object)
                };

                form.Add(new PresetHiddenInput("worklog-object") { Name = _ => "object", Value = _ => @object.Key });
                form.Add(started, duration, remaining, comment);
                form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:planning.log.submit" });
                visualTree.Content.MainPanel.AddPrimary(form);
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);
            var administrator = ContentAuthorization.MayAdministerWorkspace(@object.WorkspaceId, renderContext.Request);
            var worklogs = CoreHub.PlanningManager.GetWorklogs(@object.Id).Where(x => administrator || x.IdentityId == callerId).ToList();

            if (worklogs.Count == 0)
            {
                return;
            }

            var culture = renderContext.Request.Culture ?? CultureInfo.InvariantCulture;
            var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:planning.log.by"))
                .AddColumn(Translate("kleenestar.core:planning.log.started"))
                .AddColumn(Translate("kleenestar.core:planning.log.duration"))
                .AddColumn(Translate("kleenestar.core:planning.log.comment"));

            foreach (var worklog in worklogs)
            {
                var by = Api._1_.Approvals.ApprovalApi.NameOf(worklog.IdentityId);
                var when = worklog.Started.ToLocalTime().ToString("g", culture);
                var spent = Durations.Format(worklog.Minutes);
                var note = worklog.Comment ?? string.Empty;

                table.AddRow
                (
                    new ControlTableCell() { Text = _ => by },
                    new ControlTableCell() { Text = _ => when },
                    new ControlTableCell() { Text = _ => spent },
                    new ControlTableCell() { Text = _ => note }
                );
            }

            visualTree.Content.MainPanel.AddPrimary(table);
        }
    }
}
