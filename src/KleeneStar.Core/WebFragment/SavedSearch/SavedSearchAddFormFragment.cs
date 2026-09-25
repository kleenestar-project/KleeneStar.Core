using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.SavedSearch
{
    /// <summary>
    /// Represents the add form fragment for a saved search.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.SavedSearches.Add>]
    [Cache]
    public sealed class SavedSearchAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the input control for the saved-search name.
        /// </summary>
        public ControlFormItemInputText SavedSearchName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SavedSearch.Name),
            Label = _ => "kleenestar.core:search.saved.name.label",
            Placeholder = _ => "kleenestar.core:search.saved.name.placeholder",
            Help = _ => "kleenestar.core:search.saved.name.help",
            Required = _ => true
        };

        /// <summary>
        /// Gets the editor for the query: the WQL prompt of the search page, with its
        /// highlighting, completion and syntax check against the object model.
        /// </summary>
        public ControlDataWqlPrompt Query { get; } = SavedSearchFormItems.BuildQuery("savedsearch-add-query");

        /// <summary>
        /// Gets the input control for the optional description, written in the prose editor.
        /// </summary>
        public ControlFormItemInputText Description { get; } = SavedSearchFormItems.BuildDescription();

        /// <summary>
        /// Gets the checkbox control for the starred flag.
        /// </summary>
        public ControlFormItemInputCheck Starred { get; } = new()
        {
            Name = _ => nameof(Model.Entities.SavedSearch.Starred),
            Label = _ => "kleenestar.core:search.saved.starred.label",
            Help = _ => "kleenestar.core:search.saved.starred.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SavedSearchAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(SavedSearchName);
            Add(SavedSearchFormItems.BuildQueryPanel(Query));
            Add(Description);
            Add(Starred);

            // opened from the search page, the dialog names the expression on screen, which
            // the endpoint answers as the query the new saved search starts with
            ServiceFactory = renderContext => DataServiceDescriptor.FormData(SavedSearchFormItems.ResolveService(renderContext));
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree, Items);
        }
    }
}

