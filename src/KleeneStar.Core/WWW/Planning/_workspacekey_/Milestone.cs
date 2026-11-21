using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Linq;
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
    /// A milestone (<c>?milestone=&lt;id&gt;</c>, or none for a new one): its objects with whether
    /// they are done and how far they are traced, and the form that changes it.
    /// </summary>
    /// <remarks>
    /// The trace column answers the release question - does every change due with this release
    /// lead back to a requirement - by counting, per object, what lies upstream of it.
    /// </remarks>
    [WebIcon<IconFlagCheckered>]
    [Title("kleenestar.core:planning.milestone.title")]
    [Scope<IScopeGeneral>]
    public sealed class Milestone : IPage<VisualTreeWebApp>, IScopeGeneral
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
            var milestone = Guid.TryParse(renderContext.Request.GetParameter("milestone")?.Value, out var milestoneId)
                ? CoreHub.PlanningManager.GetMilestone(milestoneId)
                : null;

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:planning.milestone.title";

            if (workspace is null || (milestone is not null && milestone.WorkspaceId != workspace.Id))
            {
                return;
            }

            if (milestone is not null)
            {
                var progress = CoreHub.PlanningManager.GetProgress(milestone.Id);
                var summary = string.Format(Translate("kleenestar.core:planning.milestone.summary"), progress.Done, progress.Total, Durations.Format(progress.RemainingMinutes), Durations.Format(progress.LoggedMinutes));

                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => summary });

                if (progress.Hidden > 0)
                {
                    var hidden = string.Format(Translate("kleenestar.core:planning.milestone.hidden"), progress.Hidden);

                    visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => hidden, TextColor = _ => new PropertyColorText(TypeColorText.Secondary), Format = _ => TypeFormatText.Small });
                }

                var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                    .AddColumn(Translate("kleenestar.core:planning.trace.object"))
                    .AddColumn(Translate("kleenestar.core:planning.trace.class"))
                    .AddColumn(Translate("kleenestar.core:planning.trace.done"))
                    .AddColumn(Translate("kleenestar.core:planning.trace.upstream"));

                foreach (var (@object, done) in progress.Objects.OrderBy(x => x.Done).ThenBy(x => x.Object.Key))
                {
                    var label = $"{@object.Key} · {@object.Summary}";
                    var uri = CoreHub.GetUri<Issue._objectkey_.Index>()?.BindParameters(new ObjectKeyParameter(@object.Key));
                    var traceUri = CoreHub.GetUri<Work.Trace>()?.Add(new UriQuery("object", @object.Key));
                    var @class = CoreHub.ClassManager.GetClass(@object.ClassId)?.Name ?? string.Empty;
                    var upstream = Traceability.Trace(@object.Id, 2)?.Nodes.Count(x => x.Upstream) ?? 0;
                    var traced = upstream == 0 ? Translate("kleenestar.core:planning.milestone.untraced") : upstream.ToString(CultureInfo.InvariantCulture);
                    var mark = done ? "✓" : string.Empty;

                    table.AddRow
                    (
                        new ControlTableCellPanel().Add(new ControlLink() { Text = _ => label, Uri = _ => uri }),
                        new ControlTableCell() { Text = _ => @class },
                        new ControlTableCell() { Text = _ => mark },
                        new ControlTableCellPanel().Add(new ControlLink() { Text = _ => traced, Uri = _ => traceUri })
                    );
                }

                visualTree.Content.MainPanel.AddPrimary(table);
            }

            var name = new ControlFormItemInputText("milestone-name") { Name = _ => "name", Label = _ => "kleenestar.core:planning.milestone.name", Required = _ => true };
            var description = new ControlFormItemInputText("milestone-description") { Name = _ => "description", Label = _ => "kleenestar.core:planning.milestone.description" };
            var due = new ControlFormItemInputText("milestone-due") { Name = _ => "due", Label = _ => "kleenestar.core:planning.milestone.due", Help = _ => "kleenestar.core:planning.milestone.due.help" };
            var state = new ControlFormItemInputCombo("milestone-state") { Name = _ => "state", Label = _ => "kleenestar.core:planning.milestone.state" };

            foreach (var value in new[] { "open", "reached", "archived" })
            {
                var text = Translate("kleenestar.core:planning.milestone.state." + value);

                state.Add(new ControlFormItemInputComboItem() { Value = _ => value, Text = _ => text });
            }

            var form = new ControlModalForm("milestone-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Planning._workspacekey_.Milestones>()?.BindParameters(keyParameter),
                RedirectUri = _ => CoreHub.GetUri<Index>()?.BindParameters(keyParameter),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Four, PropertySpacing.Space.None)
            };

            form.Add(new PresetHiddenInput("milestone-id") { Name = _ => "id", Value = _ => milestone?.Id.ToString() ?? string.Empty });
            form.Add(name, description, due, state);

            if (milestone is not null)
            {
                form.Initialize(e =>
                {
                    e.Context.SetValue(name, new ControlFormInputValueString(milestone.Name));
                    e.Context.SetValue(description, new ControlFormInputValueString(milestone.Description));
                    e.Context.SetValue(due, new ControlFormInputValueString(milestone.Due?.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                    e.Context.SetValue(state, new ControlFormInputValueString(milestone.State.ToString().ToLowerInvariant()));
                });
            }

            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:planning.milestone.save" });
            visualTree.Content.MainPanel.AddPrimary(form);

            if (milestone is not null)
            {
                var deleteUri = CoreHub.GetUri<MilestoneDelete>()?.BindParameters(keyParameter)?.Add(new UriQuery("milestone", milestone.Id.ToString()));

                visualTree.Content.MainPanel.AddPrimary(new ControlLink() { Text = _ => "webexpress.webapp:delete.label", Uri = _ => deleteUri });
            }
        }
    }
}
