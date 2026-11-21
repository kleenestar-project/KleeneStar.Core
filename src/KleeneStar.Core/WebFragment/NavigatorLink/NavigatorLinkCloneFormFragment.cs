using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.NavigatorLink
{
    /// <summary>
    /// Represents a clone form fragment for a navigator link.
    /// </summary>
    [Title("kleenestar.core:setting.navigatorlink.clone.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Settings.NavigatorLink._navigatorlinkid_.Clone>]
    [Cache]
    public sealed class NavigatorLinkCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the navigator link.
        /// </summary>
        public ControlDataFormItemInputUnique LinkName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.NavigatorLink.Name),
            Label = _ => "kleenestar.core:setting.navigatorlink.name.label",
            Placeholder = _ => "kleenestar.core:setting.navigatorlink.name.placeholder",
            Help = _ => "kleenestar.core:setting.navigatorlink.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.NavigatorLinks.UniqueName>().ToString())
        };

        /// <summary>
        /// Gets the input text control for specifying the target address of the navigator link.
        /// </summary>
        public ControlFormItemInputText LinkUri { get; } = new()
        {
            Name = _ => nameof(Model.Entities.NavigatorLink.Uri),
            Label = _ => "kleenestar.core:setting.navigatorlink.uri.label",
            Placeholder = _ => "kleenestar.core:setting.navigatorlink.uri.placeholder",
            Help = _ => "kleenestar.core:setting.navigatorlink.uri.help",
            Required = _ => true
        };

        /// <summary>
        /// Gets the input text control for specifying the description of the navigator link.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new()
        {
            Name = _ => nameof(Model.Entities.NavigatorLink.Description),
            Label = _ => "kleenestar.core:setting.navigatorlink.description.label",
            Placeholder = _ => "kleenestar.core:setting.navigatorlink.description.placeholder",
            Help = _ => "kleenestar.core:setting.navigatorlink.description.help",
            Required = _ => false
        };


        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public StateInputSelection LinkState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.NavigatorLink.State),
            DefaultValue = _ => global::KleeneStar.Model.Entities.NavigatorLinkStateExtensions.Id(global::KleeneStar.Model.Entities.NavigatorLinkState.Active).ToString(),
            Label = _ => "kleenestar.core:setting.navigatorlink.state.label",
            Placeholder = _ => "kleenestar.core:setting.navigatorlink.state.placeholder",
            Help = _ => "kleenestar.core:setting.navigatorlink.state.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.NavigatorLinks.State>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public NavigatorLinkCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(LinkName);
            Add(LinkUri);
            Add(Description);
            Add(LinkState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.NavigatorLinks.Index>();
            ItemId = renderContext =>
            {
                var navigatorLinkId = renderContext.Request.GetParameter<NavigatorLinkIdParameter>();
                return navigatorLinkId?.Value?.ToString();
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
