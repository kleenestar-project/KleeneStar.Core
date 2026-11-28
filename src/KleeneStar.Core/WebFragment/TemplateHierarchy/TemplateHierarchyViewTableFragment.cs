using KleeneStar.Core.WebFragment.Template;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.TemplateHierarchy
{
    /// <summary>
    /// The table of the hierarchy overview that lists the hierarchies of the workspace
    /// (<c>/api/1/templatehierarchies/{workspacekey}/table</c>).
    /// </summary>
    /// <remarks>
    /// Search, quick filter and pagination are the shared fragments of the template overview
    /// (<see cref="TemplateViewSearchFragment"/>, <see cref="TemplateViewQuickfilterFragment"/>,
    /// <see cref="TemplateViewPaginationFragment"/>); the category of the sidebar narrows the
    /// rows through the filter binding.
    /// </remarks>
    [Section<SectionViewItemPrimary>]
    [Scope<TemplateHierarchyViewFragment>]
    [Cache]
    public sealed class TemplateHierarchyViewTableFragment : FragmentControlViewItem
    {
        /// <summary>
        /// Gets the table of the hierarchies.
        /// </summary>
        public ControlDataTable Table { get; } = new ControlDataTable();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateHierarchyViewTableFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Table.Fill = _ => true;
            Icon = _ => new IconSitemap();
            Title = _ => "kleenestar.core:template.hierarchy.view.title";

            // the table refreshes after a hierarchy is created, changed or removed
            Table.DataService<global::KleeneStar.Core.WWW.Api._1_.TemplateHierarchies._workspacekey_.Table>
            (
                descriptor => descriptor.WithDomain(DataChangedNotifier.DomainName(typeof(Model.Entities.TemplateHierarchy)))
            );
            Table.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = TemplateViewSearchFragment.ContentId })
                .Add(new BindFilter())
                .Add(new BindPaging() { Source = TemplateViewPaginationFragment.ContentId });

            Add(Table);
        }
    }
}
