using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Represents the search menu of the application header: the caller's saved searches, the
    /// ones shared with them and their last searches, served by
    /// <c>/api/1/savedsearches/dropdown</c>, and the entry to the search page itself.
    /// </summary>
    public class SearchDropdownControl : ControlDataDropdown
    {
        /// <summary>
        /// Gets the link to the search page, opened without a query.
        /// </summary>
        public ControlDropdownItemLink OpenSearch { get; } = new()
        {
            Text = _ => "kleenestar.core:search.dropdown.all.label",
            Icon = _ => new IconMagnifyingGlass(),
            Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Search.Index>()
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The unique identifier for the dropdown control.</param>
        public SearchDropdownControl(string id)
            : base(id)
        {
            Text = _ => "kleenestar.core:search.dropdown.label";
            Icon = _ => new IconMagnifyingGlass();
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.SavedSearches.Dropdown>().ToString());

            Add(OpenSearch);
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
