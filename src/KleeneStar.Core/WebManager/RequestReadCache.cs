using KleeneStar.Model;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using WebExpress.WebCore;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Keeps what a reading request has read of the rarely changing structure (classes, fields,
    /// statuses, workflows, identities, workspaces, boards) for the rest of that request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list renders the same class, the same assignee and the same workflow once per row,
    /// and without this every row read them again. The cache lives as long as the request,
    /// so it never answers another request, another replica's write is seen by the next
    /// request, and nothing has to be evicted.
    /// </para>
    /// <para>
    /// <b>Only a GET or HEAD request is cached.</b> A request that writes builds and changes
    /// entities, and a shared instance would hand a change that was never saved to the next
    /// reader of the same request.
    /// </para>
    /// <para>
    /// <b>An entry is keyed by the versions of the entity types it was read from</b>
    /// (<see cref="EntityVersions"/>), which every save in this process advances. A row the
    /// request itself, or anything else in the process, writes is therefore read again.
    /// </para>
    /// <para>
    /// The cached instances are shared within the request, so a reader must not change them.
    /// </para>
    /// </remarks>
    public static class RequestReadCache
    {
        /// <summary>
        /// The entries per request.
        /// </summary>
        private static readonly ConditionalWeakTable<object, ConcurrentDictionary<(string Area, object Key, long Version), object>> _entries = [];

        /// <summary>
        /// Returns the cached answer of the current request, reading it on the first call.
        /// </summary>
        /// <typeparam name="T">The type of the answer.</typeparam>
        /// <param name="area">The name of what is read, which separates keys of different readers.</param>
        /// <param name="key">The key of the answer within the area.</param>
        /// <param name="read">Reads the answer.</param>
        /// <param name="types">The entity types the answer is read from.</param>
        /// <returns>The answer.</returns>
        public static T Get<T>(string area, object key, Func<T> read, params Type[] types)
        {
            var request = WebEx.CurrentRequest;

            return request is not null && IsReading(request)
                ? Get(request, area, key, read, types)
                : read();
        }

        /// <summary>
        /// Returns the cached answer of the supplied owner, reading it on the first call.
        /// </summary>
        /// <typeparam name="T">The type of the answer.</typeparam>
        /// <param name="owner">The owner of the entries, the request.</param>
        /// <param name="area">The name of what is read.</param>
        /// <param name="key">The key of the answer within the area.</param>
        /// <param name="read">Reads the answer.</param>
        /// <param name="types">The entity types the answer is read from.</param>
        /// <returns>The answer.</returns>
        public static T Get<T>(object owner, string area, object key, Func<T> read, params Type[] types)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(read);

            var entries = _entries.GetValue(owner, _ => new ConcurrentDictionary<(string, object, long), object>());
            var entryKey = (area, key, EntityVersions.Of(types));

            if (entries.TryGetValue(entryKey, out var cached))
            {
                return (T)cached;
            }

            var value = read();

            entries[entryKey] = value;

            return value;
        }

        /// <summary>
        /// Determines whether a request only reads.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> for GET and HEAD.</returns>
        private static bool IsReading(IRequest request)
        {
            return request.Method is RequestMethod.GET or RequestMethod.HEAD;
        }
    }
}
