using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The news area: the latest blog posts of the installation, newest first, shown the way
    /// the blog overview of a workspace shows its posts.
    /// </summary>
    /// <remarks>
    /// Unlike the blog overview, which shows the stream of one workspace, this reads across all
    /// of them - on the way in, what matters is what the organization has published lately,
    /// not which workspace it was published in. The workspace is named in the meta line of each
    /// entry instead, so the post keeps its context.
    /// <para>
    /// It is the same control over the same shape of data as the blog overview
    /// (<c>ControlDataFeed</c> over <see cref="WebRestApi.RestApiBlogFeed"/>, here
    /// <c>/api/1/blogs/feed</c>): teaser, pictures, labels, likes and comments, the read marker
    /// and a button that appends the next posts. A reader meets a post in one form, wherever
    /// they meet it.
    /// </para>
    /// </remarks>
    internal static class LandingNewsSection
    {
        /// <summary>
        /// The number of posts shown before the feed offers more.
        /// </summary>
        private const int PageSize = 3;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The section.</returns>
        public static IControl Build(IRenderControlContext renderContext)
        {
            var section = new ControlSection("landing-news")
            {
                Header = _ => "kleenestar.core:landing.news.card",
                HeaderIcon = _ => new IconNewspaper(),
                Note = _ => "kleenestar.core:landing.news.hint",
                Layout = _ => TypeLayoutSection.Rule
            };

            section.Add(new ControlDataFeed("landing-news-feed")
            {
                PageSize = _ => PageSize,
                MoreLabel = _ => "kleenestar.core:object.kind.blogs.more.label",
                EmptyText = _ => "kleenestar.core:landing.news.empty",
                OpenLabel = _ => "kleenestar.core:object.kind.blogs.more.inline",
                ServiceFactory = _ => DataServiceDescriptor.QueryData
                (
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Blogs.Feed>()?.ToString()
                )
            });

            return section;
        }
    }
}
