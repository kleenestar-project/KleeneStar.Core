using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base class for the WQL query prompts. It answers the prompt's history
    /// with the queries the current identity has actually run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every prompt used to override <c>GetHistory</c> with the same pair of hand-written
    /// example expressions. That is a demonstration rather than a history: it named nothing
    /// the person in front of the screen had searched for, it was identical for every user and
    /// every entity, and it went stale silently - one of the examples pointed at a seeded
    /// object key that stopped existing the moment a class was added to the seed, because the
    /// keys are handed out from a running counter over all classes.
    /// </para>
    /// <para>
    /// The queries are recorded where they are executed, by
    /// <see cref="KleeneStarRestApiTable{TIndexItem}"/>, and both ends name the same
    /// <see cref="WqlHistorySubject"/> - the queried type - so a prompt reads back exactly
    /// what the table it feeds has run. A prompt whose surface is not a table records nothing
    /// yet and simply answers an empty history until the identity runs a query of that type
    /// somewhere that does.
    /// </para>
    /// </remarks>
    /// <typeparam name="TIndexItem">The type the prompt writes queries against.</typeparam>
    public abstract class KleeneStarRestApiWqlPrompt<TIndexItem> : RestApiWqlPrompt<TIndexItem>
        where TIndexItem : IIndexItem
    {
        /// <summary>
        /// Returns the subject the prompt's history is read under. Defaults to the queried
        /// type, which is what the table executing its queries records under as well.
        /// </summary>
        protected virtual string WqlHistorySubject => typeof(TIndexItem).FullName;

        /// <summary>
        /// Returns the queries the current identity has run against
        /// <see cref="WqlHistorySubject"/>, oldest first.
        /// </summary>
        /// <remarks>
        /// The order is the one the prompt navigates in: it starts one past the end of the
        /// list and steps backwards, so the most recently run query has to be the last entry
        /// for the first key press to reach it. The manager answers most-recent-first, which
        /// is the natural order to read and cap in, so it is reversed here rather than stored
        /// upside down.
        /// </remarks>
        /// <param name="request">The request the history is retrieved for.</param>
        /// <returns>The queries, oldest first; empty when the identity has run none.</returns>
        protected override IEnumerable<string> GetHistory(IRequest request)
        {
            return CoreHub.WqlHistoryManager
                .GetHistory(request, WqlHistorySubject)
                .Reverse();
        }
    }
}
