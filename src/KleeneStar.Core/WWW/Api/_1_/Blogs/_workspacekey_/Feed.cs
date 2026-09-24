using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Blogs._workspacekey_
{
    /// <summary>
    /// One page of the post stream of a workspace, newest first. It backs the feed on the blog
    /// overview, which shows the first page and appends the rest as the reader asks for them.
    /// </summary>
    /// <remarks>
    /// What a post looks like in the stream is <see cref="RestApiBlogFeed"/>'s, shared with the
    /// news of the start page (<see cref="Blogs.Feed"/>); this endpoint only says whose posts.
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
        /// Narrows the query to the posts of the addressed workspace, newest first.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="request">The request naming the workspace.</param>
        /// <returns>The narrowed query, or <see langword="null"/> for an unknown workspace.</returns>
        protected override IQuery<Model.Entities.Object> Narrow(IQuery<Model.Entities.Object> query, IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key);

            return workspace is null
                ? null
                : query
                    .WhereEquals(x => x.WorkspaceId, workspace.Id)
                    .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Blog)
                    .OrderByDesc(x => x.Created);
        }
    }
}
