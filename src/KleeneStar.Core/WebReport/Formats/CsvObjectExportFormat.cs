using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebReport.Formats
{
    /// <summary>
    /// The CSV export of an object (<see cref="CsvDocument"/>).
    /// </summary>
    /// <remarks>
    /// One header row (attribute labels, description, field names) and one value row (machine
    /// values, description text, field display texts), then a table block each for relations and
    /// comments: an empty row, a title row, a header row and one row per entry. The delimiter is
    /// <see cref="CsvDocument.DelimiterOf"/>; the dialog address carries
    /// <c>delimiter=semicolon</c> when the request culture writes the decimal separator as a comma.
    /// </remarks>
    public sealed class CsvObjectExportFormat : IObjectExportFormat
    {
        /// <inheritdoc/>
        public string Key => "csv";

        /// <inheritdoc/>
        public string Extension => "csv";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:object.export.format.csv.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:object.export.format.csv.description";

        /// <inheritdoc/>
        public string Content => "kleenestar.core:object.export.format.csv.content";

        /// <inheritdoc/>
        public string Badge => "CSV";

        /// <inheritdoc/>
        public TypeColorTile Color => TypeColorTile.Success;

        /// <inheritdoc/>
        public IIcon Icon => new IconFileCsv();

        /// <inheritdoc/>
        public int Order => 30;

        /// <inheritdoc/>
        public IEnumerable<IUriQuery> Parameters(IRequest request)
        {
            return ParametersFor(request?.Culture ?? CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns the download parameters for a culture: <c>delimiter=semicolon</c> when the
        /// culture writes the decimal separator as a comma, none otherwise.
        /// </summary>
        /// <param name="culture">The culture of the dialog submit.</param>
        /// <returns>The parameters.</returns>
        public static IEnumerable<IUriQuery> ParametersFor(CultureInfo culture)
        {
            if ((culture ?? CultureInfo.InvariantCulture).NumberFormat.NumberDecimalSeparator == ",")
            {
                yield return new UriQuery("delimiter", "semicolon");
            }
        }

        /// <inheritdoc/>
        public IResponse Write(ObjectExportRecord record, IRequest request)
        {
            return ToCsv(record, CsvDocument.DelimiterOf(request)).ToResponse(ObjectExport.FileName(record, Extension));
        }

        /// <summary>
        /// Writes the record as CSV.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <param name="delimiter">The cell delimiter.</param>
        /// <returns>The document.</returns>
        public static CsvDocument ToCsv(ObjectExportRecord record, char delimiter)
        {
            string T(string key) => I18N.Translate(record.Culture, "kleenestar.core:object.export." + key);
            var yes = I18N.Translate(record.Culture, "kleenestar.core:object.property.yes");
            var no = I18N.Translate(record.Culture, "kleenestar.core:object.property.no");

            var csv = new CsvDocument(delimiter)
                .Row(record.Attributes.Select(x => x.Label)
                    .Append(T("attribute.description"))
                    .Concat(record.Fields.Select(x => x.Name)))
                .Row(record.Attributes.Select(x => x.Value)
                    .Append(record.DescriptionText)
                    .Concat(record.Fields.Select(x => x.Text)));

            csv.Row().Row(T("section.relations"))
                .Row(T("relation.type"), T("relation.key"), T("relation.title"), T("relation.class"), T("relation.status"), T("relation.uri"));

            foreach (var relation in record.Relations)
            {
                csv.Row(relation.Type, relation.Key, relation.Title, relation.Class, relation.Status, relation.Uri);
            }

            csv.Row().Row(T("section.comments"))
                .Row(T("comment.author"), T("comment.created"), T("comment.visibility"), T("comment.reply"), T("comment.text"));

            foreach (var comment in record.Comments)
            {
                csv.Row
                (
                    comment.Author,
                    CsvDocument.Date(comment.Created),
                    comment.Internal ? T("comment.internal") : T("comment.public"),
                    comment.Reply ? yes : no,
                    comment.Text
                );
            }

            return csv;
        }
    }
}
