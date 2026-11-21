using KleeneStar.Core.WebFragment.Insight.Views;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebInsight
{
    /// <summary>
    /// The objects of the insight as a table or a list, with search, quickfilters and paging -
    /// the type a new insight opens on.
    /// </summary>
    public sealed class ObjectsInsightViewType : IInsightViewType
    {
        /// <inheritdoc/>
        public string Key => InsightViewTypes.Objects;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.view.objects.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.view.objects.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconTable();

        /// <inheritdoc/>
        public int Order => 0;

        /// <inheritdoc/>
        public Type Template => typeof(InsightTabObjectsTemplateFragment);
    }

    /// <summary>
    /// The dashboard of the insight: columns of freely arranged widgets. Every dashboard tab of
    /// an insight shows its one board.
    /// </summary>
    public sealed class DashboardInsightViewType : IInsightViewType
    {
        /// <inheritdoc/>
        public string Key => InsightViewTypes.Dashboard;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.view.dashboard.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.view.dashboard.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconDashboard();

        /// <inheritdoc/>
        public int Order => 1;

        /// <inheritdoc/>
        public Type Template => typeof(InsightTabDashboardTemplateFragment);
    }

    /// <summary>
    /// A Kanban board of the insight's objects; dropping a card is a workflow transition.
    /// </summary>
    public sealed class KanbanInsightViewType : IInsightViewType
    {
        /// <inheritdoc/>
        public string Key => InsightViewTypes.Kanban;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.view.kanban.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.view.kanban.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconColumns();

        /// <inheritdoc/>
        public int Order => 2;

        /// <inheritdoc/>
        public Type Template => typeof(InsightTabKanbanTemplateFragment);
    }

    /// <summary>
    /// The sprint board and the backlog of the sprints the insight's objects are planned in.
    /// </summary>
    public sealed class ScrumInsightViewType : IInsightViewType
    {
        /// <inheritdoc/>
        public string Key => InsightViewTypes.Scrum;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.view.scrum.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.view.scrum.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconBolt();

        /// <inheritdoc/>
        public int Order => 3;

        /// <inheritdoc/>
        public Type Template => typeof(InsightTabScrumTemplateFragment);
    }

    /// <summary>
    /// The insight's objects on a timeline, spanned by the date fields of their classes.
    /// </summary>
    public sealed class GanttInsightViewType : IInsightViewType
    {
        /// <inheritdoc/>
        public string Key => InsightViewTypes.Gantt;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.view.gantt.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.view.gantt.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconChartGantt();

        /// <inheritdoc/>
        public int Order => 4;

        /// <inheritdoc/>
        public Type Template => typeof(InsightTabGanttTemplateFragment);
    }

    /// <summary>
    /// The insight's objects on a month, week or agenda grid.
    /// </summary>
    public sealed class CalendarInsightViewType : IInsightViewType
    {
        /// <inheritdoc/>
        public string Key => InsightViewTypes.Calendar;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.view.calendar.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.view.calendar.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconCalendarDays();

        /// <inheritdoc/>
        public int Order => 5;

        /// <inheritdoc/>
        public Type Template => typeof(InsightTabCalendarTemplateFragment);
    }

    /// <summary>
    /// Charts computed from the history of the insight's objects: cumulative flow, velocity,
    /// average age, created vs. resolved and resolution time.
    /// </summary>
    public sealed class ReportsInsightViewType : IInsightViewType
    {
        /// <inheritdoc/>
        public string Key => InsightViewTypes.Reports;

        /// <inheritdoc/>
        public string Label => "kleenestar.core:insight.view.reports.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:insight.view.reports.description";

        /// <inheritdoc/>
        public IIcon Icon => new IconChartLine();

        /// <inheritdoc/>
        public int Order => 6;

        /// <inheritdoc/>
        public Type Template => typeof(InsightTabReportsTemplateFragment);
    }
}
