using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebApproval
{
    /// <summary>
    /// A starting point for an approval policy: the steps an established process uses.
    /// </summary>
    /// <param name="Key">The key the policy page is opened with (<c>?template=</c>).</param>
    /// <param name="Label">The i18n key of the name.</param>
    /// <param name="Description">The i18n key of the description.</param>
    /// <param name="Steps">The steps.</param>
    public sealed record ApprovalTemplate(string Key, string Label, string Description, string Steps);

    /// <summary>
    /// The approval processes the platform ships as templates.
    /// </summary>
    /// <remarks>
    /// A template fills the policy form; the groups it names (<c>Change Managers</c>,
    /// <c>CAB</c>, <c>E-CAB</c>) are the usual ones, and saving tells which do not exist yet.
    /// </remarks>
    public static class ApprovalTemplates
    {
        /// <summary>
        /// Gets the templates.
        /// </summary>
        public static IReadOnlyList<ApprovalTemplate> All { get; } =
        [
            new ApprovalTemplate
            (
                "cab",
                "kleenestar.core:approval.template.cab",
                "kleenestar.core:approval.template.cab.description",
                "Change manager: any group:Change Managers within 24h\n" +
                "Change advisory board: 2 of group:CAB within 72h"
            ),
            new ApprovalTemplate
            (
                "ecab",
                "kleenestar.core:approval.template.ecab",
                "kleenestar.core:approval.template.ecab.description",
                "Emergency change advisory board: any group:E-CAB within 4h"
            ),
            new ApprovalTemplate
            (
                "four-eyes",
                "kleenestar.core:approval.template.foureyes",
                "kleenestar.core:approval.template.foureyes.description",
                "Second pair of eyes: any group:Change Managers within 24h"
            )
        ];

        /// <summary>
        /// Returns a template.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The template, or <see langword="null"/>.</returns>
        public static ApprovalTemplate Get(string key)
        {
            return All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
