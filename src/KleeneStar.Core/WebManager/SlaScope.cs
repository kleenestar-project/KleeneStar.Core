using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Decides which service-level agreements of a class apply to one of its objects, by
    /// evaluating the scope rules of each policy against what the object carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A policy's scope rules say which objects it covers - "priority is P1", "tagged vip". Before
    /// they were evaluated every active policy of a class ran on every object of it, so an
    /// incident showed the clocks of all four priority agreements instead of the one its
    /// priority selects. The framework has no part in this: WebExpress times one agreement
    /// (<c>SlaDefinition</c>, <c>SlaEvaluator</c>); which agreements an object is held to is
    /// decided here, once, for every surface that shows or serves a clock.
    /// </para>
    /// <para>
    /// The rules combine the way a filter does: rules of the <b>same</b> type are alternatives
    /// (priority P1 <i>or</i> P2), rules of <b>different</b> types all have to hold (priority P1
    /// <i>and</i> tagged vip). A policy without rules covers every object of its class.
    /// </para>
    /// <para>
    /// Only what the object actually carries can be asked. <see cref="SlaScopeRuleType.Priority"/>
    /// is compared with the value of the class's priority field (the priority's name, which is
    /// what the field stores) and <see cref="SlaScopeRuleType.Tag"/> with the object's tags. The
    /// other rule types - contract, customer, catalog, system, site, category, source, type -
    /// name attributes no object has, so they do not restrict anything yet; treating them as
    /// "never" would silently retire every policy that states one, and the seeded catalogue does.
    /// A priority rule that names no priority of the class is a configuration nothing can
    /// satisfy, and is skipped for the same reason rather than switching its policy off.
    /// </para>
    /// </remarks>
    public static class SlaScope
    {
        /// <summary>
        /// Returns the policies among the given ones whose scope covers the object.
        /// </summary>
        /// <param name="policies">The candidate policies, typically the active ones of the
        /// object's class.</param>
        /// <param name="object">The object.</param>
        /// <param name="fieldManager">Reads the fields of the class.</param>
        /// <param name="valueManager">Reads the object's field values.</param>
        /// <param name="priorityManager">Reads the priorities the class defines.</param>
        /// <param name="tagManager">Reads the object's tags.</param>
        /// <returns>The applicable policies, in the order given.</returns>
        public static IEnumerable<SlaPolicy> Select
        (
            IEnumerable<SlaPolicy> policies,
            ObjectEntity @object,
            IFieldManager fieldManager,
            IValueManager valueManager,
            IPriorityManager priorityManager,
            IObjectTagManager tagManager
        )
        {
            var list = (policies ?? []).Where(x => x is not null).ToList();

            if (@object is null || list.Count == 0)
            {
                return [];
            }

            // the object is read only when some policy asks something of it
            if (list.All(x => (x.Scope ?? []).Count == 0))
            {
                return list;
            }

            var facts = SlaScopeFacts.Read(@object, fieldManager, valueManager, priorityManager, tagManager);

            return [.. list.Where(x => Applies(x, facts))];
        }

        /// <summary>
        /// Returns whether the scope of a policy covers an object, given what the object carries.
        /// </summary>
        /// <param name="policy">The policy.</param>
        /// <param name="facts">What the object carries.</param>
        /// <returns>True when every evaluable rule type is satisfied by at least one of its rules.</returns>
        public static bool Applies(SlaPolicy policy, SlaScopeFacts facts)
        {
            if (policy is null)
            {
                return false;
            }

            foreach (var group in (policy.Scope ?? []).Where(x => !string.IsNullOrWhiteSpace(x?.Value)).GroupBy(x => x.RuleType))
            {
                switch (group.Key)
                {
                    case SlaScopeRuleType.Priority:
                        {
                            // a rule naming no priority of the class can never be satisfied; it
                            // is a configuration mistake and is left out of the decision
                            var values = group
                                .Select(x => x.Value.Trim())
                                .Where(x => facts.ClassPriorities.Contains(x))
                                .ToList();

                            if (values.Count > 0 && !values.Any(facts.Priorities.Contains))
                            {
                                return false;
                            }

                            break;
                        }

                    case SlaScopeRuleType.Tag:
                        if (!group.Any(x => facts.Tags.Contains(x.Value.Trim())))
                        {
                            return false;
                        }

                        break;

                    default:
                        // an attribute no object carries restricts nothing yet
                        break;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// What an object carries that a scope rule can ask about, read once per object.
    /// </summary>
    public sealed class SlaScopeFacts
    {
        /// <summary>
        /// Gets the names of the priorities the object's priority fields hold.
        /// </summary>
        public HashSet<string> Priorities { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the names of the priorities the object's class defines.
        /// </summary>
        public HashSet<string> ClassPriorities { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the names of the object's tags.
        /// </summary>
        public HashSet<string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reads the facts of an object.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="fieldManager">Reads the fields of the class.</param>
        /// <param name="valueManager">Reads the object's field values.</param>
        /// <param name="priorityManager">Reads the priorities the class defines.</param>
        /// <param name="tagManager">Reads the object's tags.</param>
        /// <returns>The facts; empty sets where a manager is missing.</returns>
        public static SlaScopeFacts Read
        (
            ObjectEntity @object,
            IFieldManager fieldManager,
            IValueManager valueManager,
            IPriorityManager priorityManager,
            IObjectTagManager tagManager
        )
        {
            var facts = new SlaScopeFacts();

            if (@object is null)
            {
                return facts;
            }

            var classPriorities = priorityManager?
                .GetPriorities(new ClassIdParameter(@object.ClassId))
                .ToList() ?? [];

            foreach (var priority in classPriorities.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                facts.ClassPriorities.Add(priority.Name.Trim());
            }

            var priorityFields = fieldManager?
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(x => !x.Deprecated && x.State == FieldState.Active && x.FieldType == FieldType.Priority)
                .ToList() ?? [];

            foreach (var field in priorityFields)
            {
                var data = valueManager?.GetValue(@object.Id, field.Id)?.Data?.Trim();

                if (string.IsNullOrWhiteSpace(data))
                {
                    continue;
                }

                // the field stores the priority's name; an id is accepted as well, so a value
                // written by another client still selects its agreement
                var named = Guid.TryParse(data, out var id)
                    ? classPriorities.FirstOrDefault(x => x.Id == id)?.Name
                    : data;

                if (!string.IsNullOrWhiteSpace(named))
                {
                    facts.Priorities.Add(named.Trim());
                }
            }

            foreach (var tag in tagManager?.GetTags(@object.Id) ?? [])
            {
                if (!string.IsNullOrWhiteSpace(tag?.Name))
                {
                    facts.Tags.Add(tag.Name.Trim());
                }
            }

            return facts;
        }
    }
}
