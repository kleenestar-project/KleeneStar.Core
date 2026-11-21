using KleeneStar.Core.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// The list view of the class overview: the classes of the workspace down the left, the
    /// one picked open on the right.
    /// </summary>
    /// <remarks>
    /// It is named and drawn as the list it is, the way the object overviews name theirs
    /// (<see cref="Object.Issues.IssueTabViewListFragment"/>): the view switcher offers table,
    /// tiles and list, and a third entry called something else - it used to say <i>split</i>
    /// under a split glyph - reads as a fourth kind of view rather than as the list beside the
    /// other two.
    /// <para>
    /// The detail pane loads the class page itself, which carries a filling dashboard; the
    /// shell's fill chain deliberately stops at the master-detail and does not reach into what
    /// the pane loads, or the dashboard inside the frame would turn the split into a column.
    /// </para>
    /// </remarks>
    [Section<SectionViewItemPrimary>]
    //[Policy<ClassViewPolicy>]
    [Scope<ClassViewFragment>]
    [Order(2)]
    [Cache]
    public sealed class ClassViewListFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the master-detail composite that lists the classes and opens the picked one.
        /// </summary>
        public ListDetailControl List { get; } = new ListDetailControl()
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Classes._workspacekey_.List>().ToString()),
            Bind = _ => new Binding()
                .Add(new BindSearch() { Source = ClassViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = ClassViewPaginationFragment.ContentId })
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ClassViewListFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconList();
            Title = _ => "kleenestar.core:view.list.title";

            Add(List);
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
