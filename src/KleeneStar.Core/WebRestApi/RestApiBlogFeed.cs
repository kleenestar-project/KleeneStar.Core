using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// One page of a post stream, newest first: the shape a <c>ControlDataFeed</c> reads - title,
    /// meta line, teaser, pictures, labels, likes and comments, read marker.
    /// </summary>
    /// <remarks>
    /// Two streams read it: the blog overview of one workspace
    /// (<c>/api/1/blogs/{workspacekey}/feed</c>) and the news of the start page, which reads
    /// across every workspace the reader may see (<c>/api/1/blogs/feed</c>). They differ only
    /// in which posts they take (<see cref="Narrow"/>) and in whether the meta line has to name
    /// the workspace (<see cref="BuildMeta"/>), so everything a post looks like lives here once
    /// and the start page shows its news exactly the way the blog overview does.
    /// <para>
    /// The result is counted (<see cref="RetrieveTotal"/>), so the feed's button disappears on the
    /// last page rather than one page later. What a reader may see is decided by
    /// <c>ObjectManager</c>, which narrows every read by permission and security level.
    /// </para>
    /// </remarks>
    public abstract partial class RestApiBlogFeed : RestApiFeed<Model.Entities.Object>
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
        /// Narrows the query to the posts of this stream, newest first.
        /// </summary>
        /// <param name="query">The query the framework built (paging, search).</param>
        /// <param name="request">The request.</param>
        /// <returns>The narrowed query, or <see langword="null"/> when the stream is empty by definition (an unknown workspace).</returns>
        protected abstract IQuery<Model.Entities.Object> Narrow(IQuery<Model.Entities.Object> query, IRequest request);

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>The query context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the posts of the stream as feed entries.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The entries.</returns>
        protected override IEnumerable<RestApiFeedItem> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var narrowed = Narrow(query, request);

            if (narrowed is null)
            {
                return [];
            }

            var posts = CoreHub.ObjectManager.GetObjects(narrowed, context);
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
        /// Counts the posts of the stream.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The number of posts, or -1 when the stream is unknown.</returns>
        protected override int RetrieveTotal(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var narrowed = Narrow(query, request);

            return narrowed is null
                ? -1
                : CoreHub.ObjectManager.GetObjects(narrowed, context).Count();
        }

        /// <summary>
        /// Builds the meta line of a post: the date and who published it.
        /// </summary>
        /// <param name="post">The post.</param>
        /// <returns>The meta line.</returns>
        protected virtual string BuildMeta(Model.Entities.Object post)
        {
            var date = post.Created.ToLocalTime().ToString("d");
            var author = post.CreatorId.HasValue
                ? CoreHub.IdentityManager.GetIdentity(post.CreatorId.Value)?.Name
                : null;

            return string.IsNullOrWhiteSpace(author) ? date : date + " · " + author;
        }

        /// <summary>
        /// Collects the pictures of a post: the ones its body shows, or the post's own icon.
        /// </summary>
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
        /// Builds the likes and the comment count of a post.
        /// </summary>
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
                    Uri = identityId == Guid.Empty ? null : CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Like>()?.ToString(),
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
        /// Takes the teaser from the text under the markup of a post.
        /// </summary>
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
