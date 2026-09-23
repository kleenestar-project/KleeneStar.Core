using KleeneStar.Model.Entities;

namespace KleeneStar.Core.WebIdentity
{
    /// <summary>
    /// Describes a source that authenticates the accounts naming it in
    /// <see cref="Identity.AuthenticationSource"/>.
    /// </summary>
    /// <remarks>
    /// The core ships one - the internal source, <see cref="LocalAuthenticationSource"/>, whose
    /// passwords this installation keeps - and a plugin adds more through
    /// <see cref="AuthenticationSourceCatalog.Register"/>: a directory that checks a password
    /// itself (LDAP), or an identity provider the browser is sent to (OpenID Connect, see
    /// <see cref="OpenIdConnectAuthenticationSource"/>). The two questions every surface asks
    /// of a source are the two flags: may the sign-in form's password be checked for this
    /// account (<see cref="AcceptsPassword"/>), and does this installation own the account's
    /// credentials (<see cref="IsExternal"/>) - which decides whether a password can be set,
    /// changed or reset here at all.
    /// </remarks>
    public interface IAuthenticationSource
    {
        /// <summary>
        /// Gets the key the accounts of this source carry. Compared after
        /// <see cref="IdentitySource.Normalize"/>; <see cref="IdentitySource.Local"/> is the
        /// internal source's.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the name the source is shown under: an internationalization key, or plain text
        /// (a configured provider carries the name its administrator gave it).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the position of the source in listings. Lower comes first.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets whether the account's credentials are owned outside this installation - so no
        /// password is set, changed or reset here, and the profile points the owner to the
        /// source instead.
        /// </summary>
        bool IsExternal { get; }

        /// <summary>
        /// Gets whether the sign-in form's user name and password are checked by this source.
        /// A source the browser is redirected to answers <see langword="false"/>, and a
        /// password offered for one of its accounts is refused.
        /// </summary>
        bool AcceptsPassword { get; }

        /// <summary>
        /// Gets whether a sign-in that stands for no stored account creates one. Only
        /// meaningful for an external source: with <see langword="false"/>, an administrator
        /// creates the account first and the source's first sign-in claims it.
        /// </summary>
        bool Provisions { get; }

        /// <summary>
        /// Checks a password offered for one of this source's accounts. Only asked when
        /// <see cref="AcceptsPassword"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="account">The stored account the sign-in named. Never null.</param>
        /// <param name="password">The offered password.</param>
        /// <returns><see langword="true"/> when the password authenticates the account.</returns>
        bool VerifyPassword(Identity account, string password);
    }
}
