using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebPlanning;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
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
    /// Plans an object (<c>?object=KEY</c>): its original and remaining estimate, and the
    /// milestone of its workspace it is due with.
    /// </summary>
    [WebIcon<IconClock>]
    [Title("kleenestar.core:planning.estimate.title")]
    [Scope<IScopeGeneral>]
    public sealed class Estimate : IPage<VisualTreeWebApp>, IScopeGeneral
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

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:planning.estimate.title";

            if (@object is null || !ContentAuthorization.MayWrite(@object, renderContext.Request))
            {
                return;
            }

            var heading = $"{@object.Key} · {@object.Summary}";
            var estimate = CoreHub.PlanningManager.GetEstimate(@object.Id);

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => heading, Format = _ => TypeFormatText.H5 });

            var original = new ControlFormItemInputText("estimate-original")
            {
                Name = _ => "original",
                Label = _ => "kleenestar.core:planning.estimate.original",
                Help = _ => "kleenestar.core:planning.duration.help"
            };
            var remaining = new ControlFormItemInputText("estimate-remaining")
            {
                Name = _ => "remaining",
                Label = _ => "kleenestar.core:planning.estimate.remaining"
            };
            var milestone = new ControlFormItemInputCombo("estimate-milestone")
            {
                Name = _ => "milestone",
                Label = _ => "kleenestar.core:planning.estimate.milestone"
            };

            milestone.Add(new ControlFormItemInputComboItem() { Value = _ => "-", Text = _ => Translate("kleenestar.core:planning.estimate.milestone.none") });

            foreach (var item in CoreHub.PlanningManager.GetMilestones(@object.WorkspaceId))
            {
                if (item.State == MilestoneState.Archived && item.Id != estimate.MilestoneId)
                {
                    continue;
                }

                var id = item.Id.ToString();
                var label = item.Due is { } due ? $"{item.Name} ({due.ToLocalTime():d})" : item.Name;

                milestone.Add(new ControlFormItemInputComboItem() { Value = _ => id, Text = _ => label });
            }

            var form = new ControlModalForm("estimate-form")
            {
                Uri = _ => CoreHub.GetUri<Api._1_.Work.Estimate>(),
                RedirectUri = _ => Approvals.Decide.ObjectUri(@object)
            };

            form.Add(new PresetHiddenInput("estimate-object") { Name = _ => "object", Value = _ => @object.Key });
            form.Add(original, remaining, milestone);
            form.Initialize(e =>
            {
                e.Context.SetValue(original, new ControlFormInputValueString(estimate.Original is int o ? Durations.Format(o) : null));
                e.Context.SetValue(remaining, new ControlFormInputValueString(estimate.Remaining is int r ? Durations.Format(r) : null));
                e.Context.SetValue(milestone, new ControlFormInputValueString(estimate.MilestoneId?.ToString() ?? "-"));
            });
            form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => "kleenestar.core:planning.estimate.submit" });
            visualTree.Content.MainPanel.AddPrimary(form);
        }
    }
}
