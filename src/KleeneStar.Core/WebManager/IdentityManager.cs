using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages identity entities within the application.
    /// </summary>
    public sealed class IdentityManager : IIdentityManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// An event that fires when an identity is added.
        /// </summary>
        public event EventHandler<Identity> IdentityAdded;

        /// <summary>
        /// An event that fires when an identity is updated.
        /// </summary>
        public event EventHandler<Identity> IdentityUpdated;

        /// <summary>
        /// An event that fires when an identity is removed.
        /// </summary>
        public event EventHandler<Identity> IdentityRemoved;

        /// <summary>
        /// Returns the collection of names that are reserved and cannot be used for identities.
        /// </summary>
        public static IEnumerable<string> ReservedIdentityNames =>
        [
            "default", "admin", "system", "assets", "api", "identity",
            "identities", "icons", "setting"
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private IdentityManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns an identity based on its id.
        /// </summary>
        /// <param name="identityId">The id of the identity.</param>
        /// <returns>The identity.</returns>
        public Identity GetIdentity(Guid identityId)
        {
            var query = new Query<Identity>()
                .Where(x => x.Id == identityId)
                .WithPaging(0, 1);

            return ModelHub.GetIdentities(query)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns an identity based on its id parameter.
        /// </summary>
        /// <param name="identityId">The id parameter of the identity.</param>
        /// <returns>The identity.</returns>
        public Identity GetIdentity(IdentityIdParameter identityId)
        {
            var guid = Guid.TryParse(identityId.Value, out Guid id) ? id : Guid.Empty;

            return GetIdentity(guid);
        }

        /// <summary>
        /// Returns the identity that signs in under the supplied name: the account whose user
        /// name or e-mail address it is, compared case-insensitively.
        /// </summary>
        /// <remarks>
        /// Only an <see cref="IdentityState.Active"/> account is answered, so a locked or
        /// retired one can neither sign in nor be attributed with anything. The comparison is
        /// made in memory rather than in the query, because the two candidate columns are read
        /// with the same string and the identity table is small enough that one pass over it
        /// costs less than the two indexed queries it would take to say the same thing.
        /// </remarks>
        /// <param name="name">The user name or e-mail address. May be null.</param>
        /// <returns>The identity, or <see langword="null"/>.</returns>
        public Identity GetIdentityByLogin(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var trimmed = name.Trim();

            return GetIdentities(new Query<Identity>())
                .FirstOrDefault(x => x.State == IdentityState.Active
                    && (string.Equals(x.UserName, trimmed, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.Email, trimmed, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Returns the account an external source knows by the supplied subject, whatever its
        /// state.
        /// </summary>
        /// <param name="source">The key of the source.</param>
        /// <param name="subject">The identifier the source knows the account by.</param>
        /// <returns>The identity, or <see langword="null"/>.</returns>
        public Identity GetIdentityBySource(string source, string subject)
        {
            var key = IdentitySource.Normalize(source);

            // the internal source has no subjects: an internal account is never found this way
            if (key is null || string.IsNullOrEmpty(subject))
            {
                return null;
            }

            var query = new Query<Identity>()
                .Where(x => x.AuthenticationSource == key && x.ExternalSubject == subject)
                .WithPaging(0, 1);

            return ModelHub.GetIdentities(query).FirstOrDefault();
        }

        /// <summary>
        /// Returns the identity the given request is served for — the account whose profile
        /// settings the profile pages read and write.
        /// </summary>
        /// <remarks>
        /// Which identity that is comes from <see cref="ISessionManager.GetCurrentIdentityId"/>,
        /// which reads the signed-in user off the request's session - so the profile pages show
        /// the account of whoever is looking at them, and nothing at all to a caller who is not
        /// signed in.
        /// </remarks>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>
        /// The identity of the caller, or <see langword="null"/> when no identity can be resolved.
        /// </returns>
        public Identity GetCurrentIdentity(IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return identityId == Guid.Empty ? null : GetIdentity(identityId);
        }

        /// <summary>
        /// Retrieves a collection of identities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>An enumerable collection of identities.</returns>
        public IEnumerable<Identity> GetIdentities(IQuery<Identity> query)
        {
            return ModelHub.GetIdentities(query);
        }

        /// <summary>
        /// Returns how many identities satisfy the specified filter criteria without
        /// loading them.
        /// </summary>
        /// <param name="query">
        /// The query criteria used to filter the counted identities. Paging must be left
        /// off: a query carrying it counts the page, not the whole result.
        /// </param>
        /// <returns>The number of matching identities.</returns>
        public int CountIdentities(IQuery<Identity> query)
        {
            return ModelHub.CountIdentities(query);
        }

        /// <summary>
        /// Retrieves a collection of identities that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>An enumerable collection of identities.</returns>
        public IEnumerable<Identity> GetIdentities(IQuery<Identity> query, IQueryContext context)
        {
            return ModelHub.GetIdentities(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds an identity.
        /// </summary>
        /// <param name="identityEntity">The identity to add.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IIdentityManager Add(Identity identityEntity)
        {
            ArgumentNullException.ThrowIfNull(identityEntity);

            // one spelling of "internal" in the data, see IdentitySource
            identityEntity.AuthenticationSource = IdentitySource.Normalize(identityEntity.AuthenticationSource);

            ModelHub.Add(identityEntity);

            IdentityAdded?.Invoke(this, identityEntity);

            CoreHub.AddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.identity.created", identityEntity);

            return this;
        }

        /// <summary>
        /// Updates an identity.
        /// </summary>
        /// <param name="identityEntity">The identity to update.</param>
        /// <returns>The current instance for method chaining.</returns>
        /// <remarks>
        /// Moving an account to another authentication source drops the credentials it had
        /// with the old one - the password hash and the external subject - because they prove
        /// nothing to the new source: an account moved to an external source must not keep a
        /// password that still signs it in here, and one moved back must get a password of its
        /// own through a reset link.
        /// </remarks>
        public IIdentityManager Update(Identity identityEntity)
        {
            ArgumentNullException.ThrowIfNull(identityEntity);

            identityEntity.AuthenticationSource = IdentitySource.Normalize(identityEntity.AuthenticationSource);

            var stored = GetIdentity(identityEntity.Id);

            if (stored is not null && !IdentitySource.AreEqual(stored.AuthenticationSource, identityEntity.AuthenticationSource))
            {
                ModelHub.SetPasswordHash(identityEntity.Id, null, null);
                ModelHub.SetExternalSubject(identityEntity.Id, null);
            }

            ModelHub.Update(identityEntity);

            IdentityUpdated?.Invoke(this, identityEntity);

            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.identity.updated", identityEntity);

            return this;
        }

        /// <summary>
        /// Removes an identity.
        /// </summary>
        /// <param name="identityId">The identity id to remove.</param>
        /// <returns>The current instance for method chaining.</returns>
        public IIdentityManager Remove(Guid identityId)
        {
            var identityEntry = GetIdentity(identityId);

            if (identityEntry is not null)
            {
                ModelHub.Remove(identityEntry);
                IdentityRemoved?.Invoke(this, identityEntry);
            }

            return this;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
