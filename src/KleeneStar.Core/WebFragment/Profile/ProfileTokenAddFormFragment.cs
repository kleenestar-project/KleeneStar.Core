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

namespace KleeneStar.Core.WebFragment.Profile
{
    /// <summary>
    /// The form that creates a personal access token.
    /// </summary>
    /// <remarks>
    /// The secret is not part of this form — it does not exist until the record is written. The
    /// endpoint returns it in the response of the create call, which is the single time it can
    /// be read; from then on only its hash is stored.
    /// </remarks>
    [Title("kleenestar.core:profile.tokens.add.title")]
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Tokens.Add>]
    [Cache]
    public sealed class ProfileTokenAddFormFragment : FragmentControlDataFormAdd
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
        public ProfileTokenAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(TokenName);
            Add(Scopes);
            Add(Expires);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Profile.Tokens.Index>();
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
