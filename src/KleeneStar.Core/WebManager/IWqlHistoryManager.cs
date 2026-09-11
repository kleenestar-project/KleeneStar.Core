using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the WQL queries each identity has run, which the query prompts offer back as
    /// their history.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two halves live at opposite ends of the same request cycle and are joined by the
    /// <em>subject</em>: <see cref="Record"/> is called where a query is executed - the table
    /// endpoint, after the statement parsed - and <see cref="GetHistory"/> is called by the
    /// prompt that offered it. Both name the indexed type as the subject, which is what lets
    /// one read what the other wrote without either knowing the route the other sits on.
    /// </para>
    /// <para>
    /// The history is personal and is stored per identity, so nothing an identity searched for
    /// is visible to anybody else.
    /// </para>
    /// </remarks>
    public interface IWqlHistoryManager : IComponentManager
    {
        /// <summary>
        /// Raised after a query has been recorded.
        /// </summary>
        event EventHandler<WqlHistory> QueryRecorded;

        /// <summary>
        /// Returns the queries the supplied identity has run against a subject, most recent
        /// first.
        /// </summary>
        /// <param name="ownerId">The identity whose history is read.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="count">How many entries to return at most.</param>
        /// <returns>The queries, most recent first; empty when there are none.</returns>
        IReadOnlyList<string> GetHistory(Guid ownerId, string subject, int count = WqlHistory.Limit);

        /// <summary>
        /// Returns the queries the identity behind the supplied request has run against a
        /// subject, most recent first.
        /// </summary>
        /// <param name="request">The request naming the identity.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="count">How many entries to return at most.</param>
        /// <returns>The queries, most recent first; empty when there are none.</returns>
        IReadOnlyList<string> GetHistory(IRequest request, string subject, int count = WqlHistory.Limit);

        /// <summary>
        /// Records that an identity ran a query against a subject.
        /// </summary>
        /// <remarks>
        /// A query already in the history is moved to the front rather than repeated, and the
        /// list is capped - the oldest entry falls off.
        /// </remarks>
        /// <param name="ownerId">The identity that ran the query.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="wql">The query as it was submitted. Blank values are ignored.</param>
        /// <returns>The recorded entry, or <see langword="null"/> when nothing was recorded.</returns>
        WqlHistory Record(Guid ownerId, string subject, string wql);

        /// <summary>
        /// Records that the identity behind the supplied request ran a query against a
        /// subject.
        /// </summary>
        /// <param name="request">The request naming the identity.</param>
        /// <param name="subject">What was queried - the full name of the indexed type.</param>
        /// <param name="wql">The query as it was submitted. Blank values are ignored.</param>
        /// <returns>The recorded entry, or <see langword="null"/> when nothing was recorded.</returns>
        WqlHistory Record(IRequest request, string subject, string wql);

        /// <summary>
        /// Removes the query history of an identity, either wholly or for one subject.
        /// </summary>
        /// <param name="ownerId">The identity whose history is cleared.</param>
        /// <param name="subject">
        /// The subject to clear, or <see langword="null"/> to clear every subject.
        /// </param>
        /// <returns>The number of entries removed.</returns>
        int Clear(Guid ownerId, string subject = null);
    }
}
