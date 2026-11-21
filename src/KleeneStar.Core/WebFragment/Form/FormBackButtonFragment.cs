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
    /// Represents a back button fragment for forms, providing navigation functionality to return 
    /// to the previous view within a form context.
    /// </summary>
    [Section<SectionHeadlinePrologue>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Index>]
    [Cache]
    public sealed class FormBackButtonFragment : FragmentControlButtonLink
    {
        private static readonly IUri _uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Forms._classid_.Index>();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for 
        /// its operation. Cannot be null.
        /// </param>
        public FormBackButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:form.back.label";
            Icon = _ => new IconArrowLeft();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Secondary);
            Outline = _ => true;
            Uri = renderContext => GetUri(renderContext);
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
        /// Retrieves the URI for the index page of a form based on the current render context.
        /// </summary>
        /// <param name="renderContext">
        /// The context for the current render operation, providing access to request parameters and
        /// rendering state.
        /// </param>
        /// <returns>
        /// An object representing the URI for the form's index page, with parameters bound according 
        /// to the current context.
        /// </returns>
        private static IUri GetUri(IRenderControlContext renderContext)
        {
            var formIdParameter = renderContext.Request.GetParameter<FormIdParameter>();
            var formId = Guid.TryParse(formIdParameter?.Value, out var result) ? result : Guid.Empty;
            var form = CoreHub.FormManager.GetForm(formId);

            // the id comes from the url and may address a form that no longer exists; the
            // link then points at the unbound target rather than aborting the render
            return form is null
                ? _uri
                : _uri.BindParameters(new ClassIdParameter(form.ClassId));
        }
    }
}
