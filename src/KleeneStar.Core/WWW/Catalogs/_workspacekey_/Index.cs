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

namespace KleeneStar.Core.WWW.Catalogs._workspacekey_
{
    /// <summary>
    /// The value catalogs a workspace uses: its own and the installation-wide ones, with their
    /// source, version, last refresh and the fields that draw from them.
    /// </summary>
    /// <remarks>
    /// Workspace administration (<c>RouteAuthorization</c>, area <c>catalogs</c>).
    /// </remarks>
    [WebIcon<IconList>]
    [Title("kleenestar.core:catalog.manage.title")]
    [Description("kleenestar.core:catalog.manage.description")]
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

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:catalog.manage.title";

            if (workspace is null)
            {
                return;
            }

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => "kleenestar.core:catalog.manage.description",
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
            });
            visualTree.Content.MainPanel.AddPrimary(new ControlButtonLink()
            {
                Text = _ => "kleenestar.core:catalog.add",
                Icon = _ => new IconPlus(),
                Uri = _ => CoreHub.GetUri<Catalog>()?.BindParameters(keyParameter),
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
            });

            var catalogs = CoreHub.ValueCatalogManager.GetCatalogs(workspace.Id);

            if (catalogs.Count == 0)
            {
                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => "kleenestar.core:catalog.empty", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });

                return;
            }

            var culture = renderContext.Request.Culture ?? CultureInfo.InvariantCulture;
            var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:catalog.name"))
                .AddColumn(Translate("kleenestar.core:catalog.scope"))
                .AddColumn(Translate("kleenestar.core:catalog.source"))
                .AddColumn(Translate("kleenestar.core:catalog.version"))
                .AddColumn(Translate("kleenestar.core:catalog.refresh"))
                .AddColumn(Translate("kleenestar.core:catalog.fields"));

            foreach (var catalog in catalogs)
            {
                var uri = CoreHub.GetUri<Catalog>()?.BindParameters(keyParameter)?.Add(new UriQuery("catalog", catalog.Id.ToString()));
                var name = catalog.Name;
                var scope = Translate(catalog.WorkspaceId is null ? "kleenestar.core:catalog.scope.global" : "kleenestar.core:catalog.scope.workspace");
                var source = Translate("kleenestar.core:catalog.source." + catalog.Source.ToString().ToLowerInvariant());
                var version = $"v{catalog.CurrentVersion} · {CoreHub.ValueCatalogManager.GetValues(catalog.Id).Count}";
                var refresh = catalog.LastError is not null
                    ? catalog.LastError
                    : catalog.LastRefresh?.ToLocalTime().ToString("g", culture) ?? "-";
                var failed = catalog.LastError is not null;
                var fields = CoreHub.ValueCatalogManager.GetBindings(catalog.Id).Count.ToString(culture);

                table.AddRow
                (
                    new ControlTableCellPanel().Add(new ControlLink() { Text = _ => name, Uri = _ => uri }),
                    new ControlTableCell() { Text = _ => scope },
                    new ControlTableCell() { Text = _ => source },
                    new ControlTableCell() { Text = _ => version },
                    new ControlTableCellPanel().Add(new ControlText() { Text = _ => refresh, TextColor = _ => new PropertyColorText(failed ? TypeColorText.Danger : TypeColorText.Default) }),
                    new ControlTableCell() { Text = _ => fields }
                );
            }

            visualTree.Content.MainPanel.AddPrimary(table);
        }
    }
}
