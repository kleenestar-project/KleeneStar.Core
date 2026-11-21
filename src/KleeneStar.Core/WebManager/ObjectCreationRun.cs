using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// One act of creating objects, made idempotent by its key and atomic by taking back what it
    /// wrote when it fails.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A creation writes through several managers, each of which persists on its own - the
    /// object row, its values, a genesis commit, the objects a composite template adds, the
    /// relations joining them - so there is no single database transaction to roll back. The
    /// run is the compensation instead: every object and relation is recorded in the
    /// <see cref="ObjectCreation"/> row <em>as it is written</em>, and a failure removes them
    /// again, relations first, objects in reverse order. Because the record is persisted after
    /// every step, a creation interrupted by a crash leaves a <c>Running</c> record naming what
    /// it wrote, and <see cref="RecoverAbandoned"/> takes that back on the next start.
    /// </para>
    /// <para>
    /// The key is the client's: the wizard submits one per dialog, a REST client names it in the
    /// <c>idempotencykey</c> query parameter. A second submission under a completed key is answered
    /// with the object of the first (<see cref="Claim"/> returns no run and the existing
    /// record), one under a running key is refused rather than run beside it, and a key whose
    /// run was rolled back may run again.
    /// </para>
    /// <para>
    /// The audit log stays append-only: a rolled-back creation is visible there as created and
    /// removed, and the record keeps the error, so a failure is traceable after the fact.
    /// </para>
    /// </remarks>
    public sealed class ObjectCreationRun
    {
        /// <summary>
        /// How long a running record may stand before a start treats it as abandoned.
        /// </summary>
        public static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(15);

        private readonly ObjectCreation _record;
        private readonly List<Guid> _objects = [];
        private readonly List<Guid> _relations = [];

        /// <summary>
        /// Initializes a new instance of the class over a claimed record.
        /// </summary>
        /// <param name="record">The record the run writes its progress to.</param>
        private ObjectCreationRun(ObjectCreation record)
        {
            _record = record;
        }

        /// <summary>
        /// Gets the key the run was claimed under.
        /// </summary>
        public string Key => _record.Key;

        /// <summary>
        /// Gets the objects written so far, in the order they were written.
        /// </summary>
        public IReadOnlyList<Guid> Objects => _objects;

        /// <summary>
        /// Claims a key for a creation about to run.
        /// </summary>
        /// <param name="key">The idempotency key; a blank one runs under a fresh key, which makes
        /// the creation atomic without making it idempotent.</param>
        /// <param name="templateId">The template the creation starts from, or <c>null</c>.</param>
        /// <param name="identityId">The identity creating.</param>
        /// <param name="existing">When no run is returned, the record that holds the key.</param>
        /// <returns>The run, or <see langword="null"/> when the key is held by a running or
        /// completed creation.</returns>
        public static ObjectCreationRun Claim(string key, Guid? templateId, Guid identityId, out ObjectCreation existing)
        {
            var record = new ObjectCreation
            {
                Id = Guid.NewGuid(),
                Key = Normalize(key) ?? Guid.NewGuid().ToString("N"),
                TemplateId = templateId,
                State = ObjectCreationState.Running,
                IdentityId = identityId == Guid.Empty ? null : identityId,
                Created = DateTime.UtcNow
            };

            return ModelHub.TryClaimObjectCreation(record, out existing)
                ? new ObjectCreationRun(record)
                : null;
        }

        /// <summary>
        /// Records an object the run wrote.
        /// </summary>
        /// <param name="objectId">The object.</param>
        public void TrackObject(Guid objectId)
        {
            _objects.Add(objectId);
            Persist();
        }

        /// <summary>
        /// Records a relation the run wrote.
        /// </summary>
        /// <param name="relationId">The relation.</param>
        public void TrackRelation(Guid relationId)
        {
            _relations.Add(relationId);
            Persist();
        }

        /// <summary>
        /// Marks the run completed with the object it answers with.
        /// </summary>
        /// <param name="rootObjectId">The object the caller asked for.</param>
        public void Complete(Guid rootObjectId)
        {
            _record.RootObjectId = rootObjectId;
            _record.State = ObjectCreationState.Completed;
            _record.Finished = DateTime.UtcNow;
            Persist();
        }

        /// <summary>
        /// Takes back what the run wrote and records why.
        /// </summary>
        /// <param name="error">The failure, recorded for whoever investigates it.</param>
        public void RollBack(Exception error)
        {
            Compensate(_relations, _objects);

            _record.State = ObjectCreationState.RolledBack;
            _record.Error = error?.GetType().Name + ": " + error?.Message;
            _record.Finished = DateTime.UtcNow;
            Persist();
        }

        /// <summary>
        /// Takes back the creations a crash interrupted: running records older than
        /// <see cref="AbandonedAfter"/>.
        /// </summary>
        /// <remarks>
        /// Called once at start-up, before anybody can submit again. A record is abandoned
        /// because nothing that runs a creation lives across a restart.
        /// </remarks>
        /// <returns>The number of creations rolled back.</returns>
        public static int RecoverAbandoned()
        {
            var threshold = DateTime.UtcNow - AbandonedAfter;
            var abandoned = ModelHub.GetObjectCreations(ObjectCreationState.Running)
                .Where(x => x.Created < threshold)
                .ToList();

            foreach (var record in abandoned)
            {
                Compensate(Parse(record.RelationIds), Parse(record.ObjectIds));

                record.State = ObjectCreationState.RolledBack;
                record.Error ??= "Abandoned: the creation was interrupted and has been taken back.";
                record.Finished = DateTime.UtcNow;
                ModelHub.UpdateObjectCreation(record);
            }

            return abandoned.Count;
        }

        /// <summary>
        /// Removes relations and objects a failed creation wrote.
        /// </summary>
        /// <remarks>
        /// The removal is the system's act, not the caller's: it reads without narrowing, so an
        /// object the caller could create but not read - one in another department's workspace -
        /// is taken back as well. A step that fails is skipped rather than ending the
        /// compensation, so one stuck row does not keep the others in place.
        /// </remarks>
        /// <param name="relations">The relations to remove.</param>
        /// <param name="objects">The objects to remove, in the order they were written.</param>
        private static void Compensate(IEnumerable<Guid> relations, IEnumerable<Guid> objects)
        {
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            foreach (var relationId in relations)
            {
                try
                {
                    if (CoreHub.ObjectRelationManager.GetRelation(relationId) is { } relation)
                    {
                        CoreHub.ObjectRelationManager.Remove(relation);
                    }
                }
                catch (Exception ex)
                {
                    CoreHub.HttpServerContext?.Log?.Exception(ex);
                }
            }

            foreach (var objectId in objects.Reverse())
            {
                try
                {
                    if (CoreHub.ObjectManager.GetObject(objectId) is not null)
                    {
                        CoreHub.ObjectManager.Remove(objectId);
                    }
                }
                catch (Exception ex)
                {
                    CoreHub.HttpServerContext?.Log?.Exception(ex);
                }
            }
        }

        /// <summary>
        /// Writes the run's progress to its record.
        /// </summary>
        private void Persist()
        {
            _record.ObjectIds = string.Join(",", _objects);
            _record.RelationIds = string.Join(",", _relations);
            ModelHub.UpdateObjectCreation(_record);
        }

        /// <summary>
        /// Reads a stored id list.
        /// </summary>
        /// <param name="value">The comma-separated ids.</param>
        /// <returns>The ids.</returns>
        private static List<Guid> Parse(string value)
        {
            return [.. (value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)];
        }

        /// <summary>
        /// Normalizes a client's key: trimmed, at most 64 characters, blank as none.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The key, or <see langword="null"/>.</returns>
        private static string Normalize(string key)
        {
            var trimmed = key?.Trim();

            return string.IsNullOrEmpty(trimmed)
                ? null
                : trimmed.Length > 64 ? trimmed[..64] : trimmed;
        }
    }
}
