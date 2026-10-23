using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPlanning;
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

namespace KleeneStar.Core.WWW.MyWork
{
    /// <summary>
    /// The caller's own work across every workspace: the open objects assigned to them with
    /// what is left of each, the week's worklogs, and the load against their capacity - which
    /// they set here themselves.
    /// </summary>
    [WebIcon<IconUserClock>]
    [Title("kleenestar.core:planning.mywork.title")]
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

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:planning.mywork.title";

            if (callerId == Guid.Empty)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var culture = renderContext.Request.Culture ?? CultureInfo.InvariantCulture;
            var load = CoreHub.PlanningManager.GetPersonalLoad(callerId, now);
            var percent = load.WeeklyCapacity == 0 ? 0 : (uint)Math.Min(100, Math.Round(100.0 * load.LoggedThisWeek / load.WeeklyCapacity));
            var summary = string.Format
            (
                Translate("kleenestar.core:planning.mywork.summary"),
                load.Open.Count,
                Durations.Format(load.RemainingMinutes),
                load.Weeks.ToString("0.#", culture),
                load.Unestimated
            );
            var week = string.Format(Translate("kleenestar.core:planning.mywork.week"), Durations.Format(load.LoggedThisWeek), Durations.Format(load.WeeklyCapacity));

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => summary });
            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => week, TextColor = _ => new PropertyColorText(TypeColorText.Secondary), Format = _ => TypeFormatText.Small });
            visualTree.Content.MainPanel.AddPrimary(new ControlProgress("mywork-week")
            {
                Value = _ => percent,
                Color = _ => new PropertyColorProgress(percent >= 100 ? TypeColorProgress.Success : TypeColorProgress.Primary),
                Text = _ => percent + " %",
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three)
            });

            var plannings = load.Open.ToDictionary(x => x.Id, x => CoreHub.PlanningManager.GetEstimate(x.Id));
            var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:planning.trace.object"))
                .AddColumn(Translate("kleenestar.core:planning.mywork.workspace"))
                .AddColumn(Translate("kleenestar.core:planning.estimate.remaining"))
                .AddColumn(Translate("kleenestar.core:planning.card.logged"));

            foreach (var @object in load.Open.OrderBy(x => plannings[x.Id].Remaining is null).ThenByDescending(x => plannings[x.Id].Remaining).ThenBy(x => x.Key))
            {
                var label = $"{@object.Key} · {@object.Summary}";
                var uri = CoreHub.GetUri<Issue._objectkey_.Index>()?.BindParameters(new ObjectKeyParameter(@object.Key));
                var workspace = CoreHub.WorkspaceManager.GetWorkspace(@object.WorkspaceId)?.Name ?? string.Empty;
                var remaining = Durations.Format(plannings[@object.Id].Remaining);
                var logged = Durations.Format(plannings[@object.Id].Logged);

                table.AddRow
                (
                    new ControlTableCellPanel().Add(new ControlLink() { Text = _ => label, Uri = _ => uri }),
                    new ControlTableCell() { Text = _ => workspace },
                    new ControlTableCell() { Text = _ => remaining },
                    new ControlTableCell() { Text = _ => logged }
                );
            }

            visualTree.Content.MainPanel.AddPrimary(load.Open.Count == 0
                ? new ControlText() { Text = _ => "kleenestar.core:planning.mywork.empty", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) }
                : table);

            var capacity = new ControlFormItemInputText("mywork-capacity")
            {
                Name = _ => "capacity",
                Label = _ => "kleenestar.core:planning.mywork.capacity",
                Help = _ => "kleenestar.core:planning.mywork.capacity.help"
            };
            var form = new ControlModalForm("mywork-capacity-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Work.Capacity>(),
                RedirectUri = _ => CoreHub.GetUri<Index>(),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Four, PropertySpacing.Space.None)
            };

            form.Add(capacity);
            form.Initialize(e => e.Context.SetValue(capacity, new ControlFormInputValueString(Durations.Format(load.WeeklyCapacity))));
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:planning.mywork.capacity.submit" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
