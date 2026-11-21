using KleeneStar.Core.Test;
using KleeneStar.Model.Entities;
using System;
using System.Linq;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for
    /// <see cref="KleeneStar.Core.WebManager.NotificationCenterManager"/> — the in-app
    /// notifications an identity can come back to, as opposed to the transient toast.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestNotificationCenterManager
    {
        /// <summary>
        /// The identity the notifications are addressed to. The manager records against the
        /// signed-in user of the request that reached it; these tests reach it without a
        /// request, so they say who is acting through
        /// <see cref="KleeneStar.Core.WebManager.ISessionManager.BeginIdentity"/>.
        /// </summary>
        private static readonly Guid OwnerId = Guid.Parse("77087646-B13A-44B1-9BAC-6E66443CEDFD");

        /// <summary>
        /// Seeds the in-memory database with the identity the notifications belong to and
        /// begins acting as it, the way a request would.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <returns>The acting scope; closing it stops acting as that identity.</returns>
        private static IDisposable Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Identities.Any(x => x.Id == OwnerId))
            {
                db.Identities.Add(new Identity
                {
                    Id = OwnerId,
                    Name = "Test Owner",
                    Email = "owner@kleenestar.test",
                    PasswordHash = "$seed$v1$test"
                });
            }

            db.SaveChanges();

            return CoreHub.SessionManager.BeginIdentity(OwnerId);
        }

        /// <summary>
        /// Verifies that a recorded notification comes back with everything it was given and
        /// starts out unread — an entry nobody has seen yet is the whole point of the center.
        /// </summary>
        [Fact]
        public void Record_IsReturnedUnread()
        {
            var connectionString = $"NotificationRecord_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            CoreHub.NotificationCenterManager.Record
            (
                "kleenestar.core:notification.title.created",
                "kleenestar.core:notification.object.created",
                "BUG-1",
                "/kleenestar/issue/BUG-1"
            );

            var notifications = CoreHub.NotificationCenterManager.GetNotifications(null).ToList();

            var single = Assert.Single(notifications);
            Assert.Equal("kleenestar.core:notification.title.created", single.TitleKey);
            Assert.Equal("kleenestar.core:notification.object.created", single.MessageKey);
            Assert.Equal("BUG-1", single.Subject);
            Assert.Equal("/kleenestar/issue/BUG-1", single.TargetUri);
            Assert.False(single.Read);
            Assert.Equal(1, CoreHub.NotificationCenterManager.GetUnreadCount(null));
        }

        /// <summary>
        /// Verifies that a notification without a heading or a message is not recorded. Such
        /// an entry would occupy a row in the list while saying nothing.
        /// </summary>
        /// <param name="titleKey">The heading under test.</param>
        /// <param name="messageKey">The message under test.</param>
        [Theory]
        [InlineData(null, "kleenestar.core:notification.object.created")]
        [InlineData("kleenestar.core:notification.title.created", null)]
        [InlineData("", "")]
        [InlineData("   ", "   ")]
        public void Record_WithoutText_IsRejected(string titleKey, string messageKey)
        {
            var connectionString = $"NotificationReject_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            Assert.Null(CoreHub.NotificationCenterManager.Record(titleKey, messageKey));
            Assert.Empty(CoreHub.NotificationCenterManager.GetNotifications(null));
        }

        /// <summary>
        /// Verifies that the newest notification is listed first, which is the order the
        /// center reads in.
        /// </summary>
        [Fact]
        public void GetNotifications_NewestFirst()
        {
            var connectionString = $"NotificationOrder_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            CoreHub.NotificationCenterManager.Record("kleenestar.core:notification.title.created", "first", "A");
            CoreHub.NotificationCenterManager.Record("kleenestar.core:notification.title.updated", "second", "B");

            var subjects = CoreHub.NotificationCenterManager
                .GetNotifications(null)
                .Select(x => x.Subject)
                .ToList();

            Assert.Equal(["B", "A"], subjects);
        }

        /// <summary>
        /// Verifies that the limit is honoured and cuts from the newest end. The bell in the
        /// header shows a preview of ten; without this it would render the whole history into
        /// the dropdown.
        /// </summary>
        [Fact]
        public void GetNotifications_HonoursTheLimit()
        {
            var connectionString = $"NotificationLimit_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            for (var i = 0; i < 15; i++)
            {
                CoreHub.NotificationCenterManager.Record
                (
                    "kleenestar.core:notification.title.created",
                    "kleenestar.core:notification.object.created",
                    $"KEY-{i:00}"
                );
            }

            var preview = CoreHub.NotificationCenterManager.GetNotifications(null, limit: 10).ToList();

            Assert.Equal(10, preview.Count);
            Assert.Equal("KEY-14", preview.First().Subject);
            Assert.Equal("KEY-05", preview.Last().Subject);
            Assert.Equal(15, CoreHub.NotificationCenterManager.GetNotifications(null).Count());
        }

        /// <summary>
        /// Verifies that reading a single notification marks only that one, and that the
        /// unread count follows.
        /// </summary>
        [Fact]
        public void MarkRead_AffectsOnlyTheAddressedEntry()
        {
            var connectionString = $"NotificationMarkRead_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            var first = CoreHub.NotificationCenterManager.Record("kleenestar.core:notification.title.created", "first", "A");
            CoreHub.NotificationCenterManager.Record("kleenestar.core:notification.title.updated", "second", "B");

            CoreHub.NotificationCenterManager.MarkRead(null, first.Id);

            Assert.Equal(1, CoreHub.NotificationCenterManager.GetUnreadCount(null));

            var unread = Assert.Single(CoreHub.NotificationCenterManager.GetNotifications(null, unreadOnly: true));
            Assert.Equal("B", unread.Subject);
        }

        /// <summary>
        /// Verifies that a notification owned by somebody else is left alone. The id travels
        /// in a query string, so a caller can name any row — the manager decides which ones
        /// are theirs.
        /// </summary>
        [Fact]
        public void MarkRead_ForeignEntry_IsIgnored()
        {
            var connectionString = $"NotificationForeign_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            var foreignOwner = Guid.Parse("BBF45E5D-AA35-4382-9B84-6055193CE544");

            using (var db = CoreHubFixture.CreateDbContext(connectionString))
            {
                db.Identities.Add(new Identity
                {
                    Id = foreignOwner,
                    Name = "Somebody Else",
                    Email = "other@kleenestar.test",
                    PasswordHash = "$seed$v1$test"
                });

                db.UserNotifications.Add(new UserNotification
                {
                    Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    OwnerId = foreignOwner,
                    TitleKey = "kleenestar.core:notification.title.created",
                    MessageKey = "not yours",
                    Created = DateTime.UtcNow
                });

                db.SaveChanges();
            }

            CoreHub.NotificationCenterManager.MarkRead(null, Guid.Parse("11111111-2222-3333-4444-555555555555"));

            using var check = CoreHubFixture.CreateDbContext(connectionString);
            Assert.False(check.UserNotifications.Single(x => x.OwnerId == foreignOwner).Read);
        }

        /// <summary>
        /// Verifies that marking everything read empties the unread count while leaving the
        /// entries in place — the center is a history, not an inbox that drains itself.
        /// </summary>
        [Fact]
        public void MarkAllRead_KeepsTheEntries()
        {
            var connectionString = $"NotificationMarkAll_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            CoreHub.NotificationCenterManager.Record("kleenestar.core:notification.title.created", "first", "A");
            CoreHub.NotificationCenterManager.Record("kleenestar.core:notification.title.updated", "second", "B");

            CoreHub.NotificationCenterManager.MarkAllRead(null);

            Assert.Equal(0, CoreHub.NotificationCenterManager.GetUnreadCount(null));
            Assert.Equal(2, CoreHub.NotificationCenterManager.GetNotifications(null).Count());
        }

        /// <summary>
        /// Verifies that clearing removes the entries outright.
        /// </summary>
        [Fact]
        public void Clear_RemovesEverything()
        {
            var connectionString = $"NotificationClear_{Guid.NewGuid()}";
            using var acting = Seed(connectionString);

            CoreHub.NotificationCenterManager.Record("kleenestar.core:notification.title.created", "first", "A");

            CoreHub.NotificationCenterManager.Clear(null);

            Assert.Empty(CoreHub.NotificationCenterManager.GetNotifications(null));
        }
    }
}
