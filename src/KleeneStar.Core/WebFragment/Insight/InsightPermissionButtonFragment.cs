using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Insight
{
    /// <summary>
    /// The button on the insight overview that opens the dialog in which the installation's
    /// administrators say which groups may create insights.
    /// </summary>
    /// <remarks>
    /// Shown to the administrators only (<see cref="ContentAuthorization.MayAdministerInsightCreation"/>),
    /// the rule the dialog and its endpoint apply as well.
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Insights.Index>]
    [Cache]
    public sealed class InsightPermissionButtonFragment : FragmentControlButtonLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightPermissionButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:insight.creation.permission.label";
            PrimaryAction = _ => new ActionModal("modal-form", CoreHub.GetUri<global::KleeneStar.Core.WWW.Insights.Permission>(), TypeModalSize.ExtraLarge);
            Icon = _ => new IconUserShield();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Secondary);
        }

        /// <summary>
        /// Renders the button for an administrator, nothing for anybody else.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>The button, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request)
                || !ContentAuthorization.MayAdministerInsightCreation(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
