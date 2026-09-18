using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The invitation at the foot of the side column: a short note that what is missing or
    /// awkward can be said, and the two ways to say it.
    /// </summary>
    /// <remarks>
    /// A callout is the shape for this - a tinted aside that speaks to the reader rather than
    /// reporting something. Both actions open the same object-creation modal the rest of the
    /// application uses: feedback that arrives as an issue lands in the same lists, workflows
    /// and histories as everything else, while a separate channel would put it somewhere
    /// nobody looks.
    /// </remarks>
    internal static class LandingFeedbackSection
    {
        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The section control.</returns>
        public static IControl Build(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var callout = new ControlCallout("landing-feedback")
            {
                Title = _ => "kleenestar.core:landing.feedback.title",
                Color = _ => new PropertyColorCallout(TypeColorCallout.Info)
            };

            callout.Add(new ControlText("landing-feedback-text")
            {
                Text = _ => "kleenestar.core:landing.feedback.text",
                Format = _ => TypeFormatText.Paragraph
            });

            callout.Add(new ControlFlex
            (
                "landing-feedback-actions",
                BuildAction("landing-feedback-report", "kleenestar.core:landing.feedback.report", new IconComment()),
                BuildAction("landing-feedback-idea", "kleenestar.core:landing.feedback.idea", new IconLightbulb())
            )
            {
                Layout = _ => TypeLayoutFlex.Default,
                Justify = _ => TypeJustifiedFlex.Start,
                Gap = _ => TypeGap.Two,
                Wrap = _ => TypeWrap.Wrap
            });

            return callout;
        }

        /// <summary>
        /// Builds one of the two actions.
        /// </summary>
        /// <param name="id">The id of the button.</param>
        /// <param name="label">The resource key of the button text.</param>
        /// <param name="icon">The icon of the button.</param>
        /// <returns>The button.</returns>
        private static ControlButton BuildAction(string id, string label, WebExpress.WebCore.WebIcon.IIcon icon)
        {
            return new ControlButton(id)
            {
                Text = _ => label,
                Icon = _ => icon,
                Size = _ => TypeSizeButton.Small,
                Outline = _ => true,
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Secondary),
                PrimaryAction = _ => new ActionModal
                (
                    "modal-form",
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Add>(),
                    TypeModalSize.ExtraLarge
                )
            };
        }
    }
}
