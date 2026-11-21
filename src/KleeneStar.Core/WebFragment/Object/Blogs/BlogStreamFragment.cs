using KleeneStar.Core.WebPolicies;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Blogs
{
    /// <summary>
    /// Main-panel content of the blog overview: the workspace's posts, newest first, stacked one
    /// under the other, with a button under them that fetches the next five. The chronological
    /// navigation lives in the sidebar (<see cref="BlogSidebarTimelineFragment"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It shows five to begin with rather than the ten it used to render in one go, because a
    /// blog overview is read down rather than scanned: five posts is a page of reading, and what
    /// follows is fetched by whoever wants it instead of by everybody who opens the workspace.
    /// The posts that are already read stay on the page when more arrive - that is what separates
    /// a feed from a pager, and the reason this is
    /// <see cref="ControlDataFeed"/> rather than <c>ControlDataList</c>.
    /// </para>
    /// <para>
    /// Nothing about a post is rendered here any more. The entries come from
    /// <see cref="WWW.Api._1_.Blogs._workspacekey_.Feed"/>, which is also where the second page
    /// comes from, so the first page and every page after it are built by one implementation.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Cache]
    public sealed class BlogStreamFragment : FragmentControlPanel
    {
        /// <summary>
        /// How many posts a page of the feed holds.
        /// </summary>
        private const int PageSize = 5;

        /// <summary>
        /// Gets the feed of posts.
        /// </summary>
        public ControlDataFeed Stream { get; } = new("blog-stream")
        {
            PageSize = _ => PageSize,
            MoreLabel = _ => "kleenestar.core:object.kind.blogs.more.label",
            EmptyText = _ => "kleenestar.core:object.kind.blogs.empty",
            // the short word, not "read post": it closes a cut-off teaser rather than labelling a
            // button, and the arrow the stylesheet puts before it already says what it does
            OpenLabel = _ => "kleenestar.core:object.kind.blogs.more.inline",
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Blogs._workspacekey_.Feed>()?
                    .BindParameters(renderContext.Request)
                    .ToString()
            )
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public BlogStreamFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(Stream);
        }

        /// <summary>
        /// Renders the post stream. Returns <c>null</c> when the fragment's render conditions
        /// exclude it.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
