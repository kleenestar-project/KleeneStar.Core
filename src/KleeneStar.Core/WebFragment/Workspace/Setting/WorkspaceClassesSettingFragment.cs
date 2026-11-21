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
    /// The cog entry that leads to the data structures (classes) of the workspace.
    /// </summary>
    [Section<SectionWorkspaceSettingPrimary>]
    [Scope<IScopeGeneral>]
    [Order(40)]
    public sealed class WorkspaceClassesSettingFragment : WorkspaceSettingItemFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceClassesSettingFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:class.manage.label";
            Icon = _ => new IconClass();
        }

        /// <summary>
        /// Returns the address of the data structures (classes) of the workspace, bound to the menu's workspace.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or <see langword="null"/> to hide the entry.</returns>
        protected override IUri Destination(IRenderControlContext renderContext)
        {
            return UriOf<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>(renderContext);
        }
    }
}
