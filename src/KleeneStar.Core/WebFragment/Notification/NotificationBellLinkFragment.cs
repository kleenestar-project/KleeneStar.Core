using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Notification
{
    /// <summary>
    /// The contents of the bell in the header: the ten newest notifications, each opening what
    /// it announced, followed by the link into the notification center.
    /// </summary>
    /// <remarks>
    /// The bell itself belongs to WebExpress (<c>ControlWebAppHeaderNotification</c>). It
    /// renders only when at least one fragment is contributed to one of its sections, and it
    /// collects those fragments by type — one type yields one entry. The ten notifications are
    /// therefore emitted by this single fragment as a list of sibling nodes rather than by ten
    /// fragment classes, which is what <see cref="HtmlList"/> exists for: it writes its
    /// children one after another without a wrapping tag, so each one lands in the dropdown as
    /// its own item.
    ///
    /// The unread count rides in the trailing link's label. The bell has no badge and no way to
    /// set one — see the remedy note in the memory of this project.
    /// </remarks>
    [Section<SectionAppNotificationPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    // a caller who is not signed in has no notifications, and the header drops the bell with
    // its last entry
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Order(0)]
    public sealed class NotificationBellLinkFragment : FragmentControlDropdownItemLink
    {
        /// <summary>
        /// How many notifications the dropdown shows before it defers to the center. Ten fills
        /// the menu without turning it into the list it links to.
        /// </summary>
        private const int PreviewCount = 10;

        private readonly INotificationCenterManager _notificationCenterManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="notificationCenterManager">
        /// The manager used to read the newest notifications. Cannot be null.
        /// </param>
        public NotificationBellLinkFragment
        (
            IFragmentContext fragmentContext,
            INotificationCenterManager notificationCenterManager
        )
            : base(fragmentContext)
        {
            _notificationCenterManager = notificationCenterManager;
        }

        /// <summary>
        /// Renders the newest notifications followed by the entry that opens the center.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>
        /// The dropdown items, or the center entry alone when nothing has arrived yet.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var notifications = _notificationCenterManager
                .GetNotifications(renderContext?.Request, limit: PreviewCount)
                .ToList();

            var nodes = new HtmlList();

            foreach (var notification in notifications)
            {
                nodes.Add(BuildItem(notification, renderContext).Render(renderContext, visualTree));
            }

            if (notifications.Count > 0)
            {
                nodes.Add(new ControlDropdownItemDivider().Render(renderContext, visualTree));
            }

            nodes.Add(BuildCenterItem(renderContext).Render(renderContext, visualTree));

            return nodes;
        }

        /// <summary>
        /// Builds one dropdown entry for a notification: what happened and what it was about,
        /// leading to the route that marks it seen and continues to the subject.
        /// </summary>
        /// <param name="notification">The notification the entry shows.</param>
        /// <param name="renderContext">The render context used for translating and binding.</param>
        /// <returns>The dropdown entry.</returns>
        private static ControlDropdownItemLink BuildItem(UserNotification notification, IRenderControlContext renderContext)
        {
            // the icon of the record the notification is about, falling back to the picture of
            // the person who caused it, and only then to the glyph for what happened
            var image = notification.SubjectIcon ?? ResolveActorImage(notification);

            return new ControlDropdownItemLink($"notification-{notification.Id:N}")
            {
                Image = _ => string.IsNullOrWhiteSpace(image) ? null : new UriEndpoint(image),
                Icon = _ => string.IsNullOrWhiteSpace(image) ? ResolveIcon(notification) : null,
                Text = _ => Describe(notification, renderContext),
                Tooltip = _ => I18N.Translate(renderContext, notification.MessageKey),
                Uri = _ => BindUri(ResolveReadUri(notification), renderContext)
            };
        }

        /// <summary>
        /// Builds the trailing entry that opens the notification center, carrying the number
        /// of unread notifications when there are any.
        /// </summary>
        /// <param name="renderContext">The render context used for translating and counting.</param>
        /// <returns>The dropdown entry.</returns>
        private ControlDropdownItemLink BuildCenterItem(IRenderControlContext renderContext)
        {
            var label = I18N.Translate(renderContext, "kleenestar.core:notification.center.showall");
            var unread = _notificationCenterManager.GetUnreadCount(renderContext?.Request);
            var badge = CountBadgeFormat.Format(unread, renderContext?.Request?.Culture);

            return new ControlDropdownItemLink("notification-center")
            {
                Icon = _ => new IconBell(),
                Text = _ => string.IsNullOrEmpty(badge) ? label : $"{label} ({badge})",
                Uri = _ => BindUri(CoreHub.GetUri<global::KleeneStar.Core.WWW.Notifications.Index>(), renderContext)
            };
        }

        /// <summary>
        /// Returns the label of an entry: what happened, followed by what it happened to, and
        /// how long ago.
        /// </summary>
        /// <param name="notification">The notification being described.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The entry label.</returns>
        private static string Describe(UserNotification notification, IRenderControlContext renderContext)
        {
            var title = I18N.Translate(renderContext, notification.TitleKey);
            var subject = string.IsNullOrWhiteSpace(notification.Subject)
                ? title
                : $"{title} · {notification.Subject}";

            return $"{subject} · {DescribeActor(notification, renderContext)} · {DescribeAge(notification, renderContext)}";
        }

        /// <summary>
        /// Returns the name of the identity that caused the event, or the system when nothing
        /// but a job stands behind it.
        /// </summary>
        /// <param name="notification">The notification being described.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The name of the actor.</returns>
        private static string DescribeActor(UserNotification notification, IRenderControlContext renderContext)
        {
            var system = I18N.Translate(renderContext, "kleenestar.core:notification.center.actor.system");

            if (!notification.ActorId.HasValue)
            {
                return system;
            }

            return CoreHub.IdentityManager.GetIdentity(notification.ActorId.Value)?.Name ?? system;
        }

        /// <summary>
        /// Returns the picture of the identity that caused the event, used when the record the
        /// notification is about carries no icon of its own.
        /// </summary>
        /// <param name="notification">The notification being rendered.</param>
        /// <returns>The path of the picture, or <see langword="null"/>.</returns>
        private static string ResolveActorImage(UserNotification notification)
        {
            if (!notification.ActorId.HasValue)
            {
                return null;
            }

            return CoreHub.IdentityManager.GetIdentity(notification.ActorId.Value)?.Avatar?.Uri?.ToString();
        }

        /// <summary>
        /// Returns how long ago the notification arrived, in the coarsest unit that still says
        /// something.
        /// </summary>
        /// <param name="notification">The notification being described.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The age phrase.</returns>
        private static string DescribeAge(UserNotification notification, IRenderControlContext renderContext)
        {
            var elapsed = DateTime.UtcNow - notification.Created;

            if (elapsed < TimeSpan.FromMinutes(5))
            {
                return I18N.Translate(renderContext, "kleenestar.core:notification.center.age.now");
            }

            if (elapsed < TimeSpan.FromHours(1))
            {
                return Format(renderContext, "kleenestar.core:notification.center.age.minutes", (int)elapsed.TotalMinutes);
            }

            if (elapsed < TimeSpan.FromDays(1))
            {
                return Format(renderContext, "kleenestar.core:notification.center.age.hours", (int)elapsed.TotalHours);
            }

            return Format(renderContext, "kleenestar.core:notification.center.age.days", (int)elapsed.TotalDays);
        }

        /// <summary>
        /// Formats a translated pattern with a single count, never below one so an entry that
        /// just crossed a boundary does not read as "0 hours ago".
        /// </summary>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <param name="key">The translation key of the pattern.</param>
        /// <param name="count">The count to insert.</param>
        /// <returns>The formatted phrase.</returns>
        private static string Format(IRenderControlContext renderContext, string key, int count)
        {
            return string.Format(CultureInfo.CurrentCulture, I18N.Translate(renderContext, key), Math.Max(1, count));
        }

        /// <summary>
        /// Returns the icon for a notification, derived from the heading key so a created,
        /// updated or deleted entry is recognizable before it is read.
        /// </summary>
        /// <param name="notification">The notification being rendered.</param>
        /// <returns>The icon of the entry.</returns>
        private static IIcon ResolveIcon(UserNotification notification)
        {
            var key = notification.TitleKey ?? string.Empty;

            if (key.EndsWith(".created", StringComparison.OrdinalIgnoreCase))
            {
                return new IconPlus();
            }

            if (key.EndsWith(".updated", StringComparison.OrdinalIgnoreCase))
            {
                return new IconPen();
            }

            if (key.EndsWith(".deleted", StringComparison.OrdinalIgnoreCase))
            {
                return new IconTrashCan();
            }

            if (key.EndsWith(".error", StringComparison.OrdinalIgnoreCase))
            {
                return new IconTriangleExclamation();
            }

            return new IconBell();
        }

        /// <summary>
        /// Returns the link that marks the notification as seen and continues to what it was
        /// about, carrying the target along so the redirect knows where to land.
        /// </summary>
        /// <param name="notification">The notification being rendered.</param>
        /// <returns>The read route of the notification.</returns>
        private static IUri ResolveReadUri(UserNotification notification)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Notifications.Read>()
                .Add(new UriQuery("id", notification.Id.ToString()));

            return string.IsNullOrWhiteSpace(notification.TargetUri)
                ? uri
                : uri.Add(new UriQuery("target", notification.TargetUri));
        }

        /// <summary>
        /// Binds a target URI to the route parameters of the current request.
        /// </summary>
        /// <param name="uri">The URI to bind.</param>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The bound URI.</returns>
        private static IUri BindUri(IUri uri, IRenderControlContext renderContext)
        {
            return renderContext?.Request is null ? uri : uri.BindParameters(renderContext.Request);
        }
    }
}
