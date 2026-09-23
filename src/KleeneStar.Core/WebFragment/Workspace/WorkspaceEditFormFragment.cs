using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using System.Reflection;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// Represents a edit form fragment for a workspace.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Edit>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceAdminPolicy>>]
    [Cache]
    public sealed class WorkspaceEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the workspace.
        /// </summary>
        public ControlDataFormItemInputUnique WorkspaceName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Name),
            Label = _ => "kleenestar.core:workspace.name.label",
            Placeholder = _ => "kleenestar.core:workspace.name.placeholder",
            Help = _ => "kleenestar.core:workspace.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the key of the workspace.
        /// </summary>
        public ControlDataFormItemInputUnique Key { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Key),
            Label = _ => "kleenestar.core:workspace.key.label",
            Placeholder = _ => "kleenestar.core:workspace.key.placeholder",
            Help = _ => "kleenestar.core:workspace.key.help",
            Required = _ => true,
            MaxLength = _ => 10,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueKey>().ToString())};

        /// <summary>
        /// Gets the input tag definition for the workspace category field.
        /// </summary>
        public ControlFormItemInputTag Category { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Categories),
            Label = _ => "kleenestar.core:workspace.category.label",
            Placeholder = _ => "kleenestar.core:workspace.category.placeholder",
            Help = _ => "kleenestar.core:workspace.category.help"
        };

        /// <summary>
        /// Gets the input text control for specifying the description of the workspace.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Workspace.Description),
            Label = _ => "kleenestar.core:workspace.description.label",
            Placeholder = _ => "kleenestar.core:workspace.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection WorkspaceState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.State),
            Label = _ => "kleenestar.core:workspace.state.label",
            Placeholder = _ => "kleenestar.core:workspace.state.placeholder",
            Help = _ => "kleenestar.core:workspace.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.State>().ToString())};

        /// <summary>
        /// Gets the input selection control for the inherited workspace.
        /// </summary>
        public ControlDataFormItemInputSelection InheritedSelection { get; } = new()
        {
            Name = _ => "InheritedId",
            Label = _ => "kleenestar.core:workspace.inherited.label",
            Placeholder = _ => "kleenestar.core:workspace.inherited.placeholder",
            Help = _ => "kleenestar.core:workspace.inherited.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_.Inherited>().ToString())};

        /// <summary>
        /// Gets the input selection control for the access modifier.
        /// </summary>
        public ControlDataFormItemInputSelection AccessModifierSelection { get; } = new()
        {
            Name = _ => "AccessModifier",
            Label = _ => "kleenestar.core:workspace.accessmodifier.label",
            Placeholder = _ => "kleenestar.core:workspace.accessmodifier.placeholder",
            Help = _ => "kleenestar.core:workspace.accessmodifier.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.AccessModifier>().ToString())};

        /// <summary>
        /// Gets the checkbox control for the sealed flag.
        /// </summary>
        public ControlFormItemInputCheck WorkspaceSealed { get; } = new()
        {
            Name = _ => "Sealed",
            Label = _ => "kleenestar.core:workspace.sealed.label",
            Help = _ => "kleenestar.core:workspace.sealed.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the tenant management input.
        /// </summary>
        public ControlFormItemInputTag Tenant { get; } = new()
        {
            Name = _ => "Tenant",
            Label = _ => "kleenestar.core:workspace.tenant.label",
            Placeholder = _ => "kleenestar.core:workspace.tenant.placeholder",
            Help = _ => "kleenestar.core:workspace.tenant.help"
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Key);
            Add(WorkspaceName);
            Add(Category);
            Add(Description);
            Add(InheritedSelection);
            Add(AccessModifierSelection);
            Add(WorkspaceSealed);
            Add(Tenant);
            Add(WorkspaceState);
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
            //var key = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            //var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);

            //SetControlDisabled(InheritedSelection, workspace?.Sealed == true);
            //SetControlDisabled(AccessModifierSelection, workspace?.Sealed == true);

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Sets the value of the 'Disabled' property on the specified control object.
        /// </summary>
        /// <param name="control">
        /// The control object whose 'Disabled' property will be set. Must have a public instance 
        /// property named 'Disabled'.
        /// </param>
        /// <param name="disabled">
        /// A value indicating whether the control should be disabled. Set to <see langword="true"/>
        /// to disable the control; otherwise, <see langword="false"/>.
        /// </param>
        private static void SetControlDisabled(object control, bool disabled)
        {
            var property = control?.GetType().GetProperty("Disabled", BindingFlags.Instance | BindingFlags.Public);
            property?.SetValue(control, disabled);
        }
    }
}
