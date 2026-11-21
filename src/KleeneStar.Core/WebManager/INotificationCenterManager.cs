using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for the notification center: the in-app notifications an identity
    /// can come back to, as opposed to the transient toast that disappears on its own.
    /// </summary>
    public interface INotificationCenterManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a notification is recorded.
        /// </summary>
        event EventHandler<UserNotification> NotificationRecorded;

        /// <summary>
        /// An event that fires when notifications are marked as seen or removed.
        /// </summary>
        event EventHandler<Guid> NotificationsChanged;

        /// <summary>
        /// Records a notification for the identity the current request is served for.
        /// </summary>
        /// <param name="titleKey">The translation key of the heading.</param>
        /// <param name="messageKey">The translation key of the message.</param>
        /// <param name="subject">
        /// What the notification is about — an object key, a name — or <see langword="null"/>.
        /// </param>
        /// <param name="targetUri">
        /// The path the notification links to, or <see langword="null"/> when there is nothing
        /// to open.
        /// </param>
        /// <param name="subjectIcon">
        /// The path the icon of the record is served from, or <see langword="null"/>.
        /// </param>
        /// <returns>The recorded notification, or <see langword="null"/>.</returns>
        UserNotification Record(string titleKey, string messageKey, string subject = null, string targetUri = null, string subjectIcon = null);

        /// <summary>
        /// Returns the notifications of the identity the request is served for, newest first.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="unreadOnly">Whether to return only the ones not yet seen.</param>
        /// <param name="limit">The largest number of rows to return; 0 for all of them.</param>
        /// <returns>An enumerable collection of notifications (possibly empty).</returns>
        IEnumerable<UserNotification> GetNotifications(IRequest request, bool unreadOnly = false, int limit = 0);

        /// <summary>
        /// Returns how many notifications the identity the request is served for has not seen.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The number of unread notifications.</returns>
        int GetUnreadCount(IRequest request);

        /// <summary>
        /// Marks a single notification of the calling identity as seen.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="notificationId">The id of the notification.</param>
        /// <returns>The current instance for method chaining.</returns>
        INotificationCenterManager MarkRead(IRequest request, Guid notificationId);

        /// <summary>
        /// Marks every notification of the calling identity as seen.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The current instance for method chaining.</returns>
        INotificationCenterManager MarkAllRead(IRequest request);

        /// <summary>
        /// Removes a single notification of the calling identity.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="notificationId">The id of the notification.</param>
        /// <returns>The current instance for method chaining.</returns>
        INotificationCenterManager Remove(IRequest request, Guid notificationId);

        /// <summary>
        /// Removes every notification of the calling identity.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The current instance for method chaining.</returns>
        INotificationCenterManager Clear(IRequest request);
    }
}
