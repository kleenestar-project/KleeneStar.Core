using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Branding
{
    /// <summary>
    /// Represents the form on the appearance settings page with which the title and the icon the
    /// installation is presented under are set.
    /// </summary>
    /// <remarks>
    /// The identity is a singleton, so the form addresses the fixed record rather than reading an
    /// id from the route the way the other edit forms do. Both fields are optional: an empty one
    /// restores what the application declared in code, so the way back to the default is to clear
    /// the field rather than to know what the default was.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Branding>]
    [Cache]
    public sealed class BrandingEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control for the icon the application is presented under.
        /// </summary>
        /// <remarks>
        /// The avatar control is what the profile picture uses: it takes a dropped or picked image
        /// and submits it inline, which the REST endpoint stores as a file. See
        /// <see cref="WWW.Api._1_.Branding.Index"/>.
        /// </remarks>
        public ControlFormItemInputAvatar Icon { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Branding.Icon),
            Label = _ => "kleenestar.core:setting.branding.icon.label",
            Help = _ => "kleenestar.core:setting.branding.icon.help"
        };

        /// <summary>
        /// Gets the input control for the title the application is presented under.
        /// </summary>
        public ControlFormItemInputText Title { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Branding.Title),
            Label = _ => "kleenestar.core:setting.branding.title.label",
            Placeholder = _ => "kleenestar.core:setting.branding.title.placeholder",
            Help = _ => "kleenestar.core:setting.branding.title.help",
            Required = _ => false
        };

        /// <summary>
        /// Gets the prose editor for the text a visitor who is not signed in reads on the start
        /// page, beside the sign-in.
        /// </summary>
        public ControlFormItemInputText WelcomeText { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Branding.WelcomeText),
            Label = _ => "kleenestar.core:setting.branding.welcome.label",
            Placeholder = _ => "kleenestar.core:setting.branding.welcome.placeholder",
            Help = _ => "kleenestar.core:setting.branding.welcome.help",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public BrandingEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Icon);
            Add(Title);
            Add(WelcomeText);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Branding.Index>();

            ItemId = _ => Model.Entities.Branding.SingletonId.ToString();
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
