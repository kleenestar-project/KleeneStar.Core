using KleeneStar.Core.WebWorkflow;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The registry behind one kind of transition rule: what the core ships, plus whatever the
    /// plugins of the installation added.
    /// </summary>
    /// <remarks>
    /// A workflow is administered rather than written, so the rules it may carry cannot be an
    /// enum: an installation that needs a condition nobody anticipated must be able to contribute
    /// one, and a workflow referencing it must keep working when it is uninstalled. The manager
    /// is therefore an open registry keyed by the string a transition stores, mirroring the way
    /// object kinds and renderers are extended, and the same three questions are asked of it: what
    /// exists, what does this key mean, and is it still installed.
    /// <para>
    /// A plugin registers from its own initialization, which happens after the core's components
    /// are built - the pickers therefore project the registry per render rather than once.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRule">The kind of rule the registry holds.</typeparam>
    public interface IWorkflowRuleManager<TRule> : IComponentManager
        where TRule : IWorkflowRule
    {
        /// <summary>
        /// Gets a number that changes whenever the set of registered rules does, so a cached
        /// picker can tell that it has to project the registry again.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Gets the registered rules, ordered for display.
        /// </summary>
        IEnumerable<TRule> Rules { get; }

        /// <summary>
        /// Registers a rule. Registering a key that is already present replaces it, so an add-on
        /// may override a rule the core ships.
        /// </summary>
        /// <param name="rule">The rule to register. Must carry a key.</param>
        void Register(TRule rule);

        /// <summary>
        /// Resolves a key to its rule.
        /// </summary>
        /// <param name="key">The key. May be null.</param>
        /// <returns>The rule, or <see langword="null"/> when no rule is registered under that
        /// key - which is what a workflow referencing an uninstalled plugin reads as.</returns>
        TRule Get(string key);
    }

    /// <summary>
    /// The registry of the conditions that decide whether a transition may be taken.
    /// </summary>
    public interface IWorkflowGuardManager : IWorkflowRuleManager<IWorkflowGuard>
    {
    }

    /// <summary>
    /// The registry of the conditions a transition validates the object against.
    /// </summary>
    public interface IWorkflowValidatorManager : IWorkflowRuleManager<IWorkflowValidator>
    {
    }

    /// <summary>
    /// The registry of the actions a transition performs once it has been applied.
    /// </summary>
    public interface IWorkflowPostFunctionManager : IWorkflowRuleManager<IWorkflowPostFunction>
    {
    }
}
