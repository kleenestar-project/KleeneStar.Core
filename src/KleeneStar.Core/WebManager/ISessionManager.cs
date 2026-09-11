using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for the per-identity session manager. The manager
    /// exposes a small key/value façade over the <see cref="UserSession"/>
    /// entity together with strongly-typed helpers for the first concrete
    /// use case: persisted REST API table layouts (column order, width,
    /// visibility) per user.
    /// </summary>
    public interface ISessionManager : IComponentManager
    {
        /// <summary>
        /// The scope used to namespace REST API table layout entries.
        /// </summary>
        const string TableLayoutScope = "rest-table-layout";

        /// <summary>
        /// Resolves the identity that owns the current request - the user its session names -
        /// or <see cref="Guid.Empty"/> when nobody is signed in.
        /// </summary>
        /// <remarks>
        /// There is no fallback identity, and there must not be one: every per-user record in
        /// the application is owned by what this answers - a stored table layout, a saved
        /// search, a favourite, a comment, a notification - and answering with an account that
        /// did not act would file all of it under that person's name.
        /// </remarks>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The current identity id (may be <see cref="Guid.Empty"/>).</returns>
        Guid GetCurrentIdentityId(IRequest request);

        /// <summary>
        /// Acts as the supplied identity for the life of the returned scope: everything asking
        /// <see cref="GetCurrentIdentityId"/> without a request answers it until the scope is
        /// closed.
        /// </summary>
        /// <remarks>
        /// Inside a request nothing needs this - the session names the user, and the request
        /// carries it. Outside one, something acting on a person's behalf has to say so:
        /// scheduled work, an import, a test standing in for a signed-in caller. Saying it here
        /// keeps the alternative out of the managers, which is passing an identity through
        /// every layer that might one day want to record who acted.
        /// <para>
        /// Scopes nest and restore what they found, so a scope may be opened without knowing
        /// what its caller did. It is an <em>ambient</em> identity, not an authorization: it
        /// answers "on whose behalf", never "who is allowed".
        /// </para>
        /// </remarks>
        /// <param name="identityId">The identity to act as. <see cref="Guid.Empty"/> acts as
        /// nobody, which is how a scope disowns whatever it inherited.</param>
        /// <returns>The scope. Closing it restores the identity of the enclosing one.</returns>
        IDisposable BeginIdentity(Guid identityId);

        /// <summary>
        /// Returns the value stored under (owner, scope, key), or
        /// <see langword="null"/> if no entry exists.
        /// </summary>
        /// <param name="ownerId">The identity that owns the entry.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <returns>The stored value, or <see langword="null"/>.</returns>
        string GetValue(Guid ownerId, string scope, string key);

        /// <summary>
        /// Inserts or updates the value stored under (owner, scope, key).
        /// Passing <see langword="null"/> as <paramref name="value"/> deletes the entry.
        /// </summary>
        /// <param name="ownerId">The identity that owns the entry.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <param name="value">The new value, or <see langword="null"/> to delete.</param>
        void SetValue(Guid ownerId, string scope, string key, string value);

        /// <summary>
        /// Convenience wrapper that resolves the current identity from the
        /// request and reads the value stored under (current owner, scope, key).
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <returns>The stored value, or <see langword="null"/>.</returns>
        string GetValue(IRequest request, string scope, string key);

        /// <summary>
        /// Convenience wrapper that resolves the current identity from the
        /// request and writes the value stored under (current owner, scope, key).
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="scope">The scope namespace.</param>
        /// <param name="key">The key inside the scope.</param>
        /// <param name="value">The new value, or <see langword="null"/> to delete.</param>
        void SetValue(IRequest request, string scope, string key, string value);

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
        IReadOnlyList<RestApiTableColumnUpdate> GetTableLayout(IRequest request, string tableKey);

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
        void SetTableLayout(IRequest request, string tableKey, IEnumerable<RestApiTableColumn> columns);

        /// <summary>
        /// Applies the stored layout for <paramref name="tableKey"/> on top of the
        /// table's default column list. Columns not mentioned in the stored
        /// layout are appended at the tail with their default visibility.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="tableKey">A stable identifier for the table.</param>
        /// <param name="defaultColumns">The default columns defined by the table.</param>
        /// <returns>The columns reordered/resized for the current user.</returns>
        IEnumerable<RestApiTableColumn> ApplyStoredTableLayout
        (
            IRequest request,
            string tableKey,
            IEnumerable<RestApiTableColumn> defaultColumns
        );
    }
}
