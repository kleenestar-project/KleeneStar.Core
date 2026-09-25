using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages per-identity session/preference entries stored in the
    /// <c>UserSession</c> table. The manager is intentionally schema-less:
    /// callers pick a <c>scope</c> plus a <c>key</c> and supply an opaque
    /// payload that the producer/consumer pair knows how to interpret. The
    /// first concrete use case is REST API table layouts, for which a few
    /// typed convenience methods are provided.
    /// </summary>
    /// <remarks>
    /// <em>Whose</em> preference an entry is comes from one place -
    /// <see cref="GetCurrentIdentityId"/>, the identity the request's session names - and the
    /// overloads that take a request resolve it there rather than being handed an owner. A
    /// caller who is not signed in owns nothing: the entry is not written and reads answer
    /// null, which is the honest state and cheaper than a row nobody can ever claim. Until the
    /// session was read, every preference in the installation was filed under the seeded
    /// administrator, so one person's column widths were everybody's.
    /// </remarks>
    public sealed class SessionManager : ISessionManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// The identity a <see cref="BeginIdentity"/> scope is acting as, or <see langword="null"/>
        /// outside every such scope.
        /// </summary>
        /// <remarks>
        /// It is nullable because <em>acting as nobody</em> is a statement of its own: a scope
        /// opened on <see cref="Guid.Empty"/> disowns whatever it inherited, and must not fall
        /// through to the request that happens to be in flight.
        /// </remarks>
        private static readonly AsyncLocal<Guid?> _actingIdentityId = new();

        /// <summary>
        /// The identity of a request, resolved once and kept beside it.
        /// </summary>
        /// <remarks>
        /// A page asks this several times - every fragment that greets the user, checks a
        /// favourite or reads a preference - and the answer depends on nothing but the request,
        /// while finding it costs a token check, a row read and a look into the token store. The
        /// table holds no request alive, so an entry dies when the request does; "resolved to
        /// nobody" is stored as <see cref="ResolvedCaller.Nobody"/>, distinguishable from "not
        /// resolved yet".
        /// </remarks>
        private static readonly ConditionalWeakTable<IRequest, ResolvedCaller> _resolved = new();

        /// <summary>
        /// The caller of a request as it was resolved: the identity, and the credential it was
        /// resolved from.
        /// </summary>
        /// <param name="IdentityId">The identity, or <see cref="Guid.Empty"/> for nobody.</param>
        /// <param name="Credential">The credential, or <see langword="null"/> for nobody.</param>
        private sealed record ResolvedCaller(Guid IdentityId, WebIdentity.SessionCredential Credential)
        {
            /// <summary>
            /// The answer for a request nobody is signed in to.
            /// </summary>
            public static readonly ResolvedCaller Nobody = new(Guid.Empty, null);
        }

        /// <summary>
        /// Shared JSON serializer options for round-tripping the opaque payloads
        /// stored under (owner, scope, key). Null fields are omitted on write and
        /// property-name matching is case-insensitive on read.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The host context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private SessionManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Resolves the identity that owns the current request: the authenticated user the
        /// request's session names, or <see cref="Guid.Empty"/> when the request carries no
        /// signed-in user.
        /// </summary>
        /// <remarks>
        /// The framework carries who signed in in a signed token - an access cookie, or a
        /// personal access token as a bearer - and <see cref="WebIdentity.SessionCredential"/>
        /// reads it; this is one question asked of one place, and everything per-user in the
        /// application follows from it: who a comment is by, whose like it is, whose
        /// preferences a table layout belongs to, whom a notification is addressed to, which
        /// identity an audit event names.
        /// <para>
        /// A request with no signed-in user answers <see cref="Guid.Empty"/>, and every caller
        /// already treats that as "nobody": a preference is not stored, a notification is not
        /// recorded, a comment is refused. It used to answer with the seeded administrator,
        /// which was serviceable while nothing could be attributed at all and dishonest as soon
        /// as something could - it signed anonymous writing with a real person's name.
        /// </para>
        /// </remarks>
        /// <param name="request">The current HTTP request. May be null - a caller that has no
        /// request in hand is answered from the acting scope, else from the request being served
        /// on its call chain (<c>WebEx.CurrentRequest</c>).</param>
        /// <returns>The current identity id, or <see cref="Guid.Empty"/>.</returns>
        public Guid GetCurrentIdentityId(IRequest request)
        {
            // an explicit "act as" wins over whatever request is in flight: it is the caller
            // saying on whose behalf this runs, which is a stronger statement than the ambient
            if (request is null && _actingIdentityId.Value.HasValue)
            {
                return _actingIdentityId.Value.Value;
            }

            // the managers ask without a request - they are several calls from the endpoint and
            // shared with callers that have none - so the request being answered on this call
            // chain stands in for the one they were not given
            var current = request ?? WebEx.CurrentRequest;

            if (current is null)
            {
                return Guid.Empty;
            }

            return ResolveCached(current).IdentityId;
        }

        /// <summary>
        /// Returns the credential the request is authenticated by - the sign-in grant or the
        /// personal access token behind <see cref="GetCurrentIdentityId"/>.
        /// </summary>
        /// <param name="request">The current HTTP request. May be null, which answers from the
        /// request being served on this call chain.</param>
        /// <returns>The credential, or <see langword="null"/> when nobody is signed in.</returns>
        public WebIdentity.SessionCredential GetCurrentCredential(IRequest request)
        {
            var current = request ?? WebEx.CurrentRequest;

            return current is null ? null : ResolveCached(current).Credential;
        }

        /// <summary>
        /// Resolves the caller of a request once and keeps the answer beside the request.
        /// </summary>
        /// <param name="current">The request.</param>
        /// <returns>The resolved caller.</returns>
        private ResolvedCaller ResolveCached(IRequest current)
        {
            if (_resolved.TryGetValue(current, out var cached))
            {
                return cached;
            }

            var caller = Resolve(current);

            _resolved.AddOrUpdate(current, caller);

            return caller;
        }

        /// <summary>
        /// Acts as the supplied identity until the returned scope is closed.
        /// </summary>
        /// <param name="identityId">The identity to act as.</param>
        /// <returns>The scope.</returns>
        public IDisposable BeginIdentity(Guid identityId)
        {
            var previous = _actingIdentityId.Value;

            _actingIdentityId.Value = identityId;

            return new IdentityScope(previous);
        }

        /// <summary>
        /// Gets a value indicating whether anybody is acting on this call chain.
        /// </summary>
        public bool HasCaller => _actingIdentityId.Value.HasValue || WebEx.CurrentRequest is not null;

        /// <summary>
        /// Reads the request's credential and answers the stored account it stands for - when
        /// that account may still act, and the credential is still good.
        /// </summary>
        /// <remarks>
        /// The token is one of ours or it names nobody: the sign-in answers with the row it
        /// authenticated and an external source answers with the stored account its subject is
        /// linked to, so the token's id is the answer and nothing is matched by name - a
        /// directory entry calling itself <c>admin</c> would otherwise be handed the internal
        /// administrator.
        /// <para>
        /// A token is a snapshot, valid until it expires whatever happened since, so three
        /// questions are asked of every request that the framework does not ask:
        /// </para>
        /// <list type="bullet">
        /// <item>Is the account still <see cref="Model.Entities.IdentityState.Active"/>? A locked
        /// or disabled account stops acting at once, not when its token runs out.</item>
        /// <item>For a sign-in: has its grant been revoked - by signing out, or by ending the
        /// session from the profile? The framework revokes grants in its token store but only
        /// checks the store when a token is <em>refreshed</em>
        /// (<see cref="IIdentitySessionManager.Accepts"/>).</item>
        /// <item>For a personal access token: is it one the profile issued, not revoked, and does
        /// its scope allow the request (<see cref="IAccessTokenManager.Accepts"/>)?</item>
        /// </list>
        /// </remarks>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The resolved caller.</returns>
        private ResolvedCaller Resolve(IRequest request)
        {
            var credential = WebIdentity.SessionCredential.Read(request, _componentHub);
            var identityId = credential?.Identity?.Id ?? Guid.Empty;

            if (identityId == Guid.Empty)
            {
                return ResolvedCaller.Nobody;
            }

            var account = CoreHub.IdentityManager?.GetIdentity(identityId);

            if (account is null || account.State != Model.Entities.IdentityState.Active)
            {
                return ResolvedCaller.Nobody;
            }

            var accepted = credential.Personal
                ? CoreHub.AccessTokenManager?.Accepts(credential, request) ?? false
                : CoreHub.IdentitySessionManager?.Accepts(credential, request) ?? false;

            return accepted ? new ResolvedCaller(identityId, credential) : ResolvedCaller.Nobody;
        }

        /// <summary>
        /// Returns the value stored under (owner, scope, key), or
        /// <see langword="null"/> if no entry exists.
        /// </summary>
        /// <param name="ownerId">The identity that owns the entry.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <returns>The stored value, or <see langword="null"/>.</returns>
        public string GetValue(Guid ownerId, string scope, string key)
        {
            return ModelHub.GetUserSessionValue(ownerId, scope, key);
        }

        /// <summary>
        /// Inserts or updates the value stored under (owner, scope, key).
        /// Passing <see langword="null"/> as <paramref name="value"/> deletes the entry.
        /// </summary>
        /// <param name="ownerId">The identity that owns the entry.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <param name="value">The new value, or <see langword="null"/> to delete.</param>
        public void SetValue(Guid ownerId, string scope, string key, string value)
        {
            ModelHub.SetUserSessionValue(ownerId, scope, key, value);
        }

        /// <summary>
        /// Convenience wrapper that resolves the current identity from the
        /// request and reads the value stored under (current owner, scope, key).
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <returns>The stored value, or <see langword="null"/>.</returns>
        public string GetValue(IRequest request, string scope, string key)
        {
            var owner = GetCurrentIdentityId(request);
            return owner == Guid.Empty ? null : GetValue(owner, scope, key);
        }

        /// <summary>
        /// Convenience wrapper that resolves the current identity from the
        /// request and writes the value stored under (current owner, scope, key).
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <param name="value">The new value, or <see langword="null"/> to delete.</param>
        public void SetValue(IRequest request, string scope, string key, string value)
        {
            var owner = GetCurrentIdentityId(request);
            if (owner == Guid.Empty)
            {
                return;
            }

            SetValue(owner, scope, key, value);
        }

        /// <summary>
        /// Loads the persisted column layout for the REST API table identified
        /// by <paramref name="tableKey"/> belonging to the current request's
        /// identity, or <see langword="null"/> if nothing has been stored yet.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="tableKey">
        /// A stable identifier for the table, typically <c>typeof(MyTable).FullName</c>.
        /// </param>
        /// <returns>
        /// The previously stored column layout (id, visibility, width — in order),
        /// or <see langword="null"/> when the user has never customized the table.
        /// </returns>
        public IReadOnlyList<RestApiTableColumnUpdate> GetTableLayout(IRequest request, string tableKey)
        {
            if (string.IsNullOrWhiteSpace(tableKey))
            {
                return null;
            }

            return TableLayout.Parse(GetValue(request, ISessionManager.TableLayoutScope, tableKey));
        }

        /// <summary>
        /// Stores the column layout for the REST API table identified by
        /// <paramref name="tableKey"/> against the current request's identity.
        /// Only id / visibility / width are persisted — labels, icons, and
        /// templates remain owned by the REST API table itself.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="tableKey">
        /// A stable identifier for the table, typically <c>typeof(MyTable).FullName</c>.
        /// </param>
        /// <param name="columns">
        /// The columns in the order chosen by the user; visibility and width
        /// are taken from each column.
        /// </param>
        public void SetTableLayout(IRequest request, string tableKey, IEnumerable<RestApiTableColumn> columns)
        {
            if (string.IsNullOrWhiteSpace(tableKey))
            {
                return;
            }

            if (columns is null)
            {
                SetValue(request, ISessionManager.TableLayoutScope, tableKey, null);
                return;
            }

            SetValue(request, ISessionManager.TableLayoutScope, tableKey, TableLayout.Serialize(columns) ?? "[]");
        }

        /// <summary>
        /// Applies the stored layout for <paramref name="tableKey"/> on top of the
        /// table's default column list. Columns not mentioned in the stored
        /// layout are appended at the tail with their default visibility.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="tableKey">A stable identifier for the table.</param>
        /// <param name="defaultColumns">The default columns defined by the table.</param>
        /// <returns>The columns reordered/resized for the current user.</returns>
        public IEnumerable<RestApiTableColumn> ApplyStoredTableLayout
        (
            IRequest request,
            string tableKey,
            IEnumerable<RestApiTableColumn> defaultColumns
        )
        {
            return TableLayout.Apply(GetTableLayout(request, tableKey), defaultColumns);
        }

        /// <summary>
        /// Releases resources held by this manager.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The scope handed out by <see cref="BeginIdentity"/>. Closing it puts back the
        /// identity of the enclosing scope, however often it is disposed.
        /// </summary>
        /// <param name="previous">The identity that was current when the scope was opened.</param>
        private sealed class IdentityScope(Guid? previous) : IDisposable
        {
            private bool _closed;

            /// <summary>
            /// Restores the identity of the enclosing scope.
            /// </summary>
            public void Dispose()
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                _actingIdentityId.Value = previous;
            }
        }
    }
}
