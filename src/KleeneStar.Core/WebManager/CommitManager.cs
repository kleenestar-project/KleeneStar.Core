using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Owns the append-only commit chains of the objects: appends every commit, applies the
    /// value rows it describes in the same transaction, replays the chain to reconstruct past
    /// states, and reapplies one of them on request.
    /// </summary>
    /// <remarks>
    /// The manager is the only writer of the versioning store and — through the value staging it
    /// offers the <see cref="IValueManager"/> — the only writer of the <see cref="Value"/> rows
    /// while a commit is being recorded. That is what makes the invariant enforceable rather than
    /// merely intended: a change cannot reach the current state without also reaching the
    /// history, because both are written by the same transaction.
    /// <para>
    /// The ambient scope is held in an <see cref="AsyncLocal{T}"/> so it follows one request
    /// through its awaits without leaking into the requests being served beside it.
    /// </para>
    /// </remarks>
    public sealed class CommitManager : ICommitManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly AsyncLocal<CommitScope> _ambient = new();

        /// <summary>
        /// Raised after a commit has been appended to an object's chain.
        /// </summary>
        public event EventHandler<Commit> CommitAdded;

        /// <summary>
        /// Raised after a historical state has been reapplied as a new commit.
        /// </summary>
        public event EventHandler<CommitRestoreResult> CommitRestored;

        /// <summary>
        /// Raised after a difference between two revisions has been computed.
        /// </summary>
        public event EventHandler<CommitDiff> CommitDiffed;

        /// <summary>
        /// Gets every commit the manager holds, newest first.
        /// </summary>
        public IEnumerable<Commit> Commits
        {
            get
            {
                var query = new Query<Commit>()
                    .OrderByDesc(x => x.Created);

                return Hydrate(ModelHub.GetCommits(query));
            }
        }

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private CommitManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Opens the unit of work one action forms on one object, or joins the one already open
        /// for it.
        /// </summary>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="type">The action being recorded.</param>
        /// <param name="identityId">The identity performing the action, or <see cref="Guid.Empty"/>.</param>
        /// <param name="message">An optional message describing the intent of the change.</param>
        /// <returns>The scope. Dispose it to write, or abort it to discard.</returns>
        public ICommitScope BeginCommit(Guid objectId, CommitType type, Guid identityId, string message = null)
        {
            var existing = Find(objectId);

            if (existing is not null)
            {
                existing.Enter(type, identityId, message);

                return existing;
            }

            var scope = new CommitScope(this, _ambient.Value, objectId, type, identityId, message);
            _ambient.Value = scope;

            return scope;
        }

        /// <summary>
        /// Appends a commit to an object's chain directly, without a scope.
        /// </summary>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="type">The action being recorded.</param>
        /// <param name="changes">The field modifications the action produced. May be empty.</param>
        /// <param name="identityId">The identity performing the action, or <see cref="Guid.Empty"/>.</param>
        /// <param name="message">An optional message describing the intent of the change.</param>
        /// <returns>The appended commit, or <c>null</c> when the object cannot be identified.</returns>
        public Commit AddCommit(Guid objectId, CommitType type, IEnumerable<Change> changes, Guid identityId, string message = null)
        {
            var commit = Build(objectId, type, changes, identityId, message);

            if (commit is null)
            {
                return null;
            }

            return Persist(commit, null, null);
        }

        /// <summary>
        /// Records a single field modification against the ambient scope of the object, or — when
        /// no scope is open for it — as a commit of its own.
        /// </summary>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="change">The field modification.</param>
        /// <param name="type">The action to record when no scope is open.</param>
        /// <param name="identityId">The identity performing the action, or <see cref="Guid.Empty"/>.</param>
        public void Record(Guid objectId, Change change, CommitType type, Guid identityId)
        {
            if (change is null || string.IsNullOrWhiteSpace(change.Name))
            {
                return;
            }

            var scope = Find(objectId);

            if (scope is not null)
            {
                scope.Promote(type);
                scope.Record(change);

                return;
            }

            AddCommit(objectId, type, [change], identityId);
        }

        /// <summary>
        /// Returns the commit chain of an object, newest first.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The chain, newest first.</returns>
        public IEnumerable<Commit> GetHistory(Guid objectId)
        {
            var chain = ModelHub.GetCommitChain(objectId).Reverse().ToList();

            return Hydrate(chain);
        }

        /// <summary>
        /// Returns the commits that satisfy the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching commits.</returns>
        public IEnumerable<Commit> GetCommits(IQuery<Commit> query)
        {
            return Hydrate(ModelHub.GetCommits(query));
        }

        /// <summary>
        /// Returns the commits that satisfy the supplied query, executed inside the supplied
        /// query context.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching commits.</returns>
        public IEnumerable<Commit> GetCommits(IQuery<Commit> query, IQueryContext context)
        {
            return Hydrate(ModelHub.GetCommits(query, context as KleeneStarDbContext));
        }

        /// <summary>
        /// Returns one revision of an object's chain.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="number">The 1-based revision number.</param>
        /// <returns>The commit, or <c>null</c>.</returns>
        public Commit GetCommit(Guid objectId, int number)
        {
            return Hydrate(ModelHub.GetCommit(objectId, number));
        }

        /// <summary>
        /// Returns a commit by its unique identifier.
        /// </summary>
        /// <param name="commitId">The commit id.</param>
        /// <returns>The commit, or <c>null</c>.</returns>
        public Commit GetCommit(Guid commitId)
        {
            return Hydrate(ModelHub.GetCommit(commitId));
        }

        /// <summary>
        /// Returns the head of an object's chain.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <returns>The head commit, or <c>null</c>.</returns>
        public Commit GetHead(Guid objectId)
        {
            return Hydrate(ModelHub.GetHeadCommit(objectId));
        }

        /// <summary>
        /// Returns the complete field state of an object at one revision by replaying its chain
        /// up to that commit.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="number">The 1-based revision number.</param>
        /// <returns>The replayed state, or <c>null</c> when the chain has no such revision.</returns>
        public ObjectState GetStateAt(Guid objectId, int number)
        {
            var chain = ModelHub.GetCommitChain(objectId);

            if (chain.Count == 0)
            {
                return null;
            }

            var target = chain.FirstOrDefault(x => x.Number == number);

            if (target is null)
            {
                return null;
            }

            var replayed = Replay(chain, number);

            return new ObjectState
            {
                ObjectId = objectId,
                ObjectKey = target.ObjectKey,
                CommitId = target.Id,
                Number = target.Number,
                Timestamp = target.Created,
                IsHead = target.Number == chain[^1].Number,
                Fields = [.. replayed.Values
                    .Select(x => new ObjectFieldState
                    {
                        Name = x.Name,
                        FieldId = x.FieldId,
                        Label = ResolveLabel(x.Name, x.FieldId),
                        Value = x.Value
                    })
                    .OrderBy(x => x.IsSystem ? 0 : 1)
                    .ThenBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)]
            };
        }

        /// <summary>
        /// Returns the aggregated field difference between two revisions of an object.
        /// </summary>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="from">The revision the comparison starts at.</param>
        /// <param name="to">The revision the comparison ends at.</param>
        /// <returns>The difference, or <c>null</c> when either revision does not exist.</returns>
        public CommitDiff DiffCommits(Guid objectId, int from, int to)
        {
            var source = GetStateAt(objectId, from);
            var target = GetStateAt(objectId, to);

            if (source is null || target is null)
            {
                return null;
            }

            var keys = source.Fields.Select(x => x.Key)
                .Union(target.Fields.Select(x => x.Key), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var changes = new List<Change>();
            var ordinal = 0;

            foreach (var key in keys)
            {
                var before = source.GetByKey(key);
                var after = target.GetByKey(key);

                if (string.Equals(before?.Value, after?.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                changes.Add(new Change
                {
                    Name = after?.Name ?? before?.Name,
                    FieldId = after?.FieldId ?? before?.FieldId,
                    OldValue = before?.Value,
                    NewValue = after?.Value,
                    Ordinal = ordinal++
                });
            }

            var diff = new CommitDiff
            {
                ObjectId = objectId,
                ObjectKey = target.ObjectKey,
                From = from,
                To = to,
                Changes = changes
            };

            CommitDiffed?.Invoke(this, diff);

            return diff;
        }

        /// <summary>
        /// Reapplies the field values an object held at one revision and appends the resulting
        /// <see cref="CommitType.Restored"/> commit.
        /// </summary>
        /// <remarks>
        /// The chain is never rewound. What the restore writes is derived from the difference
        /// between the requested revision and the head, so the new commit records exactly the
        /// fields it put back — and a later reader sees both the state that was reached and the
        /// fact that it was reached by restoring.
        /// </remarks>
        /// <param name="objectId">The id of the object.</param>
        /// <param name="number">The 1-based revision number whose state is reapplied.</param>
        /// <param name="identityId">The identity performing the restore.</param>
        /// <returns>The outcome, or <c>null</c> when the object or the revision does not exist.</returns>
        public CommitRestoreResult RestoreCommit(Guid objectId, int number, Guid identityId)
        {
            var @object = CoreHub.ObjectManager.GetObject(objectId);
            var head = ModelHub.GetHeadCommit(objectId);

            if (@object is null || head is null)
            {
                return null;
            }

            var target = GetStateAt(objectId, number);

            if (target is null)
            {
                return null;
            }

            if (number >= head.Number)
            {
                // restoring the head would write nothing; reporting that is more useful than
                // appending a commit that records no change
                return new CommitRestoreResult
                {
                    ObjectId = objectId,
                    ObjectKey = @object.Key,
                    RestoredNumber = number,
                    Commit = null
                };
            }

            var current = GetStateAt(objectId, head.Number);
            var message = string.Format(CultureInfo.InvariantCulture, "Restored {0}#{1}", @object.Key, number);

            using var scope = BeginCommit(objectId, CommitType.Restored, identityId, message);

            var keys = current.Fields.Select(x => x.Key)
                .Union(target.Fields.Select(x => x.Key), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var objectTouched = false;

            foreach (var key in keys)
            {
                var before = current.GetByKey(key);
                var after = target.GetByKey(key);

                if (string.Equals(before?.Value, after?.Value, StringComparison.Ordinal))
                {
                    continue;
                }

                var fieldId = after?.FieldId ?? before?.FieldId;

                if (fieldId.HasValue)
                {
                    RestoreValue(objectId, fieldId.Value, after?.Value);

                    continue;
                }

                objectTouched |= ObjectProperty.Write(@object, after?.Name ?? before?.Name, after?.Value);
            }

            if (objectTouched)
            {
                if (identityId != Guid.Empty)
                {
                    @object.UpdaterId = identityId;
                }

                @object.Updated = DateTime.UtcNow;

                CoreHub.ObjectManager.Update(@object);
            }

            scope.Dispose();

            var result = new CommitRestoreResult
            {
                ObjectId = objectId,
                ObjectKey = @object.Key,
                RestoredNumber = number,
                Commit = scope.Commit
            };

            if (result.Changed)
            {
                CommitRestored?.Invoke(this, result);
            }

            return result;
        }

        /// <summary>
        /// Stages a value write against the ambient scope of its object, or writes it together
        /// with a commit of its own when no scope is open. Called by the
        /// <see cref="IValueManager"/>, which owns no write path of its own.
        /// </summary>
        /// <param name="value">The value row to write.</param>
        /// <param name="previous">The payload the row held before, or <c>null</c>.</param>
        /// <param name="identityId">The identity performing the write, or <see cref="Guid.Empty"/>.</param>
        internal void StageWrite(Value value, string previous, Guid identityId)
        {
            if (value is null)
            {
                return;
            }

            if (string.Equals(previous, value.Data, StringComparison.Ordinal))
            {
                // a write that changes nothing is not a change; recording it would fill the
                // history with commits no user could account for
                return;
            }

            var change = BuildFieldChange(value.FieldId, previous, value.Data);
            var scope = Find(value.ObjectId);

            if (scope is not null)
            {
                scope.Record(change);
                scope.StageUpsert(value);

                return;
            }

            var commit = Build(value.ObjectId, CommitType.Updated, [change], identityId, null);

            if (commit is null)
            {
                return;
            }

            Persist(commit, [value], null);
        }

        /// <summary>
        /// Stages a value deletion against the ambient scope of its object, or performs it
        /// together with a commit of its own when no scope is open.
        /// </summary>
        /// <param name="value">The value row to delete.</param>
        /// <param name="identityId">The identity performing the deletion, or <see cref="Guid.Empty"/>.</param>
        internal void StageRemoval(Value value, Guid identityId)
        {
            if (value is null)
            {
                return;
            }

            var change = BuildFieldChange(value.FieldId, value.Data, null);
            var scope = Find(value.ObjectId);

            if (scope is not null)
            {
                scope.Record(change);
                scope.StageRemoval(value);

                return;
            }

            var commit = Build(value.ObjectId, CommitType.Updated, [change], identityId, null);

            if (commit is null)
            {
                return;
            }

            Persist(commit, null, [value.Id]);
        }

        /// <summary>
        /// Returns the value row staged for a field by the ambient scope, so a read inside a
        /// scope sees the write the scope has not flushed yet.
        /// </summary>
        /// <param name="objectId">The owning object id.</param>
        /// <param name="fieldId">The field id.</param>
        /// <param name="value">The staged row, or <c>null</c> when it is staged for deletion.</param>
        /// <returns><see langword="true"/> when the scope has something staged for the field.</returns>
        internal bool TryGetStagedValue(Guid objectId, Guid fieldId, out Value value)
        {
            value = null;

            return Find(objectId)?.TryGetStaged(fieldId, out value) == true;
        }

        /// <summary>
        /// Merges the writes staged by the ambient scope into a set of persisted value rows, so
        /// a caller reading an object's values inside a scope sees them as they will be.
        /// </summary>
        /// <param name="objectId">The owning object id.</param>
        /// <param name="persisted">The rows read from the store.</param>
        /// <returns>The merged rows.</returns>
        internal IEnumerable<Value> OverlayValues(Guid objectId, IEnumerable<Value> persisted)
        {
            var scope = Find(objectId);

            if (scope is null)
            {
                return persisted;
            }

            var merged = (persisted ?? []).ToDictionary(x => x.FieldId, x => x);

            foreach (var staged in scope.PendingUpserts)
            {
                merged[staged.FieldId] = staged;
            }

            foreach (var removed in scope.PendingRemovals)
            {
                merged.Remove(removed.FieldId);
            }

            return [.. merged.Values];
        }

        /// <summary>
        /// Closes a scope: writes the commit it collected together with the value rows it staged,
        /// and restores the scope that was ambient before it.
        /// </summary>
        /// <param name="scope">The scope being closed.</param>
        /// <param name="aborted">Whether the scope was aborted.</param>
        /// <returns>The appended commit, or <c>null</c> when nothing was written.</returns>
        internal Commit Close(CommitScope scope, bool aborted)
        {
            if (_ambient.Value == scope)
            {
                _ambient.Value = scope.Parent;
            }

            if (aborted)
            {
                return null;
            }

            var changes = scope.Changes.ToList();

            // an update that changed nothing is not a commit; a creation, deletion, archival or
            // restore is one even when it touched no field, because the action itself is the
            // record
            if (changes.Count == 0 && scope.Type == CommitType.Updated)
            {
                return null;
            }

            var commit = Build(scope.ObjectId, scope.Type, changes, scope.IdentityId, scope.Message);

            if (commit is null)
            {
                return null;
            }

            return Persist(commit, scope.PendingUpserts, scope.PendingRemovals.Select(x => x.Id));
        }

        /// <summary>
        /// Writes a commit and the value rows travelling with it, then hydrates and announces it.
        /// </summary>
        /// <param name="commit">The commit to append.</param>
        /// <param name="upserts">The value rows to write. May be <c>null</c>.</param>
        /// <param name="removals">The ids of the value rows to delete. May be <c>null</c>.</param>
        /// <returns>The appended commit.</returns>
        private Commit Persist(Commit commit, IEnumerable<Value> upserts, IEnumerable<Guid> removals)
        {
            ModelHub.AddCommit(commit, upserts, removals);

            Hydrate(commit);

            _httpServerContext?.Log?.Debug
            (
                message: string.Format
                (
                    CultureInfo.InvariantCulture,
                    "Commit {0} ({1}) appended by {2} with {3} change(s).",
                    commit.Reference,
                    commit.Type.Token(),
                    commit.CreatedByName ?? "system",
                    commit.Changes?.Count ?? 0
                )
            );

            CommitAdded?.Invoke(this, commit);

            return commit;
        }

        /// <summary>
        /// Builds an unsaved commit for an object, snapshotting the key and the author name so
        /// the entry stays readable after either row is gone.
        /// </summary>
        /// <param name="objectId">The id of the object being changed.</param>
        /// <param name="type">The action being recorded.</param>
        /// <param name="changes">The field modifications. May be <c>null</c>.</param>
        /// <param name="identityId">The identity performing the action.</param>
        /// <param name="message">The optional commit message.</param>
        /// <returns>The commit, or <c>null</c> when the object cannot be identified at all.</returns>
        private static Commit Build(Guid objectId, CommitType type, IEnumerable<Change> changes, Guid identityId, string message)
        {
            if (objectId == Guid.Empty)
            {
                return null;
            }

            // the chain records what happened, not what the writer is cleared to look at: a
            // commit on a classified object still has to name the object it belongs to
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            var @object = CoreHub.ObjectManager.GetObject(objectId);

            // a terminal commit is appended while the object row still exists; should it already
            // be gone, the chain itself still knows what the object was called
            var key = @object?.Key ?? ModelHub.GetHeadCommit(objectId)?.ObjectKey;

            var identity = identityId == Guid.Empty
                ? null
                : CoreHub.IdentityManager.GetIdentity(identityId);

            return new Commit
            {
                ObjectId = objectId,
                ObjectKey = key,
                Type = type,
                CreatedById = identityId == Guid.Empty ? null : identityId,
                CreatedByName = identity?.Name,
                Created = DateTime.UtcNow,
                Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
                Changes = [.. (changes ?? []).Where(x => x is not null)]
            };
        }

        /// <summary>
        /// Builds the change entry describing a class field's move from one payload to another.
        /// </summary>
        /// <param name="fieldId">The id of the field.</param>
        /// <param name="previous">The payload before.</param>
        /// <param name="current">The payload after.</param>
        /// <returns>The change.</returns>
        private static Change BuildFieldChange(Guid fieldId, string previous, string current)
        {
            var field = CoreHub.FieldManager.GetField(fieldId);

            return new Change
            {
                FieldId = fieldId,
                Name = field?.Name ?? fieldId.ToString(),
                OldValue = previous,
                NewValue = current
            };
        }

        /// <summary>
        /// Applies one field of a restored state: writes the recorded payload back, or deletes
        /// the row when the field held nothing at that revision.
        /// </summary>
        /// <param name="objectId">The owning object id.</param>
        /// <param name="fieldId">The field id.</param>
        /// <param name="value">The payload to restore, or <c>null</c> to clear the field.</param>
        private static void RestoreValue(Guid objectId, Guid fieldId, string value)
        {
            var existing = CoreHub.ValueManager.GetValue(objectId, fieldId);

            if (string.IsNullOrEmpty(value))
            {
                if (existing is not null)
                {
                    CoreHub.ValueManager.Remove(existing.Id);
                }

                return;
            }

            if (existing is null)
            {
                CoreHub.ValueManager.Add(new Value
                {
                    ObjectId = objectId,
                    FieldId = fieldId,
                    Data = value,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });

                return;
            }

            existing.Data = value;
            existing.Updated = DateTime.UtcNow;

            CoreHub.ValueManager.Update(existing);
        }

        /// <summary>
        /// Replays a chain up to a revision, producing the value of every attribute the object
        /// carried at that point. Attributes cleared along the way keep an entry with a
        /// <c>null</c> value, so a reader can tell "was emptied" from "never had one".
        /// </summary>
        /// <param name="chain">The chain, oldest first.</param>
        /// <param name="number">The revision to replay up to.</param>
        /// <returns>The attribute states, keyed by name.</returns>
        private static Dictionary<string, (string Name, Guid? FieldId, string Value)> Replay(IReadOnlyList<Commit> chain, int number)
        {
            var state = new Dictionary<string, (string Name, Guid? FieldId, string Value)>(StringComparer.OrdinalIgnoreCase);

            foreach (var commit in chain.Where(x => x.Number <= number))
            {
                foreach (var change in commit.Changes ?? [])
                {
                    if (string.IsNullOrWhiteSpace(change.Name))
                    {
                        continue;
                    }

                    state[Key(change)] = (change.Name, change.FieldId, change.NewValue);
                }
            }

            return state;
        }

        /// <summary>
        /// Returns the identity of the attribute a change modifies: the field id for a class
        /// field, and the prefixed name for a system property. Mirrors
        /// <see cref="ObjectFieldState.Key"/>.
        /// </summary>
        /// <remarks>
        /// Keying by name alone would let a class field named <c>Description</c> and the object's
        /// own <c>description</c> overwrite one another - the seeded classes model exactly that
        /// pair - and a replayed state would silently lose whichever came first.
        /// </remarks>
        /// <param name="change">The change.</param>
        /// <returns>The attribute key.</returns>
        internal static string Key(Change change)
        {
            return change.FieldId.HasValue
                ? change.FieldId.Value.ToString()
                : string.Concat("system:", (change.Name ?? string.Empty).ToLowerInvariant());
        }

        /// <summary>
        /// Returns the label an attribute is shown under: the name of its field definition when
        /// it still exists, and the recorded name otherwise.
        /// </summary>
        /// <param name="name">The recorded attribute name.</param>
        /// <param name="fieldId">The field id, or <c>null</c> for a system property.</param>
        /// <returns>The label.</returns>
        private static string ResolveLabel(string name, Guid? fieldId)
        {
            if (!fieldId.HasValue)
            {
                return name;
            }

            return CoreHub.FieldManager.GetField(fieldId.Value)?.Name ?? name;
        }

        /// <summary>
        /// Fills in the navigation the store does not carry: the object the commit belongs to,
        /// the identity that wrote it, and the field definition behind each change. Any of them
        /// may stay <c>null</c> when the referenced row has since been deleted, which is exactly
        /// what the snapshotted names are there for.
        /// </summary>
        /// <param name="commit">The commit to hydrate. May be <c>null</c>.</param>
        /// <returns>The same commit.</returns>
        private static Commit Hydrate(Commit commit)
        {
            if (commit is null)
            {
                return null;
            }

            // whether the reader may see the object was decided before the chain was reached;
            // hydrating it is plumbing and must not silently produce a headless commit
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            commit.Object = CoreHub.ObjectManager.GetObject(commit.ObjectId);
            commit.CreatedBy = commit.CreatedById.HasValue
                ? CoreHub.IdentityManager.GetIdentity(commit.CreatedById.Value)
                : null;

            foreach (var change in commit.Changes ?? [])
            {
                change.Field = change.FieldId.HasValue
                    ? CoreHub.FieldManager.GetField(change.FieldId.Value)
                    : null;
            }

            return commit;
        }

        /// <summary>
        /// Hydrates a sequence of commits, resolving each referenced object and identity once
        /// rather than once per commit.
        /// </summary>
        /// <param name="commits">The commits to hydrate.</param>
        /// <returns>The hydrated commits.</returns>
        private static IEnumerable<Commit> Hydrate(IEnumerable<Commit> commits)
        {
            var materialized = (commits ?? []).ToList();

            var objects = new Dictionary<Guid, ObjectEntity>();
            var identities = new Dictionary<Guid, Identity>();
            var fields = new Dictionary<Guid, Field>();

            // see the single-commit overload: resolving the object of a chain already reached
            // is plumbing rather than a read on the user's behalf
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            foreach (var commit in materialized)
            {
                if (!objects.TryGetValue(commit.ObjectId, out var @object))
                {
                    @object = CoreHub.ObjectManager.GetObject(commit.ObjectId);
                    objects[commit.ObjectId] = @object;
                }

                commit.Object = @object;

                if (commit.CreatedById.HasValue)
                {
                    if (!identities.TryGetValue(commit.CreatedById.Value, out var identity))
                    {
                        identity = CoreHub.IdentityManager.GetIdentity(commit.CreatedById.Value);
                        identities[commit.CreatedById.Value] = identity;
                    }

                    commit.CreatedBy = identity;
                }

                foreach (var change in commit.Changes ?? [])
                {
                    if (!change.FieldId.HasValue)
                    {
                        continue;
                    }

                    if (!fields.TryGetValue(change.FieldId.Value, out var field))
                    {
                        field = CoreHub.FieldManager.GetField(change.FieldId.Value);
                        fields[change.FieldId.Value] = field;
                    }

                    change.Field = field;
                }
            }

            return materialized;
        }

        /// <summary>
        /// Returns the open scope recording changes for an object, searching the ambient scope
        /// and the ones it is nested inside.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The scope, or <c>null</c> when none is open for that object.</returns>
        private CommitScope Find(Guid objectId)
        {
            for (var scope = _ambient.Value; scope is not null; scope = scope.Parent)
            {
                if (scope.ObjectId == objectId)
                {
                    return scope;
                }
            }

            return null;
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
