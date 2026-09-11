using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base class for REST API tables that persists the user-chosen
    /// column layout (order, width, visibility) to the <c>UserSession</c> store
    /// via <see cref="WebManager.ISessionManager"/>.
    ///
    /// Subclasses implement <see cref="RetrieveDefaultColumns"/> (their built-in
    /// column definitions). On every retrieve, the stored per-user layout — if
    /// any — is laid over the defaults; on every <c>POST/PUT /Configure</c> the
    /// new layout is persisted under the table's <see cref="System.Type.FullName"/>.
    /// </summary>
    /// <typeparam name="TIndexItem">Type of the index item.</typeparam>
    public abstract class KleeneStarRestApiTable<TIndexItem> : RestApiTable<TIndexItem>
        where TIndexItem : IIndexItem
    {
        /// <summary>
        /// Returns a stable key used to address the per-user layout for this
        /// table. Defaults to the concrete type's full name so that different
        /// REST tables cannot collide.
        /// </summary>
        protected virtual string TableLayoutKey => GetType().FullName;

        /// <summary>
        /// Retrieves the column collection presented to the client. The default
        /// implementation calls <see cref="RetrieveDefaultColumns"/> and applies
        /// the layout previously stored for the current user (if any).
        /// </summary>
        /// <param name="request">The triggering request.</param>
        /// <returns>The effective column collection.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveColums(IRequest request)
        {
            var defaults = RetrieveDefaultColumns(request);
            return CoreHub.SessionManager.ApplyStoredTableLayout(request, TableLayoutKey, defaults);
        }

        /// <summary>
        /// Persists the new layout under the current identity.
        /// </summary>
        /// <param name="columns">The reordered columns as resolved by the framework.</param>
        /// <param name="request">The triggering request.</param>
        protected override void UpdateColumns(IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            CoreHub.SessionManager.SetTableLayout(request, TableLayoutKey, columns);
        }

        /// <summary>
        /// Applies a WQL statement to the query and records it as part of the current
        /// identity's query history.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the one place in the application where a WQL query is known to have been
        /// <em>run</em> rather than typed, which is what makes it the right place to record
        /// one. The framework reaches it only after the parser accepted the expression, and
        /// the prompt's own endpoints cannot stand in for it: <c>analyze</c> fires on every
        /// keystroke and again for the pre-submit validation, so it cannot tell a finished
        /// query from a half-typed one.
        /// </para>
        /// <para>
        /// The subject is the indexed type rather than this table, so the prompt that offered
        /// the query finds it again: the global object search, an issue list and an asset
        /// inventory all write WQL against the same attributes and share one history, while
        /// two prompts over different entities keep theirs apart. See
        /// <see cref="WebManager.IWqlHistoryManager"/>.
        /// </para>
        /// <para>
        /// The text recorded is the one carried on the request, not the parsed statement
        /// rewritten: the history puts an expression back into the input, and it has to be the
        /// expression that was submitted.
        /// </para>
        /// </remarks>
        /// <param name="wqlStatement">The parsed statement.</param>
        /// <param name="query">The query to filter.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The filtered query.</returns>
        protected override IQuery<TIndexItem> Filter(IWqlStatement<TIndexItem> wqlStatement, IQuery<TIndexItem> query, IRequest request)
        {
            if (wqlStatement is not null && !wqlStatement.HasErrors)
            {
                CoreHub.WqlHistoryManager.Record(request, WqlHistorySubject, request?.GetParameter("wql")?.Value);
            }

            return base.Filter(wqlStatement, query, request);
        }

        /// <summary>
        /// Returns the subject the table's queries are recorded and read back under. Defaults
        /// to the queried type, which is what the matching WQL prompt names as well.
        /// </summary>
        protected virtual string WqlHistorySubject => typeof(TIndexItem).FullName;

        /// <summary>
        /// Returns the built-in column definitions for the table. Subclasses
        /// implement this exactly as they previously implemented
        /// <c>RetrieveColums</c>; ordering / visibility / width here represent
        /// the default state shown to a user that has never customized the table.
        /// </summary>
        /// <param name="request">The triggering request.</param>
        /// <returns>The default columns.</returns>
        protected abstract IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request);
    }
}
