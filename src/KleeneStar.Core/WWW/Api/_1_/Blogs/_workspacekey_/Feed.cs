using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Blogs._workspacekey_
{
    /// <summary>
    /// One page of the post stream of a workspace, newest first. It backs the feed on the blog
    /// overview, which shows the first page and appends the rest as the reader asks for them.
    /// </summary>
    /// <remarks>
    /// The result is counted (<see cref="RetrieveTotal"/>), so the feed's button disappears on the
    /// last page rather than one page later. Counting a workspace's posts is a query the index
    /// answers cheaply; an endpoint over something expensive would leave it and accept the extra
    /// click.
    /// </remarks>
    [Title("kleenestar.core:object.kind.blogs.label")]
    [Cache]
    public sealed partial class Feed : RestApiFeed<Model.Entities.Object>
    {
        /// <summary>
        /// How many characters of a post the teaser shows.
        /// </summary>
        private const int TeaserLength = 320;

        /// <summary>
        /// How many of a post's pictures the slideshow carries. A teaser is a glance, not a
        /// gallery.
        /// </summary>
        private const int MaxImages = 5;

        /// <summary>
        /// Matches the source of a picture in the body of a post.
        /// </summary>
        [GeneratedRegex("<img\\b[^>]*?\\bsrc\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex ImageRegex();

        /// <summary>
        /// Matches any tag, so the teaser can be taken from the text under the markup.
        /// </summary>
        [GeneratedRegex("<[^>]*>")]
        private static partial Regex TagRegex();

        /// <summary>
        /// Matches a run of whitespace, which is what dropping the tags leaves behind.
        /// </summary>
        [GeneratedRegex("\\s+")]
        private static partial Regex WhitespaceRegex();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Feed()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>The query context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the posts of the addressed workspace, newest first.
        /// </summary>
        /// <param name="query">The query criteria, already narrowed to the requested page.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request whose route names the workspace.</param>
        /// <returns>The entries of the page.</returns>
        protected override IEnumerable<RestApiFeedItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var workspace = ResolveWorkspace(request);

            if (workspace is null)
            {
                return [];
            }

            var posts = CoreHub.ObjectManager.GetObjects(Narrow(query, workspace.Id), context);
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            return [.. posts.Select(x => new RestApiFeedItem
            {
                Id = x.Id.ToString(),
                Title = x.Summary,
                Meta = BuildMeta(x),
                Text = BuildTeaser(x.Description),
                Images = BuildImages(x),
                Tags = CoreHub.ObjectTagManager.GetTags(x.Id).Select(tag => tag.Name).Where(name => !string.IsNullOrWhiteSpace(name)),
                Metrics = BuildMetrics(x, identityId),

                // nobody signed in has read nothing rather than everything: the marker says what
                // is new to *this* reader, and there is no reader to speak of
                Read = identityId == Guid.Empty ? null : CoreHub.ObjectManager.IsRead(identityId, x.Id),
                Uri = ObjectKindCatalog.ResolveDetailUri(x)?.ToString()
            })];
        }

        /// <summary>
        /// Returns how many posts the workspace holds, so the feed knows when it has shown them
        /// all.
        /// </summary>
        /// <param name="query">The filtered query, without paging applied.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request whose route names the workspace.</param>
        /// <returns>The number of posts, or -1 when the workspace cannot be resolved.</returns>
        protected override int RetrieveTotal(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var workspace = ResolveWorkspace(request);

            return workspace is null
                ? -1
                : CoreHub.ObjectManager.GetObjects(Narrow(query, workspace.Id), context).Count();
        }

        /// <summary>
        /// Narrows a query to the posts of one workspace, newest first.
        /// </summary>
        /// <param name="query">The query to narrow.</param>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The narrowed query.</returns>
        private static IQuery<Model.Entities.Object> Narrow(IQuery<Model.Entities.Object> query, System.Guid workspaceId)
        {
            return query
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Blog)
                .OrderByDesc(x => x.Created);
        }

        /// <summary>
        /// Resolves the workspace the route addresses.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        private static Model.Entities.Workspace ResolveWorkspace(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return CoreHub.WorkspaceManager.GetWorkspaceByKey(key);
        }

        /// <summary>
        /// Builds the line under the heading: when the post is from, and who wrote it.
        /// </summary>
        /// <param name="post">The post.</param>
        /// <returns>The meta line.</returns>
        private static string BuildMeta(Model.Entities.Object post)
        {
            var date = post.Created.ToLocalTime().ToString("d");
            var author = post.CreatorId.HasValue
                ? CoreHub.IdentityManager.GetIdentity(post.CreatorId.Value)?.Name
                : null;

            return string.IsNullOrWhiteSpace(author) ? date : date + " · " + author;
        }

        /// <summary>
        /// Returns the pictures the teaser shows: the ones in the post itself.
        /// </summary>
        /// <remarks>
        /// A post illustrates itself, so the teaser is illustrated with what the author put in it
        /// rather than with a thumbnail invented here. Several pictures make the teaser a
        /// slideshow, which is the control's decision - this only reports what the post has. A
        /// post with none falls back to the icon of its class, so the row of teasers keeps its
        /// shape instead of alternating between two layouts.
        /// </remarks>
        /// <param name="post">The post.</param>
        /// <returns>The addresses of the pictures.</returns>
        private static IEnumerable<string> BuildImages(Model.Entities.Object post)
        {
            var images = ImageRegex().Matches(ProseText.ToHtml(post.Description) ?? string.Empty)
                .Select(x => x.Groups[1].Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(MaxImages)
                .ToList();

            if (images.Count > 0)
            {
                return images;
            }

            var icon = post.Icon?.Uri?.ToString();

            return string.IsNullOrWhiteSpace(icon) ? [] : [icon];
        }

        /// <summary>
        /// Builds the figures at the foot of a teaser: how many liked the post, and how many
        /// replied to it.
        /// </summary>
        /// <remarks>
        /// The like carries an address, so the reader can join it from the feed rather than only
        /// read the number - which is what the figure was before there was anything to click. It
        /// counts likes of the <b>post</b>; summing the likes of its comments, which is what it
        /// counted while objects could not be liked, says how lively the discussion was and not
        /// what the post was worth.
        /// </remarks>
        /// <param name="post">The post.</param>
        /// <param name="identityId">The reader, or empty when nobody is signed in.</param>
        /// <returns>The figures.</returns>
        private static IEnumerable<RestApiFeedMetric> BuildMetrics(Model.Entities.Object post, Guid identityId)
        {
            return
            [
                new RestApiFeedMetric
                {
                    Icon = new IconThumbsUp().Class,
                    Value = CoreHub.ObjectManager.GetLikeCount(post.Id).ToString(),
                    Label = I18N.Translate("kleenestar.core:object.kind.blogs.metric.likes"),

                    // no address for a reader who is not signed in: a like belongs to somebody,
                    // and offering the click only to answer 401 is worse than not offering it
                    Uri = identityId == Guid.Empty ? null : CoreHub.GetUri<Api._1_.Objects.Like>()?.ToString(),
                    Payload = JsonSerializer.Serialize(new { @object = post.Key }),
                    Active = identityId != Guid.Empty && CoreHub.ObjectManager.IsLiked(identityId, post.Id)
                },
                new RestApiFeedMetric
                {
                    Icon = new IconComment().Class,
                    Value = CoreHub.CommentManager.GetComments(post.Id).Count().ToString(),
                    Label = I18N.Translate("kleenestar.core:object.kind.blogs.metric.comments")
                }
            ];
        }

        /// <summary>
        /// Shortens the body of a post to the passage the teaser shows.
        /// </summary>
        /// <remarks>
        /// The markup is dropped rather than truncated: cutting rich text at a character count
        /// leaves an unclosed tag, and the teaser is a passage of prose rather than a small copy
        /// of the page. The cut is made at the last word that fits, so the ellipsis follows a word
        /// and not half of one.
        /// </remarks>
        /// <param name="description">The stored body of the post.</param>
        /// <returns>The teaser.</returns>
        private static string BuildTeaser(string description)
        {
            var text = TagRegex().Replace(ProseText.ToHtml(description) ?? string.Empty, " ")
                .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);

            text = WhitespaceRegex().Replace(text, " ").Trim();

            if (text.Length <= TeaserLength)
            {
                return text;
            }

            var cut = text.LastIndexOf(' ', TeaserLength);

            return string.Concat(text.AsSpan(0, cut > 0 ? cut : TeaserLength).TrimEnd(), "…");
        }
    }
}
