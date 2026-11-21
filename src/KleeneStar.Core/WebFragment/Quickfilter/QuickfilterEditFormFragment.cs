using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Quickfilter
{
    /// <summary>
    /// Represents the editor in which a quickfilter the user defined is changed.
    /// </summary>
    /// <remarks>
    /// The form reads and writes through the quickfilter endpoint of the bar itself: asked with an
    /// id it answers in the record shape a form binds to, so the dialog loads exactly the values it
    /// will send back. Which endpoint that is follows from the view the chip named.
    /// </remarks>
    [Title("kleenestar.core:quickfilter.edit.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Quickfilters.Edit>]
    [Cache]
    public sealed class QuickfilterEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Gets the input control for the chip label.
        /// </summary>
        public ControlFormItemInputText QuickfilterName { get; } = new()
        {
            Name = _ => "name",
            Label = _ => "kleenestar.core:quickfilter.name.label",
            Placeholder = _ => "kleenestar.core:quickfilter.name.placeholder",
            Help = _ => "kleenestar.core:quickfilter.name.help",
            Required = _ => true,
            MaxLength = _ => 256
        };

        /// <summary>
        /// Gets the editor for the filter expression: the same WQL prompt the view's
        /// advanced search offers, with its highlighting, its completion of attribute and
        /// value names, and its syntax check against the entity the bar filters.
        /// </summary>
        public ControlDataWqlPrompt Criteria { get; } = QuickfilterCriteria.BuildPrompt();

        /// <summary>
        /// Gets the toggle that offers the filter to everyone rather than to its owner alone.
        /// </summary>
        public ControlFormItemInputCheck Shared { get; } = new()
        {
            Name = _ => "shared",
            Label = _ => "kleenestar.core:quickfilter.shared.label",
            Help = _ => "kleenestar.core:quickfilter.shared.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public QuickfilterEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(QuickfilterName);
            Add(QuickfilterCriteria.BuildPanel(Criteria));
            Add(Shared);

            ServiceFactory = renderContext => DataServiceDescriptor.FormData(QuickfilterService.Resolve(renderContext));

            // the bar appends the filter it stands for to the address it was authored with
            ItemId = renderContext => renderContext?.Request?.GetParameter("id")?.Value;
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
