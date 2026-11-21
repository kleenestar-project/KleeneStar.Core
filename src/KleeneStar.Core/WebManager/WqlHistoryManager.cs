using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the WQL queries each identity has run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The manager is deliberately <b>not</b> wired into the audit log by
    /// <see cref="AuditManager.Connect"/>, for two reasons. A search is not a change: nothing
    /// about a record is different afterwards, so there is no before/after for
    /// <c>AuditDelta</c> to record and nothing for <c>Project</c> to replay. And the log is
    /// hash-chained and append-only, so feeding it a row per query typed into a table filter
    /// would bury the decisions it exists to preserve under browsing noise.
    /// </para>
    /// <para>
    /// What people search for is also the most personal trail the application keeps, which is
    /// an argument for keeping it out of an installation-wide log that administrators read. It
    /// is scoped to its owner, capped, and removed with the account
    /// (<c>WqlHistoryConfiguration</c> cascades it off <see cref="Identity"/>).
    /// </para>
    /// </remarks>
    public sealed class WqlHistoryManager : IWqlHistoryManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised after a query has been recorded.
        /// </summary>
        public event EventHandler<WqlHistory> QueryRecorded;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private WqlHistoryManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the queries the supplied identity has run against a subject, most recent
        /// first.
        /// </summary>
        /// <param name="ownerId">The identity whose history is read.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="count">How many entries to return at most.</param>
        /// <returns>The queries, most recent first; empty when there are none.</returns>
        public IReadOnlyList<string> GetHistory(Guid ownerId, string subject, int count = WqlHistory.Limit)
        {
            return ModelHub.GetWqlHistory(ownerId, subject, count);
        }

        /// <summary>
        /// Returns the queries the identity behind the supplied request has run against a
        /// subject, most recent first.
        /// </summary>
        /// <param name="request">The request naming the identity.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="count">How many entries to return at most.</param>
        /// <returns>The queries, most recent first; empty when there are none.</returns>
        public IReadOnlyList<string> GetHistory(IRequest request, string subject, int count = WqlHistory.Limit)
        {
            return GetHistory(CoreHub.SessionManager.GetCurrentIdentityId(request), subject, count);
        }

        /// <summary>
        /// Records that an identity ran a query against a subject.
        /// </summary>
        /// <param name="ownerId">The identity that ran the query.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="wql">The query as it was submitted. Blank values are ignored.</param>
        /// <returns>The recorded entry, or <see langword="null"/> when nothing was recorded.</returns>
        public WqlHistory Record(Guid ownerId, string subject, string wql)
        {
            var entry = ModelHub.RecordWqlQuery(ownerId, subject, wql);

            if (entry is not null)
            {
                QueryRecorded?.Invoke(this, entry);
            }

            return entry;
        }

        /// <summary>
        /// Records that the identity behind the supplied request ran a query against a
        /// subject.
        /// </summary>
        /// <param name="request">The request naming the identity.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="wql">The query as it was submitted. Blank values are ignored.</param>
        /// <returns>The recorded entry, or <see langword="null"/> when nothing was recorded.</returns>
        public WqlHistory Record(IRequest request, string subject, string wql)
        {
            return Record(CoreHub.SessionManager.GetCurrentIdentityId(request), subject, wql);
        }

        /// <summary>
        /// Removes the query history of an identity, either wholly or for one subject.
        /// </summary>
        /// <param name="ownerId">The identity whose history is cleared.</param>
        /// <param name="subject">
        /// The subject to clear, or <see langword="null"/> to clear every subject.
        /// </param>
        /// <returns>The number of entries removed.</returns>
        public int Clear(Guid ownerId, string subject = null)
        {
            return ModelHub.ClearWqlHistory(ownerId, subject);
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
