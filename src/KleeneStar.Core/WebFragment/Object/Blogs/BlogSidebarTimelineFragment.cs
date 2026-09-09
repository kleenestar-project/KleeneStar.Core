using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object.Blogs
{
    /// <summary>
    /// The timeline section of the blog overview sidebar: a section header ("Blogs")
    /// followed by the workspace's posts as hierarchical sidebar links with year
    /// entries carrying month entries carrying the posts, newest first. The section
    /// sits below the kind links (Documents, Blogs, Issues), which stay ordinary flat
    /// links; every post entry links to the object detail page.
    /// </summary>
    /// <remarks>
    /// The posts are fetched once per render, newest first, capped at
    /// <see cref="MaxItems"/> (the "top 200") to keep the sidebar responsive; the
    /// year/month grouping runs in memory over the creation timestamps and the month
    /// labels follow the request culture. The header is always visible; without posts
    /// a skeleton timeline is shown instead — the current year and month carrying a
    /// disabled empty entry — so the section communicates where new posts will
    /// appear. The fragment renders header and year entries as siblings via
    /// <see cref="HtmlList"/>, so the sidebar parser picks each of them up as a
    /// regular sidebar item.
    /// </remarks>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Edit>]
    [Policy<WorkspaceViewPolicy>]
    [Order(10)]
    [Cache]
    public sealed class BlogSidebarTimelineFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// The maximum number of posts fetched for the timeline ("top 200").
        /// </summary>
        private const int MaxItems = 200;

        private readonly IObjectManager _objectManager;
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services
        /// for its operation. Cannot be null.
        /// </param>
        /// <param name="objectManager">
        /// The object manager used to retrieve the workspace posts. Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the workspace from the request. Cannot be null.
        /// </param>
        public BlogSidebarTimelineFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Renders the section: the header followed by the year entries of the
        /// timeline, or — when the workspace holds no posts — by the skeleton timeline
        /// (current year and month with a disabled empty entry). Returns <c>null</c>
        /// only when the fragment's render conditions exclude it.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c> when suppressed.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var posts = GetPosts(renderContext);
            var culture = renderContext?.Request?.Culture ?? CultureInfo.CurrentCulture;
            var currentKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>()?.Value;

            var header = new ControlSidebarItemHeader(Id + "-header")
            {
                Text = _ => "kleenestar.core:object.kind.blogs.label"
            };

            var nodes = new HtmlList(header.Render(renderContext, visualTree));

            if (posts.Count == 0)
            {
                nodes.Add(BuildSkeleton(culture).Render(renderContext, visualTree));

                return nodes;
            }

            foreach (var entry in BuildEntries(posts, culture, currentKey))
            {
                nodes.Add(entry.Render(renderContext, visualTree));
            }

            return nodes;
        }

        /// <summary>
        /// Builds the skeleton timeline shown when the workspace holds no posts: the
        /// current year carrying the current month carrying a disabled empty entry.
        /// </summary>
        /// <param name="culture">The culture used to format the year and month labels.</param>
        /// <returns>The skeleton year entry.</returns>
        private static IControlSidebarItem BuildSkeleton(CultureInfo culture)
        {
            var now = DateTime.Now;

            var empty = new ControlSidebarItemLink("blog-empty")
            {
                Text = _ => "kleenestar.core:object.kind.blogs.none.label",
                Active = _ => TypeActive.Disabled
            };

            var month = new ControlSidebarItemLink($"blog-{now.Year}-{now.Month:D2}")
            {
                Text = _ => culture.DateTimeFormat.GetMonthName(now.Month),
                Icon = _ => new IconCalendar(),
                Expanded = _ => true
            };
            month.Add(empty);

            var year = new ControlSidebarItemLink($"blog-{now.Year}")
            {
                Text = _ => now.Year.ToString(culture),
                Icon = _ => new IconCalendarDays(),
                Expanded = _ => true
            };
            year.Add(month);

            return year;
        }

        /// <summary>
        /// Builds the year → month → post entries from the fetched posts, newest first
        /// on every level. The newest year and its months start expanded so the latest
        /// posts are reachable without a click.
        /// </summary>
        /// <param name="posts">The fetched posts, newest first.</param>
        /// <param name="culture">The culture used to format the month labels.</param>
        /// <param name="currentKey">The key of the currently displayed post, or <c>null</c>.</param>
        /// <returns>The year entries carrying their month and post subtrees.</returns>
        private static IEnumerable<IControlSidebarItem> BuildEntries(IReadOnlyList<Model.Entities.Object> posts, CultureInfo culture, string currentKey)
        {
            var newestYear = posts.Count > 0 ? posts[0].Created.Year : 0;

            // the currently displayed post is highlighted as active and its year and month
            // are expanded so it is on screen
            var current = string.IsNullOrEmpty(currentKey)
                ? null
                : posts.FirstOrDefault(x => string.Equals(x.Key, currentKey, StringComparison.OrdinalIgnoreCase));
            var currentYear = current?.Created.Year;
            var currentMonth = current?.Created.Month;

            foreach (var yearGroup in posts.GroupBy(x => x.Created.Year).OrderByDescending(g => g.Key))
            {
                var year = new ControlSidebarItemLink($"blog-{yearGroup.Key}")
                {
                    Text = _ => yearGroup.Key.ToString(culture),
                    Icon = _ => new IconCalendarDays(),
                    Expanded = _ => yearGroup.Key == newestYear || yearGroup.Key == currentYear
                };

                foreach (var monthGroup in yearGroup.GroupBy(x => x.Created.Month).OrderByDescending(g => g.Key))
                {
                    var month = new ControlSidebarItemLink($"blog-{yearGroup.Key}-{monthGroup.Key:D2}")
                    {
                        Text = _ => culture.DateTimeFormat.GetMonthName(monthGroup.Key),
                        Icon = _ => new IconCalendar(),
                        Expanded = _ => yearGroup.Key == newestYear
                            || (yearGroup.Key == currentYear && monthGroup.Key == currentMonth)
                    };

                    foreach (var post in monthGroup.OrderByDescending(x => x.Created))
                    {
                        month.Add(BuildPostEntry(post, currentKey));
                    }

                    year.Add(month);
                }

                yield return year;
            }
        }

        /// <summary>
        /// Fetches the workspace's blog-kind objects, newest first and capped at
        /// <see cref="MaxItems"/>. Returns an empty list when no workspace can be
        /// resolved from the request.
        /// </summary>
        /// <param name="renderContext">The render context carrying the workspace key parameter.</param>
        /// <returns>The capped, newest-first set of posts. The list may be empty.</returns>
        private IReadOnlyList<Model.Entities.Object> GetPosts(IRenderControlContext renderContext)
        {
            var keyParameter = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            // on the blog detail/edit pages the route carries the object key, not the
            // workspace key, so fall back to the workspace of the addressed post
            if (workspace is null)
            {
                var objectParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
                workspace = _objectManager.GetObjectByKey(objectParameter?.Value)?.Workspace;
            }

            if (workspace is null)
            {
                return [];
            }

            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspace.Id)
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Blog)
                .OrderByDesc(x => x.Created)
                .WithPaging(0, MaxItems);

            return [.. _objectManager.GetObjects(query)];
        }

        /// <summary>
        /// Builds the leaf entry of a single post: the creation day and summary as
        /// label, linked to the object detail page.
        /// </summary>
        /// <param name="post">The post to render as an entry.</param>
        /// <param name="currentKey">The key of the currently displayed post, or <c>null</c>.</param>
        /// <returns>The link entry representing the post.</returns>
        private static IControlSidebarItem BuildPostEntry(Model.Entities.Object post, string currentKey)
        {
            return new ControlSidebarItemLink("post-" + post.Id.ToString("N"))
            {
                Text = _ => $"{post.Created:yyyy-MM-dd}  {post.Summary}",
                Tooltip = _ => post.Key,
                // frozen so the sidebar's request re-bind cannot repoint every entry at the
                // currently displayed post (see ResolveDetailUriFrozen)
                Uri = _ => ObjectKindCatalog.ResolveDetailUriFrozen(post),
                Icon = _ => ObjectIcon.Resolve(post, new IconBlog()),
                Active = _ => string.Equals(post.Key, currentKey, StringComparison.OrdinalIgnoreCase)
                    ? TypeActive.Active
                    : TypeActive.None
            };
        }
    }
}
