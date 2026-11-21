using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Notification
{
    /// <summary>
    /// Sidebar entry that marks every notification as seen, carrying the number still unread
    /// as its badge.
    /// </summary>
    /// <remarks>
    /// The badge is the reason this sits in the sidebar rather than among the quick filters:
    /// it is the one number the center is opened for, and it stays visible while the table
    /// below is searched and filtered.
    /// </remarks>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Notifications.Index>]
    [Order(0)]
    public sealed class NotificationSidebarReadAllLinkFragment : FragmentControlSidebarItemLink
    {
        private readonly INotificationCenterManager _notificationCenterManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context associated with the fragment.</param>
        /// <param name="notificationCenterManager">
        /// The manager used to count the unread notifications. Cannot be null.
        /// </param>
        public NotificationSidebarReadAllLinkFragment
        (
            IFragmentContext fragmentContext,
            INotificationCenterManager notificationCenterManager
        )
            : base(fragmentContext)
        {
            _notificationCenterManager = notificationCenterManager;

            Icon = _ => new IconCheckDouble();
            Text = _ => "kleenestar.core:notification.center.readall";
            Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Notifications.ReadAll>();
            Badge = ResolveBadge;
            BadgeColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Primary);
        }

        /// <summary>
        /// Resolves the badge: the number of notifications not yet seen.
        /// </summary>
        /// <remarks>
        /// An account with nothing unread shows no badge rather than a zero — there is nothing
        /// to act on, and a "0" next to "mark all as read" would read as a broken counter.
        /// </remarks>
        /// <param name="renderContext">The render context carrying the current request.</param>
        /// <returns>The count, or <see langword="null"/> when everything has been seen.</returns>
        private string ResolveBadge(IRenderControlContext renderContext)
        {
            var unread = _notificationCenterManager.GetUnreadCount(renderContext?.Request);

            return CountBadgeFormat.Format(unread, renderContext?.Request?.Culture);
        }

        /// <summary>
        /// Convert the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
