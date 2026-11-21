using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Search header of the version history dialog: the box the commit list narrows itself by.
    /// </summary>
    /// <remarks>
    /// A plain search box rather than the advanced one the issue overview uses: a commit chain
    /// has no Wql endpoint behind it, and the terms a user brings to a history — a revision
    /// number, a name, a word from a commit message — need no query language.
    /// </remarks>
    [Section<SectionViewHeaderPrimary>]
    [Scope<ObjectHistoryViewFragment>]
    [Cache]
    public sealed class ObjectHistorySearchFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// Represents the unique identifier for the content used in this context.
        /// </summary>
        public static readonly string ContentId = "id_7C4E1A9B3D6F4208A5B7E0C2D9F1A634";

        /// <summary>
        /// Gets the search control used to narrow the commit chain.
        /// </summary>
        public ControlSearch Search { get; } = new ControlSearch(ContentId)
        {
            Icon = _ => new IconMagnifyingGlass()
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectHistorySearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Search.Placeholder = renderContext => I18N.Translate(renderContext, "kleenestar.core:object.history.search.placeholder");

            Add(Search);
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
            return base.Render(renderContext, visualTree);
        }
    }
}
