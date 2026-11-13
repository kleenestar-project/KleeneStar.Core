using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebSpreadsheet;

namespace KleeneStar.Core.WebReport
{
    using Field = KleeneStar.Model.Entities.Field;
    using ObjectEntity = KleeneStar.Model.Entities.Object;

    /// <summary>
    /// A system attribute of an exported object: a stable key, the translated label, the
    /// machine value and the display text.
    /// </summary>
    /// <param name="Key">The key used as JSON property name.</param>
    /// <param name="Label">The translated label (PDF row, CSV header).</param>
    /// <param name="Value">The machine value (JSON, CSV): dates in ISO 8601 UTC, or <see langword="null"/>.</param>
    /// <param name="Text">The display text (PDF): dates in the request culture, or <see langword="null"/>.</param>
    public sealed record ObjectExportAttribute(string Key, string Label, string Value, string Text);

    /// <summary>
    /// A field value of an exported object.
    /// </summary>
    /// <param name="Name">The field name.</param>
    /// <param name="Type">The field type.</param>
    /// <param name="Value">The stored value; <see langword="null"/> for an empty field and for a password.</param>
    /// <param name="Text">The display text (<see cref="ObjectValueFormat"/>); prose as plain text.</param>
    public sealed record ObjectExportField(string Name, FieldType Type, string Value, string Text);

    /// <summary>
    /// The content of an object export, collected once and written in every format.
    /// </summary>
    public sealed class ObjectExportRecord
    {
        /// <summary>
        /// Gets the object key.
        /// </summary>
        public string Key { get; init; }

        /// <summary>
        /// Gets the summary.
        /// </summary>
        public string Summary { get; init; }

        /// <summary>
        /// Gets the system attributes in display order.
        /// </summary>
        public IReadOnlyList<ObjectExportAttribute> Attributes { get; init; } = [];

        /// <summary>
        /// Gets the description as stored (editor document, workbook or plain text).
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the description as plain text.
        /// </summary>
        public string DescriptionText { get; init; }

        /// <summary>
        /// Gets the field values in the order of the class's active fields.
        /// </summary>
        public IReadOnlyList<ObjectExportField> Fields { get; init; } = [];

        /// <summary>
        /// Gets the relations whose other end the caller may read.
        /// </summary>
        public IReadOnlyList<ObjectExportRelation> Relations { get; init; } = [];

        /// <summary>
        /// Gets the comments that reach the caller, in thread order.
        /// </summary>
        public IReadOnlyList<ObjectExportComment> Comments { get; init; } = [];

        /// <summary>
        /// Gets the time of the export (UTC).
        /// </summary>
        public DateTime Exported { get; init; }

        /// <summary>
        /// Gets the culture the texts are written in.
        /// </summary>
        public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    }

    /// <summary>
    /// Collects the content of a single object export (<see cref="ObjectExportRecord"/>); the
    /// formats (<see cref="IObjectExportFormat"/>, offered by <see cref="WebManager.IObjectExportManager"/>)
    /// write it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Collect"/> reads the object's system attributes and the values of the active
    /// fields of its class (<see cref="ClassFields.Active"/>, without fields named after the
    /// system attributes <c>Summary</c> and <c>Description</c>), the relations and the comments
    /// (<c>ObjectExport.Activity.cs</c>). Revisions are not exported. Display texts follow
    /// <see cref="ObjectValueFormat"/> in the request's culture; a password field exports no value.
    /// System attributes carry a machine value (dates in ISO 8601 UTC) and a display text (dates
    /// in the request culture).
    /// </para>
    /// <para>
    /// <see cref="DescriptionText"/> reads a workbook through
    /// <see cref="SpreadsheetWorkbook.ToPlainText()"/>, without the preview limit of
    /// <see cref="WorkbookText.ToPlainText(SpreadsheetWorkbook)"/>, so every cell reaches the file.
    /// </para>
    /// </remarks>
    public static partial class ObjectExport
    {
        /// <summary>
        /// Collects the content of an object export.
        /// </summary>
        /// <param name="object">The object, read through the object manager.</param>
        /// <param name="request">The request, carrying the culture.</param>
        /// <returns>The record.</returns>
        public static ObjectExportRecord Collect(ObjectEntity @object, IRequest request)
        {
            var culture = request?.Culture ?? CultureInfo.InvariantCulture;
            var @class = CoreHub.ClassManager.GetClass(@object.ClassId);
            var workspace = CoreHub.WorkspaceManager.GetWorkspace(@object.WorkspaceId);
            var kind = CoreHub.ObjectKindManager.GetKind(@object.Kind);
            var securityLevel = @object.SecurityLevelId is Guid levelId && levelId != Guid.Empty
                ? CoreHub.SecurityLevelManager.GetSecurityLevel(levelId)
                : null;

            string T(string key) => I18N.Translate(request, key);
            string Identity(Guid? id) => id is Guid value && value != Guid.Empty
                ? CoreHub.IdentityManager.GetIdentity(value)?.Name
                : null;
            string Label(string key) => T("kleenestar.core:object.export.attribute." + key.ToLowerInvariant());
            ObjectExportAttribute Attribute(string key, string text) => new(key, Label(key), text, text);
            ObjectExportAttribute Date(string key, DateTime value) => new
            (
                key,
                Label(key),
                CsvDocument.Date(value),
                DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("g", culture)
            );

            var values = CoreHub.ValueManager.GetValues(@object.Id)
                .GroupBy(x => x.FieldId)
                .ToDictionary(x => x.Key, x => x.First().Data);

            var fields = ClassFields.Active(@object.ClassId)
                .Where(field => !AliasesAttribute(field))
                .Select(field => CollectField(field, values.TryGetValue(field.Id, out var data) ? data : null, request))
                .ToList();

            return new ObjectExportRecord
            {
                Key = @object.Key,
                Summary = @object.Summary,
                Attributes =
                [
                    Attribute("key", @object.Key),
                    Attribute("summary", @object.Summary),
                    Attribute("kind", kind is null ? @object.Kind : T(kind.Label)),
                    Attribute("workspace", workspace?.Name),
                    Attribute("class", @class?.Name),
                    Attribute("state", T(@object.State.Text())),
                    Attribute("securityLevel", securityLevel?.Name ?? T("kleenestar.core:securitylevel.none.label")),
                    Attribute("creator", Identity(@object.CreatorId)),
                    Attribute("assignee", Identity(@object.AssigneeId)),
                    Date("created", @object.Created),
                    Date("updated", @object.Updated)
                ],
                Description = @object.Description,
                DescriptionText = DescriptionText(@object.Description),
                Fields = fields,
                Relations = CollectRelations(@object, request),
                Comments = CollectComments(@object),
                Exported = DateTime.UtcNow,
                Culture = culture
            };
        }

        /// <summary>
        /// Determines whether a field names a system attribute of the object (<c>Summary</c>,
        /// <c>Description</c>). Such a field holds no value row; the attribute is exported once.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns><see langword="true"/> for an alias.</returns>
        private static bool AliasesAttribute(Field field)
        {
            return string.Equals(field.Name, nameof(ObjectEntity.Summary), StringComparison.OrdinalIgnoreCase)
                || string.Equals(field.Name, nameof(ObjectEntity.Description), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Collects one field value.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="data">The stored value, or <see langword="null"/>.</param>
        /// <param name="request">The request, carrying the culture.</param>
        /// <returns>The exported field.</returns>
        private static ObjectExportField CollectField(Field field, string data, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return new ObjectExportField(field.Name, field.FieldType, null, null);
            }

            if (field.FieldType == FieldType.Password)
            {
                return new ObjectExportField(field.Name, field.FieldType, null, ObjectValueFormat.Format(request, field, data));
            }

            var text = ObjectValueFormat.IsRichText(field)
                ? ProseText.ToPlainText(data)
                : ObjectValueFormat.Format(request, field, data);

            return new ObjectExportField(field.Name, field.FieldType, data, text);
        }

        /// <summary>
        /// Returns the description as plain text for an export: every cell of a workbook in
        /// row-major order, the words of an editor document or the text as stored.
        /// </summary>
        /// <param name="description">The stored description.</param>
        /// <returns>The text; empty for an empty description.</returns>
        public static string DescriptionText(string description)
        {
            return WorkbookText.Read(description) is SpreadsheetWorkbook workbook
                ? workbook.ToPlainText()
                : ProseText.ToPlainText(description);
        }

        /// <summary>
        /// Returns the file name of an export: the object key with the extension.
        /// </summary>
        /// <param name="record">The record.</param>
        /// <param name="extension">The file extension without dot.</param>
        /// <returns>The file name.</returns>
        public static string FileName(ObjectExportRecord record, string extension)
        {
            var name = string.Concat((record.Key ?? "object").Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '"' ? '_' : c));

            return name + "." + extension;
        }

        /// <summary>
        /// Returns bytes as a file download (<c>Content-Disposition: attachment</c>).
        /// </summary>
        /// <param name="content">The file content.</param>
        /// <param name="contentType">The media type.</param>
        /// <param name="fileName">The file name.</param>
        /// <returns>The response.</returns>
        public static IResponse Attachment(byte[] content, string contentType, string fileName)
        {
            var response = new ResponseOK { Content = content };

            response.Header.ContentType = contentType;
            response.Header.ContentLength = content.Length;
            response.Header.ContentDisposition = $"attachment; filename=\"{fileName}\"";

            return response;
        }
    }
}
