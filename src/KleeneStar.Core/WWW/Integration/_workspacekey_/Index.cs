using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Globalization;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Integration._workspacekey_
{
    /// <summary>
    /// The connections of a workspace to other systems: its incoming webhooks, with the address
    /// and token another system is configured with, and its outgoing ones, which rules call.
    /// </summary>
    /// <remarks>
    /// The page is workspace administration (<c>RouteAuthorization</c>, area <c>integration</c>).
    /// </remarks>
    [WebIcon<IconLink>]
    [Title("kleenestar.core:integration.manage.title")]
    [Description("kleenestar.core:integration.manage.description")]
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

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:integration.manage.title";

            if (workspace is null)
            {
                return;
            }

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => "kleenestar.core:integration.manage.description",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
            });

            var newUri = CoreHub.GetUri<Webhook>()?.BindParameters(keyParameter);

            visualTree.Content.MainPanel.AddPrimary(new ControlButtonLink()
            {
                Text = _ => "kleenestar.core:integration.webhook.add",
                Icon = _ => new IconPlus(),
                Uri = _ => newUri,
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
            });

            var webhooks = CoreHub.WebhookManager.GetWebhooks(workspace.Id);

            if (webhooks.Count == 0)
            {
                visualTree.Content.MainPanel.AddPrimary(new ControlText()
                {
                    Text = _ => "kleenestar.core:integration.webhook.empty",
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return;
            }

            var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:integration.webhook.name"))
                .AddColumn(Translate("kleenestar.core:integration.webhook.direction"))
                .AddColumn(Translate("kleenestar.core:integration.webhook.target"))
                .AddColumn(Translate("kleenestar.core:integration.webhook.state"))
                .AddColumn(Translate("kleenestar.core:integration.webhook.last"))
                .AddColumn("");

            foreach (var webhook in webhooks)
            {
                var editUri = CoreHub.GetUri<Webhook>()?.BindParameters(keyParameter)?.Add(new UriQuery("webhook", webhook.Id.ToString()));
                var deleteUri = CoreHub.GetUri<Delete>()?.BindParameters(keyParameter)?.Add(new UriQuery("webhook", webhook.Id.ToString()));
                var incoming = webhook.Direction == WebhookDirection.Incoming;
                var direction = Translate(incoming ? "kleenestar.core:integration.webhook.direction.incoming" : "kleenestar.core:integration.webhook.direction.outgoing");
                var target = incoming
                    ? $"{Api._1_.Webhooks._workspacekey_.Index.AddressOf(webhook)} · {webhook.Integration}"
                    : webhook.Url;
                var state = Translate(webhook.State == WebhookState.Active ? "kleenestar.core:integration.webhook.state.active" : "kleenestar.core:integration.webhook.state.disabled");
                var last = webhook.LastDelivery is { } delivered
                    ? $"{delivered.ToLocalTime().ToString("g", renderContext.Request.Culture ?? CultureInfo.InvariantCulture)} · {webhook.LastResult}"
                    : "-";
                var name = webhook.Name;
                var deleteLabel = Translate("webexpress.webapp:delete.label");

                table.AddRow
                (
                    new ControlTableCellPanel().Add(new ControlLink() { Text = _ => name, Uri = _ => editUri }),
                    new ControlTableCell() { Text = _ => direction },
                    new ControlTableCell() { Text = _ => target },
                    new ControlTableCell() { Text = _ => state },
                    new ControlTableCell() { Text = _ => last },
                    new ControlTableCellPanel().Add(new ControlLink() { Text = _ => deleteLabel, Uri = _ => deleteUri })
                );
            }

            visualTree.Content.MainPanel.AddPrimary(table);
        }
    }
}
