using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// The lifecycle a <see cref="WorkspaceTemplateClass"/> is created with: its states, the
    /// transitions between them, and the workflow that holds both.
    /// </summary>
    /// <remarks>
    /// States and transitions refer to each other by name, because nothing has an id before the
    /// workspace exists. The first state is where an object starts; the states marked
    /// <see cref="WorkspaceTemplateStatus.IsEnd"/> are where it ends. The workflow is bound to
    /// the class's <see cref="Model.Entities.FieldType.Workflow"/> field, which is what makes the
    /// lifecycle visible on an object at all - a class declaring a workflow and no such field
    /// has a workflow nobody can move through.
    /// </remarks>
    public sealed class WorkspaceTemplateWorkflow
    {
        /// <summary>
        /// Gets the name of the workflow.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets what the workflow describes - an internationalization key or plain text.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the states, in the order they are laid out on the designer canvas. The first
        /// is the start state.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateStatus> Statuses { get; init; } = [];

        /// <summary>
        /// Gets the transitions between the states.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateTransition> Transitions { get; init; } = [];
    }

    /// <summary>
    /// One state of a <see cref="WorkspaceTemplateWorkflow"/>.
    /// </summary>
    public sealed class WorkspaceTemplateStatus
    {
        /// <summary>
        /// The category of a state that is planned but not started.
        /// </summary>
        public const string ToDo = "ToDo";

        /// <summary>
        /// The category of a state that is being worked on.
        /// </summary>
        public const string InProgress = "InProgress";

        /// <summary>
        /// The category of a state that waits for somebody outside the team. An SLA clock is
        /// typically paused in it.
        /// </summary>
        public const string Waiting = "Waiting";

        /// <summary>
        /// The category of a state that closes the object - it settles an SLA and counts as
        /// complete in the progress rollup.
        /// </summary>
        public const string Done = "Done";

        /// <summary>
        /// Gets the name of the state - an internationalization key or plain text, resolved once
        /// when the workspace is created. A workflow field stores the resolved name.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets what the state means - an internationalization key or plain text.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the path of the icon the state is created with.
        /// </summary>
        public string Icon { get; init; }

        /// <summary>
        /// Gets the status category, one of <see cref="ToDo"/>, <see cref="InProgress"/>,
        /// <see cref="Waiting"/> and <see cref="Done"/>. The categories are the installation's,
        /// and the category is what the board columns, the SLA clock and the progress rollup
        /// read - not the name of the state.
        /// </summary>
        public string Category { get; init; } = ToDo;

        /// <summary>
        /// Gets whether the workflow ends in this state.
        /// </summary>
        public bool IsEnd { get; init; }
    }

    /// <summary>
    /// One transition of a <see cref="WorkspaceTemplateWorkflow"/>.
    /// </summary>
    public sealed class WorkspaceTemplateTransition
    {
        /// <summary>
        /// Gets the name of the transition, which is what the button moving an object reads.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets what the transition does - an internationalization key or plain text.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the name of the state the transition leaves.
        /// </summary>
        public string From { get; init; }

        /// <summary>
        /// Gets the name of the state the transition enters.
        /// </summary>
        public string To { get; init; }
    }
}
