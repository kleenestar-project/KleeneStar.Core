using KleeneStar.Core.WebFragment.Object.Assets;
using KleeneStar.Core.WebFragment.Object.Issues;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Maps between a view type of an objects tab and the tab template that renders it.
    /// </summary>
    /// <remarks>
    /// The template is chosen by the view type <em>and</em> the object kind, because each kind
    /// embeds its own set of templates on its page: the issues overview carries the issue
    /// templates, the assets overview the asset ones. A mapping that knows only the view type
    /// cannot name the right template for both, and naming a template the page does not carry
    /// leaves the client without one to bind.
    ///
    /// The ids are derived from the fragment types rather than written out, so renaming or moving
    /// a template fragment is a compile error here instead of a tab that silently falls back to the
    /// first registered template.
    /// </remarks>
    public static class ObjectViewTemplate
    {
        private static readonly Dictionary<string, IReadOnlyList<(ObjectViewType Type, Type Fragment)>> _templates = new(StringComparer.OrdinalIgnoreCase)
        {
            [ObjectKind.Issue] =
            [
                // the curated issue list and the plain table share the composite view template,
                // and so does the list; the view type decides which content endpoint fills it
                (ObjectViewType.Table, typeof(IssueTabViewTemplateFragment)),
                (ObjectViewType.List, typeof(IssueTabViewTemplateFragment)),
                (ObjectViewType.Issues, typeof(IssueTabViewTemplateFragment)),
                (ObjectViewType.Dashboard, typeof(IssueTabDashboardTemplateFragment)),
                (ObjectViewType.Kanban, typeof(IssueTabKanbanTemplateFragment)),

                // the sprint board and the backlog were merged into one scrum view, so both
                // types render the same template. keeping both mapped means a tab persisted
                // as either type still resolves — no view type had to be appended to the
                // ordinal-persisted enum, and no stored tab had to be migrated. ScrumSprint
                // is declared first, so that is the type a newly added scrum tab is created
                // as.
                (ObjectViewType.ScrumSprint, typeof(IssueTabScrumTemplateFragment)),
                (ObjectViewType.ScrumBacklog, typeof(IssueTabScrumTemplateFragment)),

                // the timeline and the calendar read the same date fields, but each renders
                // its own control, so each has a template of its own
                (ObjectViewType.Gantt, typeof(IssueTabGanttTemplateFragment)),
                (ObjectViewType.Scheduler, typeof(IssueTabSchedulerTemplateFragment))
            ],
            [ObjectKind.Asset] =
            [
                (ObjectViewType.Table, typeof(AssetTabViewTemplateFragment)),
                (ObjectViewType.List, typeof(AssetTabViewTemplateFragment)),
                (ObjectViewType.Assets, typeof(AssetTabViewTemplateFragment)),
                (ObjectViewType.Dashboard, typeof(AssetTabDashboardTemplateFragment)),
                (ObjectViewType.Kanban, typeof(AssetTabKanbanTemplateFragment))

                // the asset overview embeds no scrum, gantt or scheduler templates, so those
                // view types are absent here on purpose and are neither offered nor
                // resolvable for assets
            ]
        };

        /// <summary>
        /// Returns the client-side id of a tab template fragment.
        /// </summary>
        /// <remarks>
        /// The client identifies a template by the id of the element the fragment rendered, which
        /// the fragment base derives from its type: the full name, lower-cased, with the dots
        /// replaced by dashes.
        /// </remarks>
        /// <param name="fragment">The tab template fragment type.</param>
        /// <returns>The id the client knows the template by.</returns>
        public static string TemplateId(Type fragment)
        {
            return fragment?.FullName?.ToLowerInvariant()?.Replace('.', '-');
        }

        /// <summary>
        /// Returns the id of the tab template that renders a view type for an object kind.
        /// </summary>
        /// <param name="type">The view type.</param>
        /// <param name="kind">The object kind whose tab the view belongs to.</param>
        /// <returns>
        /// The template id, or null when the kind has no template for that view type and the view
        /// therefore cannot be shown on its page.
        /// </returns>
        public static string ResolveTemplateId(ObjectViewType type, string kind)
        {
            if (kind is null || !_templates.TryGetValue(kind, out var templates))
            {
                return null;
            }

            var fragment = templates
                .Where(x => x.Type == type)
                .Select(x => x.Fragment)
                .FirstOrDefault();

            return fragment is null ? null : TemplateId(fragment);
        }

        /// <summary>
        /// Returns the view type a template id denotes for an object kind.
        /// </summary>
        /// <remarks>
        /// Several view types can share one template, in which case the first one declared for the
        /// kind wins — that is the type a newly added tab is created as.
        /// </remarks>
        /// <param name="templateId">The template id the client reported.</param>
        /// <param name="kind">The object kind whose tab the view is being added to.</param>
        /// <returns>
        /// The view type, or null when the id denotes no template of that kind.
        /// </returns>
        public static ObjectViewType? ResolveViewType(string templateId, string kind)
        {
            if (string.IsNullOrWhiteSpace(templateId) || kind is null || !_templates.TryGetValue(kind, out var templates))
            {
                return null;
            }

            foreach (var (type, fragment) in templates)
            {
                if (string.Equals(TemplateId(fragment), templateId, StringComparison.OrdinalIgnoreCase))
                {
                    return type;
                }
            }

            return null;
        }
    }
}
