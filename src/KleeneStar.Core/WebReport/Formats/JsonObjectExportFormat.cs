using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebReport.Formats
{
    /// <summary>
    /// The JSON export of an object: UTF-8, indented, camel case.
    /// </summary>
    /// <remarks>
    /// Members: the system attributes as machine values (keys of
    /// <see cref="ObjectExportAttribute.Key"/>), <c>description</c> (plain text),
    /// <c>fields</c> (<c>name</c>, <c>type</c>, <c>value</c>, <c>text</c>), <c>relations</c>,
    /// <c>comments</c> and <c>exported</c>. Non-ASCII text is written unescaped.
    /// </remarks>
    public sealed class JsonObjectExportFormat : IObjectExportFormat
    {
        /// <summary>
        /// The serializer options.
        /// </summary>
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        /// <inheritdoc/>
        public string Key => "json";

        /// <inheritdoc/>
        public string Extension => "json";

        /// <inheritdoc/>
        public string Label => "kleenestar.core:object.export.format.json.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.core:object.export.format.json.description";

        /// <inheritdoc/>
        public string Content => "kleenestar.core:object.export.format.json.content";

        /// <inheritdoc/>
        public string Badge => "JSON";

        /// <inheritdoc/>
        public TypeColorTile Color => TypeColorTile.Primary;

        /// <inheritdoc/>
        public IIcon Icon => new IconFileCode();

        /// <inheritdoc/>
        public int Order => 20;

        /// <inheritdoc/>
        public IResponse Write(ObjectExportRecord record, IRequest request)
        {
            return ObjectExport.Attachment(ToJson(record), "application/json; charset=utf-8", ObjectExport.FileName(record, Extension));
        }

        /// <summary>
        /// Writes the record as JSON.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <returns>The UTF-8 bytes.</returns>
        public static byte[] ToJson(ObjectExportRecord record)
        {
            var payload = record.Attributes.ToDictionary(x => x.Key, x => (object)x.Value);

            payload["description"] = record.DescriptionText;
            payload["fields"] = record.Fields.Select(x => new
            {
                name = x.Name,
                type = x.Type.ToString(),
                value = x.Value,
                text = x.Text
            });
            payload["relations"] = record.Relations.Select(x => new
            {
                type = x.Type,
                key = x.Key,
                title = x.Title,
                @class = x.Class,
                status = x.Status,
                uri = x.Uri
            });
            payload["comments"] = record.Comments.Select(x => new
            {
                author = x.Author,
                created = CsvDocument.Date(x.Created),
                @internal = x.Internal,
                reply = x.Reply,
                text = x.Text
            });
            payload["exported"] = CsvDocument.Date(record.Exported);

            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize<IDictionary<string, object>>(payload, _options));
        }
    }
}
