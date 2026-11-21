using KleeneStar.Model.Entities;
using System;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The unit of work one user action forms on one object. Every change reported while the
    /// scope is open joins the same commit; the value rows the action wrote and the commit
    /// describing them are persisted together when the scope closes.
    /// </summary>
    /// <remarks>
    /// A scope is what turns "the object manager wrote three things" into "one edit happened".
    /// Without it, each value write would end up as a commit of its own and the history would
    /// read as noise rather than as the actions a user took.
    /// <para>
    /// Scopes nest. Opening a scope for an object that already has one joins the outer scope and
    /// closing the inner one writes nothing, so a manager may open a scope for its own operation
    /// without knowing whether a caller has already opened one around it. Opening a scope for a
    /// <i>different</i> object — a child object created as part of the same action — starts a
    /// separate chain that closes on its own.
    /// </para>
    /// <para>
    /// Disposing without <see cref="Abort"/> commits. Aborting discards both the commit and the
    /// value writes it carried, which is what an operation that failed half way through needs:
    /// the current state never diverges from the head of the chain.
    /// </para>
    /// </remarks>
    public interface ICommitScope : IDisposable
    {
        /// <summary>
        /// Gets the id of the object this scope records changes for.
        /// </summary>
        Guid ObjectId { get; }

        /// <summary>
        /// Gets or sets the action the resulting commit records. Setting a more specific type
        /// than the one the scope was opened with promotes it — an update that turns out to be a
        /// workflow transition is recorded as a transition.
        /// </summary>
        CommitType Type { get; set; }

        /// <summary>
        /// Gets or sets the optional message describing the intent of the change.
        /// </summary>
        string Message { get; set; }

        /// <summary>
        /// Gets the commit the scope appended, or <c>null</c> while the scope is still open,
        /// when it was aborted, or when nothing worth recording happened.
        /// </summary>
        Commit Commit { get; }

        /// <summary>
        /// Discards everything the scope collected, including the value writes. The scope writes
        /// nothing when it is disposed afterwards.
        /// </summary>
        void Abort();
    }
}
