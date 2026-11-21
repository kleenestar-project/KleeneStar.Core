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
    /// The cog entry that leads to the object templates of the workspace.
    /// </summary>
    [Section<SectionWorkspaceSettingPrimary>]
    [Scope<IScopeGeneral>]
    [Order(50)]
    public sealed class WorkspaceTemplatesSettingFragment : WorkspaceSettingItemFragment
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceTemplatesSettingFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:template.manage.label";
            Icon = _ => new IconTemplate();
        }

        /// <summary>
        /// Returns the address of the object templates of the workspace, bound to the menu's workspace.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or <see langword="null"/> to hide the entry.</returns>
        protected override IUri Destination(IRenderControlContext renderContext)
        {
            return UriOf<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>(renderContext);
        }
    }
}
