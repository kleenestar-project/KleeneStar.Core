using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Projects an <see cref="Attachment"/> onto the shape the file surfaces need: the icon that
    /// types it, the address it is downloaded from and the version it holds among the files of
    /// the same name.
    /// </summary>
    /// <remarks>
    /// The projection is shared between the server-rendered file view of
    /// <see cref="WebFragment.Object.ObjectAttachmentCardFragment"/> and the REST endpoint that
    /// answers the same files afterwards, which is what keeps an entry that arrives through the
    /// API indistinguishable from one the page rendered into itself: both derive the icon from
    /// the same rules and both address the same download resource.
    /// </remarks>
    internal static class AttachmentProjection
    {
        /// <summary>
        /// Returns the icon that best represents the supplied content type / file name.
        /// </summary>
        /// <remarks>
        /// The icon is returned as the framework's concrete <see cref="Icon"/> rather than as
        /// <see cref="WebExpress.WebCore.WebIcon.IIcon"/>, because the REST endpoint hands the
        /// client the symbolic name instead of a rendered element.
        /// </remarks>
        /// <param name="contentType">The MIME content type, or <c>null</c>.</param>
        /// <param name="fileName">The file name, used as a fallback when the content type is
        /// unspecified.</param>
        /// <returns>The icon to display next to the file.</returns>
        public static Icon ResolveIcon(string contentType, string fileName)
        {
            var type = contentType?.ToLowerInvariant() ?? string.Empty;
            var name = fileName?.ToLowerInvariant() ?? string.Empty;

            if (type.StartsWith("image/"))
            {
                return new IconFileImage();
            }

            if (type == "application/pdf" || name.EndsWith(".pdf"))
            {
                return new IconFilePdf();
            }

            if (type.Contains("word") || name.EndsWith(".doc") || name.EndsWith(".docx"))
            {
                return new IconFileWord();
            }

            if (type.Contains("spreadsheet") || type.Contains("excel") || name.EndsWith(".xls") || name.EndsWith(".xlsx") || name.EndsWith(".csv"))
            {
                return new IconFileExcel();
            }

            if (type.Contains("zip") || name.EndsWith(".zip") || name.EndsWith(".7z") || name.EndsWith(".rar"))
            {
                return new IconFileZipper();
            }

            if (type.StartsWith("text/") || name.EndsWith(".txt") || name.EndsWith(".log"))
            {
                return new IconFileLines();
            }

            return new IconFile();
        }

        /// <summary>
        /// Builds the download URI for the supplied attachment: the binary download resource
        /// with the attachment id carried in the <c>id</c> query parameter.
        /// </summary>
        /// <param name="attachmentId">The id of the attachment to download.</param>
        /// <returns>The download URI, or <c>null</c> when the endpoint is not registered.</returns>
        public static IUri ResolveDownloadUri(Guid attachmentId)
        {
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Attachments.Download>()?
                .Add(new UriQuery("id", attachmentId.ToString()));
        }

        /// <summary>
        /// Groups the supplied attachments into files: one group per file name, holding every
        /// version of that name from the oldest to the newest.
        /// </summary>
        /// <remarks>
        /// The name is the identity of a file across its versions, so what a reader counts and
        /// pages through are the groups, not the rows: three uploads of two names are two files,
        /// one of which unfolds to its history. Grouping here rather than at each call site is
        /// what keeps the badge on the section, the total the endpoint reports and the entries the
        /// control shows counting the same thing.
        /// <para>
        /// The groups keep the chronological order of the files - a file is placed by when it
        /// first appeared, not by when it was last replaced - which is the order the attachment
        /// card has always listed in.
        /// </para>
        /// </remarks>
        /// <param name="attachments">The attachments of one object.</param>
        /// <returns>The files, each with its versions oldest first.</returns>
        public static IReadOnlyList<IReadOnlyList<Attachment>> GroupVersions(IEnumerable<Attachment> attachments)
        {
            return [.. (attachments ?? [])
                .GroupBy(x => x.FileName ?? string.Empty, StringComparer.Ordinal)
                .Select(group => (IReadOnlyList<Attachment>)[.. group.OrderBy(x => x.Version).ThenBy(x => x.Created)])
                .OrderBy(group => group.Min(x => x.Created))];
        }
    }
}
