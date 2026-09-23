using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a avatar form fragment for a workspace.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceAdminPolicy>>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Avatar>]
    [Cache]
    public sealed class WorkspaceAvatarFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the workspace.
        /// </summary>
        public ControlFormItemInputAvatar Avatar { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Icon),
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceAvatarFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Avatar);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Index>();
            ItemId = renderContext =>
            {
                var key = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
                var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);
                return workspace?.Id.ToString();
            };
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
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}

