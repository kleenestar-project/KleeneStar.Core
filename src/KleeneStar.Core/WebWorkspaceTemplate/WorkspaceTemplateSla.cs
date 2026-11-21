using KleeneStar.Model.Entities;
using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// One service-level agreement a <see cref="WorkspaceTemplateClass"/> is created with.
    /// </summary>
    /// <remarks>
    /// It refers to the calendar it runs in and to the states it pauses in by name - both are
    /// declared beside it on the same class and have no id before the workspace exists.
    /// </remarks>
    public sealed class WorkspaceTemplateSla
    {
        /// <summary>
        /// Gets the name of the policy.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets what the policy promises - an internationalization key or plain text.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets how pressing the policy is.
        /// </summary>
        public SlaPriority Priority { get; init; } = SlaPriority.Medium;

        /// <summary>
        /// Gets the name of the calendar of the same class the clock runs in, or null for the
        /// class's default calendar.
        /// </summary>
        public string Calendar { get; init; }

        /// <summary>
        /// Gets the names of the states the clock is stopped in.
        /// </summary>
        public IReadOnlyList<string> PauseOn { get; init; } = [];

        /// <summary>
        /// Gets the channels a breach is announced through.
        /// </summary>
        public SlaNotificationChannels Notifications { get; init; } = SlaNotificationChannels.Email | SlaNotificationChannels.InApp;

        /// <summary>
        /// Gets the time targets of the policy.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateSlaTarget> Targets { get; init; } = [];

        /// <summary>
        /// Gets the rules saying which objects the policy covers.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateSlaScope> Scope { get; init; } = [];

        /// <summary>
        /// Gets the escalation levels, in the order they are reached.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateSlaEscalation> Escalations { get; init; } = [];
    }

    /// <summary>
    /// One time target of a <see cref="WorkspaceTemplateSla"/>.
    /// </summary>
    /// <param name="Name">What the target is called, e.g. <c>First response</c>.</param>
    /// <param name="Kind">What is measured.</param>
    /// <param name="Value">The budget.</param>
    /// <param name="Unit">The unit of the budget.</param>
    public sealed record WorkspaceTemplateSlaTarget(string Name, SlaTargetKind Kind, int Value, SlaTargetUnit Unit);

    /// <summary>
    /// One scope rule of a <see cref="WorkspaceTemplateSla"/>.
    /// </summary>
    /// <param name="Type">What the rule looks at.</param>
    /// <param name="Value">The value it matches.</param>
    public sealed record WorkspaceTemplateSlaScope(SlaScopeRuleType Type, string Value);

    /// <summary>
    /// One escalation level of a <see cref="WorkspaceTemplateSla"/>.
    /// </summary>
    /// <param name="AfterValue">How long after the breach threat the level is reached.</param>
    /// <param name="Unit">The unit of <paramref name="AfterValue"/>.</param>
    /// <param name="Notify">Who is told.</param>
    public sealed record WorkspaceTemplateSlaEscalation(int AfterValue, SlaTargetUnit Unit, string Notify);
}
