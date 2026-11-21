using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebTheme;
using System;
using System.Globalization;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Appearance
{
    /// <summary>
    /// The base of the adjustments card of an appearance page (core profile, portal profile): what the reader lays over whichever theme is in
    /// use - an accent color of their own, the font size, the density and whether anything
    /// moves.
    /// </summary>
    /// <remarks>
    /// The form edits the caller's record through <typeparamref name="TEndpoint"/>; whatever id it
    /// sends, the endpoint changes only the caller's. After a save the page reloads, because
    /// the adjustments are a stylesheet the server writes into every page
    /// (<see cref="AppearanceFragmentBase"/>). Every input is a selection rather than a
    /// checkbox or free text, so each answer is one of a known set - the color picker excepted,
    /// which the endpoint checks.
    /// </remarks>
    /// <typeparam name="TEndpoint">The application's endpoint, derived from <see cref="RestApiAppearance"/>.</typeparam>
    public abstract class AppearanceFormFragmentBase<TEndpoint> : FragmentControlDataFormEdit
        where TEndpoint : RestApiAppearance
    {
        /// <summary>
        /// Gets the choice between the theme's accent and a custom one.
        /// </summary>
        public ControlFormItemInputSelection AccentMode { get; } = new ControlFormItemInputSelection()
        {
            Name = _ => RestApiAppearance.AccentModeField,
            Label = _ => "kleenestar.core:profile.appearance.accent.label",
            Help = _ => "kleenestar.core:profile.appearance.accent.help"
        }
            .Add
            (
                new ControlFormItemInputSelectionItem(AppearanceOptions.AccentTheme)
                {
                    Text = _ => "kleenestar.core:profile.appearance.accent.theme"
                },
                new ControlFormItemInputSelectionItem(AppearanceOptions.AccentCustom)
                {
                    Text = _ => "kleenestar.core:profile.appearance.accent.custom"
                }
            ) as ControlFormItemInputSelection;

        /// <summary>
        /// Gets the picker of the custom accent color.
        /// </summary>
        public ControlFormItemInputColor AccentColor { get; } = new()
        {
            Name = _ => RestApiAppearance.AccentColorField,
            Label = _ => "kleenestar.core:profile.appearance.accentcolor.label",
            Help = _ => "kleenestar.core:profile.appearance.accentcolor.help",
            Color = _ => AppearanceOptions.DefaultAccent
        };

        /// <summary>
        /// Gets the font size.
        /// </summary>
        public ControlFormItemInputSelection FontScale { get; } = CreateFontScale();

        /// <summary>
        /// Gets the density.
        /// </summary>
        public ControlFormItemInputSelection Density { get; } = new ControlFormItemInputSelection()
        {
            Name = _ => RestApiAppearance.DensityField,
            Label = _ => "kleenestar.core:profile.appearance.density.label",
            Help = _ => "kleenestar.core:profile.appearance.density.help"
        }
            .Add
            (
                new ControlFormItemInputSelectionItem(AppearanceOptions.Comfortable)
                {
                    Text = _ => "kleenestar.core:profile.appearance.density.comfortable"
                },
                new ControlFormItemInputSelectionItem(AppearanceOptions.Compact)
                {
                    Text = _ => "kleenestar.core:profile.appearance.density.compact"
                }
            ) as ControlFormItemInputSelection;

        /// <summary>
        /// Gets the motion choice.
        /// </summary>
        public ControlFormItemInputSelection ReduceMotion { get; } = new ControlFormItemInputSelection()
        {
            Name = _ => RestApiAppearance.ReduceMotionField,
            Label = _ => "kleenestar.core:profile.appearance.reducemotion.label",
            Help = _ => "kleenestar.core:profile.appearance.reducemotion.help"
        }
            .Add
            (
                new ControlFormItemInputSelectionItem("false")
                {
                    Text = _ => "kleenestar.core:profile.appearance.reducemotion.system"
                },
                new ControlFormItemInputSelectionItem("true")
                {
                    Text = _ => "kleenestar.core:profile.appearance.reducemotion.always"
                }
            ) as ControlFormItemInputSelection;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected AppearanceFormFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(AccentMode);
            Add(AccentColor);
            Add(FontScale);
            Add(Density);
            Add(ReduceMotion);

            this.DataService<TEndpoint>();

            // the endpoint reads and writes the caller's record whatever id it is sent; the id
            // is only what the edit form needs to address a record at all
            ItemId = renderContext =>
            {
                var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

                return identityId == Guid.Empty ? null : identityId.ToString();
            };

            // the adjustments are a stylesheet the server writes into the page
            SuccessAction = _ => TypeRestFormSuccess.Reload;
        }

        /// <summary>
        /// Renders the form for a signed-in caller and nothing for anybody else.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            if (CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request) == Guid.Empty)
            {
                return null;
            }

            // the card is titled like the theme card above it, so the page reads as two parts
            var title = new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.appearance.adjust.section"),
                Format = _ => TypeFormatText.H4,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two, PropertySpacing.Space.None)
            };

            var description = new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.appearance.adjust.description"),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two, PropertySpacing.Space.None)
            };

            return new HtmlElementTextContentDiv
            (
                title.Render(renderContext, visualTree),
                description.Render(renderContext, visualTree),
                base.Render(renderContext, visualTree)
            );
        }

        /// <summary>
        /// Creates the font size selection from the offered sizes.
        /// </summary>
        /// <returns>The selection.</returns>
        private static ControlFormItemInputSelection CreateFontScale()
        {
            var selection = new ControlFormItemInputSelection()
            {
                Name = _ => RestApiAppearance.FontScaleField,
                Label = _ => "kleenestar.core:profile.appearance.fontscale.label",
                Help = _ => "kleenestar.core:profile.appearance.fontscale.help"
            };

            foreach (var scale in AppearanceOptions.FontScales)
            {
                var value = scale.ToString(CultureInfo.InvariantCulture);

                selection.Add(new ControlFormItemInputSelectionItem(value)
                {
                    Text = _ => scale == AppearanceOptions.DefaultFontScale
                        ? "kleenestar.core:profile.appearance.fontscale.default"
                        : value + " %"
                });
            }

            return selection;
        }
    }
}
