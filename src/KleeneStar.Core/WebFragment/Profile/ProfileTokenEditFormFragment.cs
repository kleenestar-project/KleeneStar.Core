using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The form that renames a personal access token and changes the scopes it grants.
    /// </summary>
    /// <remarks>
    /// The secret is never part of an edit. A token whose secret has to change is revoked and
    /// created anew, so that whatever used the old one fails loudly instead of silently
    /// continuing with different permissions.
    /// </remarks>
    [Title("kleenestar.core:profile.tokens.edit.title")]
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Tokens.Edit>]
    [Cache]
    public sealed class ProfileTokenEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control for the label of the token.
        /// </summary>
        public ControlFormItemInputText TokenName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.AccessToken.Name),
            Label = _ => "kleenestar.core:profile.tokens.name.label",
            Placeholder = _ => "kleenestar.core:profile.tokens.name.placeholder",
            Help = _ => "kleenestar.core:profile.tokens.name.help",
            Required = _ => true
        };

        /// <summary>
        /// Gets the multi-select control for the scopes the token grants.
        /// </summary>
        public ControlDataFormItemInputSelection Scopes { get; } = new()
        {
            Name = _ => nameof(Model.Entities.AccessToken.Scopes),
            Label = _ => "kleenestar.core:profile.tokens.scopes.label",
            Help = _ => "kleenestar.core:profile.tokens.scopes.help",
            MultiSelect = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Profile.Tokens.Scopes>().ToString())
        };

        /// <summary>
        /// Gets the input control for the expiry date.
        /// </summary>
        public ControlFormItemInputDate Expires { get; } = new()
        {
            Name = _ => nameof(Model.Entities.AccessToken.Expires),
            Label = _ => "kleenestar.core:profile.tokens.expires.label",
            Help = _ => "kleenestar.core:profile.tokens.expires.help",
            // culture-neutral ISO pattern so value and format stay in sync across cultures
            Format = _ => "yyyy-MM-dd",
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ProfileTokenEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(TokenName);
            Add(Scopes);
            Add(Expires);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Profile.Tokens.Index>();

            ItemId = renderContext => renderContext?.Request?.GetParameter<ParameterId>()?.Value;
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
