using WebExpress.WebCore.WebSection;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The entries of the workspace cog above the divider: what administers the workspace.
    /// The core's entries and a plugin's stand here, ordered by <c>[Order]</c>.
    /// </summary>
    public sealed class SectionWorkspaceSettingPrimary : ISection
    {
    }

    /// <summary>
    /// The entries of the workspace cog below the divider: what ends it (delete).
    /// </summary>
    public sealed class SectionWorkspaceSettingSecondary : ISection
    {
    }
}
