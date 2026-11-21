using KleeneStar.Model.Entities;
using System.Linq;
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
    /// The form on the account page: login data, interface language, time zone and the regional
    /// formats dates are rendered in.
    /// </summary>
    /// <remarks>
    /// The e-mail address and the user name are shown but not editable here. Both identify the
    /// account rather than describe it — changing the address is a confirmation flow and
    /// changing the handle rewrites every URL that names it, so neither belongs behind the same
    /// save button as a time zone.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Profile.Account>]
    [Cache]
    public sealed class ProfileAccountEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the read-only control showing the login e-mail address.
        /// </summary>
        public ControlFormItemInputText Email { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Email),
            Label = _ => "kleenestar.core:profile.account.email.label",
            Help = _ => "kleenestar.core:profile.account.email.help",
            Disabled = _ => true
        };

        /// <summary>
        /// Gets the read-only control showing the user name.
        /// </summary>
        public ControlFormItemInputText UserName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.UserName),
            Label = _ => "kleenestar.core:profile.account.username.label",
            Help = _ => "kleenestar.core:profile.account.username.help",
            Disabled = _ => true
        };

        /// <summary>
        /// Gets the country control for the language of the user interface: every offered
        /// language under the flag of its country (<see cref="UiLanguage.Country"/>);
        /// <see cref="Model.Converters.LanguageConverter"/> maps the chosen country back to the
        /// language code the identity stores.
        /// </summary>
        public ControlFormItemInputCountry Language { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.Language),
            Label = _ => "kleenestar.core:profile.account.language.label",
            Help = _ => "kleenestar.core:profile.account.language.help",
            Countries = _ => UiLanguage.All.Select(x => x.Country)
        };

        /// <summary>
        /// Gets the selection control for the time zone.
        /// </summary>
        public ControlFormItemInputCombo TimeZone { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.TimeZone),
            Label = _ => "kleenestar.core:profile.account.timezone.label",
            Help = _ => "kleenestar.core:profile.account.timezone.help"
        };

        /// <summary>
        /// Gets the selection control for the date format.
        /// </summary>
        public ControlFormItemInputCombo DateFormat { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.DateFormat),
            Label = _ => "kleenestar.core:profile.account.dateformat.label",
            Help = _ => "kleenestar.core:profile.account.dateformat.help"
        };

        /// <summary>
        /// Gets the selection control for the first day of the week.
        /// </summary>
        public ControlDataFormItemInputSelection WeekStart { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Identity.WeekStart),
            Label = _ => "kleenestar.core:profile.account.weekstart.label",
            Help = _ => "kleenestar.core:profile.account.weekstart.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Profile.Weekstart>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ProfileAccountEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the empty entry is how the identity says "detect it", which is what an account
            // that has never touched the setting means
            TimeZone.Add(new ControlFormItemInputComboItem()
            {
                Value = _ => string.Empty,
                Text = _ => ProfileRegionalFormats.DescribeAutomaticTimeZone()
            });

            foreach (var (id, label) in ProfileRegionalFormats.TimeZones)
            {
                TimeZone.Add(new ControlFormItemInputComboItem()
                {
                    Value = _ => id,
                    Text = _ => label
                });
            }

            foreach (var pattern in ProfileRegionalFormats.DatePatterns)
            {
                DateFormat.Add(new ControlFormItemInputComboItem()
                {
                    Value = _ => pattern,
                    Text = _ => ProfileRegionalFormats.DescribeDatePattern(pattern)
                });
            }

            Add(Email);
            Add(UserName);
            Add(Language);
            Add(TimeZone);
            Add(DateFormat);
            Add(WeekStart);

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
