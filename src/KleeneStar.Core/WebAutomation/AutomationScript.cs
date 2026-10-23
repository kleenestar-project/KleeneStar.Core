using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;

namespace KleeneStar.Core.WebAutomation
{
    /// <summary>
    /// The action script of a rule: one action per line, the key first, the argument after it.
    /// Blank lines and lines starting with <c>#</c> are ignored.
    /// </summary>
    /// <remarks>
    /// A script rather than a form per action: a rule reads top to bottom like what it does
    /// (<c>assign service.desk</c>, <c>transition In Progress</c>, <c>comment Taken over.</c>),
    /// it is written and reviewed as text, and an action a plugin registers is usable at once
    /// without a form of its own.
    /// </remarks>
    public static class AutomationScript
    {
        /// <summary>
        /// Splits a script into its actions.
        /// </summary>
        /// <param name="script">The script.</param>
        /// <returns>The lines that name an action, with their line numbers.</returns>
        public static IReadOnlyList<AutomationScriptLine> Parse(string script)
        {
            var lines = new List<AutomationScriptLine>();
            var number = 0;

            foreach (var raw in (script ?? string.Empty).Split('\n'))
            {
                number++;

                var line = raw.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separator = line.IndexOfAny([' ', '\t']);
                var key = separator < 0 ? line : line[..separator];
                var argument = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();

                lines.Add(new AutomationScriptLine(number, key, argument));
            }

            return lines;
        }

        /// <summary>
        /// Validates a script against the action catalog and the class a rule is narrowed to.
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="class">The class, or <see langword="null"/>.</param>
        /// <param name="workspaceId">The rule's workspace, or empty when unknown.</param>
        /// <returns>The problems found, each naming its line; empty for a valid script.</returns>
        public static IReadOnlyList<string> Validate(string script, Class @class, Guid workspaceId = default)
        {
            var problems = new List<string>();
            var lines = Parse(script);

            if (lines.Count == 0)
            {
                problems.Add("The rule performs no action.");
            }

            foreach (var line in lines)
            {
                var action = AutomationActionCatalog.Get(line.Key);

                if (action is null)
                {
                    problems.Add($"Line {line.Number}: unknown action '{line.Key}'.");
                    continue;
                }

                var problem = action.Validate(line.Argument, @class, workspaceId);

                if (problem is not null)
                {
                    problems.Add($"Line {line.Number}: {problem}");
                }
            }

            return problems;
        }
    }

    /// <summary>
    /// One action of a script.
    /// </summary>
    /// <param name="Number">The line number, counted from one.</param>
    /// <param name="Key">The action key.</param>
    /// <param name="Argument">The argument, possibly empty.</param>
    public sealed record AutomationScriptLine(int Number, string Key, string Argument);
}
