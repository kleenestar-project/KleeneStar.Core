using KleeneStar.Core.WebIdentity;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The sources accounts are authenticated by - the semantic lookup behind the persisted
    /// <see cref="Identity.AuthenticationSource"/> key. It does not gate persistence (an account
    /// keeps the key of a source whose plugin is gone), but it reads such an account the opposite
    /// way to the renderers: an unknown key authenticates nobody, because the only fallback there
    /// could be is the internal password check of an account whose password was never kept here.
    /// </summary>
    public interface IAuthenticationSourceManager : IComponentManager
    {
        /// <summary>
        /// Gets a number that changes whenever the set of sources does, so a cached picker can
        /// tell that a source arrived after it was built.
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Gets the sources, ordered by <see cref="IAuthenticationSource.Order"/> and then by key.
        /// </summary>
        IEnumerable<IAuthenticationSource> Sources { get; }

        /// <summary>
        /// Registers a source that is no type of a plugin - one per configuration entry (OpenID
        /// Connect in the core, LDAP in its plugin). A key that is present is taken over, which is
        /// how a plugin would replace the internal source.
        /// </summary>
        /// <param name="source">The source. Must not be null and must carry a key.</param>
        void Register(IAuthenticationSource source);

        /// <summary>
        /// Removes a registered source, so its accounts can no longer sign in. The internal
        /// source cannot be removed, only replaced.
        /// </summary>
        /// <param name="key">The key of the source.</param>
        /// <returns><see langword="true"/> when a source was removed.</returns>
        bool Unregister(string key);

        /// <summary>
        /// Returns the source under a key.
        /// </summary>
        /// <param name="key">The key. A blank key names the internal source.</param>
        /// <returns>The source, or <see langword="null"/>.</returns>
        IAuthenticationSource GetSource(string key);

        /// <summary>
        /// Returns the source that authenticates an account.
        /// </summary>
        /// <param name="account">The account. May be null.</param>
        /// <returns>The source, or <see langword="null"/> when the account is null or names a
        /// source nobody provides - in which case it signs in by no means at all.</returns>
        IAuthenticationSource Resolve(Identity account);

        /// <summary>
        /// Determines whether this installation owns the credentials of an account, so its
        /// password may be set, changed and reset here. An account whose source nobody provides
        /// is not answered as internal: its key says somebody else owns its credentials.
        /// </summary>
        /// <param name="account">The account. May be null, which answers false.</param>
        /// <returns><see langword="true"/> for an account of a known, internal source.</returns>
        bool ManagesPassword(Identity account);
    }
}
