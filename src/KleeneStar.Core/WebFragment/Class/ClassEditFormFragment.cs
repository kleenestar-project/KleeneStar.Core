using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System.Collections.Generic;
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
    /// Represents a edit form fragment for a class.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Class._classid_.Edit>]
    [Cache]
    public sealed class ClassEditFormFragment : FragmentControlDataFormEdit
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
            ServiceFactory = ctx => DataServiceDescriptor
                .QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.UniqueName>().ToString())
                .BindPathVariables(BuildWorkspaceKeyBindings(ctx))};

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
            ServiceFactory = ctx => DataServiceDescriptor
                .QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Inherited>().ToString())
                .BindPathVariables(BuildWorkspaceKeyBindings(ctx))};

        /// <summary>
        /// Gets the checkbox control for the abstract flag.
        /// </summary>
        public ControlFormItemInputCheck ClassIsAbstract { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.IsAbstract),
            Label = _ => "kleenestar.core:class.isabstract.label",
            Layout = _ => TypeLayoutCheck.Switch,
            Help = _ => "kleenestar.core:class.isabstract.help"
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
            ServiceFactory = ctx => DataServiceDescriptor
                .QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.Parent>().ToString())
                .BindPathVariables(BuildWorkspaceKeyBindings(ctx))};

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
        /// matching kind overview (documents, blogs, issues, …), and changing the kind
        /// re-stamps the existing objects of the class.
        /// </summary>
        public ControlFormItemInputSelection KindSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.Kind),
            Label = _ => "kleenestar.core:class.kind.label",
            Help = _ => "kleenestar.core:class.kind.help"
        };

        /// <summary>
        /// Gets the input selection control for the renderer - the surface the objects of
        /// the class are read and written through. Left empty the class follows the
        /// default of its object type, which is what a class that was never configured
        /// does; only the renderers the chosen type offers are accepted.
        /// </summary>
        public ControlFormItemInputSelection RendererSelection { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Class.Renderer),
            Label = _ => "kleenestar.core:class.renderer.label",
            Placeholder = _ => "kleenestar.core:class.renderer.placeholder",
            Help = _ => "kleenestar.core:class.renderer.help"
        };

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
        public ClassEditFormFragment(IFragmentContext fragmentContext)
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

            // the renderer options come from the extensible renderer catalog for the same
            // reason - a plugin that ships a renderer becomes selectable here without this
            // dialog knowing it exists. The list is not filtered by the chosen object type,
            // which is picked in the same form: the endpoint refuses a pairing the type does
            // not offer, and says which ones it does
            // the entry that clears the field leads the list, the way the home-document
            // selection leads with its empty one - the way back to the default has to be
            // offered, not merely reachable by never having chosen
            RendererSelection.Add(new ControlFormItemInputSelectionItem(ObjectRendererCatalog.Automatic)
            {
                Text = _ => "kleenestar.core:class.renderer.automatic"
            });

            RendererSelection.Add(ObjectRendererCatalog.Renderers
                .Select(renderer => new ControlFormItemInputSelectionItem(renderer.Key)
                {
                    Text = _ => renderer.Label,
                    Icon = _ => renderer.Icon
                }));

            // The form's REST service is declared by the endpoint type so the
            // client loads and submits the class through the emitted
            // wx-service island. ItemId addresses the row in the body.
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

        /// <summary>
        /// Builds the manual ${workspacekey} path variable bindings for a
        /// service descriptor whose endpoint is keyed by the workspace route
        /// parameter but rendered on a page that only carries the class id.
        /// The class referenced by the request is loaded to read its
        /// workspace key, which substitutes the placeholder the sitemap
        /// would otherwise leave in the resolved base address. The
        /// automatic request binding of <see cref="EmitDataIslands"/>
        /// leaves the placeholder intact on this scope (the edit page is
        /// keyed by classid, not workspacekey), so the manual binding is
        /// required to make the client call the concrete resource.
        /// </summary>
        /// <param name="renderContext">The current render context, or null.</param>
        /// <returns>The bindings to apply, possibly empty when no workspace is resolvable.</returns>
        private static IEnumerable<KeyValuePair<string, string>> BuildWorkspaceKeyBindings(IRenderControlContext renderContext)
        {
            var request = renderContext?.Request;
            if (request == null)
            {
                return System.Array.Empty<KeyValuePair<string, string>>();
            }

            var classParameter = request.GetParameter<ClassIdParameter>();
            if (classParameter == null)
            {
                return System.Array.Empty<KeyValuePair<string, string>>();
            }

            var @class = CoreHub.ClassManager?.GetClass(classParameter);
            if (@class?.Workspace == null || string.IsNullOrEmpty(@class.Workspace.Key))
            {
                return System.Array.Empty<KeyValuePair<string, string>>();
            }

            return new[]
            {
                new KeyValuePair<string, string>("workspacekey", @class.Workspace.Key)
            };
        }
    }
}
