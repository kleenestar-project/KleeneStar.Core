using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The prose editor of the document and blog kinds: the framework's
    /// <see cref="ControlDataModalEditor"/>, configured for an object of this application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything the surface does - writing every change into an unpublished draft, publishing
    /// on submit, resuming a draft on reopen, the save state, the discard, the shared writing
    /// surface - belongs to the framework control. What is left here is what only this
    /// application knows: which two endpoints carry the two meanings of save, which row is being
    /// edited, which channel two authors of the same object share, and the one entry the
    /// overflow menu gains beside the discard.
    /// </para>
    /// <para>
    /// The record service is <see cref="WWW.Api._1_.Prose.Index"/> - its <c>GET</c> answers the
    /// draft where one exists and the published text otherwise, and its <c>PUT</c> is the
    /// publication, which ends the draft inside its own commit. The draft service is
    /// <see cref="WWW.Api._1_.Drafts._objectkey_.Index"/>, which writes no commit at all. Keeping
    /// the publication on the record endpoint is what makes an interrupted publish harmless: the
    /// draft is dropped by the same transaction that applies the text, never by the client.
    /// </para>
    /// <para>
    /// The subclasses differ in one thing only - whether the dialog opens with the page. On a
    /// reading view it waits for the edit button; on the object's own edit route it is the page.
    /// </para>
    /// </remarks>
    public abstract class ObjectProseEditorFragmentBase : ControlDataModalEditor, IFragmentControl<ControlDataModalEditor>
    {
        /// <summary>
        /// The well-known id the edit button targets. It is shared by both subclasses because a
        /// page carries exactly one of them.
        /// </summary>
        public const string ModalId = "modal-prose-editor";

        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        protected ObjectProseEditorFragmentBase(IFragmentContext fragmentContext)
            : base(ModalId)
        {
            FragmentContext = fragmentContext;

            Title.Name = _ => nameof(Model.Entities.Object.Summary);
            Title.Placeholder = _ => "kleenestar.core:object.summary.placeholder";

            Body.Name = _ => nameof(Model.Entities.Object.Description);
            Body.Placeholder = _ => "kleenestar.core:object.description.placeholder";

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Prose.Index>();
            this.DraftService<global::KleeneStar.Core.WWW.Api._1_.Drafts._objectkey_.Index>();

            ItemId = renderContext => ResolveObject(renderContext)?.Id.ToString();

            // the channel is the object rather than the page, so the two authors of one document
            // meet whether they came from the document tree or from a search result
            Collaborative = _ => true;
            CollaborationId = renderContext => "prose-" + ResolveObject(renderContext)?.Id.ToString("N");

            MoreItems.Add(new ControlDropdownItemLink("prose-editor-changes")
            {
                Text = _ => "kleenestar.core:object.draft.changes.label",
                Icon = _ => new IconCodeCompare(),

                // the comparison is read once and closed, so it opens over the editor rather
                // than replacing it - the text that is not saved yet has to survive the look
                PrimaryAction = renderContext =>
                {
                    var @object = ResolveObject(renderContext);
                    var uri = @object is null
                        ? null
                        : CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Draft>()?
                            .BindParameters(new ObjectKeyParameter(@object.Key));

                    return uri is null
                        ? null
                        : new ActionModal(ObjectDraftChangesModalFragment.ModalId, uri, TypeModalSize.Large);
                }
            });
        }

        /// <summary>
        /// Renders the editor. Returns <c>null</c> when the fragment's render conditions exclude
        /// it or when the request addresses no object - a dialog that cannot name the row it
        /// edits would load nothing and publish nowhere.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return ResolveObject(renderContext) is null
                ? null
                : base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the object the request addresses.
        /// </summary>
        /// <param name="renderContext">The render context carrying the object key.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static Model.Entities.Object ResolveObject(IRenderControlContext renderContext)
        {
            return CoreHub.ObjectManager.GetObjectByKey(renderContext?.Request?.GetParameter<ObjectKeyParameter>());
        }
    }
}
