using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebCore.Internationalization;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// Sets up the processes a template brings - its approval policies (automation rules are a plugin's, see <see cref="WebManager.IWorkspaceTemplateManager.TemplateApplied"/>) - in
    /// the workspace it created.
    /// </summary>
    /// <remarks>
    /// Every policy and rule passes the same validation as one written by hand, and what does
    /// not pass is skipped and reported rather than stored half-working: a template that names
    /// a state its class does not have must not leave a policy behind that can never run.
    /// </remarks>
    public static partial class WorkspaceTemplateProcesses
    {
        [GeneratedRegex(@"\{(?<key>[A-Za-z0-9._-]+:[A-Za-z0-9._-]+)\}")]
        private static partial Regex KeyRegex();

        /// <summary>
        /// Sets up the processes of a template.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="identityId">The creator, or empty.</param>
        /// <param name="culture">The creator's language, or <see langword="null"/>.</param>
        /// <returns>What was set up.</returns>
        public static WorkspaceTemplateProcessResult Apply(IWorkspaceTemplate template, Guid workspaceId, Guid identityId, CultureInfo culture)
        {
            var result = new WorkspaceTemplateProcessResult();
            var creator = identityId == Guid.Empty ? null : CoreHub.IdentityManager.GetIdentity(identityId)?.UserName;
            var classes = CoreHub.ClassManager
                .GetClasses(new WebExpress.WebIndex.Queries.Query<Class>().WhereEquals(x => x.WorkspaceId, workspaceId))
                .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var declared in template.Classes ?? [])
            {
                if (!classes.TryGetValue(declared.Name ?? string.Empty, out var @class))
                {
                    continue;
                }

                foreach (var approval in declared.Approvals ?? [])
                {
                    if (CoreHub.ApprovalManager.GetPolicies(workspaceId).Any(x => x.ClassId == @class.Id && string.Equals(x.Name, Text(approval.Name, culture), StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (creator is null)
                    {
                        result.Skipped.Add($"{approval.Name}: nobody to approve without a creator.");
                        continue;
                    }

                    var statuses = CoreHub.StatusManager.GetStatuses(new ClassIdParameter(@class.Id)).ToList();
                    var policy = new ApprovalPolicy
                    {
                        Id = Guid.NewGuid(),
                        WorkspaceId = workspaceId,
                        ClassId = @class.Id,
                        Name = Text(approval.Name, culture),
                        Description = Text(approval.Description, culture),
                        Steps = Resolve(approval.Steps, creator, culture),
                        State = ApprovalPolicyState.Active,
                        AllowSelfApproval = approval.AllowSelfApproval,
                        RequestStatusId = State(statuses, approval.RequestState, culture),
                        ApprovedStatusId = State(statuses, approval.ApprovedState, culture),
                        RejectedStatusId = State(statuses, approval.RejectedState, culture),
                        CreatedById = identityId
                    };

                    if (Refused(CoreHub.ApprovalManager.Validate(policy), policy.Name, result))
                    {
                        continue;
                    }

                    CoreHub.ApprovalManager.AddPolicy(policy);
                    result.ApprovalPolicies.Add(policy);
                }
            }

            return result;
        }

        /// <summary>
        /// Replaces the placeholders of a text: <c>{creator}</c> and <c>{namespace:key}</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="creator">The creator's user name, or <see langword="null"/>.</param>
        /// <param name="culture">The language.</param>
        /// <returns>The text.</returns>
        public static string Resolve(string text, string creator, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var resolved = KeyRegex().Replace(text, match => Text(match.Groups["key"].Value, culture));

            return creator is null ? resolved : resolved.Replace("{creator}", creator, StringComparison.Ordinal);
        }

        /// <summary>
        /// Translates a text that may be an i18n key - in the creator's language, as everything a
        /// template sets up. Public, so a plugin setting up its part of a template does the same.
        /// </summary>
        /// <param name="text">The text or key.</param>
        /// <param name="culture">The language.</param>
        /// <returns>The text.</returns>
        public static string Text(string text, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return culture is null ? I18N.Translate(text) : I18N.Translate(culture, text);
        }

        /// <summary>
        /// Finds a state of a class by the name the template declared, translated as the
        /// workflow's states were.
        /// </summary>
        /// <param name="statuses">The states of the class.</param>
        /// <param name="declared">The declared name, or <see langword="null"/>.</param>
        /// <param name="culture">The language.</param>
        /// <returns>The state's id, or <see langword="null"/>.</returns>
        private static Guid? State(IReadOnlyList<Status> statuses, string declared, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(declared))
            {
                return null;
            }

            var name = Text(declared, culture);

            return statuses.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Name, declared, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// Records the problems of a policy or rule.
        /// </summary>
        /// <param name="problems">The problems.</param>
        /// <param name="name">The name.</param>
        /// <param name="result">The result.</param>
        /// <returns><see langword="true"/> when there are problems.</returns>
        private static bool Refused(IReadOnlyList<string> problems, string name, WorkspaceTemplateProcessResult result)
        {
            if (problems.Count == 0)
            {
                return false;
            }

            result.Skipped.Add($"{name}: {string.Join(" ", problems)}");

            return true;
        }
    }
}
