using WebExpress.WebApp.WebData;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// The board resource of an insight's Kanban tab, shared by the board and the search and
    /// quickfilter above it through the tab's view state.
    /// </summary>
    public sealed class InsightKanbanResource : IDataResource
    {
    }

    /// <summary>
    /// The plan resource of an insight's Gantt tab.
    /// </summary>
    public sealed class InsightGanttResource : IDataResource
    {
    }

    /// <summary>
    /// The calendar resource of an insight's calendar tab.
    /// </summary>
    public sealed class InsightCalendarResource : IDataResource
    {
    }

    /// <summary>
    /// The sprint board resource of an insight's Scrum tab.
    /// </summary>
    public sealed class InsightScrumBoardResource : IDataResource
    {
    }

    /// <summary>
    /// The backlog resource of an insight's Scrum tab.
    /// </summary>
    public sealed class InsightScrumBacklogResource : IDataResource
    {
    }
}
