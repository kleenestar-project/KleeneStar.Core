using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing identities, including adding, retrieving, and removing, as well as
    /// handling identity-related events.
    /// </summary>
    public interface IIdentityManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when an identity is added.
        /// </summary>
        event EventHandler<Identity> IdentityAdded;

        /// <summary>
        /// An event that fires when an identity is updated.
        /// </summary>
        event EventHandler<Identity> IdentityUpdated;

        /// <summary>
        /// An event that fires when an identity is removed.
        /// </summary>
        event EventHandler<Identity> IdentityRemoved;

        /// <summary>
        /// Returns an identity based on its id.
        /// </summary>
        /// <param name="identityId">The id of the identity.</param>
        /// <returns>The identity.</returns>
        Identity GetIdentity(Guid identityId);

        /// <summary>
        /// Returns an identity based on its id parameter.
        /// </summary>
        /// <param name="identityId">The id parameter of the identity.</param>
        /// <returns>The identity.</returns>
        Identity GetIdentity(IdentityIdParameter identityId);

        /// <summary>
        /// Returns the identity that signs in under the supplied name: the account whose user
        /// name or e-mail address it is, compared case-insensitively.
        /// </summary>
        /// <remarks>
        /// This is the one lookup a login goes through, and the one the session resolver goes
        /// through for an identity that names itself rather than carrying our id - a person
        /// types the same string into the login form either way, and both paths have to agree
        /// on which account it means. An account that is not
        /// <see cref="Model.Entities.IdentityState.Active"/> is not answered: a locked or
        /// retired account may not sign in, and may not be attributed with anything either.
        /// </remarks>
        /// <param name="name">The user name or e-mail address. May be null.</param>
        /// <returns>The identity, or <see langword="null"/> when no active account carries
        /// that name.</returns>
        Identity GetIdentityByLogin(string name);

        /// <summary>
        /// Returns the identity the given request is served for — the account whose profile
        /// settings the profile pages read and write.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>
        /// The identity of the caller, or <see langword="null"/> when no identity can be resolved.
        /// </returns>
        Identity GetCurrentIdentity(IRequest request);

        /// <summary>
        /// Retrieves a collection of identities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>An enumerable collection of identities.</returns>
        IEnumerable<Identity> GetIdentities(IQuery<Identity> query);

        /// <summary>
        /// Retrieves a collection of identities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>An enumerable collection of identities.</returns>
        IEnumerable<Identity> GetIdentities(IQuery<Identity> query, IQueryContext context);

        /// <summary>
        /// Returns how many identities satisfy the supplied filter criteria without loading
        /// them - the figure behind a headline such as the landing page's people count.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted identities. Paging must be left
        /// off: a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching identities.</returns>
        int CountIdentities(IQuery<Identity> query);

        /// <summary>
        /// Adds an identity.
        /// </summary>
        /// <param name="identityEntity">The identity to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        IIdentityManager Add(Identity identityEntity);

        /// <summary>
        /// Updates an identity.
        /// </summary>
        /// <param name="identityEntity">The identity to update.</param>
        /// <returns>The current instance for method chaining.</returns>
        IIdentityManager Update(Identity identityEntity);

        /// <summary>
        /// Removes an identity.
        /// </summary>
        /// <param name="identityId">The identity id to remove.</param>
        /// <returns>The current instance for method chaining.</returns>
        IIdentityManager Remove(Guid identityId);
    }
}
