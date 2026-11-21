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
    /// The view of the template overview that lists the hierarchies of the workspace, toggled
    /// beside the template table (<c>/api/1/templatehierarchies/{workspacekey}/table</c>).
    /// </summary>
    /// <remarks>
    /// The search and the filters of the overview narrow it as they narrow the templates. It
    /// pages on its own: the pagination of the overview counts the templates.
    /// </remarks>
    [Section<SectionViewItemPrimary>]
    [Scope<TemplateViewFragment>]
    [Order(1)]
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
                .Add(new BindFilter());

            Add(Table);
        }
    }
}
