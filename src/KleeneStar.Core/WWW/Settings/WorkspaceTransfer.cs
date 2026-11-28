using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSettingPage;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings
{
    /// <summary>
    /// The settings page on which workspaces are exported into a zip archive and such an
    /// archive is imported - also one written by an older or a newer version.
    /// </summary>
    /// <remarks>
    /// The export holds everything that belongs to the chosen workspaces; accounts, groups,
    /// tenants and other data of the installation are not exported (<c>kleenestar.transfer.md</c>).
    /// The page is for the installation's administrators: an export holds every object of a
    /// workspace, classified ones included.
    /// </remarks>
    [Title("kleenestar.core:workspace.transfer.title")]
    [WebIcon<IconArrowRightArrowLeft>]
    [SettingGroup<SettingGroupGeneralGeneral>()]
    [SettingSection(SettingSection.Primary)]
    [Policy<global::WebExpress.WebCore.WebPolicies.AuthenticatedAccessPolicy>]
    [Scope<IScopeAdmin>]
    public sealed class WorkspaceTransfer : ISettingPage<VisualTreeWebAppSetting>, IScopeAdmin
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public WorkspaceTransfer()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebAppSetting visualTree)
        {
            if (!AccountAuthorization.IsAdministrator(renderContext.Request))
            {
                throw PageAuthorization.Refuse();
            }

            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Transfer>().ToString();

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:workspace.transfer.description"),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None)
            });

            var tabs = new ControlTab("ks-transfer-tabs")
            {
                Layout = _ => TypeLayoutTab.Underline
            };

            var export = new ControlTabView("ks-transfer-tab-export")
            {
                Title = _ => I18N.Translate(renderContext, "kleenestar.core:workspace.transfer.export.header")
            };

            export.Add(new WorkspaceTransferExportControl()
            {
                Uri = _ => uri,
                Workspaces = _ => CoreHub.WorkspaceManager
                    .GetWorkspaces(new Query<Workspace>())
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            });

            var import = new ControlTabView("ks-transfer-tab-import")
            {
                Title = _ => I18N.Translate(renderContext, "kleenestar.core:workspace.transfer.import.header")
            };

            import.Add(new WorkspaceTransferImportControl()
            {
                Uri = _ => uri
            });

            tabs.Add(export);
            tabs.Add(import);

            visualTree.Content.MainPanel.AddPrimary(tabs);
        }
    }
}
