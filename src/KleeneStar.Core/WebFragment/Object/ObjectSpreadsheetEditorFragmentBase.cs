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
    /// The workbook editor of a document rendered as a spreadsheet: the framework's
    /// <see cref="ControlDataModalSpreadsheet"/>, configured for an object of this application.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the workbook counterpart of <see cref="ObjectProseEditorFragmentBase"/> and shares
    /// its two endpoints: the workbook travels as the object's
    /// <see cref="Model.Entities.Object.Description"/>, so <see cref="WWW.Api._1_.Prose.Index"/>
    /// loads draft-or-published and publishes on <c>PUT</c>, and
    /// <see cref="WWW.Api._1_.Drafts._objectkey_.Index"/> autosaves without a commit. Both
    /// recognise a spreadsheet document and accept only a workbook for it
    /// (<see cref="WebControl.WorkbookText"/>). Drafting, publishing, the discard, the save state
    /// and the shared surface are the framework control's.
    /// </para>
    /// <para>
    /// The subclasses differ in one thing only - whether the dialog opens with the page.
    /// </para>
    /// </remarks>
    public abstract class ObjectSpreadsheetEditorFragmentBase : ControlDataModalSpreadsheet, IFragmentControl<ControlDataModalSpreadsheet>
    {
        /// <summary>
        /// The well-known id the edit button targets. It is shared by both subclasses because a
        /// page carries exactly one of them.
        /// </summary>
        public const string ModalId = "modal-spreadsheet-editor";

        /// <summary>
        /// Gets the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        protected ObjectSpreadsheetEditorFragmentBase(IFragmentContext fragmentContext)
            : base(ModalId)
        {
            FragmentContext = fragmentContext;

            Title.Name = _ => nameof(Model.Entities.Object.Summary);
            Title.Placeholder = _ => "kleenestar.core:object.summary.placeholder";

            Body.Name = _ => nameof(Model.Entities.Object.Description);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Prose.Index>();
            this.DraftService<global::KleeneStar.Core.WWW.Api._1_.Drafts._objectkey_.Index>();

            ItemId = renderContext => ResolveObject(renderContext)?.Id.ToString();

            // the channel is the object, like the prose editor's, under a prefix of its own so a
            // class switched between the two renderers never puts a text and a grid on one channel
            Collaborative = _ => true;
            CollaborationId = renderContext => "spreadsheet-" + ResolveObject(renderContext)?.Id.ToString("N");

            MoreItems.Add(new ControlDropdownItemLink("spreadsheet-editor-changes")
            {
                Text = _ => "kleenestar.core:object.draft.changes.label",
                Icon = _ => new IconCodeCompare(),

                // opens over the editor, so the workbook that is not saved yet survives the look
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
        /// it or when the request addresses no object.
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
