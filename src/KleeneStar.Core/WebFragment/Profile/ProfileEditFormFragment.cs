using WebExpress.WebApp.WebControl;
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
    /// The form on the profile page: the information other members of the tenant get to see —
    /// picture, display name, self-description and contact channels.
    /// </summary>
    /// <remarks>
    /// The form addresses the calling identity rather than reading an id from the route, which
    /// is what makes it a profile rather than an identity editor: <see cref="ItemId"/> is the
    /// current identity, and the endpoint behind it refuses to serve anybody else's account.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Index>]
    [Cache]
    public sealed class ProfileEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control for the profile picture.
        /// </summary>
        public ControlFormItemInputAvatar Avatar { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Avatar),
            Label = _ => "kleenestar.core:profile.field.avatar.label",
            Help = _ => "kleenestar.core:profile.field.avatar.help"
        };

        /// <summary>
        /// Gets the input control for the display name.
        /// </summary>
        public ControlFormItemInputText DisplayName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Name),
            Label = _ => "kleenestar.core:profile.field.displayname.label",
            Help = _ => "kleenestar.core:profile.field.displayname.placeholder",
            Required = _ => true
        };

        /// <summary>
        /// Gets the input control for the self-description.
        /// </summary>
        public ControlFormItemInputText Bio { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Bio),
            Label = _ => "kleenestar.core:profile.field.bio.label",
            Help = _ => "kleenestar.core:profile.field.bio.placeholder",
            Format = _ => TypeEditTextFormat.Multiline,
            Rows = _ => 3,
            Required = _ => false
        };

        /// <summary>
        /// Gets the selection control for the international dialling prefix.
        /// </summary>
        public ControlFormItemInputCombo PhoneCountry { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.PhoneCountry),
            Label = _ => "kleenestar.core:profile.field.phonecountry.label",
            Help = _ => "kleenestar.core:profile.field.phonecountry.help"
        };

        /// <summary>
        /// Gets the input control for the phone number.
        /// </summary>
        public ControlFormItemInputText Phone { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Phone),
            Label = _ => "kleenestar.core:profile.field.phone.label",
            Help = _ => "kleenestar.core:profile.field.phone.placeholder",
            Placeholder = _ => "151 23456789",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input control for the personal web site.
        /// </summary>
        public ControlFormItemInputText Website { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Website),
            Label = _ => "kleenestar.core:profile.field.website.label",
            Help = _ => "kleenestar.core:profile.field.website.placeholder",
            Placeholder = _ => "example.com",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input control for the location.
        /// </summary>
        public ControlFormItemInputText Location { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Location),
            Label = _ => "kleenestar.core:profile.field.location.label",
            Help = _ => "kleenestar.core:profile.field.location.placeholder",
            Required = _ => false
        };

        /// <summary>
        /// Gets the input control for the job title.
        /// </summary>
        public ControlFormItemInputText Position { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Position),
            Label = _ => "kleenestar.core:profile.field.position.label",
            Help = _ => "kleenestar.core:profile.field.position.placeholder",
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ProfileEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            foreach (var (code, label) in ProfileDiallingCodes.All)
            {
                PhoneCountry.Add(new ControlFormItemInputComboItem()
                {
                    Value = _ => code,
                    Text = _ => label
                });
            }

            Add(Avatar);
            Add(DisplayName);
            Add(Bio);
            Add(PhoneCountry);
            Add(Phone);
            Add(Website);
            Add(Location);
            Add(Position);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Profile.Index>();

            ItemId = renderContext => CoreHub.SessionManager
                .GetCurrentIdentityId(renderContext?.Request)
                .ToString();
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
