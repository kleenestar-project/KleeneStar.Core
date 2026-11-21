using KleeneStar.Core.WebApproval;
using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Linq;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.ApprovalPolicies._workspacekey_
{
    /// <summary>
    /// The approval policies of a workspace, and the templates a new one starts from.
    /// </summary>
    /// <remarks>
    /// The page is workspace administration (<c>RouteAuthorization</c>, area
    /// <c>approvalpolicies</c>).
    /// </remarks>
    [WebIcon<IconStamp>]
    [Title("kleenestar.core:approval.manage.title")]
    [Description("kleenestar.core:approval.manage.description")]
    [WorkspaceKeySegment]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
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

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:approval.manage.title";

            if (workspace is null)
            {
                return;
            }

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => "kleenestar.core:approval.manage.description",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
            });

            var buttons = new ControlPanel();

            buttons.Add(new ControlButtonLink()
            {
                Text = _ => "kleenestar.core:approval.policy.add",
                Icon = _ => new IconPlus(),
                Uri = _ => CoreHub.GetUri<Policy>()?.BindParameters(keyParameter),
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.Two, PropertySpacing.Space.None)
            });

            foreach (var template in ApprovalTemplates.All)
            {
                var label = Translate(template.Label);
                var uri = CoreHub.GetUri<Policy>()?.BindParameters(keyParameter)?.Add(new UriQuery("template", template.Key));

                buttons.Add(new ControlButtonLink()
                {
                    Text = _ => label,
                    Uri = _ => uri,
                    BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Secondary),
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.Two, PropertySpacing.Space.None)
                });
            }

            visualTree.Content.MainPanel.AddPrimary(buttons);

            var policies = CoreHub.ApprovalManager.GetPolicies(workspace.Id);

            if (policies.Count == 0)
            {
                visualTree.Content.MainPanel.AddPrimary(new ControlText()
                {
                    Text = _ => "kleenestar.core:approval.policy.empty",
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return;
            }

            var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:approval.policy.name"))
                .AddColumn(Translate("kleenestar.core:approval.policy.class"))
                .AddColumn(Translate("kleenestar.core:approval.policy.steps"))
                .AddColumn(Translate("kleenestar.core:approval.policy.state"))
                .AddColumn("");

            foreach (var policy in policies)
            {
                var editUri = CoreHub.GetUri<Policy>()?.BindParameters(keyParameter)?.Add(new UriQuery("policy", policy.Id.ToString()));
                var deleteUri = CoreHub.GetUri<Delete>()?.BindParameters(keyParameter)?.Add(new UriQuery("policy", policy.Id.ToString()));
                var name = policy.Name;
                var @class = CoreHub.ClassManager.GetClass(policy.ClassId)?.Name ?? "-";
                var steps = string.Join(" → ", ApprovalSteps.Parse(policy.Steps).Select(x => x.Name));
                var state = Translate(policy.State == ApprovalPolicyState.Active ? "kleenestar.core:approval.policy.state.active" : "kleenestar.core:approval.policy.state.disabled");
                var deleteLabel = Translate("webexpress.webapp:delete.label");

                table.AddRow
                (
                    new ControlTableCellPanel().Add(new ControlLink() { Text = _ => name, Uri = _ => editUri }),
                    new ControlTableCell() { Text = _ => @class },
                    new ControlTableCell() { Text = _ => steps },
                    new ControlTableCell() { Text = _ => state },
                    new ControlTableCellPanel().Add(new ControlLink() { Text = _ => deleteLabel, Uri = _ => deleteUri })
                );
            }

            visualTree.Content.MainPanel.AddPrimary(table);
        }
    }
}
