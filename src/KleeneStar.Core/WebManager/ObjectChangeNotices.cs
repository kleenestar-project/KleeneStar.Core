using System;
using System.Threading;
using WebExpress.WebApp.WebMessageQueue;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Announces object changes on WebExpress's data change channel, so every open page whose
    /// lists declare the object domain reloads - the overviews,
    /// the search - also when the change did not come through a CRUD endpoint (a move, a bulk
    /// action, an operation run, an automation).
    /// </summary>
    /// <remarks>
    /// <see cref="ObjectManager"/> announces every add, update and removal. A burst - the objects
    /// of a batch move, of a bulk action, of a recursive run - is folded into one announcement by
    /// <see cref="Batch"/>: inside the scope the changes are only marked, and the outermost scope
    /// announces once when it closes. The client coalesces as well, but every announcement is a
    /// message to every subscribed connection, so the server does not send ten thousand of them.
    /// Without a message queue (the tests) an announcement does nothing.
    /// </remarks>
    public static class ObjectChangeNotices
    {
        private static readonly AsyncLocal<Scope> _batch = new();

        /// <summary>
        /// Gets or sets what sends an announcement - the data change channel by default; tests
        /// replace it to count what would have been sent (the pattern of the webhook transport).
        /// </summary>
        public static Action<Guid?> Transport { get; set; } = SendToChannel;

        /// <summary>
        /// Announces a change of an object (or marks it inside a batch).
        /// </summary>
        /// <param name="objectId">The object, or <see langword="null"/> for "objects changed".</param>
        public static void Notify(Guid? objectId = null)
        {
            if (_batch.Value is { } scope)
            {
                scope.Pending = true;

                return;
            }

            Send(objectId);
        }

        /// <summary>
        /// Opens a scope whose changes are announced once, when the outermost scope closes.
        /// </summary>
        /// <returns>The scope.</returns>
        public static IDisposable Batch()
        {
            if (_batch.Value is { } outer)
            {
                outer.Depth++;

                return outer;
            }

            var scope = new Scope();

            _batch.Value = scope;

            return scope;
        }

        /// <summary>
        /// Sends one announcement, fire and forget: a transport failure never fails the write.
        /// </summary>
        /// <param name="objectId">The object, or <see langword="null"/>.</param>
        private static void Send(Guid? objectId)
        {
            try
            {
                (Transport ?? SendToChannel)(objectId);
            }
            catch
            {
                // the channel is a courtesy to the other open pages
            }
        }

        /// <summary>
        /// Sends one announcement on the data change channel.
        /// </summary>
        /// <param name="objectId">The object, or <see langword="null"/>.</param>
        private static void SendToChannel(Guid? objectId)
        {
            try
            {
                _ = DataChangedNotifier.NotifyAsync<Model.Entities.Object>(DataChangeOperation.Updated, objectId?.ToString());
            }
            catch
            {
                // the channel is a courtesy to the other open pages
            }
        }

        /// <summary>
        /// A batch scope.
        /// </summary>
        private sealed class Scope : IDisposable
        {
            public int Depth { get; set; } = 1;

            public bool Pending { get; set; }

            public void Dispose()
            {
                if (--Depth > 0)
                {
                    return;
                }

                _batch.Value = null;

                if (Pending)
                {
                    Send(null);
                }
            }
        }
    }
}
