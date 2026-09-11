using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebFragment.Object;
using System.Linq;
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

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// Represents a clone form fragment for a class.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Class._classid_.Clone>]
    [Cache]
    public sealed class ClassCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the class.
        /// </summary>
        public ControlDataFormItemInputUnique ClassName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.Name),
            Label = _ => "kleenestar.core:class.name.label",
            Placeholder = _ => "kleenestar.core:class.name.placeholder",
            Help = _ => "kleenestar.core:class.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the description of the class.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Class.Description),
            Label = _ => "kleenestar.core:class.description.label",
            Placeholder = _ => "kleenestar.core:class.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the inherited class.
        /// </summary>
        public ControlDataFormItemInputSelection InheritedSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.InheritedId),
            Label = _ => "kleenestar.core:class.inherited.label",
            Placeholder = _ => "kleenestar.core:class.inherited.placeholder",
            Help = _ => "kleenestar.core:class.inherited.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Inherited>().ToString())};

        /// <summary>
        /// Gets the checkbox control for the abstract flag.
        /// </summary>
        public ControlFormItemInputCheck ClassIsAbstract { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.IsAbstract),
            Label = _ => "kleenestar.core:class.isabstract.label",
            Help = _ => "kleenestar.core:class.isabstract.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the input selection control for the parent class.
        /// </summary>
        public ControlDataFormItemInputSelection ParentSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.ParentId),
            Label = _ => "kleenestar.core:class.parent.label",
            Placeholder = _ => "kleenestar.core:class.parent.placeholder",
            Help = _ => "kleenestar.core:class.parent.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Parent>().ToString())};

        /// <summary>
        /// Gets the tag input control for specifying the allowed children classes.
        /// </summary>
        public ControlFormItemInputTag AllowedChildren { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.AllowedChildren),
            Label = _ => "kleenestar.core:class.allowedchildren.label",
            Placeholder = _ => "kleenestar.core:class.allowedchildren.placeholder",
            Help = _ => "kleenestar.core:class.allowedchildren.help"
        };

        /// <summary>
        /// Gets the input selection control for the object kind. The class is the
        /// single source of the kind: every object of the class is presented in the
        /// matching kind overview (documents, blogs, issues, …).
        /// </summary>
        public ControlFormItemInputSelection KindSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.Kind),
            Label = _ => "kleenestar.core:class.kind.label",
            Help = _ => "kleenestar.core:class.kind.help"
        };

        /// <summary>
        /// Gets the input selection control for the renderer - the surface the objects of
        /// the class are read and written through. Left at <em>follow the object type</em>
        /// the class follows the default of its type, which is what a class that was never
        /// configured does; only the renderers the chosen type offers are accepted. It
        /// projects the renderer catalog itself, so the three class dialogs offer one list
        /// and a plugin's renderer reaches all of them - see
        /// <see cref="ObjectRendererSelectionControl"/>.
        /// </summary>
        public ControlFormItemInputSelection RendererSelection { get; } = new ObjectRendererSelectionControl();

        /// <summary>
        /// Gets the input selection control for the access modifier.
        /// </summary>
        public ControlDataFormItemInputSelection AccessModifierSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.AccessModifier),
            Label = _ => "kleenestar.core:class.accessmodifier.label",
            Placeholder = _ => "kleenestar.core:class.accessmodifier.placeholder",
            Help = _ => "kleenestar.core:class.accessmodifier.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes.AccessModifier>().ToString())};

        /// <summary>
        /// Gets the checkbox control for the sealed flag.
        /// </summary>
        public ControlFormItemInputCheck ClassSealed { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.Sealed),
            Label = _ => "kleenestar.core:class.sealed.label",
            Help = _ => "kleenestar.core:class.sealed.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection ClassState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.State),
            Label = _ => "kleenestar.core:class.state.label",
            Placeholder = _ => "kleenestar.core:class.state.placeholder",
            Help = _ => "kleenestar.core:class.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes.State>().ToString())};

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ClassCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(ClassName);
            Add(Description);
            Add(KindSelection);
            Add(RendererSelection);
            Add(InheritedSelection);
            Add(ClassIsAbstract);
            Add(ParentSelection);
            Add(AllowedChildren);
            Add(AccessModifierSelection);
            Add(ClassSealed);
            Add(ClassState);

            // the kind options come from the extensible object-kind catalog, so add-on
            // kinds automatically become selectable
            KindSelection.Add(ObjectKindCatalog.Kinds
                .Select(kind => new ControlFormItemInputSelectionItem(kind.Key)
                {
                    Text = _ => kind.Label
                }));

            // the renderer options are not filled in here: the catalog is open and a plugin
            // registers into it after this fragment was built, so RendererSelection projects
            // it per render instead of being handed a snapshot that predates every add-on

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Classes.Index>();
            ItemId = renderContext =>
            {
                var classId = renderContext.Request.GetParameter<ClassIdParameter>();
                return classId?.Value?.ToString();
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
