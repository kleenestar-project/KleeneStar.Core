using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Attachments._objectkey_
{
    /// <summary>
    /// REST endpoint backing the <c>ControlDataFileView</c> on an object's detail page. The URL
    /// is <c>/api/1/attachments/{objectkey}</c>; the <c>{objectkey}</c> segment is declared via
    /// <see cref="ObjectKeySegmentAttribute"/> so the control's data island binds it from the
    /// current request.
    /// </summary>
    /// <remarks>
    /// The endpoint is what turns the attachment card from a list the page rendered once into a
    /// surface that reflects the store: a file another user attached, or one this user just
    /// uploaded, arrives here without the page being loaded again.
    /// <para>
    /// The attachments of a single object are few, so the set is read through
    /// <see cref="IAttachmentManager"/> and narrowed in memory rather than through the reverse
    /// index - the same reasoning the relation endpoint follows.
    /// </para>
    /// <para>
    /// Paging counts <i>files</i>, not rows: the client folds the versions of one name into a
    /// single entry, so a page holds a number of files with all of their versions and the total is
    /// the number of files across every page. Paging by row would report a count nobody can see
    /// and could cut a version chain in half between two pages.
    /// </para>
    /// <para>
    /// A description edited in place arrives as <c>PUT</c> with the file named in the payload, and
    /// is persisted through <see cref="IAttachmentManager.SetDescription"/>. Only the version on
    /// display carries an editor - an earlier version is a record of what was - which the client
    /// enforces; the endpoint writes whichever row the payload names.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.attachment.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : RestApiFile<Attachment>
    {
        /// <summary>
        /// The number of files answered when the client names no page size, matching the default
        /// the file view control starts with.
        /// </summary>
        private const int DefaultPageSize = 50;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Returns the page of files the client asked for, of the object the route addresses.
        /// Every version of a file on the page travels with it, so the client can fold the chain.
        /// </summary>
        /// <param name="query">The query criteria supplied by the control. Unused - the set is
        /// the attachments of one object and is narrowed in memory.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The files of the requested page, versions included.</returns>
        protected override IEnumerable<RestApiFileItem> RetrieveItems(IQuery<Attachment> query, IQueryContext context, IRequest request)
        {
            var pageNumber = Math.Max(ParseInt(request, "p", 0), 0);
            var pageSize = ParseInt(request, "l", DefaultPageSize);

            if (pageSize <= 0)
            {
                pageSize = DefaultPageSize;
            }

            return AttachmentProjection.GroupVersions(Match(request))
                .Skip(pageNumber * pageSize)
                .Take(pageSize)
                .SelectMany(file => file)
                .Select(x => ToItem(x, request))
                .ToList();
        }

        /// <summary>
        /// Returns how many files the object holds after the search was applied, across every
        /// page. Versions of one file count once, because that is what the reader sees.
        /// </summary>
        /// <param name="query">The query the page was taken from.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The total.</returns>
        protected override int? RetrieveTotal(IQuery<Attachment> query, IRequest request)
        {
            return AttachmentProjection.GroupVersions(Match(request)).Count;
        }

        /// <summary>
        /// Persists a description that was edited in place in the file view.
        /// </summary>
        /// <remarks>
        /// A payload naming a file that is not attached to the object the route addresses is
        /// refused, so the endpoint of one object cannot be used to caption another's files. The
        /// refusal is raised rather than returned because the base class owns the response: it
        /// turns an exception out of here into a bad request, which is what an unusable payload
        /// deserves - answering 200 would tell the client an edit was stored that never was.
        /// </remarks>
        /// <param name="id">The id of the attachment whose description changed.</param>
        /// <param name="description">The new description.</param>
        /// <param name="request">The incoming request.</param>
        /// <exception cref="InvalidOperationException">The payload names no attachment of this
        /// object.</exception>
        protected override void UpdateDescription(string id, string description, IRequest request)
        {
            if (!Guid.TryParse(id, out var attachmentId) || !Attachments(request).Any(x => x.Id == attachmentId))
            {
                throw new InvalidOperationException($"'{id}' is not a file of this object.");
            }

            // the framework's entry point is not virtual; a refusal travels as the failure it
            // already reports
            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(ResolveObject(request), request, typeof(global::KleeneStar.Core.WebPermissions.ObjectAttachPermission)))
            {
                throw new UnauthorizedAccessException("The files of this object may be changed by those who may attach to it only.");
            }

            CoreHub.AttachmentManager.SetDescription(attachmentId, description);
        }

        /// <summary>
        /// Returns the attachments of the addressed object that satisfy the search, oldest
        /// first.
        /// </summary>
        /// <remarks>
        /// The search covers the file name and the description, which is everything the entry
        /// shows as text; searching the payload would be the reverse index's job and is not what
        /// the box above a file list promises.
        /// </remarks>
        /// <param name="request">The incoming request.</param>
        /// <returns>The matching attachments. The list may be empty.</returns>
        private static List<Attachment> Match(IRequest request)
        {
            var attachments = Attachments(request);
            var search = request?.GetParameter("q")?.Value?.Trim();

            if (string.IsNullOrEmpty(search))
            {
                return attachments;
            }

            return attachments
                .Where(x => Contains(x.FileName, search) || Contains(x.Description, search))
                .ToList();
        }

        /// <summary>
        /// Returns every attachment of the addressed object, oldest first and unfiltered.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The attachments. The list may be empty.</returns>
        private static List<Attachment> Attachments(IRequest request)
        {
            var @object = ResolveObject(request);

            return @object is null
                ? []
                : CoreHub.AttachmentManager.GetAttachments(@object.Id).ToList();
        }

        /// <summary>
        /// Projects an attachment onto the file the control renders.
        /// </summary>
        /// <param name="attachment">The attachment.</param>
        /// <param name="request">The incoming request, which carries the culture the size and
        /// the date are formatted in.</param>
        /// <returns>The file.</returns>
        private static RestApiFileItem ToItem(Attachment attachment, IRequest request)
        {
            return new RestApiFileItem
            {
                Id = attachment.Id.ToString(),
                Name = attachment.FileName,
                Version = attachment.Version,
                Uri = AttachmentProjection.ResolveDownloadUri(attachment.Id)?.ToString(),

                // the client resolves the symbolic name against the active icon set, so the
                // page's set stays the one authority on what a document looks like
                Icon = AttachmentProjection.ResolveIcon(attachment.ContentType, attachment.FileName).Symbol,
                Size = FormatSize(attachment.Size, request),
                Date = FormatDate(attachment.Created, request),
                Description = attachment.Description
            };
        }

        /// <summary>
        /// Resolves the object the route addresses.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The object, or <see langword="null"/> when the route names none.</returns>
        private static ObjectEntity ResolveObject(IRequest request)
        {
            return CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);
        }

        /// <summary>
        /// Determines whether the supplied text contains the search term, ignoring case.
        /// </summary>
        /// <param name="text">The text to search, may be absent.</param>
        /// <param name="search">The search term.</param>
        /// <returns><see langword="true"/> when the term occurs in the text.</returns>
        private static bool Contains(string text, string search)
        {
            return text is not null && text.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads an integer query parameter, falling back when it is missing or not a number.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="defaultValue">The value used when the parameter says nothing.</param>
        /// <returns>The parsed value, or the fallback.</returns>
        private static int ParseInt(IRequest request, string name, int defaultValue)
        {
            return int.TryParse(request?.GetParameter(name)?.Value, out var value) ? value : defaultValue;
        }
    }
}
