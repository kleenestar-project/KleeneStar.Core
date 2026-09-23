using KleeneStar.Core.WebIdentity;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Owns the password of an internal account, the one-time links that set it, and the link
    /// between a stored account and the external source that authenticates it.
    /// </summary>
    /// <remarks>
    /// A reset link's secret exists exactly once, like an access token's:
    /// <see cref="IssuePasswordReset"/> hands it to the administrator and stores only its hash.
    /// Nothing here ever logs, stores or returns a password; the audit log hears of a changed
    /// password through <see cref="PasswordChanged"/> and learns how, never what.
    /// </remarks>
    public sealed partial class CredentialManager : ICredentialManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// The prefix every reset secret carries, so it is recognizable as a KleeneStar
        /// credential wherever it turns up.
        /// </summary>
        private const string ResetPrefix = "ksr_";

        /// <summary>
        /// An event that fires when the password of an account was set.
        /// </summary>
        public event EventHandler<Identity> PasswordChanged;

        /// <summary>
        /// An event that fires when a reset link was issued.
        /// </summary>
        public event EventHandler<PasswordReset> PasswordResetIssued;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private CredentialManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Authenticates a sign-in made with a user name (or e-mail address) and a password.
        /// </summary>
        /// <param name="login">The user name or e-mail address.</param>
        /// <param name="password">The offered password.</param>
        /// <returns>The authenticated account, or <see langword="null"/>.</returns>
        public Identity Authenticate(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password) || password.Length > ICredentialManager.MaximumLength)
            {
                return null;
            }

            var account = CoreHub.IdentityManager.GetIdentityByLogin(login);
            var source = AuthenticationSourceCatalog.Resolve(account);

            if (account is null || source is null || !source.AcceptsPassword)
            {
                // the refusal costs what a verification costs, so the time it takes does not
                // tell an unknown name from a known one, or an external account from an
                // internal one
                IdentityPassword.Verify(null, password);

                return null;
            }

            return source.VerifyPassword(account, password) ? account : null;
        }

        /// <summary>
        /// Checks a new password against the password rules.
        /// </summary>
        /// <param name="account">The account it is meant for. May be null.</param>
        /// <param name="password">The new password.</param>
        /// <returns>The internationalization keys of the rules it breaks.</returns>
        public IReadOnlyList<string> ValidatePassword(Identity account, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return ["kleenestar.core:password.validation.required"];
            }

            var errors = new List<string>();

            if (password.Length < ICredentialManager.MinimumLength)
            {
                errors.Add("kleenestar.core:password.validation.tooshort");
            }

            if (password.Length > ICredentialManager.MaximumLength)
            {
                errors.Add("kleenestar.core:password.validation.toolong");
            }

            // a password that is the account's own name is the first thing anybody tries
            if (account is not null && new[] { account.UserName, account.Email, account.Name }
                .Any(x => !string.IsNullOrWhiteSpace(x) && string.Equals(x.Trim(), password.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("kleenestar.core:password.validation.resemblesaccount");
            }

            return errors;
        }

        /// <summary>
        /// Changes the password of an internal account on behalf of its owner.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <param name="currentPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>The outcome.</returns>
        public CredentialResult ChangePassword(Guid identityId, string currentPassword, string newPassword)
        {
            var account = identityId == Guid.Empty ? null : CoreHub.IdentityManager.GetIdentity(identityId);

            if (account is null || account.State != IdentityState.Active)
            {
                return CredentialResult.Of(CredentialOutcome.UnknownAccount);
            }

            if (!AuthenticationSourceCatalog.ManagesPassword(account))
            {
                return CredentialResult.Of(CredentialOutcome.ExternalAccount);
            }

            // an account that never had a password here gets one through a reset link, never
            // by a change that nothing could prove
            if (!IdentityPassword.IsUsable(account.PasswordHash)
                || !AuthenticationSourceCatalog.Resolve(account).VerifyPassword(account, currentPassword ?? string.Empty))
            {
                return CredentialResult.Of(CredentialOutcome.WrongPassword);
            }

            var errors = ValidatePassword(account, newPassword);

            if (errors.Count > 0)
            {
                return new CredentialResult(CredentialOutcome.WeakPassword, errors);
            }

            ModelHub.SetPasswordHash(account.Id, IdentityPassword.Hash(account, newPassword), DateTime.UtcNow);

            PasswordChanged?.Invoke(this, account);

            return CredentialResult.Of(CredentialOutcome.Succeeded);
        }

        /// <summary>
        /// Issues a one-time link that sets the password of an internal account.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <param name="issuerId">The administrator issuing it, or <see cref="Guid.Empty"/>.</param>
        /// <param name="secret">The secret the link carries.</param>
        /// <returns>The outcome.</returns>
        public CredentialResult IssuePasswordReset(Guid identityId, Guid issuerId, out string secret)
        {
            secret = null;

            var account = identityId == Guid.Empty ? null : CoreHub.IdentityManager.GetIdentity(identityId);

            if (account is null || account.State != IdentityState.Active)
            {
                return CredentialResult.Of(CredentialOutcome.UnknownAccount);
            }

            if (!AuthenticationSourceCatalog.ManagesPassword(account))
            {
                return CredentialResult.Of(CredentialOutcome.ExternalAccount);
            }

            secret = ResetPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

            var reset = new PasswordReset
            {
                IdentityId = account.Id,
                IssuedById = issuerId == Guid.Empty ? null : issuerId,
                TokenHash = Hash(secret),
                Expires = DateTime.UtcNow + ICredentialManager.ResetLifetime
            };

            ModelHub.AddPasswordReset(reset);

            PasswordResetIssued?.Invoke(this, reset);

            return new CredentialResult(CredentialOutcome.Succeeded, [], reset);
        }

        /// <summary>
        /// Returns the open reset link a secret belongs to.
        /// </summary>
        /// <param name="secret">The secret from the link.</param>
        /// <returns>The link, or <see langword="null"/> when it sets nothing.</returns>
        public PasswordReset GetPasswordReset(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret) || secret.Length > 128)
            {
                return null;
            }

            var reset = ModelHub.GetPasswordResetByHash(Hash(secret.Trim()));

            if (reset is null || !reset.IsOpen(DateTime.UtcNow))
            {
                return null;
            }

            // a link outlives neither the account's activity nor its being internal: an account
            // moved to an external source in the meantime is not this installation's to set
            var account = CoreHub.IdentityManager.GetIdentity(reset.IdentityId);

            return account is not null && account.State == IdentityState.Active && AuthenticationSourceCatalog.ManagesPassword(account)
                ? reset
                : null;
        }

        /// <summary>
        /// Sets the password through a reset link, and spends the link.
        /// </summary>
        /// <param name="secret">The secret from the link.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>The outcome.</returns>
        public CredentialResult CompletePasswordReset(string secret, string newPassword)
        {
            var reset = GetPasswordReset(secret);

            if (reset is null)
            {
                return CredentialResult.Of(CredentialOutcome.InvalidLink);
            }

            var account = CoreHub.IdentityManager.GetIdentity(reset.IdentityId);
            var errors = ValidatePassword(account, newPassword);

            if (errors.Count > 0)
            {
                return new CredentialResult(CredentialOutcome.WeakPassword, errors);
            }

            // the link is re-read and spent inside the same save that sets the password, so two
            // submissions of one link cannot both succeed
            if (ModelHub.CompletePasswordReset(reset.Id, IdentityPassword.Hash(account, newPassword)) is null)
            {
                return CredentialResult.Of(CredentialOutcome.InvalidLink);
            }

            PasswordChanged?.Invoke(this, account);

            return CredentialResult.Of(CredentialOutcome.Succeeded);
        }

        /// <summary>
        /// Answers the stored account an external sign-in stands for.
        /// </summary>
        /// <param name="sourceKey">The key of the external source.</param>
        /// <param name="claims">What the source vouched for.</param>
        /// <returns>The account, or <see langword="null"/>.</returns>
        public Identity SignInExternal(string sourceKey, ExternalAccountClaims claims)
        {
            var key = IdentitySource.Normalize(sourceKey);
            var source = AuthenticationSourceCatalog.GetSource(key);

            if (key is null || source is null || !source.IsExternal || string.IsNullOrWhiteSpace(claims?.Subject))
            {
                return null;
            }

            var subject = claims.Subject.Trim();
            var linked = CoreHub.IdentityManager.GetIdentityBySource(key, subject);

            if (linked is not null)
            {
                return linked.State == IdentityState.Active ? linked : null;
            }

            var claimed = Claim(key, subject, claims);

            if (claimed is not null)
            {
                return claimed;
            }

            return source.Provisions ? Provision(key, subject, claims) : null;
        }

        /// <summary>
        /// Binds the one account an administrator prepared for this source to the subject
        /// signing in, when the source vouches for the account's e-mail address.
        /// </summary>
        /// <param name="key">The normalized source key.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="claims">The claims.</param>
        /// <returns>The claimed account, or <see langword="null"/>.</returns>
        private static Identity Claim(string key, string subject, ExternalAccountClaims claims)
        {
            if (!claims.EmailVerified || string.IsNullOrWhiteSpace(claims.Email))
            {
                return null;
            }

            var email = claims.Email.Trim();
            var waiting = CoreHub.IdentityManager
                .GetIdentities(new Query<Identity>().Where(x => x.AuthenticationSource == key && x.ExternalSubject == null))
                .Where(x => x.State == IdentityState.Active && string.Equals(x.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            // two accounts waiting for the same address is a mistake to resolve by hand, not by
            // picking one
            if (waiting.Count != 1)
            {
                return null;
            }

            ModelHub.SetExternalSubject(waiting[0].Id, subject);

            return CoreHub.IdentityManager.GetIdentity(waiting[0].Id);
        }

        /// <summary>
        /// Creates the account a provisioning source's sign-in stands for.
        /// </summary>
        /// <param name="key">The normalized source key.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="claims">The claims.</param>
        /// <returns>The created account.</returns>
        private static Identity Provision(string key, string subject, ExternalAccountClaims claims)
        {
            var id = Guid.NewGuid();
            var name = FirstOf(claims.Name, claims.UserName, claims.Email, subject);

            var account = new Identity(id)
            {
                Name = name.Length > 128 ? name[..128] : name,
                UserName = UniqueUserName(FirstOf(claims.UserName, claims.Email?.Split('@')[0], name)),
                Email = claims.Email?.Trim() ?? string.Empty,
                EmailVerified = claims.EmailVerified,
                State = IdentityState.Active,
                Avatar = GenerateIcon(id),
                AuthenticationSource = key
            };

            CoreHub.IdentityManager.Add(account);
            ModelHub.SetExternalSubject(id, subject);

            return CoreHub.IdentityManager.GetIdentity(id);
        }

        /// <summary>
        /// Generates the placeholder picture of a provisioned account.
        /// </summary>
        /// <remarks>
        /// A sign-in must not fail over a picture: an account without one is shown under the
        /// generic silhouette until its owner picks one.
        /// </remarks>
        /// <param name="id">The account.</param>
        /// <returns>The picture, or <see langword="null"/> when none could be generated.</returns>
        private static WebExpress.WebUI.WebIcon.ImageIcon GenerateIcon(Guid id)
        {
            try
            {
                return CoreHub.GenerateIcon(id);
            }
            catch (Exception ex)
            {
                CoreHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);

                return null;
            }
        }

        /// <summary>
        /// Derives a login name no other account carries from a suggestion.
        /// </summary>
        /// <param name="suggestion">The suggested name.</param>
        /// <returns>The unique login name.</returns>
        private static string UniqueUserName(string suggestion)
        {
            var slug = UserNameInvalid().Replace((suggestion ?? string.Empty).Trim().ToLowerInvariant(), ".").Trim('.');

            if (string.IsNullOrEmpty(slug))
            {
                slug = "user";
            }

            if (slug.Length > 56)
            {
                slug = slug[..56];
            }

            var taken = CoreHub.IdentityManager
                .GetIdentities(new Query<Identity>())
                .Select(x => x.UserName)
                .Where(x => x is not null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var candidate = slug;

            for (var i = 2; taken.Contains(candidate); i++)
            {
                candidate = $"{slug}{i}";
            }

            return candidate;
        }

        /// <summary>
        /// Returns the first of the supplied values that is not blank.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>The first non-blank value, trimmed; empty when all are blank.</returns>
        private static string FirstOf(params string[] values)
        {
            return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Hashes a reset secret. A secret of 256 random bits needs no salt or work factor:
        /// there is nothing to guess.
        /// </summary>
        /// <param name="secret">The secret.</param>
        /// <returns>The hexadecimal SHA-256 hash.</returns>
        private static string Hash(string secret)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        }

        /// <summary>
        /// Matches the characters a login name does not carry.
        /// </summary>
        [GeneratedRegex("[^a-z0-9._-]+")]
        private static partial Regex UserNameInvalid();

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
