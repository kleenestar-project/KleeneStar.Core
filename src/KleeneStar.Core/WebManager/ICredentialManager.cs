using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Owns what proves an account is who it says: the password of an internal account, the
    /// one-time links that set it, and the link between a stored account and the external
    /// source that authenticates it.
    /// </summary>
    /// <remarks>
    /// Internal and external accounts are told apart by their
    /// <see cref="Identity.AuthenticationSource"/> through
    /// <see cref="WebIdentity.AuthenticationSourceCatalog"/>. Every password operation here
    /// refuses an external account with <see cref="CredentialOutcome.ExternalAccount"/>: its
    /// password is not this installation's to know, let alone to set.
    /// </remarks>
    public interface ICredentialManager : IComponentManager
    {
        /// <summary>
        /// The shortest password accepted.
        /// </summary>
        const int MinimumLength = 8;

        /// <summary>
        /// The longest password accepted. Hashing work grows with the length, so an unbounded
        /// password is a way to make the server busy.
        /// </summary>
        const int MaximumLength = 256;

        /// <summary>
        /// How long a reset link sets a password after it was issued.
        /// </summary>
        static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(24);

        /// <summary>
        /// An event that fires when the password of an account was set.
        /// </summary>
        event EventHandler<Identity> PasswordChanged;

        /// <summary>
        /// An event that fires when a reset link was issued.
        /// </summary>
        event EventHandler<PasswordReset> PasswordResetIssued;

        /// <summary>
        /// Authenticates a sign-in made with a user name (or e-mail address) and a password.
        /// </summary>
        /// <remarks>
        /// The account is asked of the source it names: the internal source checks the stored
        /// hash, a directory plugin checks its own. An account of a source that does not take
        /// passwords (an identity provider the browser is sent to), of a source that is not
        /// installed, or that is not active, is refused. A name that matches nothing still costs
        /// a hash verification, so the answer time does not tell a known name from an unknown one.
        /// </remarks>
        /// <param name="login">The user name or e-mail address.</param>
        /// <param name="password">The offered password.</param>
        /// <returns>The authenticated account, or <see langword="null"/>.</returns>
        Identity Authenticate(string login, string password);

        /// <summary>
        /// Checks a new password against the password rules.
        /// </summary>
        /// <param name="account">The account it is meant for. May be null.</param>
        /// <param name="password">The new password.</param>
        /// <returns>The internationalization keys of the rules it breaks; empty when it passes.</returns>
        IReadOnlyList<string> ValidatePassword(Identity account, string password);

        /// <summary>
        /// Changes the password of an internal account on behalf of its owner, who proves it by
        /// the current one.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <param name="currentPassword">The current password.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>The outcome.</returns>
        CredentialResult ChangePassword(Guid identityId, string currentPassword, string newPassword);

        /// <summary>
        /// Issues a one-time link that sets the password of an internal account, and spends
        /// every earlier link of the account.
        /// </summary>
        /// <param name="identityId">The account.</param>
        /// <param name="issuerId">The administrator issuing it, or <see cref="Guid.Empty"/>.</param>
        /// <param name="secret">The secret the link carries - the only time it can be read.</param>
        /// <returns>The outcome; <see cref="CredentialResult.Reset"/> carries the stored link.</returns>
        CredentialResult IssuePasswordReset(Guid identityId, Guid issuerId, out string secret);

        /// <summary>
        /// Returns the open reset link a secret belongs to - still unspent, unexpired, and for
        /// an active internal account.
        /// </summary>
        /// <param name="secret">The secret from the link.</param>
        /// <returns>The link, or <see langword="null"/> when it sets nothing.</returns>
        PasswordReset GetPasswordReset(string secret);

        /// <summary>
        /// Sets the password through a reset link, and spends the link.
        /// </summary>
        /// <param name="secret">The secret from the link.</param>
        /// <param name="newPassword">The new password.</param>
        /// <returns>The outcome.</returns>
        CredentialResult CompletePasswordReset(string secret, string newPassword);

        /// <summary>
        /// Answers the stored account an external sign-in stands for.
        /// </summary>
        /// <remarks>
        /// The account is found by the pair of source and subject first. Failing that, an
        /// account an administrator created for this source and that has not signed in yet is
        /// claimed - but only by a <em>verified</em> e-mail address equal to its own, and only
        /// when exactly one such account waits. Failing that, a source that provisions creates
        /// the account. An internal account is never matched by name or e-mail: that would let
        /// whoever controls a directory entry take over an account of this installation.
        /// </remarks>
        /// <param name="sourceKey">The key of the external source.</param>
        /// <param name="claims">What the source vouched for.</param>
        /// <returns>The account, or <see langword="null"/> when the sign-in stands for none.</returns>
        Identity SignInExternal(string sourceKey, ExternalAccountClaims claims);
    }
}
