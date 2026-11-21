using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Blogs
{
    /// <summary>
    /// One page of the posts of every workspace the reader may see, newest first - the news of
    /// the start page (<c>LandingNewsSection</c>).
    /// </summary>
    /// <remarks>
    /// The same stream as the blog overview of a workspace (<see cref="RestApiBlogFeed"/>), read
    /// across workspaces, so the start page shows its news the way the blog overview does. Since
    /// the posts come from several workspaces, the meta line names the one each came from.
    /// Archived posts stay out, as they did on the start page before.
    /// </remarks>
    [Title("kleenestar.core:object.kind.blogs.label")]
    [Cache]
    public sealed class Feed : RestApiBlogFeed
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Feed()
        {
        }

        /// <summary>
        /// Narrows the query to the active posts of every workspace, newest first.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="request">The request.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<Model.Entities.Object> Narrow(IQuery<Model.Entities.Object> query, IRequest request)
        {
            return query
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Blog)
                .Where(x => x.State == Model.Entities.WorkspaceState.Active)
                .OrderByDesc(x => x.Created);
        }

        /// <summary>
        /// Builds the meta line of a post: the workspace it came from, then date and author.
        /// </summary>
        /// <param name="post">The post.</param>
        /// <returns>The meta line.</returns>
        protected override string BuildMeta(Model.Entities.Object post)
        {
            var workspace = CoreHub.WorkspaceManager.GetWorkspace(post.WorkspaceId)?.Name;
            var meta = base.BuildMeta(post);

            return string.IsNullOrWhiteSpace(workspace) ? meta : workspace + " · " + meta;
        }
    }
}
