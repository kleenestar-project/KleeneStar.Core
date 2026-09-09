using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Represents a dropdown control for selecting a workspace.
    /// </summary>
    public class WorkspaceDropdownControl : ControlDataDropdown
    {
        /// <summary>
        /// Gets the control link for adding a new workspace.
        /// </summary>
        public ControlDropdownItemLink AddWorkspace { get; } = new()
        {
            Text = _ => "kleenestar.core:workspace.add.label",
            Icon = _ => new IconPlus(),
            PrimaryAction = _ => new ActionModal("modal-form", CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces.Add>(), TypeModalSize.ExtraLarge),
        };

        /// <summary>
        /// Gets the control link for managing workspaces.
        /// </summary>
        public ControlDropdownItemLink ManageWorkspace { get; } = new()
        {
            Text = _ => "kleenestar.core:workspace.manage.label",
            Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Workspaces.Index>(),
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The unique identifier for the dropdown control.</param>
        public WorkspaceDropdownControl(string id)
            : base(id)
        {
            // the typed declaration rather than a hand-written descriptor, because it also
            // derives the domain the endpoint serves (Workspace) off the endpoint type. The
            // domain is what puts the menu on the live update channel: a workspace created
            // from the dialog this very dropdown offers announces itself there, and without
            // the domain the menu would go on showing the list it loaded when the page was
            // built - the new workspace missing from the one place its creator looks for it
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Dropdown>();

            Add(AddWorkspace);
            Add(ManageWorkspace);
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
