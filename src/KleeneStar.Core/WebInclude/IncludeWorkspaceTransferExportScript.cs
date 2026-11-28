using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes the export tab of the workspace transfer settings page.
    /// </summary>
    [Asset("/assets/js/kleenestar.core.workspace.transfer.export.js")]
    [Scope<global::KleeneStar.Core.WWW.Settings.WorkspaceTransfer>]
    public sealed class IncludeWorkspaceTransferExportScript : IInclude
    {
    }
}
