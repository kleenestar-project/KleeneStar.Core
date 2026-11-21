using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPlanning;
using System;
using System.Globalization;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Planning._workspacekey_
{
    /// <summary>
    /// The planning of a workspace: whether time is tracked, the team's load against its
    /// capacity, and the milestones with their progress.
    /// </summary>
    /// <remarks>
    /// Workspace administration (<c>RouteAuthorization</c>, area <c>planning</c>): the team's
    /// load shows what each person logged this week.
    /// </remarks>
    [WebIcon<IconCalendarCheck>]
    [Title("kleenestar.core:planning.manage.title")]
    [Description("kleenestar.core:planning.manage.description")]
    [WorkspaceKeySegment]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
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

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:planning.manage.title";

            if (workspace is null)
            {
                return;
            }

            var culture = renderContext.Request.Culture ?? CultureInfo.InvariantCulture;
            var tracking = CoreHub.PlanningManager.IsTimeTracking(workspace.Id);
            var toggle = new ControlFormItemInputCombo("planning-timetracking")
            {
                Name = _ => "timeTracking",
                Label = _ => "kleenestar.core:planning.manage.timetracking"
            };

            toggle.Add
            (
                new ControlFormItemInputComboItem() { Value = _ => "false", Text = _ => Translate("kleenestar.core:planning.manage.timetracking.off") },
                new ControlFormItemInputComboItem() { Value = _ => "true", Text = _ => Translate("kleenestar.core:planning.manage.timetracking.on") }
            );

            var settings = new ControlModalForm("planning-settings-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Planning._workspacekey_.Index>()?.BindParameters(keyParameter),
                RedirectUri = _ => CoreHub.GetUri<Index>()?.BindParameters(keyParameter)
            };

            settings.Add(toggle);
            settings.Initialize(e => e.Context.SetValue(toggle, new ControlFormInputValueString(tracking ? "true" : "false")));
            settings.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:planning.manage.save" });
            visualTree.Content.MainPanel.AddPrimary(settings);

            Heading(visualTree, "kleenestar.core:planning.manage.team");

            var team = CoreHub.PlanningManager.GetTeamLoad(workspace.Id, DateTime.UtcNow);
            var teamTable = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:planning.manage.person"))
                .AddColumn(Translate("kleenestar.core:planning.manage.open"))
                .AddColumn(Translate("kleenestar.core:planning.estimate.remaining"))
                .AddColumn(Translate("kleenestar.core:planning.manage.weeks"))
                .AddColumn(Translate("kleenestar.core:planning.manage.logged"));

            foreach (var load in team)
            {
                var name = Api._1_.Approvals.ApprovalApi.NameOf(load.IdentityId) ?? "-";
                var open = $"{load.Open.Count} ({string.Format(Translate("kleenestar.core:planning.manage.unestimated"), load.Unestimated)})";
                var remaining = Durations.Format(load.RemainingMinutes);
                var weeks = load.Weeks.ToString("0.#", culture);
                var logged = $"{Durations.Format(load.LoggedThisWeek)} / {Durations.Format(load.WeeklyCapacity)}";

                teamTable.AddRow
                (
                    new ControlTableCell() { Text = _ => name },
                    new ControlTableCell() { Text = _ => open },
                    new ControlTableCell() { Text = _ => remaining },
                    new ControlTableCell() { Text = _ => weeks },
                    new ControlTableCell() { Text = _ => logged }
                );
            }

            visualTree.Content.MainPanel.AddPrimary(team.Count == 0
                ? new ControlText() { Text = _ => "kleenestar.core:planning.manage.team.empty", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) }
                : teamTable);

            Heading(visualTree, "kleenestar.core:planning.milestone.heading");

            visualTree.Content.MainPanel.AddPrimary(new ControlButtonLink()
            {
                Text = _ => "kleenestar.core:planning.milestone.add",
                Icon = _ => new IconPlus(),
                Uri = _ => CoreHub.GetUri<Milestone>()?.BindParameters(keyParameter),
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.None)
            });

            var milestones = CoreHub.PlanningManager.GetMilestones(workspace.Id);
            var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:planning.milestone.name"))
                .AddColumn(Translate("kleenestar.core:planning.milestone.due"))
                .AddColumn(Translate("kleenestar.core:planning.milestone.progress"))
                .AddColumn(Translate("kleenestar.core:planning.estimate.remaining"))
                .AddColumn(Translate("kleenestar.core:planning.milestone.state"));

            foreach (var milestone in milestones)
            {
                var progress = CoreHub.PlanningManager.GetProgress(milestone.Id);
                var uri = CoreHub.GetUri<Milestone>()?.BindParameters(keyParameter)?.Add(new UriQuery("milestone", milestone.Id.ToString()));
                var name = milestone.Name;
                var due = milestone.Due?.ToLocalTime().ToString("d", culture) ?? "-";
                var percent = (uint)progress.Percent;
                var text = $"{progress.Done} / {progress.Total}";
                var remaining = Durations.Format(progress.RemainingMinutes);
                var state = Translate("kleenestar.core:planning.milestone.state." + milestone.State.ToString().ToLowerInvariant());
                var overdue = milestone.State == Model.Entities.MilestoneState.Open && milestone.Due < DateTime.UtcNow;

                table.AddRow
                (
                    new ControlTableCellPanel().Add(new ControlLink() { Text = _ => name, Uri = _ => uri }),
                    new ControlTableCellPanel().Add(new ControlText() { Text = _ => due, TextColor = _ => new PropertyColorText(overdue ? TypeColorText.Danger : TypeColorText.Default) }),
                    new ControlTableCellPanel().Add(new ControlProgress($"milestone-{milestone.Id:N}") { Value = _ => percent, Text = _ => text }),
                    new ControlTableCell() { Text = _ => remaining },
                    new ControlTableCell() { Text = _ => state }
                );
            }

            visualTree.Content.MainPanel.AddPrimary(milestones.Count == 0
                ? new ControlText() { Text = _ => "kleenestar.core:planning.milestone.empty", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) }
                : table);
        }

        /// <summary>
        /// Adds a section heading.
        /// </summary>
        /// <param name="visualTree">The visual tree.</param>
        /// <param name="text">The i18n key.</param>
        private static void Heading(VisualTreeWebApp visualTree, string text)
        {
            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => text,
                Format = _ => TypeFormatText.H5,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Four, PropertySpacing.Space.Two)
            });
        }
    }
}
