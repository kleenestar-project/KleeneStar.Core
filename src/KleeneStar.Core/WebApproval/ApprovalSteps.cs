using KleeneStar.Core.WebAutomation.Actions;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebExpress.WebIndex.Queries;
using Group = KleeneStar.Model.Entities.Group;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebApproval
{
    /// <summary>
    /// How many approvers of a step have to agree.
    /// </summary>
    public enum ApprovalMode
    {
        /// <summary>One approver.</summary>
        Any,

        /// <summary>Every named account, and one member of every named group.</summary>
        All,

        /// <summary>A number of distinct approvers.</summary>
        Quorum
    }

    /// <summary>
    /// A step of an approval as it was resolved when the approval was requested.
    /// </summary>
    public sealed class ApprovalStep
    {
        /// <summary>Gets or sets the name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets how many approvers have to agree.</summary>
        public ApprovalMode Mode { get; set; }

        /// <summary>Gets or sets the number of approvers a quorum needs.</summary>
        public int Quorum { get; set; } = 1;

        /// <summary>Gets or sets the accounts that decide.</summary>
        public List<Guid> Identities { get; set; } = [];

        /// <summary>Gets or sets the groups whose members decide.</summary>
        public List<Guid> Groups { get; set; } = [];

        /// <summary>Gets or sets the approvers as written, for display.</summary>
        public string Approvers { get; set; }

        /// <summary>Gets or sets the hours the step may take before it is overdue, or <c>null</c>.</summary>
        public int? DueHours { get; set; }

        /// <summary>Gets or sets whether the step applies to the object; a step whose condition
        /// did not hold is passed over.</summary>
        public bool Applies { get; set; } = true;
    }

    /// <summary>
    /// Reads, checks and resolves the steps of an approval policy, and stores the resolved steps
    /// with an approval.
    /// </summary>
    /// <remarks>
    /// A line reads <c>&lt;name&gt;: &lt;any | all | n of&gt; &lt;approvers&gt; [if &lt;WQL&gt;] [within &lt;hours&gt;h]</c>;
    /// approvers are user names or e-mail addresses and <c>group:&lt;name&gt;</c>, separated by
    /// commas. Blank lines and lines starting with <c>#</c> are ignored.
    /// </remarks>
    public static partial class ApprovalSteps
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        [GeneratedRegex(@"^(?<name>[^:]+):\s*(?<mode>any|all|(?<n>\d+)\s+of)\s+(?<rest>.+)$", RegexOptions.IgnoreCase)]
        private static partial Regex LineRegex();

        [GeneratedRegex(@"\s+within\s+(?<hours>\d+)\s*h\s*$", RegexOptions.IgnoreCase)]
        private static partial Regex WithinRegex();

        [GeneratedRegex(@"\s+if\s+(?<condition>.+)$", RegexOptions.IgnoreCase)]
        private static partial Regex IfRegex();

        /// <summary>
        /// A step as written in a policy, before its approvers and its condition are resolved.
        /// </summary>
        /// <param name="Number">The line number.</param>
        /// <param name="Name">The name.</param>
        /// <param name="Mode">The mode.</param>
        /// <param name="Quorum">The quorum.</param>
        /// <param name="Approvers">The approvers as written.</param>
        /// <param name="Condition">The WQL condition, or <see langword="null"/>.</param>
        /// <param name="DueHours">The hours, or <see langword="null"/>.</param>
        public sealed record Definition(int Number, string Name, ApprovalMode Mode, int Quorum, IReadOnlyList<string> Approvers, string Condition, int? DueHours);

        /// <summary>
        /// Reads the steps of a policy.
        /// </summary>
        /// <param name="script">The steps, one per line.</param>
        /// <param name="problems">The lines that could not be read.</param>
        /// <returns>The steps that could.</returns>
        public static IReadOnlyList<Definition> Parse(string script, List<string> problems = null)
        {
            var steps = new List<Definition>();
            var lines = (script ?? string.Empty).Replace("\r\n", "\n").Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var match = LineRegex().Match(line);

                if (!match.Success)
                {
                    problems?.Add($"Line {index + 1}: write the step as 'name: any|all|n of approvers [if WQL] [within hours h]'.");
                    continue;
                }

                var rest = match.Groups["rest"].Value.Trim();
                int? due = null;
                string condition = null;

                if (WithinRegex().Match(rest) is { Success: true } within)
                {
                    due = int.Parse(within.Groups["hours"].Value, CultureInfo.InvariantCulture);
                    rest = rest[..within.Index].Trim();
                }

                if (IfRegex().Match(rest) is { Success: true } @if)
                {
                    condition = @if.Groups["condition"].Value.Trim();
                    rest = rest[..@if.Index].Trim();
                }

                var mode = match.Groups["n"].Success
                    ? ApprovalMode.Quorum
                    : string.Equals(match.Groups["mode"].Value, "all", StringComparison.OrdinalIgnoreCase) ? ApprovalMode.All : ApprovalMode.Any;
                var quorum = match.Groups["n"].Success ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
                var approvers = rest.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                steps.Add(new Definition(index + 1, match.Groups["name"].Value.Trim(), mode, quorum, approvers, condition, due));
            }

            return steps;
        }

        /// <summary>
        /// Checks the steps of a policy: every line readable, every approver known, every
        /// condition valid WQL, every quorum reachable.
        /// </summary>
        /// <param name="script">The steps.</param>
        /// <returns>The problems; empty when the steps are valid.</returns>
        public static IReadOnlyList<string> Validate(string script)
        {
            var problems = new List<string>();
            var steps = Parse(script, problems);

            if (steps.Count == 0 && problems.Count == 0)
            {
                problems.Add("The policy needs at least one step.");
            }

            foreach (var step in steps)
            {
                if (step.Approvers.Count == 0)
                {
                    problems.Add($"Line {step.Number}: name the approvers.");
                }

                foreach (var approver in step.Approvers.Where(x => ResolveApprover(x) is null))
                {
                    problems.Add($"Line {step.Number}: unknown approver '{approver}'.");
                }

                if (step.Mode == ApprovalMode.Quorum && step.Quorum < 1)
                {
                    problems.Add($"Line {step.Number}: a quorum needs at least one approver.");
                }

                // a quorum of named accounts alone can be checked; a group may grow
                if (step.Mode == ApprovalMode.Quorum && step.Approvers.All(x => !IsGroup(x)) && step.Quorum > step.Approvers.Count)
                {
                    problems.Add($"Line {step.Number}: {step.Quorum} approvals cannot be reached by {step.Approvers.Count} accounts.");
                }

                if (step.Condition is not null && !WqlFilter.TryValidate<ObjectEntity>(step.Condition, out var error))
                {
                    problems.Add($"Line {step.Number}: the condition is not valid WQL: {error}");
                }

                if (step.DueHours is 0)
                {
                    problems.Add($"Line {step.Number}: a step needs at least an hour.");
                }
            }

            return problems;
        }

        /// <summary>
        /// Resolves the steps of a policy for an object: the approvers to their ids, the
        /// conditions against the object as it is now.
        /// </summary>
        /// <param name="script">The steps.</param>
        /// <param name="object">The object to approve.</param>
        /// <returns>The resolved steps.</returns>
        public static List<ApprovalStep> Resolve(string script, ObjectEntity @object)
        {
            return [.. Parse(script).Select(step =>
            {
                var resolved = new ApprovalStep
                {
                    Name = step.Name,
                    Mode = step.Mode,
                    Quorum = step.Quorum,
                    Approvers = string.Join(", ", step.Approvers),
                    DueHours = step.DueHours,
                    Applies = Holds(step.Condition, @object)
                };

                foreach (var approver in step.Approvers)
                {
                    switch (ResolveApprover(approver))
                    {
                        case Group group:
                            resolved.Groups.Add(group.Id);
                            break;
                        case Identity identity:
                            resolved.Identities.Add(identity.Id);
                            break;
                    }
                }

                return resolved;
            })];
        }

        /// <summary>
        /// Reads the steps an approval was requested with.
        /// </summary>
        /// <param name="approval">The approval.</param>
        /// <returns>The steps.</returns>
        public static List<ApprovalStep> Of(Approval approval)
        {
            if (string.IsNullOrWhiteSpace(approval?.Steps))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<ApprovalStep>>(approval.Steps, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// Writes resolved steps for storing with an approval.
        /// </summary>
        /// <param name="steps">The steps.</param>
        /// <returns>The JSON.</returns>
        public static string Serialize(IEnumerable<ApprovalStep> steps)
        {
            return JsonSerializer.Serialize(steps, JsonOptions);
        }

        /// <summary>
        /// Resolves an approver as written: <c>group:&lt;name&gt;</c> to a group, anything else to
        /// an active account.
        /// </summary>
        /// <param name="text">The approver.</param>
        /// <returns>The <see cref="Group"/> or <see cref="Identity"/>, or <see langword="null"/>.</returns>
        public static object ResolveApprover(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (IsGroup(text))
            {
                var name = text.Trim()["group:".Length..].Trim();

                return CoreHub.GroupManager
                    .GetGroups(new Query<Group>())
                    .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            return StandardActions.ResolveIdentity(text.Trim());
        }

        /// <summary>
        /// Determines whether an approver as written names a group.
        /// </summary>
        /// <param name="text">The approver.</param>
        /// <returns><see langword="true"/> for <c>group:&lt;name&gt;</c>.</returns>
        private static bool IsGroup(string text)
        {
            return text.Trim().StartsWith("group:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Evaluates a step's condition against the object.
        /// </summary>
        /// <param name="condition">The WQL condition, or <see langword="null"/>.</param>
        /// <param name="object">The object.</param>
        /// <returns><see langword="true"/> when the step applies.</returns>
        private static bool Holds(string condition, ObjectEntity @object)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            try
            {
                return WqlFilter.Apply(condition, [@object]).Any();
            }
            catch (Exception)
            {
                // a condition that stopped compiling asks for the approval rather than waiving it
                return true;
            }
        }
    }
}
