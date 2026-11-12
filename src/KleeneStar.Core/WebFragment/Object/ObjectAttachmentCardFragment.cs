using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The attachment section of the object view: the files attached to the object currently
    /// displayed on <see cref="WWW.Issue._objectkey_.Index"/> and offers a drag-and-drop
    /// upload zone for adding new ones.
    /// </summary>
    /// <remarks>
    /// The section hosts a <see cref="ControlDataFileView"/> followed by a
    /// <see cref="ControlUpload"/>. The view offers the same set of files in two presentations -
    /// the tabular list and the tile board - and reads them from
    /// <see cref="WWW.Api._1_.Attachments._objectkey_.Index"/>, so a file another user attached
    /// shows up without the page being loaded again. The attachments the manager already knows
    /// are rendered into the control as well; they are what the section shows until the first
    /// response arrives, which keeps the card populated rather than empty on first paint.
    /// <para>
    /// The upload posts back to the object's own page route; its
    /// <see cref="ControlUpload.Process(System.Action{ControlFormEventItemProcess{ControlFormInputValueFile}})"/>
    /// handler persists the attachment through the manager. The view follows the upload through
    /// a <see cref="BindUpload"/>, so a finished upload appears in both presentations at once
    /// instead of waiting for a reload.
    /// </para>
    /// <para>
    /// Two things go beyond listing the files. A name that is already attached is stored as the
    /// <b>next version</b> of that file rather than as a second file, and the surface shows the
    /// chain as one entry that unfolds to its history - see <see cref="Attachment.Version"/>. And
    /// the <b>description of a file is edited in place</b>: the change travels to
    /// <see cref="WWW.Api._1_.Attachments._objectkey_.Index"/> as a <c>PUT</c> naming the file, so
    /// captioning a document costs neither a dialog nor a page load.
    /// </para>
    /// </remarks>
    [Section<SectionContentSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Attachments>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Order(0)]
    [Cache]
    public sealed class ObjectAttachmentCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// The id of the upload control. The file view names it in its upload bind, so the two
        /// have to agree on it - a literal spelled twice would drift silently.
        /// </summary>
        private const string UploadId = "object-attachment-upload";

        private readonly IObjectManager _objectManager;
        private readonly IAttachmentManager _attachmentManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current
        /// object from the URL-bound object key.</param>
        /// <param name="attachmentManager">The attachment manager used to read and persist
        /// the object's files.</param>
        public ObjectAttachmentCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IAttachmentManager attachmentManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _attachmentManager = attachmentManager;
        }

        /// <summary>
        /// Renders the attachment card for the current object. Returns <c>null</c> when the
        /// fragment's render conditions exclude it or when no object can be resolved from
        /// the request.
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

            // the count rides in the header so a folded section still answers the only question
            // a reader has about it - is there anything in here - without being unfolded. It
            // counts files rather than rows, because a file uploaded three times is one file with
            // a history and that is what the unfolded section shows
            var files = AttachmentProjection.GroupVersions(_attachmentManager.GetAttachments(@object.Id));
            var count = files.Count;

            var section = new ControlSection("object-attachment-section")
            {
                Header = _ => "kleenestar.core:object.attachment.card.header",
                HeaderIcon = _ => new IconPaperClip(),
                Layout = _ => TypeLayoutSection.Rule,
                Badge = count > 0 ? _ => count.ToString(CultureInfo.InvariantCulture) : null
            };

            section.Add(BuildFileView(files));
            section.Add(BuildUpload(@object, renderContext));

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the file view of the object's existing attachments: the list and the tile
        /// presentation of one set of files, backed by the attachment endpoint and following the
        /// upload control.
        /// </summary>
        /// <remarks>
        /// The control is built per render rather than held as a property, because the files it
        /// is seeded with are added to the instance - a shared one would accumulate the
        /// attachments of every object that was ever displayed.
        /// <para>
        /// The seeded entries carry the plain attachment id, which is the id the endpoint
        /// answers with, so the entry the page rendered and the entry the response carries are
        /// the same file rather than two. Every version is seeded, each with its stored number,
        /// so the control folds a chain into one entry on the first paint rather than showing the
        /// same name several times until the endpoint answers.
        /// </para>
        /// </remarks>
        /// <param name="files">The object's attachments, grouped into files.</param>
        /// <returns>The configured file view control.</returns>
        private static ControlDataFileView BuildFileView(IReadOnlyList<IReadOnlyList<Attachment>> files)
        {
            var fileView = new ControlDataFileView("object-attachment-list")
            {
                ServiceFactory = renderContext => DataServiceDescriptor
                    .ListData(ResolveDataUri(renderContext)?.ToString())

                    // the caption of a file is edited in place and written back against the same
                    // address, which is the shape RestApiFile answers on
                    .WithUpdateMethod("PUT")

                    // without a declared domain the view never learns of a file somebody else
                    // attached; the endpoint's own type says nothing about what it serves
                    .WithDomain(DataChangedNotifier.DomainName(typeof(Attachment))),

                // the description is the one piece of a file a reader adds themselves, and
                // leaving the card to change it would cost more than the change is worth
                EditableDescription = _ => true,

                // the upload is the reader's business, not the upload control's: the control
                // stays a plain upload zone and this view listens to it
                Bind = _ => new Binding().Add(new BindUpload { Source = UploadId })
            };

            fileView.Add(files.SelectMany(file => file).Select(a => new ControlFileListItem(a.Id.ToString())
            {
                Icon = _ => AttachmentProjection.ResolveIcon(a.ContentType, a.FileName),
                Name = _ => a.FileName,
                Size = _ => a.Size,
                Date = _ => a.Created,
                Version = _ => a.Version,
                Description = _ => a.Description,
                Uri = _ => AttachmentProjection.ResolveDownloadUri(a.Id)
            }));

            return fileView;
        }

        /// <summary>
        /// Builds the drag-and-drop upload control. The control posts back to the object's
        /// page route and persists every selected file through the attachment manager when
        /// the form is processed.
        /// </summary>
        /// <param name="object">The object the uploaded files are attached to.</param>
        /// <param name="renderContext">The current render context; used to bind the upload
        /// URI to the active request's route parameters.</param>
        /// <returns>The configured upload control.</returns>
        private ControlUpload BuildUpload(Model.Entities.Object @object, IRenderControlContext renderContext)
        {
            var uploadUri = ResolveUploadUri(renderContext);
            var objectId = @object.Id;

            var upload = new ControlUpload(UploadId)
            {
                Uri = _ => uploadUri,
                Multiple = _ => true,
                AutoUpload = _ => true,
                Placeholder = _ => "kleenestar.core:object.attachment.upload.placeholder",
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Three, PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.None)
            };

            upload.Process(eventArgument => PersistUpload(objectId, eventArgument.Value));

            return upload;
        }

        /// <summary>
        /// Persists a single uploaded file: writes the binary payload below the application
        /// data directory and stores the attachment metadata through the manager. Failures
        /// are swallowed so a single bad file does not break the page render.
        /// </summary>
        /// <param name="objectId">The id of the object the file is attached to.</param>
        /// <param name="value">The uploaded file value (name, content type and bytes).</param>
        private void PersistUpload(Guid objectId, ControlFormInputValueFile value)
        {
            if (value is null || string.IsNullOrWhiteSpace(value.Name))
            {
                return;
            }

            try
            {
                _attachmentManager.Add
                (
                    objectId,
                    value.Name,
                    value.ContentType.ToString(),
                    value.Data ?? [],
                    description: null,
                    uploaderId: null
                );
            }
            catch
            {
                // upload is best-effort per file; ignore failures from a single bad payload
            }
        }

        /// <summary>
        /// Resolves the URI the file view reads its files from: the attachment endpoint bound to
        /// the active request, so the object-key segment carries the object on display.
        /// </summary>
        /// <param name="renderContext">The current render context.</param>
        /// <returns>The bound endpoint URI, or <c>null</c> when it is not registered.</returns>
        private static IUri ResolveDataUri(IRenderControlContext renderContext)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Attachments._objectkey_.Index>();

            if (uri is null)
            {
                return null;
            }

            return renderContext?.Request is null
                ? uri
                : uri.BindParameters(renderContext.Request);
        }

        /// <summary>
        /// Resolves the URI the upload control posts to: the object's own page route bound
        /// to the active request so the object-key segment is preserved.
        /// </summary>
        /// <param name="renderContext">The current render context.</param>
        /// <returns>The bound upload URI, or <c>null</c> when the page is not registered.</returns>
        private static IUri ResolveUploadUri(IRenderControlContext renderContext)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>();
            if (uri is null)
            {
                return null;
            }

            return renderContext?.Request is null
                ? uri
                : uri.BindParameters(renderContext.Request);
        }
    }
}
