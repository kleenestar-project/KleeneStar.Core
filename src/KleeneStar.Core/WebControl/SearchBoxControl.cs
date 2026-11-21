using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Represents the global search box of the application header. The user types straight into
    /// the header and the matches drop down underneath the box: with an empty term the calling
    /// identity's most recently opened objects, with a term the objects matching it across every
    /// kind. Selecting an entry opens the object; the entry below the suggestions — and the enter
    /// key — carries the term over to the search page, which searches all workspaces.
    /// </summary>
    public class SearchBoxControl : ControlDataSearch
    {
        /// <summary>
        /// The number of suggestions the box offers before the user has to narrow the term.
        /// </summary>
        private const int MaxSuggestions = 10;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The unique identifier for the search box.</param>
        public SearchBoxControl(string id)
            : base(id)
        {
            Placeholder = _ => "kleenestar.core:search.header.placeholder";
            Icon = _ => new IconMagnifyingGlass();
            EmptyText = _ => "kleenestar.core:search.header.empty";
            MaxItems = _ => MaxSuggestions;
            SubmitUri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Search.Index>();
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Dropdown>().ToString());

            // the way out of the suggestions: the full search over all workspaces, always
            // reachable below them no matter what the term matched
            Footer = new ControlLink($"{id}-all")
            {
                Text = _ => "kleenestar.core:search.dropdown.all.label",
                Icon = _ => new IconMagnifyingGlass(),
                Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Search.Index>()
            };
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
