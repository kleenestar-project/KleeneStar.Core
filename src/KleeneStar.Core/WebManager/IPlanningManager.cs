using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Personal planning: time tracking with estimates, capacities and loads, and milestones.
    /// </summary>
    public interface IPlanningManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a worklog is added, changed or removed.
        /// </summary>
        event EventHandler<Worklog> WorklogChanged;

        /// <summary>
        /// An event that fires when a milestone is added.
        /// </summary>
        event EventHandler<Milestone> MilestoneAdded;

        /// <summary>
        /// An event that fires when a milestone is changed.
        /// </summary>
        event EventHandler<Milestone> MilestoneUpdated;

        /// <summary>
        /// An event that fires when a milestone is removed.
        /// </summary>
        event EventHandler<Milestone> MilestoneRemoved;

        /// <summary>
        /// Determines whether work is logged on a workspace's objects.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns><see langword="true"/> when time tracking is on.</returns>
        bool IsTimeTracking(Guid workspaceId);

        /// <summary>
        /// Switches time tracking of a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="enabled">Whether work is logged.</param>
        void SetTimeTracking(Guid workspaceId, bool enabled);

        /// <summary>
        /// Returns the weekly capacity of a person: what they set, else a working week.
        /// </summary>
        /// <param name="identityId">The person.</param>
        /// <returns>The minutes per week.</returns>
        int GetWeeklyCapacity(Guid identityId);

        /// <summary>
        /// Sets the weekly capacity of a person.
        /// </summary>
        /// <param name="identityId">The person.</param>
        /// <param name="minutes">The minutes per week, 0 to 168 hours.</param>
        /// <exception cref="PlanningRefusedException">The capacity is out of range.</exception>
        void SetWeeklyCapacity(Guid identityId, int minutes);

        /// <summary>
        /// Logs work on an object.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <param name="identityId">Who worked.</param>
        /// <param name="started">When the work began.</param>
        /// <param name="minutes">How long it took.</param>
        /// <param name="comment">What was done, or <see langword="null"/>.</param>
        /// <param name="remaining">The work left afterwards; <see langword="null"/> reduces the
        /// remaining estimate by the time logged.</param>
        /// <returns>The worklog.</returns>
        /// <exception cref="PlanningRefusedException">Time tracking is off, or the entry is not valid.</exception>
        Worklog Log(Guid objectId, Guid identityId, DateTime started, int minutes, string comment, int? remaining = null);

        /// <summary>
        /// Changes a worklog.
        /// </summary>
        /// <param name="worklogId">The worklog.</param>
        /// <param name="identityId">Who changes it.</param>
        /// <param name="administrator">Whether that one administers the workspace; otherwise only the author may.</param>
        /// <param name="started">When the work began.</param>
        /// <param name="minutes">How long it took.</param>
        /// <param name="comment">What was done.</param>
        /// <returns>The worklog.</returns>
        /// <exception cref="PlanningRefusedException">The change is not possible.</exception>
        Worklog UpdateWorklog(Guid worklogId, Guid identityId, bool administrator, DateTime started, int minutes, string comment);

        /// <summary>
        /// Removes a worklog.
        /// </summary>
        /// <param name="worklogId">The worklog.</param>
        /// <param name="identityId">Who removes it.</param>
        /// <param name="administrator">Whether that one administers the workspace; otherwise only the author may.</param>
        /// <exception cref="PlanningRefusedException">The removal is not possible.</exception>
        void RemoveWorklog(Guid worklogId, Guid identityId, bool administrator);

        /// <summary>
        /// Returns the worklogs of an object.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The worklogs, the newest first.</returns>
        IReadOnlyList<Worklog> GetWorklogs(Guid objectId);

        /// <summary>
        /// Returns a worklog.
        /// </summary>
        /// <param name="worklogId">The worklog.</param>
        /// <returns>The worklog, or <see langword="null"/>.</returns>
        Worklog GetWorklog(Guid worklogId);

        /// <summary>
        /// Returns the worklogs of a person in a period.
        /// </summary>
        /// <param name="identityId">The person.</param>
        /// <param name="from">The start, inclusive.</param>
        /// <param name="to">The end, exclusive.</param>
        /// <returns>The worklogs.</returns>
        IReadOnlyList<Worklog> GetWorklogs(Guid identityId, DateTime from, DateTime to);

        /// <summary>
        /// Returns the estimates and the time logged of an object.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <returns>The figures.</returns>
        ObjectEstimate GetEstimate(Guid objectId);

        /// <summary>
        /// Sets the estimates of an object.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <param name="original">The original estimate in minutes, or <see langword="null"/>.</param>
        /// <param name="remaining">The remaining estimate in minutes, or <see langword="null"/>.</param>
        /// <exception cref="PlanningRefusedException">An estimate is negative.</exception>
        void SetEstimate(Guid objectId, int? original, int? remaining);

        /// <summary>
        /// Returns what a person has on their plate, as they may see it.
        /// </summary>
        /// <param name="identityId">The person.</param>
        /// <param name="now">The moment the week is counted from.</param>
        /// <returns>The load.</returns>
        PersonalLoad GetPersonalLoad(Guid identityId, DateTime now);

        /// <summary>
        /// Returns the load of everyone with open work in a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="now">The moment the week is counted from.</param>
        /// <returns>One load per assignee.</returns>
        IReadOnlyList<PersonalLoad> GetTeamLoad(Guid workspaceId, DateTime now);

        /// <summary>
        /// Returns the milestones of a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The milestones, the earliest due first.</returns>
        IReadOnlyList<Milestone> GetMilestones(Guid workspaceId);

        /// <summary>
        /// Returns a milestone.
        /// </summary>
        /// <param name="milestoneId">The milestone.</param>
        /// <returns>The milestone, or <see langword="null"/>.</returns>
        Milestone GetMilestone(Guid milestoneId);

        /// <summary>
        /// Adds or changes a milestone.
        /// </summary>
        /// <param name="milestone">The milestone.</param>
        /// <returns>The manager, for chaining.</returns>
        /// <exception cref="PlanningRefusedException">The milestone is not valid.</exception>
        IPlanningManager SaveMilestone(Milestone milestone);

        /// <summary>
        /// Removes a milestone; its objects stay, without a milestone.
        /// </summary>
        /// <param name="milestoneId">The milestone.</param>
        /// <returns>The manager, for chaining.</returns>
        IPlanningManager RemoveMilestone(Guid milestoneId);

        /// <summary>
        /// Puts an object on a milestone of its workspace, or takes it off.
        /// </summary>
        /// <param name="objectId">The object.</param>
        /// <param name="milestoneId">The milestone, or <see langword="null"/>.</param>
        /// <exception cref="PlanningRefusedException">The milestone is of another workspace.</exception>
        void Assign(Guid objectId, Guid? milestoneId);

        /// <summary>
        /// Returns how far a milestone is, counting the objects the caller may see and the others
        /// anonymously.
        /// </summary>
        /// <param name="milestoneId">The milestone.</param>
        /// <returns>The progress.</returns>
        MilestoneProgress GetProgress(Guid milestoneId);
    }

    /// <summary>
    /// The estimates and the time logged of an object.
    /// </summary>
    /// <param name="Original">The original estimate in minutes, or <see langword="null"/>.</param>
    /// <param name="Remaining">The remaining estimate in minutes, or <see langword="null"/>.</param>
    /// <param name="Logged">The minutes logged.</param>
    /// <param name="MilestoneId">The milestone, or <see langword="null"/>.</param>
    public sealed record ObjectEstimate(int? Original, int? Remaining, int Logged, Guid? MilestoneId);

    /// <summary>
    /// What a person has on their plate.
    /// </summary>
    /// <param name="IdentityId">The person.</param>
    /// <param name="Open">The open objects assigned to them.</param>
    /// <param name="RemainingMinutes">The remaining estimates of those objects.</param>
    /// <param name="Unestimated">How many of them carry no remaining estimate.</param>
    /// <param name="LoggedThisWeek">The minutes they logged this week.</param>
    /// <param name="WeeklyCapacity">Their capacity per week.</param>
    public sealed record PersonalLoad(Guid IdentityId, IReadOnlyList<ObjectEntity> Open, int RemainingMinutes, int Unestimated, int LoggedThisWeek, int WeeklyCapacity)
    {
        /// <summary>
        /// Gets the weeks the remaining work takes at the person's capacity.
        /// </summary>
        public double Weeks => WeeklyCapacity <= 0 ? 0 : (double)RemainingMinutes / WeeklyCapacity;
    }

    /// <summary>
    /// How far a milestone is.
    /// </summary>
    /// <param name="Total">The objects due with it the caller may see.</param>
    /// <param name="Done">How many of them are done.</param>
    /// <param name="Hidden">The objects due with it the caller may not see, counted without naming them.</param>
    /// <param name="RemainingMinutes">The remaining estimates of the open ones.</param>
    /// <param name="LoggedMinutes">The time logged on them.</param>
    /// <param name="Objects">The visible objects, each with whether it is done.</param>
    public sealed record MilestoneProgress(int Total, int Done, int Hidden, int RemainingMinutes, int LoggedMinutes, IReadOnlyList<(ObjectEntity Object, bool Done)> Objects)
    {
        /// <summary>
        /// Gets the percentage done.
        /// </summary>
        public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Done / Total);
    }

    /// <summary>
    /// A planning action that is not possible, with the reason the caller is meant to read.
    /// </summary>
    /// <param name="message">The reason.</param>
    /// <param name="forbidden">Whether the caller lacks the right rather than the request being wrong.</param>
    public sealed class PlanningRefusedException(string message, bool forbidden = false) : InvalidOperationException(message)
    {
        /// <summary>
        /// Gets whether the caller lacks the right rather than the request being wrong.
        /// </summary>
        public bool Forbidden { get; } = forbidden;
    }
}
