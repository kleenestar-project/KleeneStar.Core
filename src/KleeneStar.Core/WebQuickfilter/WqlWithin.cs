using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace KleeneStar.Core.WebQuickfilter
{
    /// <summary>
    /// The WQL predicate <c>within("KEY")</c> - true for every object below that object (or any
    /// object), at any depth - and its negation <c>not within("KEY")</c>
    /// (<c>kleenestar.hierarchy.md</c>).
    /// </summary>
    /// <remarks>
    /// WebIndex registers scalar functions per parser instance only (remedy prompt W5), so the
    /// predicate is expanded <b>before</b> parsing into the set condition the grammar already has:
    /// <c>Id in ("…", "…")</c> over the subtree (<see cref="WebManager.ObjectHierarchy.GetDescendantIds"/>,
    /// one range query on the path index). The object is resolved narrowed - an unknown or unreadable
    /// key selects nothing. Every KleeneStar WQL site goes through
    /// <see cref="WqlFilter"/>, which expands; the framework's own search table parses the raw
    /// expression and does not know the predicate.
    /// </remarks>
    public static partial class WqlWithin
    {
        private const string Nothing = "00000000-0000-0000-0000-000000000000";

        /// <summary>
        /// Expands every <c>within(…)</c> of an expression; an expression without one is returned
        /// unchanged.
        /// </summary>
        /// <param name="wql">The expression.</param>
        /// <param name="resolve">Answers the subtree ids of a key (default: narrowed lookup plus
        /// the descendants), <see langword="null"/> for an unknown key.</param>
        /// <returns>The expanded expression.</returns>
        public static string Expand(string wql, Func<string, Guid[]> resolve = null)
        {
            if (string.IsNullOrWhiteSpace(wql) || wql.IndexOf("within", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return wql;
            }

            resolve ??= Subtree;

            return Predicate().Replace(wql, match =>
            {
                var key = match.Groups["key"].Success ? match.Groups["key"].Value : match.Groups["key2"].Value;
                var ids = resolve(key.Trim()) ?? [];
                var values = ids.Length == 0
                    ? $"\"{Nothing}\""
                    : string.Join(", ", ids.Select(x => $"\"{x}\""));

                return match.Groups["not"].Success
                    ? $"Id not in ({values})"
                    : $"Id in ({values})";
            });
        }

        /// <summary>
        /// Answers the ids below the object of a key, read narrowed.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The ids, or <see langword="null"/> for an unknown or unreadable key.</returns>
        private static Guid[] Subtree(string key)
        {
            var root = string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);

            return root is null ? null : [.. WebManager.ObjectHierarchy.GetDescendantIds(root.Id)];
        }

        [GeneratedRegex(@"(?<not>\bnot\s+)?\bwithin\s*\(\s*(?:""(?<key>[^""]*)""|'(?<key2>[^']*)')\s*\)", RegexOptions.IgnoreCase)]
        private static partial Regex Predicate();
    }
}
