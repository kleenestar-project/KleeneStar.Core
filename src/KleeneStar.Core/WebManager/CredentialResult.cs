using KleeneStar.Model.Entities;
using System.Collections.Generic;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Names how a credential operation ended.
    /// </summary>
    public enum CredentialOutcome
    {
        /// <summary>
        /// The operation did what it was asked.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The account does not exist, or is not active.
        /// </summary>
        UnknownAccount,

        /// <summary>
        /// The account's credentials are owned by an external source, so no password is set
        /// here.
        /// </summary>
        ExternalAccount,

        /// <summary>
        /// The current password offered to prove the owner was wrong.
        /// </summary>
        WrongPassword,

        /// <summary>
        /// The new password breaks a password rule; <see cref="CredentialResult.Errors"/> says
        /// which.
        /// </summary>
        WeakPassword,

        /// <summary>
        /// The reset link is unknown, spent or expired.
        /// </summary>
        InvalidLink
    }

    /// <summary>
    /// The result of a credential operation.
    /// </summary>
    /// <param name="Outcome">How it ended.</param>
    /// <param name="Errors">The internationalization keys of the rules a new password broke.</param>
    /// <param name="Reset">The link a reset issue stored.</param>
    public sealed record CredentialResult(CredentialOutcome Outcome, IReadOnlyList<string> Errors = null, PasswordReset Reset = null)
    {
        /// <summary>
        /// Gets whether the operation succeeded.
        /// </summary>
        public bool Succeeded => Outcome == CredentialOutcome.Succeeded;

        /// <summary>
        /// Creates a result of the supplied outcome.
        /// </summary>
        /// <param name="outcome">The outcome.</param>
        /// <returns>The result.</returns>
        public static CredentialResult Of(CredentialOutcome outcome) => new(outcome, []);
    }

    /// <summary>
    /// What an external source vouched for about the account signing in.
    /// </summary>
    /// <param name="Subject">The identifier the source knows the account by. Required.</param>
    /// <param name="UserName">The login name the source suggests, if any.</param>
    /// <param name="Name">The display name, if any.</param>
    /// <param name="Email">The e-mail address, if any.</param>
    /// <param name="EmailVerified">Whether the source vouches that the address is the
    /// account's own.</param>
    public sealed record ExternalAccountClaims(string Subject, string UserName, string Name, string Email, bool EmailVerified);
}
