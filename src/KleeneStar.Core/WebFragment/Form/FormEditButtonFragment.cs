using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Form
{
    /// <summary>
    /// Represents a control fragment that provides a button link for edit a form within the workspace.
    /// </summary>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Index>]
    [Cache]
    public sealed class FormEditButtonFragment : FragmentControlButtonLink
    {
        private static readonly IUri _uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Form._formid_.Edit>();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public FormEditButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:form.edit.label";
            Icon = _ => new IconPen();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                GetUri(renderContext),
                TypeModalSize.ExtraLarge
            );
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
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Retrieves the URI for the edit page of a form based on the current render context.
        /// </summary>
        /// <param name="renderContext">
        /// The context for the current render operation, providing access to request parameters and
        /// rendering state.
        /// </param>
        /// <returns>
        /// An object representing the URI for the edit page, with parameters bound according 
        /// to the current context.
        /// </returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            var formIdParameter = renderContext.Request.GetParameter<FormIdParameter>();
            var formId = Guid.TryParse(formIdParameter?.Value, out var result) ? result : Guid.Empty;

            return _uri.BindParameters(new FormIdParameter(formId));
        }
    }
}
