using KleeneStar.Model;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Identity;
using System;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// The internal source: the account's password is kept by this installation, as the hash in
    /// <see cref="Identity.PasswordHash"/>.
    /// </summary>
    public sealed class LocalAuthenticationSource : IAuthenticationSource
    {
        /// <summary>
        /// Gets the key of the internal source.
        /// </summary>
        public string Key => IdentitySource.Local;

        /// <summary>
        /// Gets the name the source is shown under.
        /// </summary>
        public string Name => "kleenestar.core:authentication.source.local";

        /// <summary>
        /// Gets the position of the source in listings: first.
        /// </summary>
        public int Order => 0;

        /// <summary>
        /// Gets whether the credentials are owned elsewhere: they are not.
        /// </summary>
        public bool IsExternal => false;

        /// <summary>
        /// Gets whether the sign-in form's password is checked here: it is.
        /// </summary>
        public bool AcceptsPassword => true;

        /// <summary>
        /// Gets whether a sign-in creates an account: an internal account is created by an
        /// administrator, never by signing in.
        /// </summary>
        public bool Provisions => false;

        /// <summary>
        /// Checks the password against the stored hash, and stores a fresh hash when the scheme
        /// asks for one - which is how a raised work factor reaches the stored passwords without
        /// anybody having to reset theirs.
        /// </summary>
        /// <param name="account">The stored account.</param>
        /// <param name="password">The offered password.</param>
        /// <returns><see langword="true"/> when the password authenticates the account.</returns>
        public bool VerifyPassword(Identity account, string password)
        {
            var result = IdentityPassword.Verify(account, password);

            if (result == PasswordVerificationResult.SuccessRehashNeeded && account is not null)
            {
                try
                {
                    ModelHub.SetPasswordHash(account.Id, IdentityPassword.Hash(account, password), account.PasswordChanged ?? DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    // the sign-in is right either way; the old hash still verifies next time
                    CoreHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);
                }
            }

            return result != PasswordVerificationResult.Failed;
        }
    }
}
