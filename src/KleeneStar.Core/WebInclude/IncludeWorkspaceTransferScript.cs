using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes the background jobs of the workspace transfer settings page: starting, following and
    /// drawing their progress.
    /// </summary>
    [Asset("/assets/js/kleenestar.core.workspace.transfer.jobs.js")]
    [Scope<global::KleeneStar.Core.WWW.Settings.WorkspaceTransfer>]
    public sealed class IncludeWorkspaceTransferScript : IInclude
    {
    }
}
