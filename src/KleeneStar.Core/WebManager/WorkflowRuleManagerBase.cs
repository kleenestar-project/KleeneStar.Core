using KleeneStar.Core.WebWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The shared half of the three rule registries: the dictionary, the ordering, the version
    /// the pickers watch, and the normalization of a key.
    /// </summary>
    /// <remarks>
    /// The three managers differ in what they hold and in nothing else, so they share this base
    /// rather than repeating it - a divergence between them would show up as a rule that can be
    /// registered but not resolved, and only under one of the three.
    /// </remarks>
    /// <typeparam name="TRule">The kind of rule the registry holds.</typeparam>
    public abstract class WorkflowRuleManagerBase<TRule> : IWorkflowRuleManager<TRule>
        where TRule : IWorkflowRule
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, TRule> _rules = new(StringComparer.OrdinalIgnoreCase);
        private int _version;

        /// <summary>
        /// Gets a number that changes whenever the set of registered rules does.
        /// </summary>
        public int Version => Volatile.Read(ref _version);

        /// <summary>
        /// Gets the registered rules, ordered by their display order and then by key.
        /// </summary>
        public IEnumerable<TRule> Rules
        {
            get
            {
                lock (_sync)
                {
                    return [.. _rules.Values
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];
                }
            }
        }

        /// <summary>
        /// Registers a rule.
        /// </summary>
        /// <param name="rule">The rule to register.</param>
        public void Register(TRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);

            var key = Normalize(rule.Key)
                ?? throw new ArgumentException("A workflow rule must carry a key.", nameof(rule));

            lock (_sync)
            {
                _rules[key] = rule;

                // the transition dialog is a cached fragment and projects the registry again when
                // this number has moved, which is how a rule a plugin contributed after the
                // dialog was built still becomes selectable
                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>
        /// Resolves a key to its rule.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The rule, or <see langword="null"/>.</returns>
        public TRule Get(string key)
        {
            var normalized = Normalize(key);

            if (normalized is null)
            {
                return default;
            }

            lock (_sync)
            {
                return _rules.TryGetValue(normalized, out var rule) ? rule : default;
            }
        }

        /// <summary>
        /// Releases resources held by this manager.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Trims a key and answers <see langword="null"/> for one that says nothing.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The normalized key, or <see langword="null"/>.</returns>
        private static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
        }
    }
}
