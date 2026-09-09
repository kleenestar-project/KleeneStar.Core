using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using System.Collections.Generic;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebIndex.Queries;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The news area: the latest blog posts of the installation, newest first, each with the
    /// workspace it came from as its tag and the line saying who published it and when.
    /// </summary>
    /// <remarks>
    /// Unlike the blog overview, which shows the stream of one workspace, this reads across all
    /// of them - on the way in, what matters is what the organization has published lately,
    /// not which workspace it was published in. The workspace is named on the entry instead, so
    /// the post keeps its context.
    /// </remarks>
    internal static class LandingNewsSection
    {
        /// <summary>
        /// The maximum number of posts shown.
        /// </summary>
        private const int MaxItems = 4;

        /// <summary>
        /// The number of characters an excerpt is cut to before its trailing ellipsis.
        /// </summary>
        private const int ExcerptLength = 140;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="objectManager">The object manager used to fetch the posts.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The section element.</returns>
        public static IControl Build(IObjectManager objectManager, IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var posts = GetPosts(objectManager);

            var section = new ControlSection("landing-news")
            {
                Header = _ => "kleenestar.core:landing.news.card",
                HeaderIcon = _ => new IconComment(),
                Note = _ => "kleenestar.core:landing.news.hint",
                Layout = _ => TypeLayoutSection.Rule
            };

            if (posts.Count == 0)
            {
                section.Add(new ControlText("landing-news-empty")
                {
                    Text = _ => "kleenestar.core:landing.news.empty",
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return section;
            }

            // two per row, filling the column: a fixed tile width would leave the rest of the
            // line empty, and the entries are read as one feed rather than as separate cards
            var tiles = new ControlGroup("landing-news-tiles")
            {
                Columns = _ => 2,
                Spacing = _ => TypeSpacingGroup.Wide
            };

            foreach (var post in posts)
            {
                tiles.Add(BuildEntry(post, renderContext));
            }

            section.Add(tiles);

            return section;
        }

        /// <summary>
        /// Builds a single entry: the workspace as its tag, the summary as the title, the
        /// opening of the text, and the line naming the author and the age.
        /// </summary>
        /// <param name="post">The post to render.</param>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The entry element.</returns>
        private static IControl BuildEntry(Model.Entities.Object post, IRenderControlContext renderContext)
        {
            var id = post.Id.ToString("N");
            var panel = new ControlPanel("landing-news-" + id);

            if (!string.IsNullOrWhiteSpace(post.Workspace?.Name))
            {
                panel.Add(new ControlBadge("landing-news-tag-" + id)
                {
                    Value = _ => post.Workspace.Name,
                    BackgroundColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Primary)
                });
            }

            panel.Add(new ControlLink("landing-news-open-" + id)
            {
                Text = _ => post.Summary,
                Tooltip = _ => post.Key,
                Icon = _ => ObjectIcon.Resolve(post, new IconNewspaper()),
                Uri = _ => ObjectKindCatalog.ResolveDetailUri(post)
            });

            if (!string.IsNullOrWhiteSpace(post.Description))
            {
                panel.Add(new ControlText("landing-news-excerpt-" + id)
                {
                    Text = _ => Excerpt(post.Description),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                    Format = _ => TypeFormatText.Paragraph
                });
            }

            panel.Add(new ControlText("landing-news-meta-" + id)
            {
                Text = _ => LandingHtml.Join(Author(post), LandingHtml.Age(post.Created, renderContext)),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small
            });

            return panel;
        }

        /// <summary>
        /// Returns the name of whoever published the post.
        /// </summary>
        /// <remarks>
        /// Resolved through the identity manager rather than through <c>post.Creator</c>: the
        /// object query includes the workspace and the parent, not the creator, so the
        /// navigation property is null here. Four posts means at most four lookups.
        /// </remarks>
        /// <param name="post">The post.</param>
        /// <returns>The author's name, or <c>null</c> when the post has no creator.</returns>
        private static string Author(Model.Entities.Object post)
        {
            return post.CreatorId.HasValue
                ? CoreHub.IdentityManager?.GetIdentity(post.CreatorId.Value)?.Name
                : null;
        }

        /// <summary>
        /// Cuts a text to the length an excerpt is read at, breaking on the last word rather
        /// than mid-syllable.
        /// </summary>
        /// <param name="text">The text to cut.</param>
        /// <returns>The excerpt.</returns>
        private static string Excerpt(string text)
        {
            if (text.Length <= ExcerptLength)
            {
                return text;
            }

            var cut = text.LastIndexOf(' ', ExcerptLength);

            return string.Concat(text[..(cut > 0 ? cut : ExcerptLength)], "…");
        }

        /// <summary>
        /// Fetches the newest active blog posts of the installation.
        /// </summary>
        /// <param name="objectManager">The object manager.</param>
        /// <returns>The capped, newest-first set of posts. The list may be empty.</returns>
        private static IReadOnlyList<Model.Entities.Object> GetPosts(IObjectManager objectManager)
        {
            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Blog)
                .Where(x => x.State == Model.Entities.WorkspaceState.Active)
                .OrderByDesc(x => x.Created)
                .WithPaging(0, MaxItems);

            return [.. objectManager.GetObjects(query)];
        }
    }
}
