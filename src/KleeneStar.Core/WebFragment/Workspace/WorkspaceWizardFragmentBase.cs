using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The steps the wizards over an existing workspace share: first what the workspace is
    /// called and what it is about, then how it relates to other workspaces and which tenants it
    /// belongs to.
    /// </summary>
    /// <remarks>
    /// <see cref="WorkspaceEditFormFragment"/> and <see cref="WorkspaceCloneFormFragment"/> derive
    /// from it and set mode, finish label and icon. Both load the workspace of the route from
    /// <c>/api/1/workspaces</c> (the clone mode answers the copy's starting values) and submit the
    /// answers of both steps together on the last step. The creation wizard
    /// (<see cref="WorkspaceAddFormFragment"/>) starts with a template step and stands apart.
    /// </remarks>
    public abstract class WorkspaceWizardFragmentBase : FragmentControlDataWizard
    {
        private const string StepGeneral = "step-general";
        private const string StepSettings = "step-settings";

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
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueName>().ToString())
        };

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
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueKey>().ToString())
        };

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
        public ControlFormItemInputText Description { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Description),
            Label = _ => "kleenestar.core:workspace.description.label",
            Placeholder = _ => "kleenestar.core:workspace.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public StateInputSelection WorkspaceState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.State),
            DefaultValue = _ => global::KleeneStar.Model.Entities.WorkspaceStateExtensions.Id(global::KleeneStar.Model.Entities.WorkspaceState.Active).ToString(),
            Label = _ => "kleenestar.core:workspace.state.label",
            Placeholder = _ => "kleenestar.core:workspace.state.placeholder",
            Help = _ => "kleenestar.core:workspace.state.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.State>().ToString())
        };

        /// <summary>
        /// Gets the input selection control for the inherited workspace.
        /// </summary>
        public ControlDataFormItemInputSelection InheritedSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.InheritedId),
            Label = _ => "kleenestar.core:workspace.inherited.label",
            Placeholder = _ => "kleenestar.core:workspace.inherited.placeholder",
            Help = _ => "kleenestar.core:workspace.inherited.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces._workspacekey_.Inherited>().ToString())
        };

        /// <summary>
        /// Gets the input selection control for the access modifier.
        /// </summary>
        public ControlDataFormItemInputSelection AccessModifierSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.AccessModifier),
            Label = _ => "kleenestar.core:workspace.accessmodifier.label",
            Placeholder = _ => "kleenestar.core:workspace.accessmodifier.placeholder",
            Help = _ => "kleenestar.core:workspace.accessmodifier.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.AccessModifier>().ToString())
        };

        /// <summary>
        /// Gets the checkbox control for the sealed flag.
        /// </summary>
        public ControlFormItemInputCheck WorkspaceSealed { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Sealed),
            Label = _ => "kleenestar.core:workspace.sealed.label",
            Help = _ => "kleenestar.core:workspace.sealed.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the multi-select naming the tenants the workspace is assigned to.
        /// </summary>
        public ControlDataFormItemInputSelection Tenant { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.TenantIds),
            Label = _ => "kleenestar.core:workspace.tenant.label",
            Placeholder = _ => "kleenestar.core:workspace.tenant.placeholder",
            Help = _ => "kleenestar.core:workspace.tenant.help",
            MultiSelect = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Tenants>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected WorkspaceWizardFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var general = new ControlDataWizardPage(StepGeneral)
            {
                Title = _ => "kleenestar.core:workspace.wizard.step.general.title",
                Subtitle = _ => "kleenestar.core:workspace.wizard.step.general.subtitle"
            };
            general.Add(Key);
            general.Add(WorkspaceName);
            general.Add(Category);
            general.Add(Description);

            var settings = new ControlDataWizardPage(StepSettings)
            {
                Title = _ => "kleenestar.core:workspace.wizard.step.settings.title",
                Subtitle = _ => "kleenestar.core:workspace.wizard.step.settings.subtitle"
            };
            settings.Add(InheritedSelection);
            settings.Add(AccessModifierSelection);
            settings.Add(WorkspaceSealed);
            settings.Add(Tenant);
            settings.Add(WorkspaceState);

            Add(general, settings);

            // the wizard shapes its own load and submit requests and picks the method per
            // request, so it takes the endpoint and nothing else
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Index>();

            ItemId = renderContext =>
            {
                var key = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
                var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key?.Value);

                return workspace?.Id.ToString();
            };
        }
    }
}
