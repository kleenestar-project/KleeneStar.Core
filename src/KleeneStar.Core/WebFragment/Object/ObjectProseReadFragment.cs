using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Main-panel content of the prose reading views shared by the document and blog detail
    /// pages (<see cref="WWW.Document._objectkey_.Index"/> and
    /// <see cref="WWW.Blog._objectkey_.Index"/>): the object's rich-text
    /// <see cref="Model.Entities.Object.Description"/> as a page rather than as a card, with the
    /// tags of the object closing it off underneath. The title is already shown by the page
    /// headline, so it is not repeated here. On a blog post the body is preceded by a small meta
    /// line carrying the creation date and the author.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The body is handed to <see cref="ControlContent"/> rather than emitted as raw markup.
    /// What the WYSIWYG editor stores is its whole working surface - add-on frames, column
    /// resizers, the empty paragraphs a caret needs beside a block it must not type into - and
    /// printing that verbatim shows the reader the scaffolding. The control hands the value to
    /// the client, which strips the editing apparatus and lays out the document, so one stored
    /// value serves the author and the reader instead of a second, hand-maintained
    /// representation.
    /// </para>
    /// <para>
    /// It is a reading view, not an editor: there are no inline smart-edit controls as on the
    /// issue detail. Editing is reached through <see cref="ObjectProseEditButtonFragment"/> in
    /// the headline, and what it opens is the draft, not necessarily what is shown here - an
    /// unpublished draft deliberately does not reach this view.
    /// </para>
    /// <para>
    /// It is the reading view of <em>one renderer</em>, not of the two kinds:
    /// <see cref="ProseRendererCondition"/> gates it, so a document or a post whose class
    /// renders as a form gets <see cref="ObjectFormReadFragment"/> on the same route instead.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<ProseRendererCondition>]
    [Cache]
    public sealed class ObjectProseReadFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IIdentityManager _identityManager;
        private readonly IObjectTagManager _tagManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the addressed
        /// object from the URL-bound object key.</param>
        /// <param name="identityManager">The identity manager used to resolve the blog
        /// post's author display name.</param>
        /// <param name="tagManager">The tag manager used to read the tags shown under the
        /// text.</param>
        public ObjectProseReadFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IIdentityManager identityManager,
            IObjectTagManager tagManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _identityManager = identityManager;
            _tagManager = tagManager;
        }

        /// <summary>
        /// Renders the reading view. Returns <c>null</c> when the fragment's render
        /// conditions exclude it or when no object can be resolved from the request.
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

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var id = @object.Id.ToString("N");

            // the body of a document or a post is the page, not a thing on it - it carries no
            // frame and no section label, only the reading measure the stylesheet gives it
            var body = new ControlPanel("object-prose-" + id)
            {
                Classes = ["wx-kleenestar-object-prose"]
            };

            var isBlog = string.Equals(@object.Kind, Model.Entities.ObjectKind.Blog, StringComparison.OrdinalIgnoreCase);

            if (isBlog)
            {
                body.Add(new ControlText("object-prose-meta-" + id)
                {
                    Text = _ => BuildBlogMeta(@object),
                    Format = _ => TypeFormatText.Small
                });
            }

            body.Add(new ControlContent("object-prose-body-" + id)
            {
                Content = _ => @object.Description,
                Format = _ => TypeFormatContent.RichText,

                // an empty document says so in its own body rather than through a separate
                // empty-state panel, so the page keeps the shape it will have once it is written
                Placeholder = _ => isBlog
                    ? "kleenestar.core:object.kind.blog.read.empty"
                    : "kleenestar.core:object.kind.document.read.empty",

                // the reading measure and the height the body claims are laid out in
                // kleenestar.css; the control only carries the hook
                Classes = ["ks-prose-content"]
            });

            var tags = BuildTagRow(@object, id);

            if (tags is not null)
            {
                body.Add(tags);
            }

            return body.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the row of tag badges that closes the text off, or <see langword="null"/> when
        /// the object carries no tags.
        /// </summary>
        /// <remarks>
        /// The tags sit under the text rather than in the reference column beside it, because on
        /// a document they read as what the piece is about - the last line of the article, the
        /// way a post ends on its labels - and not as one more property of a record.
        /// </remarks>
        /// <param name="object">The object whose tags are shown.</param>
        /// <param name="id">The object id, already formatted for use in element ids.</param>
        /// <returns>The tag row, or <see langword="null"/>.</returns>
        private IControl BuildTagRow(Model.Entities.Object @object, string id)
        {
            var tags = _tagManager.GetTags(@object.Id).ToList();

            if (tags.Count == 0)
            {
                return null;
            }

            var row = new ControlPanel("object-prose-tags-" + id)
            {
                Classes = ["ks-prose-tags"]
            };

            foreach (var tag in tags)
            {
                row.Add(ObjectTagBadge.Create(tag, "object-prose-tag-"));
            }

            return row;
        }

        /// <summary>
        /// Builds the blog meta line: the creation date, optionally followed by the
        /// author display name resolved from <see cref="Model.Entities.Object.CreatorId"/>.
        /// </summary>
        /// <param name="object">The blog post whose meta line is built.</param>
        /// <returns>The meta text, e.g. <c>"2026-07-12 · Jane Doe"</c>.</returns>
        private string BuildBlogMeta(Model.Entities.Object @object)
        {
            var date = @object.Created.ToString("yyyy-MM-dd");

            var author = @object.CreatorId.HasValue
                ? _identityManager.GetIdentity(@object.CreatorId.Value)?.Name
                : null;

            return string.IsNullOrWhiteSpace(author) ? date : date + " · " + author;
        }
    }
}
