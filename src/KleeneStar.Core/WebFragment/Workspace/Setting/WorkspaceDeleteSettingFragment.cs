using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace.Setting
{
    /// <summary>
    /// The cog entry that opens the dialog deleting the workspace.
    /// </summary>
    [Section<SectionWorkspaceSettingSecondary>]
    [Scope<IScopeGeneral>]
    [Order(10)]
    public sealed class WorkspaceDeleteSettingFragment : WorkspaceSettingItemFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceDeleteSettingFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "webexpress.webapp:delete.label";
            Icon = _ => new IconTrash();
            Color = _ => TypeColorText.Danger;
        }

        /// <summary>
        /// Gets the size of the dialog the entry opens.
        /// </summary>
        protected override TypeModalSize? ModalSize => TypeModalSize.Default;

        /// <summary>
        /// Returns the address of the dialog deleting the workspace, bound to the menu's workspace.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or <see langword="null"/> to hide the entry.</returns>
        protected override IUri Destination(IRenderControlContext renderContext)
        {
            return UriOf<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Delete>(renderContext);
        }
    }
}
