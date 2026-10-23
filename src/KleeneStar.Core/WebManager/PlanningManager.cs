using KleeneStar.Core.WebPlanning;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Personal planning.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Time tracking is off until a workspace switches it on. A worklog reduces the object's
    /// remaining estimate by the time logged unless the person says what is left; the original
    /// estimate stays what was planned, so planned, spent and left can be compared afterwards.
    /// </para>
    /// <para>
    /// Loads and milestone progress read the objects as the caller may see them: an object
    /// hidden by grants or clearance is counted (a milestone says how much it holds) but never
    /// named. An object counts as done when its workflow state stands for done - the reading
    /// the plan views and the progress bars use.
    /// </para>
    /// </remarks>
    public sealed class PlanningManager : IPlanningManager
    {
        /// <summary>
        /// The capacity of a person who set none: a working week.
        /// </summary>
        public const int DefaultWeeklyCapacity = Durations.Week;

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <inheritdoc/>
        public event EventHandler<Worklog> WorklogChanged;

        /// <inheritdoc/>
        public event EventHandler<Milestone> MilestoneAdded;

        /// <inheritdoc/>
        public event EventHandler<Milestone> MilestoneUpdated;

        /// <inheritdoc/>
        public event EventHandler<Milestone> MilestoneRemoved;

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private PlanningManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public bool IsTimeTracking(Guid workspaceId)
        {
            return ModelHub.GetTimeTracking(workspaceId);
        }

        /// <inheritdoc/>
        public void SetTimeTracking(Guid workspaceId, bool enabled)
        {
            ModelHub.SetTimeTracking(workspaceId, enabled);
        }

        /// <inheritdoc/>
        public int GetWeeklyCapacity(Guid identityId)
        {
            return ModelHub.GetWeeklyCapacity(identityId) ?? DefaultWeeklyCapacity;
        }

        /// <inheritdoc/>
        public void SetWeeklyCapacity(Guid identityId, int minutes)
        {
            if (minutes < 0 || minutes > 7 * 24 * 60)
            {
                throw new PlanningRefusedException("A weekly capacity lies between 0 and 168 hours.");
            }

            ModelHub.SetWeeklyCapacity(identityId, minutes);
        }

        /// <inheritdoc/>
        public Worklog Log(Guid objectId, Guid identityId, DateTime started, int minutes, string comment, int? remaining = null)
        {
            var @object = ReadObject(objectId) ?? throw new PlanningRefusedException("The object does not exist.");

            if (!ModelHub.GetTimeTracking(@object.WorkspaceId))
            {
                throw new PlanningRefusedException("Time tracking is off in this workspace.");
            }

            Check(identityId, started, minutes);

            if (remaining is < 0)
            {
                throw new PlanningRefusedException("The remaining estimate cannot be negative.");
            }

            var worklog = new Worklog
            {
                Id = Guid.NewGuid(),
                ObjectId = objectId,
                IdentityId = identityId,
                Started = started.ToUniversalTime(),
                Minutes = minutes,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            ModelHub.SaveWorklog(worklog);

            // what is left goes down by what was spent, unless the person knows better
            ModelHub.UpdateObjectPlanning(objectId, row =>
            {
                row.RemainingEstimateMinutes = remaining
                    ?? (row.RemainingEstimateMinutes is int left ? Math.Max(0, left - minutes) : null);
            });

            WorklogChanged?.Invoke(this, worklog);

            return worklog;
        }

        /// <inheritdoc/>
        public Worklog UpdateWorklog(Guid worklogId, Guid identityId, bool administrator, DateTime started, int minutes, string comment)
        {
            var worklog = Owned(worklogId, identityId, administrator);

            Check(worklog.IdentityId, started, minutes);

            worklog.Started = started.ToUniversalTime();
            worklog.Minutes = minutes;
            worklog.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

            ModelHub.SaveWorklog(worklog);
            WorklogChanged?.Invoke(this, worklog);

            return worklog;
        }

        /// <inheritdoc/>
        public void RemoveWorklog(Guid worklogId, Guid identityId, bool administrator)
        {
            var worklog = Owned(worklogId, identityId, administrator);

            ModelHub.SaveWorklog(worklog, remove: true);
            WorklogChanged?.Invoke(this, worklog);
        }

        /// <inheritdoc/>
        public IReadOnlyList<Worklog> GetWorklogs(Guid objectId)
        {
            return ModelHub.GetWorklogs(objectId);
        }

        /// <inheritdoc/>
        public Worklog GetWorklog(Guid worklogId)
        {
            return ModelHub.GetWorklog(worklogId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<Worklog> GetWorklogs(Guid identityId, DateTime from, DateTime to)
        {
            return ModelHub.GetWorklogs(identityId, from, to);
        }

        /// <inheritdoc/>
        public ObjectEstimate GetEstimate(Guid objectId)
        {
            var row = ModelHub.GetObjectPlannings([objectId]).GetValueOrDefault(objectId);
            var logged = ModelHub.GetLoggedMinutes([objectId]).GetValueOrDefault(objectId);

            return new ObjectEstimate(row?.OriginalEstimateMinutes, row?.RemainingEstimateMinutes, logged, row?.MilestoneId);
        }

        /// <inheritdoc/>
        public void SetEstimate(Guid objectId, int? original, int? remaining)
        {
            if (original is < 0 || remaining is < 0)
            {
                throw new PlanningRefusedException("An estimate cannot be negative.");
            }

            ModelHub.UpdateObjectPlanning(objectId, row =>
            {
                row.OriginalEstimateMinutes = original;

                // a first estimate is also what is left
                row.RemainingEstimateMinutes = remaining ?? (row.RemainingEstimateMinutes is null ? original : row.RemainingEstimateMinutes);
            });
        }

        /// <inheritdoc/>
        public PersonalLoad GetPersonalLoad(Guid identityId, DateTime now)
        {
            var judge = new DoneJudge();
            var open = CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().Where(x => x.AssigneeId == identityId))
                .Where(x => x.State == WorkspaceState.Active && !judge.IsDone(x))
                .ToList();

            return Load(identityId, open, now);
        }

        /// <inheritdoc/>
        public IReadOnlyList<PersonalLoad> GetTeamLoad(Guid workspaceId, DateTime now)
        {
            var judge = new DoneJudge();

            return [.. CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, workspaceId))
                .Where(x => x.AssigneeId is not null && x.State == WorkspaceState.Active && !judge.IsDone(x))
                .GroupBy(x => x.AssigneeId.Value)
                .Select(x => Load(x.Key, [.. x], now))
                .OrderByDescending(x => x.Weeks)];
        }

        /// <inheritdoc/>
        public IReadOnlyList<Milestone> GetMilestones(Guid workspaceId)
        {
            return ModelHub.GetMilestones(workspaceId);
        }

        /// <inheritdoc/>
        public Milestone GetMilestone(Guid milestoneId)
        {
            return ModelHub.GetMilestone(milestoneId);
        }

        /// <inheritdoc/>
        public IPlanningManager SaveMilestone(Milestone milestone)
        {
            if (string.IsNullOrWhiteSpace(milestone?.Name))
            {
                throw new PlanningRefusedException("The milestone needs a name.");
            }

            milestone.Name = milestone.Name.Trim();

            if (ModelHub.GetMilestones(milestone.WorkspaceId).Any(x => x.Id != milestone.Id && string.Equals(x.Name, milestone.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new PlanningRefusedException($"The workspace already has a milestone named '{milestone.Name}'.");
            }

            var existing = milestone.Id != Guid.Empty && ModelHub.GetMilestone(milestone.Id) is not null;

            milestone.Id = milestone.Id == Guid.Empty ? Guid.NewGuid() : milestone.Id;
            milestone.Created = milestone.Created == default ? DateTime.UtcNow : milestone.Created;
            milestone.Updated = DateTime.UtcNow;

            ModelHub.SaveMilestone(milestone);
            (existing ? MilestoneUpdated : MilestoneAdded)?.Invoke(this, milestone);

            return this;
        }

        /// <inheritdoc/>
        public IPlanningManager RemoveMilestone(Guid milestoneId)
        {
            var milestone = ModelHub.GetMilestone(milestoneId);

            if (milestone is not null)
            {
                ModelHub.SaveMilestone(milestone, remove: true);
                MilestoneRemoved?.Invoke(this, milestone);
            }

            return this;
        }

        /// <inheritdoc/>
        public void Assign(Guid objectId, Guid? milestoneId)
        {
            var @object = ReadObject(objectId) ?? throw new PlanningRefusedException("The object does not exist.");

            if (milestoneId is Guid id && ModelHub.GetMilestone(id)?.WorkspaceId != @object.WorkspaceId)
            {
                throw new PlanningRefusedException("The milestone belongs to another workspace.");
            }

            ModelHub.UpdateObjectPlanning(objectId, row => row.MilestoneId = milestoneId);
        }

        /// <inheritdoc/>
        public MilestoneProgress GetProgress(Guid milestoneId)
        {
            var ids = ModelHub.GetMilestoneObjects(milestoneId);
            var visible = ids.Select(CoreHub.ObjectManager.GetObject).Where(x => x is not null).ToList();
            var plannings = ModelHub.GetObjectPlannings(visible.Select(x => x.Id));
            var logged = ModelHub.GetLoggedMinutes(visible.Select(x => x.Id));
            var judge = new DoneJudge();
            var objects = visible.Select(x => (Object: x, Done: judge.IsDone(x))).ToList();

            return new MilestoneProgress
            (
                objects.Count,
                objects.Count(x => x.Done),
                ids.Count - visible.Count,
                objects.Where(x => !x.Done).Sum(x => plannings.GetValueOrDefault(x.Object.Id)?.RemainingEstimateMinutes ?? 0),
                visible.Sum(x => logged.GetValueOrDefault(x.Id)),
                objects
            );
        }

        /// <summary>
        /// Determines whether an object is done.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns><see langword="true"/> when its state stands for done.</returns>
        internal static bool IsDone(ObjectEntity @object)
        {
            return ObjectProgressManager.OwnProgress(@object) >= 100;
        }

        /// <summary>
        /// Builds the load of a person from their open objects.
        /// </summary>
        /// <param name="identityId">The person.</param>
        /// <param name="open">The open objects.</param>
        /// <param name="now">The moment the week is counted from.</param>
        /// <returns>The load.</returns>
        private PersonalLoad Load(Guid identityId, List<ObjectEntity> open, DateTime now)
        {
            var plannings = ModelHub.GetObjectPlannings(open.Select(x => x.Id));
            var week = StartOfWeek(now);

            return new PersonalLoad
            (
                identityId,
                open,
                open.Sum(x => plannings.GetValueOrDefault(x.Id)?.RemainingEstimateMinutes ?? 0),
                open.Count(x => plannings.GetValueOrDefault(x.Id)?.RemainingEstimateMinutes is null),
                ModelHub.GetWorklogs(identityId, week, week.AddDays(7)).Sum(x => x.Minutes),
                GetWeeklyCapacity(identityId)
            );
        }

        /// <summary>
        /// Returns the Monday a week begins on.
        /// </summary>
        /// <param name="now">A moment of the week.</param>
        /// <returns>Monday, midnight UTC.</returns>
        internal static DateTime StartOfWeek(DateTime now)
        {
            var date = now.ToUniversalTime().Date;

            return DateTime.SpecifyKind(date.AddDays(-(((int)date.DayOfWeek + 6) % 7)), DateTimeKind.Utc);
        }

        /// <summary>
        /// Checks the figures of a worklog.
        /// </summary>
        /// <param name="identityId">Who worked.</param>
        /// <param name="started">When.</param>
        /// <param name="minutes">How long.</param>
        private static void Check(Guid identityId, DateTime started, int minutes)
        {
            if (identityId == Guid.Empty)
            {
                throw new PlanningRefusedException("Work is logged by a signed-in person.", forbidden: true);
            }

            if (minutes < 1 || minutes > 24 * 60)
            {
                throw new PlanningRefusedException("A worklog lasts between a minute and a day.");
            }

            if (started.ToUniversalTime() > DateTime.UtcNow.AddHours(1))
            {
                throw new PlanningRefusedException("Work cannot be logged ahead of time.");
            }
        }

        /// <summary>
        /// Returns a worklog its caller may change.
        /// </summary>
        /// <param name="worklogId">The worklog.</param>
        /// <param name="identityId">The caller.</param>
        /// <param name="administrator">Whether the caller administers the workspace.</param>
        /// <returns>The worklog.</returns>
        private static Worklog Owned(Guid worklogId, Guid identityId, bool administrator)
        {
            var worklog = ModelHub.GetWorklog(worklogId) ?? throw new PlanningRefusedException("The worklog does not exist.");

            if (!administrator && (identityId == Guid.Empty || worklog.IdentityId != identityId))
            {
                throw new PlanningRefusedException("Only its author or an administrator changes a worklog.", forbidden: true);
            }

            return worklog;
        }

        /// <summary>
        /// Reads an object whatever its classification.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static ObjectEntity ReadObject(Guid objectId)
        {
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            return CoreHub.ObjectManager.GetObject(objectId);
        }

        /// <summary>
        /// Releases the resources of the manager.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
