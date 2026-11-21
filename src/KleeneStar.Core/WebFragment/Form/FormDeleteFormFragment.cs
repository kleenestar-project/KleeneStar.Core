using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Form
{
    /// <summary>
    /// Represents a delete form fragment for a form.
    /// </summary>
    /// <remarks>
    /// Standard forms cannot be deleted. When this fragment is rendered for a standard form,
    /// it displays an informational message instead of the delete confirmation dialog.
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Delete>]
    [Cache]
    public sealed class FormDeleteFormFragment : FragmentControlDataFormDelete
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public FormDeleteFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Forms.Index>();
            ItemId = renderContext =>
            {
                var formId = renderContext.Request.GetParameter<FormIdParameter>();
                return formId?.Value?.ToString();
            };
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
        /// An HTML node representing the rendered control, or a warning message
        /// if the form is a standard form that cannot be deleted.
        /// </returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var param = renderContext.Request.GetParameter<FormIdParameter>();
            var formId = Guid.TryParse(param?.Value, out var id) ? id : Guid.Empty;

            // standard forms cannot be deleted
            if (CoreHub.FormManager.IsStandardForm(formId))
            {
                return new HtmlElementTextSemanticsSpan
                (
                    new HtmlText(I18N.Translate(renderContext, "kleenestar.core:form.delete.standard.warning"))
                );
            }

            return base.Render(renderContext, visualTree);
        }
    }
}

