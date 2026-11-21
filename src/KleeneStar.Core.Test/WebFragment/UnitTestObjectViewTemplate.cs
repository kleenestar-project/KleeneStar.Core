using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebFragment.Object.Assets;
using KleeneStar.Core.WebFragment.Object.Issues;
using KleeneStar.Model.Entities;
using System;
using System.Linq;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Provides unit tests for <see cref="ObjectViewTemplate"/> — the mapping between a view type
    /// of an objects tab and the template fragment that renders it.
    /// </summary>
    /// <remarks>
    /// The predecessor of this mapping was a table of hard-written fragment ids that had drifted
    /// away from the fragments: no id matched, so every tab a user added came out as a table
    /// whatever they picked. These tests therefore assert against the fragment <em>types</em>, so
    /// renaming or moving a template breaks the build or a test rather than the feature.
    /// </remarks>
    public class UnitTestObjectViewTemplate
    {
        /// <summary>
        /// Verifies that the id is derived the way the fragment base derives the id of the element
        /// it renders, which is what the client matches against.
        /// </summary>
        [Fact]
        public void TemplateId_IsTheLowerCasedFullNameWithDashes()
        {
            Assert.Equal
            (
                "kleenestar-core-webfragment-object-issues-issuetabdashboardtemplatefragment",
                ObjectViewTemplate.TemplateId(typeof(IssueTabDashboardTemplateFragment))
            );
        }

        /// <summary>
        /// Verifies that every view type an issues tab offers names the issue template that
        /// renders it.
        /// </summary>
        /// <param name="type">The view type under test.</param>
        /// <param name="expected">The template fragment expected to render it.</param>
        [Theory]
        [InlineData(ObjectViewType.Table, typeof(IssueTabViewTemplateFragment))]
        [InlineData(ObjectViewType.List, typeof(IssueTabViewTemplateFragment))]
        [InlineData(ObjectViewType.Issues, typeof(IssueTabViewTemplateFragment))]
        [InlineData(ObjectViewType.Dashboard, typeof(IssueTabDashboardTemplateFragment))]
        [InlineData(ObjectViewType.Kanban, typeof(IssueTabKanbanTemplateFragment))]
        // the sprint board and the backlog were merged into one scrum view, so both types
        // resolve to the same template and a tab persisted as either one still renders
        [InlineData(ObjectViewType.ScrumSprint, typeof(IssueTabScrumTemplateFragment))]
        [InlineData(ObjectViewType.ScrumBacklog, typeof(IssueTabScrumTemplateFragment))]
        [InlineData(ObjectViewType.Gantt, typeof(IssueTabGanttTemplateFragment))]
        [InlineData(ObjectViewType.Scheduler, typeof(IssueTabSchedulerTemplateFragment))]
        public void ResolveTemplateId_ForIssues_NamesTheIssueTemplate(ObjectViewType type, Type expected)
        {
            Assert.Equal
            (
                ObjectViewTemplate.TemplateId(expected),
                ObjectViewTemplate.ResolveTemplateId(type, ObjectKind.Issue)
            );
        }

        /// <summary>
        /// Verifies that the asset tab names the asset templates rather than the issue ones, which
        /// its page does not carry.
        /// </summary>
        /// <param name="type">The view type under test.</param>
        /// <param name="expected">The template fragment expected to render it.</param>
        [Theory]
        [InlineData(ObjectViewType.Table, typeof(AssetTabViewTemplateFragment))]
        [InlineData(ObjectViewType.List, typeof(AssetTabViewTemplateFragment))]
        [InlineData(ObjectViewType.Assets, typeof(AssetTabViewTemplateFragment))]
        [InlineData(ObjectViewType.Dashboard, typeof(AssetTabDashboardTemplateFragment))]
        [InlineData(ObjectViewType.Kanban, typeof(AssetTabKanbanTemplateFragment))]
        public void ResolveTemplateId_ForAssets_NamesTheAssetTemplate(ObjectViewType type, Type expected)
        {
            Assert.Equal
            (
                ObjectViewTemplate.TemplateId(expected),
                ObjectViewTemplate.ResolveTemplateId(type, ObjectKind.Asset)
            );
        }

        /// <summary>
        /// Verifies that the same view type yields a different template per kind, which is the
        /// reason the mapping takes the kind at all.
        /// </summary>
        [Fact]
        public void ResolveTemplateId_DiffersPerKind()
        {
            Assert.NotEqual
            (
                ObjectViewTemplate.ResolveTemplateId(ObjectViewType.Dashboard, ObjectKind.Issue),
                ObjectViewTemplate.ResolveTemplateId(ObjectViewType.Dashboard, ObjectKind.Asset)
            );
        }

        /// <summary>
        /// Verifies that a view type the kind has no template for is reported as absent instead of
        /// naming a template the page does not carry.
        /// </summary>
        /// <param name="type">The view type under test.</param>
        [Theory]
        [InlineData(ObjectViewType.ScrumSprint)]
        [InlineData(ObjectViewType.ScrumBacklog)]
        [InlineData(ObjectViewType.Issues)]
        [InlineData(ObjectViewType.Gantt)]
        [InlineData(ObjectViewType.Scheduler)]
        public void ResolveTemplateId_ForAssets_HasNoScrumOrIssueTemplates(ObjectViewType type)
        {
            Assert.Null(ObjectViewTemplate.ResolveTemplateId(type, ObjectKind.Asset));
        }

        /// <summary>
        /// Verifies that an unknown kind names nothing rather than guessing a template.
        /// </summary>
        [Fact]
        public void ResolveTemplateId_ForAnUnknownKind_NamesNothing()
        {
            Assert.Null(ObjectViewTemplate.ResolveTemplateId(ObjectViewType.Table, "nosuchkind"));
            Assert.Null(ObjectViewTemplate.ResolveTemplateId(ObjectViewType.Table, null));
        }

        /// <summary>
        /// Verifies the round trip the tab endpoint relies on when a user adds a tab: the id the
        /// client reports comes back as the view type that template renders. This is the step that
        /// used to fail for every type, leaving Table as the fallback.
        /// </summary>
        /// <param name="type">The view type under test.</param>
        [Theory]
        [InlineData(ObjectViewType.Dashboard)]
        [InlineData(ObjectViewType.Kanban)]
        [InlineData(ObjectViewType.ScrumSprint)]
        public void ResolveViewType_ForIssues_RoundTripsTheDistinctTemplates(ObjectViewType type)
        {
            var templateId = ObjectViewTemplate.ResolveTemplateId(type, ObjectKind.Issue);

            Assert.Equal(type, ObjectViewTemplate.ResolveViewType(templateId, ObjectKind.Issue));
        }

        /// <summary>
        /// Verifies that the template shared by several view types resolves to the first one
        /// declared, which is the type a newly added tab is created as.
        /// </summary>
        [Fact]
        public void ResolveViewType_ForTheSharedTemplate_YieldsTable()
        {
            var templateId = ObjectViewTemplate.ResolveTemplateId(ObjectViewType.List, ObjectKind.Issue);

            Assert.Equal(ObjectViewType.Table, ObjectViewTemplate.ResolveViewType(templateId, ObjectKind.Issue));
        }

        /// <summary>
        /// Verifies that a tab persisted as either scrum type still resolves to the merged scrum
        /// template, and that the round trip settles on the first type declared for it — so an
        /// existing backlog tab keeps rendering instead of falling back to a table.
        /// </summary>
        [Fact]
        public void ResolveViewType_ForTheScrumTemplate_YieldsScrumSprint()
        {
            var sprintTemplate = ObjectViewTemplate.ResolveTemplateId(ObjectViewType.ScrumSprint, ObjectKind.Issue);
            var backlogTemplate = ObjectViewTemplate.ResolveTemplateId(ObjectViewType.ScrumBacklog, ObjectKind.Issue);

            Assert.Equal(sprintTemplate, backlogTemplate);
            Assert.Equal(ObjectViewType.ScrumSprint, ObjectViewTemplate.ResolveViewType(backlogTemplate, ObjectKind.Issue));
        }

        /// <summary>
        /// Verifies that a template of another kind is not accepted, so an asset tab cannot be
        /// created as a scrum board the asset page cannot render.
        /// </summary>
        [Fact]
        public void ResolveViewType_RejectsATemplateOfAnotherKind()
        {
            var issueTemplate = ObjectViewTemplate.ResolveTemplateId(ObjectViewType.Dashboard, ObjectKind.Issue);

            Assert.Null(ObjectViewTemplate.ResolveViewType(issueTemplate, ObjectKind.Asset));
        }

        /// <summary>
        /// Verifies that an unknown or missing id is reported as unresolved, leaving the caller to
        /// decide on a fallback.
        /// </summary>
        /// <param name="templateId">The template id under test.</param>
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("kleenestar-core-webfragment-object-objecttabdashboardtemplatefragment")]
        public void ResolveViewType_ForAnUnknownId_IsUnresolved(string templateId)
        {
            Assert.Null(ObjectViewTemplate.ResolveViewType(templateId, ObjectKind.Issue));
        }

        /// <summary>
        /// Verifies that every template an issues tab can name is one the page actually embeds, by
        /// checking the fragment types are registered for the issues tab.
        /// </summary>
        [Fact]
        public void ResolveTemplateId_ForIssues_NamesOnlyEmbeddedTemplates()
        {
            var embedded = new[]
            {
                typeof(IssueTabViewTemplateFragment),
                typeof(IssueTabDashboardTemplateFragment),
                typeof(IssueTabKanbanTemplateFragment),
                typeof(IssueTabScrumTemplateFragment),
                typeof(IssueTabGanttTemplateFragment),
                typeof(IssueTabSchedulerTemplateFragment)
            }
                .Select(ObjectViewTemplate.TemplateId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var named = Enum.GetValues<ObjectViewType>()
                .Select(x => ObjectViewTemplate.ResolveTemplateId(x, ObjectKind.Issue))
                .Where(x => x is not null);

            Assert.All(named, x => Assert.Contains(x, embedded));
        }
    }
}
