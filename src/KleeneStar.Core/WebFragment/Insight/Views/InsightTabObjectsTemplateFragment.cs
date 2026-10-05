using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object.Issues;
using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Tab template of an insight's objects tab: the objects the insight selects as a table or a
    /// list, with search, quickfilters and paging - the composite the issue overview's issue tab
    /// is made of. The content is contributed by <see cref="InsightTabObjectsViewFragment"/> and
    /// the fragments scoped to it.
    /// </summary>
    [Section<SectionTabViewPrimary>]
    [Scope<InsightTabFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabObjectsTemplateFragment : InsightTabTemplateFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabObjectsTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext, InsightViewTypes.Objects)
        {
        }
    }

    /// <summary>
    /// The view control of the objects tab; the search, quickfilter, table, list and pagination
    /// fragments attach themselves to it.
    /// </summary>
    [Section<WebExpress.WebApp.WebSection.SectionTabTemplatePrimary>]
    [Scope<InsightTabObjectsTemplateFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabObjectsViewFragment : FragmentControlView, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabObjectsViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Layout = _ => TypeLayoutView.ToggleGroup;
        }
    }

    /// <summary>
    /// The search of the objects tab, shared by the table and the list.
    /// </summary>
    [Section<SectionViewHeaderPrimary>]
    [Scope<InsightTabObjectsViewFragment>]
    [Cache]
    public sealed class InsightTabObjectsSearchFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// The id of the search, which the table and the list bind to.
        /// </summary>
        public static readonly string ContentId = "id_7B21C4E95A0D4F1B8E3A6C2D9F04B7E1";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabObjectsSearchFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildSearch(ContentId));
        }
    }

    /// <summary>
    /// The quickfilter bar of the objects tab: starred, assigned to me, created by me, archived,
    /// and the filters users defined on the insight.
    /// </summary>
    [Section<SectionViewHeaderSecondary>]
    [Scope<InsightTabObjectsViewFragment>]
    [Cache]
    public sealed class InsightTabObjectsQuickfilterFragment : FragmentControlViewHeader
    {
        /// <summary>
        /// The id of the quickfilter bar.
        /// </summary>
        public static readonly string ContentId = "id_4C9E1A72D5B348F0A7E2B6D10C83F95A";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabObjectsQuickfilterFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(InsightViewControls.BuildQuickfilter<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Quickfilter>(ContentId));
        }
    }

    /// <summary>
    /// The pager of the objects tab, shared by the table and the list.
    /// </summary>
    [Section<SectionViewFooterPrimary>]
    [Scope<InsightTabObjectsViewFragment>]
    [Cache]
    public sealed class InsightTabObjectsPaginationFragment : FragmentControlViewFooter
    {
        /// <summary>
        /// The id of the pager, which the table and the list bind to.
        /// </summary>
        public static readonly string ContentId = "id_E1D86B3F09A2447CB5F3A8D2C6E19B70";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabObjectsPaginationFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(new ControlPagination(ContentId));
        }
    }

    /// <summary>
    /// The table of the objects tab, with the column chooser and inline editing of the issue
    /// overview's table. The column layout is stored per reader, insight and tab: the tab
    /// endpoint writes the tab into the data service address when the template is instantiated
    /// (binding <c>insighttable</c>).
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<InsightTabObjectsViewFragment>]
    [Order(0)]
    [Cache]
    public sealed class InsightTabObjectsTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// The selector the tab-scoped endpoint address is written to: the data service island
        /// of this table.
        /// </summary>
        private const string ServiceIslandSelector = ".wx-webapp-table > wx-service";

        /// <summary>
        /// Gets the table.
        /// </summary>
        public ControlDataTable Table { get; } = new ControlDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabObjectsTableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Table.Fill = _ => true;

            Icon = _ => new IconTable();
            Title = _ => "kleenestar.core:view.table.title";

            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.Table>
            (
                descriptor => descriptor.WithDomain(DataChangedNotifier.DomainName(typeof(Model.Entities.Object)))
            );
            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = InsightTabObjectsSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = InsightTabObjectsPaginationFragment.ContentId })
                .Add(new BindTemplate().Add
                (
                    "insighttable",
                    TypeBindMode.Attr,
                    target: ServiceIslandSelector,
                    name: "base-uri"
                ));

            Add(Table);
        }

        /// <summary>
        /// Renders the table, first injecting the cell renderer the columns are drawn and edited
        /// with - the issue overview's, since the columns are the same.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var script = IssueTableInlineEditScript.Value;

            if (!string.IsNullOrEmpty(script))
            {
                visualTree.AddHeaderScript(script);
            }

            return base.Render(renderContext, visualTree);
        }
    }

    /// <summary>
    /// The list of the objects tab: the objects as a list beside the reduced reading view of the
    /// selected one.
    /// </summary>
    [Section<SectionViewItemPrimary>]
    [Scope<InsightTabObjectsViewFragment>]
    [Order(1)]
    [Cache]
    public sealed class InsightTabObjectsListFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the list.
        /// </summary>
        public ListDetailControl List { get; } = new ListDetailControl()
        {
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.List>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public InsightTabObjectsListFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconList();
            Title = _ => "kleenestar.core:view.list.title";

            List.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = InsightTabObjectsSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = InsightTabObjectsPaginationFragment.ContentId });

            Add(List);
        }
    }
}
