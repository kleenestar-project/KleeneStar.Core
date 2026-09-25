using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebIdentity;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Search
{
    /// <summary>
    /// Contributes the search menu to the application header, after the dashboards: saved
    /// searches, searches shared with the caller and the last searches, each opening the search
    /// page on its query.
    /// </summary>
    /// <remarks>
    /// Every entry of the menu is somebody's - their saved searches and their search history - so
    /// the menu is offered to signed-in callers only; the search box in the header stays for
    /// everybody.
    /// </remarks>
    [Section<SectionAppNavigationPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Scope<IScopeStatusPage>]
    [Condition<SignedInCondition>]
    [Order(10)]
    [Cache]
    public sealed class SearchDropdownFragment : SearchDropdownControl, IFragmentControl<SearchDropdownControl>, IFragmentControlNavigationItem
    {
        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; private set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its
        /// operation. Cannot be null.
        /// </param>
        public SearchDropdownFragment(IFragmentContext fragmentContext)
            : base(fragmentContext?.FragmentId?.ToString())
        {
            FragmentContext = fragmentContext;
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or null for an anonymous caller.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
