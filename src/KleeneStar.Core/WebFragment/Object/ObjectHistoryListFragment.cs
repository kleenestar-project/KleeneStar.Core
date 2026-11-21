using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
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

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The commit chain of the version history dialog: a REST-backed list of the object's
    /// revisions on the master side, the selected revision on the detail side, bound to the
    /// search box and the pager of the view.
    /// </summary>
    /// <remarks>
    /// Composed exactly like the issue overview's list view — a <see cref="ListDetailControl"/>
    /// over a list endpoint, with a <see cref="Binding"/> naming the two surfaces. Nothing about
    /// a history needs its own machinery: the endpoint narrows and pages the chain, and the
    /// composite keeps the selection.
    /// </remarks>
    [Section<SectionViewItemPrimary>]
    [Scope<ObjectHistoryViewFragment>]
    [Order(0)]
    [Cache]
    public sealed class ObjectHistoryListFragment : FragmentControlViewItem
    {
        /// <summary>
        /// The number of revisions shown per page.
        /// </summary>
        private const int PageSize = 10;

        /// <summary>
        /// Gets the master-detail composite listing the revisions of the object.
        /// </summary>
        public ListDetailControl List { get; } = new ListDetailControl()
        {
            Closable = _ => false,
            MasterInitialSize = _ => 260,

            // the view is copied into a modal rather than laid into the content pane, so it
            // brings a height of its own instead of taking one from a host that has none
            Fill = _ => false,
            Styles =
            [
               "--wx-master-detail-height: 55vh;",
                "min-height: 22rem;"
            ]
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectHistoryListFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconClockRotateLeft();
            Title = _ => "kleenestar.core:object.history.title";

            List.Bind = _ => new Binding()
                .Add(new BindSearch() { Source = ObjectHistorySearchFragment.ContentId })
                .Add(new BindPaging() { Source = ObjectHistoryPaginationFragment.ContentId });

            Add(List);
        }

        /// <summary>
        /// Renders the control as an HTML node, binding the list to the chain of the object the
        /// request addresses.
        /// </summary>
        /// <remarks>
        /// The endpoint address carries the object key, so it can only be built once the request
        /// is known — unlike the overviews, whose endpoint is the same for every caller.
        /// </remarks>
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
            var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();

            if (CoreHub.ObjectManager.GetObjectByKey(objectKey) is null)
            {
                return null;
            }

            List.ServiceFactory = context => DataServiceDescriptor.QueryData
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Commits._objectkey_.Index>()
                    .BindParameters(context?.Request)
                    .ToString()
            );

            // a dialog is shorter than an overview, so the chain is walked in smaller steps than
            // the fifty entries a list defaults to - which on a normal history would mean a pager
            // that never has a second page to offer
            List.List.State(state => state.PageSize(PageSize));

            return base.Render(renderContext, visualTree);
        }
    }
}
