using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The <see cref="ICommitScope"/> implementation owned by the <see cref="CommitManager"/>.
    /// Collects the changes and the pending value writes of one action and hands them to the
    /// manager to be persisted together when the outermost scope closes.
    /// </summary>
    internal sealed class CommitScope : ICommitScope
    {
        private readonly CommitManager _manager;
        private readonly List<Change> _changes = [];
        private readonly Dictionary<Guid, Value> _pendingUpserts = [];
        private readonly Dictionary<Guid, Value> _pendingRemovals = [];

        private int _depth = 1;
        private bool _aborted;
        private bool _closed;

        /// <summary>
        /// Gets the scope that was ambient when this one was opened, restored when it closes.
        /// </summary>
        internal CommitScope Parent { get; }

        /// <summary>
        /// Gets the id of the object this scope records changes for.
        /// </summary>
        public Guid ObjectId { get; }

        /// <summary>
        /// Gets the identity that initiated the action.
        /// </summary>
        internal Guid IdentityId { get; private set; }

        /// <summary>
        /// Gets or sets the action the resulting commit records.
        /// </summary>
        public CommitType Type { get; set; }

        /// <summary>
        /// Gets or sets the optional message describing the intent of the change.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets the commit the scope appended, or <c>null</c>.
        /// </summary>
        public Commit Commit { get; private set; }

        /// <summary>
        /// Gets the changes collected so far, in the order they were recorded.
        /// </summary>
        internal IReadOnlyList<Change> Changes => _changes;

        /// <summary>
        /// Gets the value rows to write when the scope closes.
        /// </summary>
        internal IReadOnlyCollection<Value> PendingUpserts => _pendingUpserts.Values;

        /// <summary>
        /// Gets the value rows to delete when the scope closes.
        /// </summary>
        internal IReadOnlyCollection<Value> PendingRemovals => _pendingRemovals.Values;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="manager">The manager that owns the scope and performs the write.</param>
        /// <param name="parent">The scope that was ambient when this one was opened.</param>
        /// <param name="objectId">The id of the object the scope records changes for.</param>
        /// <param name="type">The action the resulting commit records.</param>
        /// <param name="identityId">The identity that initiated the action.</param>
        /// <param name="message">The optional commit message.</param>
        internal CommitScope(CommitManager manager, CommitScope parent, Guid objectId, CommitType type, Guid identityId, string message)
        {
            _manager = manager;
            Parent = parent;
            ObjectId = objectId;
            Type = type;
            IdentityId = identityId;
            Message = message;
        }

        /// <summary>
        /// Joins an already open scope. The nested <see cref="Dispose"/> then only decrements the
        /// depth; only the outermost close writes.
        /// </summary>
        /// <param name="type">The action the joining caller intends to record.</param>
        /// <param name="identityId">The identity the joining caller names, if any.</param>
        /// <param name="message">The message the joining caller supplies, if any.</param>
        internal void Enter(CommitType type, Guid identityId, string message)
        {
            _depth++;

            Promote(type);

            // an inner caller that knows who is acting fills in for an outer one that did not
            if (IdentityId == Guid.Empty && identityId != Guid.Empty)
            {
                IdentityId = identityId;
            }

            if (string.IsNullOrWhiteSpace(Message) && !string.IsNullOrWhiteSpace(message))
            {
                Message = message;
            }
        }

        /// <summary>
        /// Raises the recorded action to the more specific of the two. An action reported from
        /// the inside (a workflow transition) says more about what happened than the generic
        /// update a caller opened the scope with, and must not be flattened back to it.
        /// </summary>
        /// <param name="type">The type being reported.</param>
        internal void Promote(CommitType type)
        {
            if (Rank(type) > Rank(Type))
            {
                Type = type;
            }
        }

        /// <summary>
        /// Records a field modification. A repeated change to the same attribute inside one
        /// scope collapses into a single entry that keeps the first old value and the last new
        /// one, so an edit that passes through an intermediate value does not read as two.
        /// </summary>
        /// <remarks>
        /// Attributes are matched by <see cref="CommitManager.Key"/> rather than by name: a class
        /// field called <c>Description</c> and the object's own <c>description</c> are two
        /// different things, and collapsing them into one entry would lose a change.
        /// </remarks>
        /// <param name="change">The change to record.</param>
        internal void Record(Change change)
        {
            if (change is null || string.IsNullOrWhiteSpace(change.Name))
            {
                return;
            }

            var key = CommitManager.Key(change);
            var existing = _changes.FirstOrDefault(x => string.Equals(CommitManager.Key(x), key, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                _changes.Add(change);

                return;
            }

            existing.NewValue = change.NewValue;
        }

        /// <summary>
        /// Stages a value row to be written when the scope closes, and forgets any pending
        /// removal of the same field.
        /// </summary>
        /// <param name="value">The value row to write.</param>
        internal void StageUpsert(Value value)
        {
            if (value is null)
            {
                return;
            }

            _pendingRemovals.Remove(value.FieldId);
            _pendingUpserts[value.FieldId] = value;
        }

        /// <summary>
        /// Stages a value row to be deleted when the scope closes, and forgets any pending write
        /// of the same field.
        /// </summary>
        /// <param name="value">The value row to delete.</param>
        internal void StageRemoval(Value value)
        {
            if (value is null)
            {
                return;
            }

            _pendingUpserts.Remove(value.FieldId);
            _pendingRemovals[value.FieldId] = value;
        }

        /// <summary>
        /// Returns the pending write or deletion staged for a field, so a read inside the scope
        /// sees what the scope has already been told.
        /// </summary>
        /// <param name="fieldId">The field id.</param>
        /// <param name="value">The staged value row, or <c>null</c> when it is staged for deletion.</param>
        /// <returns><see langword="true"/> when the scope has something staged for the field.</returns>
        internal bool TryGetStaged(Guid fieldId, out Value value)
        {
            if (_pendingUpserts.TryGetValue(fieldId, out value))
            {
                return true;
            }

            if (_pendingRemovals.ContainsKey(fieldId))
            {
                value = null;

                return true;
            }

            value = null;

            return false;
        }

        /// <summary>
        /// Returns whether the scope has staged a deletion of the value row with the supplied id.
        /// </summary>
        /// <param name="valueId">The value id.</param>
        /// <returns><see langword="true"/> when the row is staged for deletion.</returns>
        internal bool IsRemoved(Guid valueId)
        {
            return _pendingRemovals.Values.Any(x => x.Id == valueId);
        }

        /// <summary>
        /// Discards everything the scope collected, including the value writes.
        /// </summary>
        public void Abort()
        {
            _aborted = true;

            _changes.Clear();
            _pendingUpserts.Clear();
            _pendingRemovals.Clear();
        }

        /// <summary>
        /// Closes the scope. The outermost close hands the collected changes and value writes to
        /// the manager, which persists them together; a nested close only decrements the depth.
        /// </summary>
        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            if (--_depth > 0)
            {
                return;
            }

            _closed = true;

            Commit = _manager.Close(this, _aborted);
        }

        /// <summary>
        /// Ranks the commit types by how much they say about what happened, so the more specific
        /// one survives when several are reported for the same scope.
        /// </summary>
        /// <param name="type">The commit type.</param>
        /// <returns>The rank; higher wins.</returns>
        private static int Rank(CommitType type)
        {
            return type switch
            {
                CommitType.Updated => 0,
                CommitType.Transitioned => 1,
                CommitType.Archived => 2,
                CommitType.Restored => 3,
                CommitType.Deleted => 4,
                CommitType.Created => 5,
                _ => 0
            };
        }
    }
}
