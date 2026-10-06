using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
using System.Globalization;
using WebExpress.WebCore.WebEmail;

namespace KleeneStar.Core.Test.WebIdentity
{
    /// <summary>
    /// Provides unit tests for mailing a password link: when it is sent, what the message
    /// carries, and how every answer of the mail delivery is reported.
    /// </summary>
    public class UnitTestPasswordResetMail
    {
        private const string Link = "https://ks.example.org/kleenestar/setpassword?token=abc%2B1";

        /// <summary>
        /// A mail manager standing in for WebExpress's, recording what it was asked to send.
        /// </summary>
        private sealed class FakeMail : IEmailManager
        {
            public bool Enabled { get; init; } = true;

            public Func<EmailSendResult> Answer { get; init; } = () => EmailSendResult.Accepted;

            public List<(string ApplicationId, EmailMessage Message)> Sent { get; } = [];

            public EmailStatus GetStatus() => new() { Enabled = Enabled, Profiles = [] };

            public void RegisterProvider(IEmailProvider provider)
            {
            }

            public bool UnregisterProvider(IEmailProvider provider) => false;

            public Task<EmailSendResult> SendAsync(string applicationId, EmailMessage message, string profile = null, CancellationToken cancellationToken = default)
            {
                Sent.Add((applicationId, message));

                return Task.FromResult(Answer());
            }

            public void Dispose()
            {
            }
        }

        private static Identity Account(string email = "anna@example.org", string language = "de") => new()
        {
            Id = Guid.NewGuid(),
            Name = "Anna <Admin>",
            Email = email,
            Language = language
        };

        private static PasswordReset Reset() => new()
        {
            Id = Guid.Parse("7E000000-0000-4000-8000-000000000001"),
            Expires = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc)
        };

        /// <summary>
        /// Verifies that nothing is sent where the installation delivers no mail.
        /// </summary>
        [Fact]
        public void Send_DoesNothingWhenMailIsDisabled()
        {
            var mail = new FakeMail { Enabled = false };

            var outcome = PasswordResetMail.Send(Account(), Reset(), Link, CultureInfo.InvariantCulture, mail);

            Assert.Equal(PasswordResetMailOutcome.Disabled, outcome);
            Assert.Empty(mail.Sent);
            Assert.False(PasswordResetMail.WouldSend(Account(), mail));
        }

        /// <summary>
        /// Verifies that an account without an address gets nothing, and the dialog does not
        /// promise it.
        /// </summary>
        [Fact]
        public void Send_DoesNothingWithoutAnAddress()
        {
            var mail = new FakeMail();

            var outcome = PasswordResetMail.Send(Account(email: "  "), Reset(), Link, CultureInfo.InvariantCulture, mail);

            Assert.Equal(PasswordResetMailOutcome.NoAddress, outcome);
            Assert.Empty(mail.Sent);
            Assert.False(PasswordResetMail.WouldSend(Account(email: null), mail));
            Assert.True(PasswordResetMail.WouldSend(Account(), mail));
        }

        /// <summary>
        /// Verifies that the message goes to the account, carries the link in both bodies, and
        /// has a delivery id derived from the stored link, so a retry is not sent twice.
        /// </summary>
        [Fact]
        public void Send_MailsTheLinkUnderAStableDeliveryId()
        {
            var mail = new FakeMail();

            var outcome = PasswordResetMail.Send(Account(), Reset(), Link, CultureInfo.InvariantCulture, mail);

            Assert.Equal(PasswordResetMailOutcome.Sent, outcome);

            var message = Assert.Single(mail.Sent).Message;

            Assert.Equal(["anna@example.org"], message.To);
            Assert.Equal("kleenestar-password-reset-7e000000000040008000000000000001", message.DeliveryId);
            Assert.Contains(Link, message.TextBody);

            // the markup encodes what it embeds - the link and the name alike
            Assert.Contains("href=\"https://ks.example.org/kleenestar/setpassword?token=abc%2B1\"", message.HtmlBody);
            Assert.DoesNotContain("<Admin>", message.HtmlBody);
            Assert.False(string.IsNullOrWhiteSpace(message.Subject));
        }

        /// <summary>
        /// Verifies that a delivery claimed before is reported as such, not as a new message.
        /// </summary>
        [Fact]
        public void Send_ReportsAnEarlierAttempt()
        {
            var mail = new FakeMail { Answer = () => EmailSendResult.AlreadyAttempted };

            Assert.Equal(PasswordResetMailOutcome.AlreadySent, PasswordResetMail.Send(Account(), Reset(), Link, CultureInfo.InvariantCulture, mail));
        }

        /// <summary>
        /// Verifies that a failed delivery is reported rather than thrown, so the link is still
        /// shown to the administrator.
        /// </summary>
        [Fact]
        public void Send_ReportsAFailureInsteadOfThrowing()
        {
            var mail = new FakeMail { Answer = () => throw new EmailException(EmailError.DeliveryFailed) };

            Assert.Equal(PasswordResetMailOutcome.Failed, PasswordResetMail.Send(Account(), Reset(), Link, CultureInfo.InvariantCulture, mail));
        }
    }
}
