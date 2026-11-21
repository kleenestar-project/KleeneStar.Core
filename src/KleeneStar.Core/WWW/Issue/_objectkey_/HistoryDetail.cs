using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Issue._objectkey_
{
    /// <summary>
    /// The detail side of the history dialog: one commit of an object, addressed by
    /// <c>?commit={number|id}</c>. Fetched into the frame of the master-detail composite on
    /// <see cref="History"/> whenever a commit is selected.
    /// </summary>
    /// <remarks>
    /// It is a page of its own rather than markup the history page renders up front, because a
    /// chain of any length would otherwise have to replay and render every revision before the
    /// dialog could open. The frame extracts the page's main content region, so the page renders
    /// as a normal page when opened directly — which is the deep link to a single revision.
    /// </remarks>
    [WebIcon<IconClockRotateLeft>]
    [Title("kleenestar.core:object.history.commit.title")]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class HistoryDetail : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// The name of the query parameter naming the commit: its revision number or its id.
        /// </summary>
        public const string CommitParameter = "commit";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public HistoryDetail()
        {
        }

        /// <summary>
        /// Processing of the resource. The content is contributed entirely by the scoped commit
        /// fragment, so no work is required here.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
