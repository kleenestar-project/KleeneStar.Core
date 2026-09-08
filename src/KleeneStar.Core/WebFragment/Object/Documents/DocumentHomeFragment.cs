using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using System;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebFragment.Object.Documents
{
    /// <summary>
    /// Main-panel content of the document overview: the workspace's home document, read as a
    /// page. The tree itself lives in the sidebar (<see cref="DocumentSidebarTreeFragment"/>), so
    /// the overview opens like a wiki space: navigation on the left, the home page in the middle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Which document that is belongs to <see cref="IWorkspaceManager.GetHome"/>: the one chosen
    /// through the document's own more menu, and failing that the first root of the page tree by
    /// summary.
    /// </para>
    /// <para>
    /// <b>It is the page, not a card on it.</b> A card frames a preview of something that lives
    /// elsewhere; the home document is what the reader came for, so it is laid out the way the
    /// document's own detail view lays it out (<see cref="ObjectProseReadFragment"/>) - the same
    /// <see cref="ControlContent"/>, the same reading measure. The body is handed to that control
    /// rather than emitted as raw markup because what the WYSIWYG editor stores is its whole
    /// working surface, and printing that verbatim shows the reader the scaffolding.
    /// </para>
    /// <para>
    /// The title is repeated here even though the detail view leaves it to the page headline: the
    /// headline of this page says "Documents", so without it the reader would not know which
    /// document they are looking at.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Policy<WorkspaceViewPolicy>]
    [Cache]
    public sealed class DocumentHomeFragment : FragmentControlPanel
    {
        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="workspaceManager">The workspace manager used to resolve the workspace from the request.</param>
        public DocumentHomeFragment(IFragmentContext fragmentContext, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Renders the home document. Returns <c>null</c> when the fragment's render
        /// conditions exclude it or when no workspace can be resolved from the request.
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

            var keyParameter = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            if (workspace is null)
            {
                return null;
            }

            var home = _workspaceManager.GetHome(workspace.Id);

            if (home is null)
            {
                var empty = new ControlText("document-home-empty")
                {
                    Text = _ => "kleenestar.core:object.kind.documents.empty",
                    Format = _ => TypeFormatText.Paragraph
                };

                return empty.Render(renderContext, visualTree);
            }

            var id = home.Id.ToString("N");

            // the same shell the document's own reading view uses, so the home page and the page
            // it links to are laid out identically
            var body = new ControlPanel("document-home-" + id)
            {
                Classes = ["wx-kleenestar-object-prose"]
            };

            body.Add(new ControlText("document-home-title-" + id)
            {
                Text = _ => home.Summary,
                Format = _ => TypeFormatText.H2
            });

            body.Add(new ControlContent("document-home-body-" + id)
            {
                Content = _ => home.Description,
                Format = _ => TypeFormatContent.RichText,

                // an empty document says so in its own body rather than through a separate
                // empty-state panel, so the page keeps the shape it will have once it is written
                Placeholder = _ => "kleenestar.core:object.kind.document.read.empty",

                // the reading measure and the height the body claims are laid out in
                // kleenestar.css; the control only carries the hook
                Classes = ["ks-prose-content"]
            });

            body.Add(BuildMetrics(renderContext, home, id));

            return body.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the figures that close the page off: how many liked the document, and how many
        /// commented on it.
        /// </summary>
        /// <remarks>
        /// The like carries an address, so the reader can join it from here rather than only read
        /// the number - see <see cref="ControlLike"/> for why a reader who is not signed in gets
        /// the figure without one. The comment count is a readout and stays one: commenting
        /// happens on the document itself, where the thread is.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <param name="home">The home document.</param>
        /// <param name="id">The document id, already formatted for use in element ids.</param>
        /// <returns>The row of figures.</returns>
        private static IControl BuildMetrics(IRenderControlContext renderContext, ObjectEntity home, string id)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request);

            var like = new ControlLike("document-home-likes-" + id)
            {
                Value = _ => CoreHub.ObjectManager.GetLikeCount(home.Id),
                Active = _ => CoreHub.ObjectManager.IsLiked(identityId, home.Id),
                Label = _ => "kleenestar.core:object.kind.documents.metric.likes",

                // no address for a reader who is not signed in: a like belongs to somebody, and
                // offering the click only to answer 401 is worse than not offering it
                Uri = _ => identityId == Guid.Empty
                    ? null
                    : CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Like>(),
                Payload = _ => System.Text.Json.JsonSerializer.Serialize(new { @object = home.Key })
            };

            var comments = new ControlPanelFlex
            (
                "document-home-comments-" + id,
                new ControlText
                {
                    Text = _ => CoreHub.CommentManager.GetComments(home.Id).Count().ToString()
                },
                new ControlIcon
                {
                    Icon = _ => new IconComment()
                }
            )
            {
                Classes = ["ks-object-metric"]
            };

            return new ControlPanelFlex("document-home-metrics-" + id, like, comments)
            {
                Classes = ["ks-object-metrics"]
            };
        }
    }
}
