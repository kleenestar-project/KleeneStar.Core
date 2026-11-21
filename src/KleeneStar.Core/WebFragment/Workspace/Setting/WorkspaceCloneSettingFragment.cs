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
    /// The cog entry that opens the dialog cloning the workspace.
    /// </summary>
    [Section<SectionWorkspaceSettingPrimary>]
    [Scope<IScopeGeneral>]
    [Order(20)]
    public sealed class WorkspaceCloneSettingFragment : WorkspaceSettingItemFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceCloneSettingFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "webexpress.webapp:clone.label";
            Icon = _ => new IconClone();
        }

        /// <summary>
        /// Gets the size of the dialog the entry opens.
        /// </summary>
        protected override TypeModalSize? ModalSize => TypeModalSize.ExtraLarge;

        /// <summary>
        /// Returns the address of the dialog cloning the workspace, bound to the menu's workspace.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or <see langword="null"/> to hide the entry.</returns>
        protected override IUri Destination(IRenderControlContext renderContext)
        {
            return UriOf<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Clone>(renderContext);
        }
    }
}
