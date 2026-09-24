using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The conversation of an object, docked at the foot of its page: the thread and the
    /// composer, both folded until the reader opens them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The thread used to stand in the flow of the page above the docked composer, so on a long
    /// document it sat wherever the text ended and pushed the page longer by its own height -
    /// while the composer beside it stayed in reach at the foot. Both now share one dock
    /// (<c>.ks-comment-dock</c> in <c>kleenestar.css</c>): two sticky strips of their own would
    /// cover each other. Folded, the dock is two header lines; the thread's header still says
    /// how many comments there are, and an unfolded thread scrolls inside the dock rather than
    /// taking the page with it.
    /// </para>
    /// <para>
    /// The thread is read from and the composer posts to the same endpoint,
    /// <c>/api/1/comments/{objectkey}</c>. The composer shows its <b>WYSIWYG form right away</b>
    /// rather than the framework's one-line trigger, so unfolding its section is the only
    /// gesture between reading and writing; the control offers no option for that, which is
    /// what <see cref="CommentComposerExpandScript"/> exists for.
    /// </para>
    /// </remarks>
    [Section<SectionContentSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(int.MaxValue)]
    [Cache]
    public sealed class ObjectCommentDockFragment : FragmentControlPanel
    {
        /// <summary>
        /// The name of the meta element carrying the thread's endpoint for the badge script.
        /// </summary>
        public const string CommentsMetaName = "kleenestar.comments";

        private readonly IObjectManager _objectManager;
        private readonly ICommentManager _commentManager;

        /// <summary>
        /// Gets the REST-backed thread of the object.
        /// </summary>
        public ControlDataComment Comments { get; } = new("object-comments")
        {
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Comments._objectkey_.Index>()
                    .BindParameters(renderContext.Request)
                    .ToString()
            ),
            CurrentUser = _ => "Admin User"
        };

        /// <summary>
        /// Gets the REST-backed comment composer.
        /// </summary>
        public ControlDataCommentComposer Composer { get; } = new("object-comment-composer")
        {
            // the composer would otherwise mount on its one-line trigger and only build the
            // editor once that is clicked; the class asks the companion script to open it
            Classes = [CommentComposerExpandScript.OptInClass],
            Placeholder = renderContext => I18N.Translate(renderContext, "kleenestar.core:comment.composer.placeholder"),
            ServiceFactory = renderContext => DataServiceDescriptor.QueryData
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Comments._objectkey_.Index>()
                    .BindParameters(renderContext.Request)
                    .ToString()
            ),
            CurrentUser = _ => "Admin User"
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object.</param>
        /// <param name="commentManager">The comment manager used to count the thread.</param>
        public ObjectCommentDockFragment(IFragmentContext fragmentContext, IObjectManager objectManager, ICommentManager commentManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _commentManager = commentManager;
        }

        /// <summary>
        /// Renders the dock with the thread and the composer, both folded.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>The dock, or <see langword="null"/> when the page shows no object.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var script = CommentComposerExpandScript.Value;

            if (!string.IsNullOrEmpty(script))
            {
                visualTree.AddHeaderScript(script);
            }

            // the thread arrives from the rest endpoint, but the count is cheap to read here -
            // and it is what makes the folded thread still say whether there is one. It counts
            // what the thread shows: comments and replies, not the deleted ones it redacts
            var count = _commentManager.GetComments(@object.Id).Count(x => x.State != Model.Entities.CommentState.Deleted);

            // objectcomments.js keeps the badge in step once a comment is added, answered or
            // deleted; it reads the thread from here
            visualTree.AddMeta
            (
                CommentsMetaName,
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Comments._objectkey_.Index>()
                    .BindParameters(renderContext.Request)
                    .ToString()
            );

            var thread = new ControlSection("object-comments-section")
            {
                Header = _ => "kleenestar.core:object.comments.card.header",
                HeaderIcon = _ => new IconComments(),
                Expanded = _ => false,
                Layout = _ => TypeLayoutSection.Rule,
                // shown at zero too: "no comments yet" is an answer the folded thread should give
                Badge = _ => count.ToString(CultureInfo.InvariantCulture)
            };

            thread.Add(Comments);

            var composer = new ControlSection("object-comment-composer-section")
            {
                Header = _ => "kleenestar.core:object.comment.composer.card.header",
                HeaderIcon = _ => new IconPenToSquare(),
                Expanded = _ => false,
                Layout = _ => TypeLayoutSection.Rule
            };

            composer.Add(Composer);

            var dock = new ControlPanel("object-comment-dock")
            {
                Classes = ["ks-comment-dock"]
            };

            dock.Add(thread, composer);

            return dock.Render(renderContext, visualTree);
        }
    }
}
