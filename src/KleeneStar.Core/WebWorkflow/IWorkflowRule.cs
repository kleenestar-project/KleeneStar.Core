using WebExpress.WebCore.WebIcon;

namespace KleeneStar.Core.WebWorkflow
{
    /// <summary>
    /// What every rule of a transition has in common: a key it is administered under and the
    /// three things a person picking it needs to read.
    /// </summary>
    /// <remarks>
    /// The key is what the workflow stores - never the type name, which would tie an
    /// administered workflow to an assembly - and it is what a plugin has to keep stable once
    /// transitions carry it. The label and the description are internationalization keys, so the
    /// catalog reads in the language of the request.
    /// </remarks>
    public interface IWorkflowRule
    {
        /// <summary>
        /// Gets the stable key the rule is stored and administered under, for example
        /// <c>assignee.set</c>. Keys are compared case-insensitively.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the internationalization key of the one-line description - what the rule asks or
        /// does, in the words of somebody administering a workflow rather than of somebody
        /// reading the code.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the icon representing the rule in the pickers.
        /// </summary>
        IIcon Icon => null;

        /// <summary>
        /// Gets the display order within listings. Lower values are listed first.
        /// </summary>
        int Order => 100;
    }

    /// <summary>
    /// A condition that decides whether a transition may be taken at all.
    /// </summary>
    /// <remarks>
    /// A guard answers before the move is offered: a state a guard refuses is not shown as a next
    /// step, and asking for it anyway is refused with <c>NotAllowed</c>. It therefore says
    /// nothing about <em>why</em> - a move that is not offered needs no explanation, and one that
    /// does needs a validator instead.
    /// </remarks>
    public interface IWorkflowGuard : IWorkflowRule
    {
        /// <summary>
        /// Determines whether the move may be taken.
        /// </summary>
        /// <param name="context">What is known about the move.</param>
        /// <returns><see langword="true"/> when this guard holds.</returns>
        bool Evaluate(WorkflowRuleContext context);
    }

    /// <summary>
    /// A condition the object has to satisfy for a transition to be accepted.
    /// </summary>
    /// <remarks>
    /// A validator answers when the move is actually made, with the screen already filled in, and
    /// its failure is reported to the person making it - which is why it carries a
    /// <see cref="Message"/>. "You may not do this" is a guard; "you may, but not like this" is a
    /// validator.
    /// </remarks>
    public interface IWorkflowValidator : IWorkflowRule
    {
        /// <summary>
        /// Gets the internationalization key of the sentence shown when the validator refuses -
        /// what has to be true, said as an instruction rather than as a fault.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Determines whether the object satisfies the validator.
        /// </summary>
        /// <param name="context">What is known about the move, including what the screen carries.</param>
        /// <returns><see langword="true"/> when this validator is satisfied.</returns>
        bool Evaluate(WorkflowRuleContext context);
    }

    /// <summary>
    /// An action a transition performs once it has been applied.
    /// </summary>
    /// <remarks>
    /// A post function runs inside the commit of the transition, so what it changes is part of
    /// the same revision as the state change and the history reads as one action. A post function
    /// that throws does not undo the move: the state change was legitimate, and a failing
    /// follow-up is reported rather than allowed to hold the workflow hostage.
    /// </remarks>
    public interface IWorkflowPostFunction : IWorkflowRule
    {
        /// <summary>
        /// Performs the action.
        /// </summary>
        /// <param name="context">What is known about the move that just completed.</param>
        void Execute(WorkflowRuleContext context);
    }
}
