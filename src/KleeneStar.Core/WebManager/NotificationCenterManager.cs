using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the notification center: the in-app notifications an identity can come back to.
    /// </summary>
    /// <remarks>
    /// Every notification the application raises through <see cref="CoreHub.AddNotification"/>
    /// is recorded here as well. The toast that appears at the same time is transient and
    /// global — it is drained by the first client that polls for it and is gone whether or not
    /// anybody was looking. The center is what makes those events survive: addressed to an
    /// identity, kept until read, and listed behind the bell in the header.
    /// </remarks>
    public sealed class NotificationCenterManager : INotificationCenterManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when a notification is recorded.
        /// </summary>
        public event EventHandler<UserNotification> NotificationRecorded;

        /// <summary>
        /// An event that fires when notifications are marked as seen or removed.
        /// </summary>
        public event EventHandler<Guid> NotificationsChanged;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private NotificationCenterManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Records a notification for the identity the current request is served for.
        /// </summary>
        /// <remarks>
        /// Called from <see cref="CoreHub.AddNotification"/>, which the managers reach without
        /// a request in hand, so the addressee comes from
        /// <see cref="ISessionManager.GetCurrentIdentityId"/> with a null request: the
        /// signed-in user of the request that started this call chain. An act nobody is signed
        /// in for is not announced to anybody — the notification is dropped rather than
        /// delivered to a person who did not cause it, which is what the seeded-admin fallback
        /// used to do.
        /// </remarks>
        /// <param name="titleKey">The translation key of the heading.</param>
        /// <param name="messageKey">The translation key of the message.</param>
        /// <param name="subject">What the notification is about, or <see langword="null"/>.</param>
        /// <param name="targetUri">The path the notification links to, or <see langword="null"/>.</param>
        /// <param name="subjectIcon">
        /// The path the icon of the record is served from, or <see langword="null"/>.
        /// </param>
        /// <returns>The recorded notification, or <see langword="null"/>.</returns>
        public UserNotification Record(string titleKey, string messageKey, string subject = null, string targetUri = null, string subjectIcon = null)
        {
            if (string.IsNullOrWhiteSpace(titleKey) || string.IsNullOrWhiteSpace(messageKey))
            {
                return null;
            }

            var ownerId = CoreHub.SessionManager?.GetCurrentIdentityId(null) ?? Guid.Empty;

            if (ownerId == Guid.Empty)
            {
                return null;
            }

            var notification = new UserNotification
            {
                OwnerId = ownerId,
                // who caused the event. It is the same identity the notification is addressed
                // to, because a manager announces what the caller just did back to the caller;
                // the column is filled from the acting identity rather than copied from the
                // owner, so it keeps telling the two apart once something addresses a
                // notification to somebody other than its cause.
                ActorId = ownerId,
                TitleKey = titleKey,
                MessageKey = messageKey,
                Subject = subject,
                TargetUri = targetUri,
                SubjectIcon = subjectIcon,
                Read = false,
                Created = DateTime.UtcNow
            };

            try
            {
                ModelHub.Add(notification);
            }
            catch (Exception)
            {
                // recording is best-effort: a notification that cannot be stored must not take
                // down the operation that raised it. The toast still appears.
                return null;
            }

            NotificationRecorded?.Invoke(this, notification);

            return notification;
        }

        /// <summary>
        /// Returns the notifications of the identity the request is served for, newest first.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="unreadOnly">Whether to return only the ones not yet seen.</param>
        /// <param name="limit">The largest number of rows to return; 0 for all of them.</param>
        /// <returns>An enumerable collection of notifications (possibly empty).</returns>
        public IEnumerable<UserNotification> GetNotifications(IRequest request, bool unreadOnly = false, int limit = 0)
        {
            return ModelHub.GetUserNotifications
            (
                CoreHub.SessionManager.GetCurrentIdentityId(request),
                unreadOnly,
                limit
            );
        }

        /// <summary>
        /// Returns how many notifications the identity the request is served for has not seen.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The number of unread notifications.</returns>
        public int GetUnreadCount(IRequest request)
        {
            return ModelHub.GetUnreadUserNotificationCount(CoreHub.SessionManager.GetCurrentIdentityId(request));
        }

        /// <summary>
        /// Marks a single notification of the calling identity as seen.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="notificationId">The id of the notification.</param>
        /// <returns>The current instance for method chaining.</returns>
        public INotificationCenterManager MarkRead(IRequest request, Guid notificationId)
        {
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            ModelHub.MarkUserNotificationRead(notificationId, ownerId);
            NotificationsChanged?.Invoke(this, ownerId);

            return this;
        }

        /// <summary>
        /// Marks every notification of the calling identity as seen.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The current instance for method chaining.</returns>
        public INotificationCenterManager MarkAllRead(IRequest request)
        {
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (ModelHub.MarkAllUserNotificationsRead(ownerId) > 0)
            {
                NotificationsChanged?.Invoke(this, ownerId);
            }

            return this;
        }

        /// <summary>
        /// Removes a single notification of the calling identity.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="notificationId">The id of the notification.</param>
        /// <returns>The current instance for method chaining.</returns>
        public INotificationCenterManager Remove(IRequest request, Guid notificationId)
        {
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            ModelHub.RemoveUserNotification(notificationId, ownerId);
            NotificationsChanged?.Invoke(this, ownerId);

            return this;
        }

        /// <summary>
        /// Removes every notification of the calling identity.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The current instance for method chaining.</returns>
        public INotificationCenterManager Clear(IRequest request)
        {
            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (ModelHub.RemoveUserNotifications(ownerId) > 0)
            {
                NotificationsChanged?.Invoke(this, ownerId);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
