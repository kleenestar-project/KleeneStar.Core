using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Net;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebEmail;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// The outcome of mailing a password link.
    /// </summary>
    public enum PasswordResetMailOutcome
    {
        /// <summary>
        /// The installation delivers no mail; the link is only shown.
        /// </summary>
        Disabled,

        /// <summary>
        /// The account carries no e-mail address; the link is only shown.
        /// </summary>
        NoAddress,

        /// <summary>
        /// The mail server accepted the message - which says nothing about the inbox.
        /// </summary>
        Sent,

        /// <summary>
        /// This link was mailed before, by this or another replica; it is not sent twice.
        /// </summary>
        AlreadySent,

        /// <summary>
        /// The delivery failed or was refused; the link is still shown to be handed over.
        /// </summary>
        Failed
    }

    /// <summary>
    /// Sends a freshly issued password link to the account it is for, through WebExpress's
    /// shared mail delivery (<c>WebExpress:Email</c>).
    /// </summary>
    /// <remarks>
    /// The mail is an addition, not a replacement: the issuing dialog still shows the link,
    /// because a delivery the server accepted may never arrive and the administrator is the one
    /// who notices. The delivery id is derived from the stored link, so a retry - or a second
    /// replica handling the same request - does not send it twice. Nothing about the message is
    /// logged here; the framework logs the delivery without addresses or bodies, and the
    /// issuance itself is already in the audit log.
    /// <para>
    /// The link is the one the dialog shows. It is built on <c>WebExpress:ExternalUri</c> when
    /// one is configured and on the issuing administrator's own origin otherwise; only an
    /// administrator can issue it, so the origin is never a stranger's.
    /// </para>
    /// </remarks>
    public static class PasswordResetMail
    {
        /// <summary>
        /// Determines whether the installation delivers mail at all.
        /// </summary>
        /// <param name="mail">The mail manager, or <see langword="null"/> for the framework's.</param>
        /// <returns><see langword="true"/> when mail delivery is enabled.</returns>
        public static bool IsAvailable(IEmailManager mail = null)
        {
            mail ??= CoreHub.ComponentHub?.EmailManager;

            try
            {
                return mail?.GetStatus()?.Enabled == true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether a link issued for the account would be mailed to it.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <param name="mail">The mail manager, or <see langword="null"/> for the framework's.</param>
        /// <returns><see langword="true"/> when mail is enabled and the account has an address.</returns>
        public static bool WouldSend(Identity account, IEmailManager mail = null)
        {
            return !string.IsNullOrWhiteSpace(account?.Email) && IsAvailable(mail);
        }

        /// <summary>
        /// Mails a password link to the account it was issued for.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <param name="reset">The stored link, whose id makes the delivery id.</param>
        /// <param name="link">The absolute link carrying the secret.</param>
        /// <param name="fallbackCulture">The language used when the account names none.</param>
        /// <param name="mail">The mail manager, or <see langword="null"/> for the framework's.</param>
        /// <returns>What happened to the message.</returns>
        public static PasswordResetMailOutcome Send(Identity account, PasswordReset reset, string link, CultureInfo fallbackCulture, IEmailManager mail = null)
        {
            mail ??= CoreHub.ComponentHub?.EmailManager;

            if (!IsAvailable(mail))
            {
                return PasswordResetMailOutcome.Disabled;
            }

            if (string.IsNullOrWhiteSpace(account?.Email) || reset is null || string.IsNullOrEmpty(link))
            {
                return PasswordResetMailOutcome.NoAddress;
            }

            var culture = ResolveCulture(account, fallbackCulture);
            var message = Compose(account, reset, link, culture);
            var applicationId = CoreHub.ApplicationContext?.ApplicationId?.ToString() ?? "kleenestar.core";

            try
            {
                var result = mail.SendAsync(applicationId, message).GetAwaiter().GetResult();

                return result == EmailSendResult.AlreadyAttempted
                    ? PasswordResetMailOutcome.AlreadySent
                    : PasswordResetMailOutcome.Sent;
            }
            catch (Exception)
            {
                // EmailException carries the category, and its inner exception provider
                // diagnostics that must not reach the client; the framework has logged both
                return PasswordResetMailOutcome.Failed;
            }
        }

        /// <summary>
        /// Composes the message: a short text, the link, its expiry, and what to do with an
        /// unexpected one - as plain text and as the same in markup.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <param name="reset">The stored link.</param>
        /// <param name="link">The link.</param>
        /// <param name="culture">The language of the message.</param>
        /// <returns>The message.</returns>
        public static EmailMessage Compose(Identity account, PasswordReset reset, string link, CultureInfo culture)
        {
            var expires = reset.Expires.ToLocalTime().ToString("g", culture);
            var greeting = I18N.Translate(culture, "kleenestar.core:setting.identity.password.mail.greeting", account.Name ?? account.UserName ?? string.Empty);
            var intro = I18N.Translate(culture, "kleenestar.core:setting.identity.password.mail.intro", expires);
            var ignore = I18N.Translate(culture, "kleenestar.core:setting.identity.password.mail.ignore");

            return new EmailMessage
            {
                DeliveryId = $"kleenestar-password-reset-{reset.Id:N}",
                To = [account.Email.Trim()],
                Subject = I18N.Translate(culture, "kleenestar.core:setting.identity.password.mail.subject"),
                TextBody = $"{greeting}\n\n{intro}\n\n{link}\n\n{ignore}\n",
                HtmlBody = $"<p>{WebUtility.HtmlEncode(greeting)}</p>"
                    + $"<p>{WebUtility.HtmlEncode(intro)}</p>"
                    + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(link)}</a></p>"
                    + $"<p>{WebUtility.HtmlEncode(ignore)}</p>"
            };
        }

        /// <summary>
        /// Returns the language the account reads in, falling back to the issuer's.
        /// </summary>
        /// <param name="account">The account.</param>
        /// <param name="fallback">The fallback language.</param>
        /// <returns>The culture.</returns>
        private static CultureInfo ResolveCulture(Identity account, CultureInfo fallback)
        {
            if (!string.IsNullOrWhiteSpace(account?.Language))
            {
                try
                {
                    return CultureInfo.GetCultureInfo(account.Language.Trim());
                }
                catch (CultureNotFoundException)
                {
                }
            }

            return fallback ?? CultureInfo.InvariantCulture;
        }
    }
}
