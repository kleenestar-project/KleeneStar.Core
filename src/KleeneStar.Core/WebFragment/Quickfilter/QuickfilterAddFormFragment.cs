using KleeneStar.Core.WebQuickfilter;
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
    /// Represents the editor in which a user defines a new quickfilter for the bar the dialog was
    /// opened from.
    /// </summary>
    /// <remarks>
    /// The form writes through the quickfilter endpoint of the bar itself, which is what the
    /// framework's create verb expects — the bar adopts the answer and shows the new chip without
    /// a reload. Which endpoint that is follows from the view the chip named, rather than from an
    /// address handed in, so this dialog cannot be pointed at something else.
    /// </remarks>
    [Title("kleenestar.core:quickfilter.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Quickfilters.Add>]
    [Cache]
    public sealed class QuickfilterAddFormFragment : FragmentControlDataFormAdd
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
        /// <remarks>
        /// The expression is the same WQL the view's advanced query accepts, so it can be tried out
        /// in the search bar before it is stored here. The framework passes it through untouched —
        /// what a filter selects is left to the application.
        /// </remarks>
        public ControlDataWqlPrompt Criteria { get; } = QuickfilterCriteria.BuildPrompt();

        /// <summary>
        /// Gets the toggle that offers the filter to everyone rather than to its owner alone.
        /// </summary>
        /// <remarks>
        /// The framework's filter payload has no field for this, so it rides along in the same body
        /// and is read from there; see <see cref="CustomQuickfilterSupport.Create"/>.
        /// </remarks>
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
        public QuickfilterAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(QuickfilterName);
            Add(QuickfilterCriteria.BuildPanel(Criteria));
            Add(Shared);

            ServiceFactory = renderContext => DataServiceDescriptor.FormData(QuickfilterService.Resolve(renderContext));
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
