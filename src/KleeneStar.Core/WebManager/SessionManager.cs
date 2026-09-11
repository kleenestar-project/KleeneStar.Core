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
        /// while finding it costs a session lookup and a row read. The table holds no request
        /// alive, so an entry dies when the request does. The box is a one-element array
        /// because "resolved to nobody" has to be storable and distinguishable from "not
        /// resolved yet".
        /// </remarks>
        private static readonly ConditionalWeakTable<IRequest, Guid[]> _resolved = new();

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
        /// The session is where the framework keeps who signed in - <c>IdentityManager.Login</c>
        /// binds the identity to <c>request.Session</c> and <c>GetCurrentIdentity</c> reads it
        /// back - so this is one question asked of one place, and everything per-user in the
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

            if (_resolved.TryGetValue(current, out var cached))
            {
                return cached[0];
            }

            var identityId = Resolve(current);

            _resolved.AddOrUpdate(current, [identityId]);

            return identityId;
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
        /// Reads the signed-in identity off the request's session and answers the stored
        /// identity it stands for.
        /// </summary>
        /// <remarks>
        /// The identity in the session is usually one of ours - the sign-in endpoint answers
        /// with the row it authenticated - and then its id is the answer. It need not be: the
        /// framework takes identities from every registered provider, so one that names itself
        /// rather than carrying our id is matched to a stored account by user name, e-mail or
        /// display name. An identity that matches no account is <em>not</em> invented as one:
        /// it may sign in and read, and everything that would have to record an author refuses
        /// instead of attributing the act to somebody else.
        /// </remarks>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The identity id, or <see cref="Guid.Empty"/>.</returns>
        private Guid Resolve(IRequest request)
        {
            var identity = _componentHub?.IdentityManager?.GetCurrentIdentity(request);

            if (identity is null)
            {
                return Guid.Empty;
            }

            if (identity.Id != Guid.Empty && CoreHub.IdentityManager?.GetIdentity(identity.Id) is not null)
            {
                return identity.Id;
            }

            return CoreHub.IdentityManager?.GetIdentityByLogin(identity.Name)?.Id
                ?? CoreHub.IdentityManager?.GetIdentityByLogin(identity.Email)?.Id
                ?? Guid.Empty;
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

            var json = GetValue(request, ISessionManager.TableLayoutScope, tableKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var stored = JsonSerializer.Deserialize<List<RestApiTableColumnUpdate>>(json, _jsonOptions);
                return stored?.Where(c => !string.IsNullOrWhiteSpace(c?.Id)).ToList();
            }
            catch (JsonException)
            {
                return null;
            }
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

            var snapshot = columns
                .Where(c => !string.IsNullOrWhiteSpace(c?.Id))
                .Select(c => new RestApiTableColumnUpdate
                {
                    Id = c.Id,
                    Visible = c.Visible,
                    Width = c.Width
                })
                .ToList();

            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            SetValue(request, ISessionManager.TableLayoutScope, tableKey, json);
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
            if (defaultColumns is null)
            {
                yield break;
            }

            var defaults = defaultColumns.ToList();
            var stored = GetTableLayout(request, tableKey);

            if (stored is null || stored.Count == 0)
            {
                foreach (var column in defaults)
                {
                    yield return column;
                }

                yield break;
            }

            var lookup = defaults.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // emit columns in the stored order, copying visibility/width
            foreach (var update in stored)
            {
                if (string.IsNullOrWhiteSpace(update.Id) ||
                    !lookup.TryGetValue(update.Id, out var template) ||
                    !seen.Add(template.Id))
                {
                    continue;
                }

                yield return new RestApiTableColumn
                {
                    Id = template.Id,
                    Name = template.Name,
                    Label = template.Label,
                    Icon = template.Icon,
                    Template = template.Template,
                    Visible = update.Visible ?? template.Visible,
                    Width = update.Width ?? template.Width
                };
            }

            // append any column the stored layout does not know about (e.g. a
            // column added in a newer build) at the tail with its default state
            foreach (var column in defaults)
            {
                if (seen.Contains(column.Id))
                {
                    continue;
                }

                yield return column;
            }
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
