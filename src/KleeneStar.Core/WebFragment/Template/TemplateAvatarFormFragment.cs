using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// Represents an avatar form fragment for a template, through which its icon is chosen.
    /// </summary>
    /// <remarks>
    /// The icon is edited on its own rather than as a field of the edit form, the way a workspace
    /// and a class handle theirs: picking a picture is a different interaction from filling in a
    /// form, and the overview shows the icon on the row it belongs to.
    /// </remarks>
    [Title("kleenestar.core:template.icon.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Template._templateid_.Avatar>]
    [Cache]
    public sealed class TemplateAvatarFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control for choosing the icon of the template.
        /// </summary>
        public ControlFormItemInputAvatar Avatar { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Template.Icon),
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateAvatarFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Avatar);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Templates.Index>();
            ItemId = renderContext =>
            {
                var templateId = renderContext.Request.GetParameter<TemplateIdParameter>();
                return templateId?.Value?.ToString();
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
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
