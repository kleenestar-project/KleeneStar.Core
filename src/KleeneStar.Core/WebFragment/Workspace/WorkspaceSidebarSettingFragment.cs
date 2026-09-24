using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a sidebar item link fragment that displays the 'All' quick filter option in the workspace sidebar.
    /// </summary>
    [Section<SectionSidebarToolbarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    // every entry of the cog administers the workspace, so the cog is its administrators'
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceAdminPolicy>>]
    [Cache]
    public sealed class WorkspaceSidebarSettingFragment : FragmentControlToolbarItemDropdown
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager used to retrieve workspace information. Cannot be null.
        /// </param>
        public WorkspaceSidebarSettingFragment(IFragmentContext fragmentContext, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _workspaceManager = workspaceManager;

            Alignment = _ => TypeToolbarItemAlignment.Right;
            Icon = _ => new IconCog();
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();

            // on the per-kind detail pages the route carries only the object key, so the
            // workspace-bound action links are resolved through the addressed object's
            // workspace instead
            if (string.IsNullOrEmpty(keyParameter?.Value))
            {
                var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();
                var workspaceKey = CoreHub.ObjectManager.GetObjectByKey(objectKey?.Value)?.Workspace?.Key;
                keyParameter = new WorkspaceKeyParameter(workspaceKey);
            }

            var editUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Edit>()?
                .BindParameters(keyParameter);
            var cloneUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Clone>()?
                .BindParameters(keyParameter);
            var permissionsUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Permissions>()?
                .BindParameters(keyParameter);
            var deleteUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Delete>()?
                .BindParameters(keyParameter);

            var items = new IControlDropdownItem[]
            {
                new ControlDropdownItemHeader()
                {
                    Text = _ => "kleenestar.core:workspace.dropdown.label"
                },
                new ControlDropdownItemLink()
                {
                    Text = _ => "webexpress.webapp:edit.label",
                    Icon = _ => new IconPen(),
                    PrimaryAction =_ =>  new ActionModal("modal-form", editUri, TypeModalSize.ExtraLarge),
                },
                new ControlDropdownItemLink()
                {
                    Text =_ =>  "webexpress.webapp:clone.label",
                    Icon = _ => new IconClone(),
                    PrimaryAction = _ => new ActionModal("modal-form", cloneUri, TypeModalSize.ExtraLarge),
                },
                new ControlDropdownItemLink()
                {
                    Text = _ => "kleenestar.core:workspace.permissions.label",
                    Icon = _ => new IconUserShield(),
                    PrimaryAction =_ =>  new ActionModal("modal-form", permissionsUri, TypeModalSize.ExtraLarge),
                },
                new ControlDropdownItemLink()
                {
                    Text = _ => "kleenestar.core:class.manage.label",
                    Icon = _ => new IconClass(),
                    Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>()?
                        .BindParameters(keyParameter)
                },
                 new ControlDropdownItemLink()
                {
                    Text =_ =>  "kleenestar.core:template.manage.label",
                    Icon =_ =>  new IconTemplate(),
                    Uri =_ =>  CoreHub.GetUri<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>()?
                        .BindParameters(keyParameter)
                },
                new ControlDropdownItemDivider(),
                new ControlDropdownItemLink("Delete")
                {
                    Text = _ => "webexpress.webapp:delete.label",
                    Icon =_ =>  new IconTrash(),
                    PrimaryAction = _ => new ActionModal("modal-form", deleteUri, TypeModalSize.Default),
                    Color = _ => TypeColorText.Danger
                }
            };

            return base.Render(renderContext, visualTree, items);
        }
    }
}
