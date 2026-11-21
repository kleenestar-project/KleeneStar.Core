using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Notification
{
    /// <summary>
    /// The pagination below the notification center table.
    /// </summary>
    [Section<SectionViewFooterPrimary>]
    [Scope<NotificationViewFragment>]
    [Cache]
    public sealed class NotificationViewPaginationFragment : FragmentControlViewFooter
    {
        /// <summary>
        /// The id the table binds its paging source to.
        /// </summary>
        public static readonly string ContentId = "id_7F0D62B4A18E4C93B5271EAD0C86F314";

        /// <summary>
        /// Gets the pagination settings for controlling how the notifications are paged.
        /// </summary>
        public ControlPagination Pagination { get; } = new ControlPagination(ContentId)
        {
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public NotificationViewPaginationFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Pagination);
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
