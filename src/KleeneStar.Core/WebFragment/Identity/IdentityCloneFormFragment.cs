using KleeneStar.Core.WebParameter;
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

namespace KleeneStar.Core.WebFragment.Identity
{
    /// <summary>
    /// Represents a clone form fragment for an identity.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Identity._identityid_.Clone>]
    [Cache]
    public sealed class IdentityCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input text control for specifying the name of the identity.
        /// </summary>
        public ControlDataFormItemInputUnique IdentityName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Name),
            Label = _ => "kleenestar.core:setting.identity.name.label",
            Placeholder = _ => "kleenestar.core:setting.identity.name.placeholder",
            Help = _ => "kleenestar.core:setting.identity.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Identities.UniqueName>().ToString())};

        /// <summary>
        /// Gets the input text control for specifying the email of the identity.
        /// </summary>
        public ControlFormItemInputText Email { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Identity.Email),
            Label = _ => "kleenestar.core:setting.identity.email.label",
            Placeholder = _ => "kleenestar.core:setting.identity.email.placeholder",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection IdentityState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.State),
            Label = _ => "kleenestar.core:setting.identity.state.label",
            Placeholder = _ => "kleenestar.core:setting.identity.state.placeholder",
            Help = _ => "kleenestar.core:setting.identity.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Identities.State>().ToString())};

        /// <summary>
        /// Gets the selection of the source that authenticates the account: the installation
        /// itself, or an external source a plugin registered. Moving an account to another
        /// source drops the credentials it had with the old one.
        /// </summary>
        public ControlDataFormItemInputSelection AuthenticationSource { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.AuthenticationSource),
            Label = _ => "kleenestar.core:setting.identity.source.label",
            Help = _ => "kleenestar.core:setting.identity.source.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Identities.Sources>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IdentityCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(IdentityName);
            Add(Email);
            Add(IdentityState);
            Add(AuthenticationSource);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Identities.Index>();
            ItemId = renderContext =>
            {
                var identityId = renderContext.Request.GetParameter<IdentityIdParameter>();
                return identityId?.Value?.ToString();
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
            // a caller who does not administer the accounts gets the reason, not a form whose
            // load the endpoint refuses
            return IdentityFormGuard.Refuse(renderContext, visualTree)
                ?? base.Render(renderContext, visualTree);
        }
    }
}
