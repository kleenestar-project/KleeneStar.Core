using KleeneStar.Core.WebManager;
using System.Linq;
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
    /// The button in the headline of the notification center that empties it.
    /// </summary>
    /// <remarks>
    /// Emptying the center acts on everything the table below shows, which is why it sits with
    /// that table rather than in the sidebar: the destructive action belongs next to what it
    /// destroys. It is hidden while there is nothing to clear — a button that can only do
    /// nothing is worse than no button.
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Core.WWW.Notifications.Index>]
    [Order(0)]
    public sealed class NotificationClearButtonFragment : FragmentControlButtonLink
    {
        private readonly INotificationCenterManager _notificationCenterManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="notificationCenterManager">
        /// The manager used to tell whether there is anything to clear. Cannot be null.
        /// </param>
        public NotificationClearButtonFragment
        (
            IFragmentContext fragmentContext,
            INotificationCenterManager notificationCenterManager
        )
            : base(fragmentContext)
        {
            _notificationCenterManager = notificationCenterManager;

            Text = _ => "kleenestar.core:notification.center.clear";
            Icon = _ => new IconTrashCan();
            Outline = _ => true;
            TextColor = _ => new PropertyColorText(TypeColorText.Danger);
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            Uri = renderContext => CoreHub
                .GetUri<global::KleeneStar.Core.WWW.Notifications.Clear>()?
                .BindParameters(renderContext?.Request);
        }

        /// <summary>
        /// Renders the button, or nothing when the center holds no notifications.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!_notificationCenterManager.GetNotifications(renderContext?.Request, limit: 1).Any())
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
