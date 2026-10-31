using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The cell template of an object overview table column.
    /// </summary>
    internal sealed class ObjectTableColumnTemplate : IRestApiTableColumnTemplate
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// Gets the framework inline editor template identifier.
        /// </summary>
        public string Type => "inline-edit";

        /// <summary>
        /// Gets a value indicating whether the cell offers an inline editor.
        /// </summary>
        public bool Editable { get; private init; }

        /// <summary>
        /// Gets the stock renderer the read-only side of the cell is drawn with, one of
        /// <c>text</c>, <c>numeric</c>, <c>date</c>, <c>tag</c> or <c>combo</c>.
        /// </summary>
        public string Kind { get; private init; }

        /// <summary>
        /// Gets the selectable items of a <c>combo</c> cell, empty for the other kinds.
        /// </summary>
        public IReadOnlyList<RestApiTableColumnTemplateItem> Items { get; private init; } = [];

        /// <summary>
        /// Gets the date format of a <c>date</c> cell.
        /// </summary>
        public string Format { get; private init; } = "yyyy-MM-dd";

        /// <summary>
        /// Gets the address of the object CRUD endpoint an inline edit writes through.
        /// The renderer appends the row's object id to it.
        /// </summary>
        public string Endpoint { get; private set; }

        /// <summary>
        /// Points the template's inline editor at the object CRUD endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint address.</param>
        public void BindEndpoint(string endpoint)
        {
            Endpoint = endpoint;
        }

        /// <summary>
        /// Returns a read-only cell drawn with the supplied stock renderer.
        /// </summary>
        /// <param name="kind">The stock renderer id.</param>
        /// <returns>The template.</returns>
        public static ObjectTableColumnTemplate ReadOnly(string kind)
        {
            return new ObjectTableColumnTemplate { Kind = kind, Editable = false };
        }

        /// <summary>
        /// Returns an inline-editable cell drawn with the supplied stock renderer.
        /// </summary>
        /// <param name="kind">The stock renderer id.</param>
        /// <returns>The template.</returns>
        public static ObjectTableColumnTemplate Input(string kind)
        {
            return new ObjectTableColumnTemplate { Kind = kind, Editable = true };
        }

        /// <summary>
        /// Returns a cell that shows one of a fixed set of values and, when editable,
        /// offers that set as a drop-down.
        /// </summary>
        /// <param name="items">The selectable items.</param>
        /// <param name="editable">Whether the cell offers the editor.</param>
        /// <returns>The template.</returns>
        public static ObjectTableColumnTemplate Combo(IReadOnlyList<RestApiTableColumnTemplateItem> items, bool editable)
        {
            return new ObjectTableColumnTemplate
            {
                Kind = "combo",
                Editable = editable,
                Items = items ?? []
            };
        }

        /// <summary>
        /// Serializes the template into the <c>{ type, options }</c> shape the client
        /// template registry consumes. The items travel as an embedded JSON string,
        /// which is the shape the stock combo renderer parses, so the options can be
        /// handed to it unchanged.
        /// </summary>
        /// <returns>The JSON representation.</returns>
        public string ToJson()
        {
            var template = new RestApiTableColumnTemplateInlineEdit(Editable)
            {
                Endpoint = Endpoint,
                Kind = Kind,
                Editor = Kind switch
                {
                    "numeric" => "number",
                    "date" => "date",
                    "combo" => "select",
                    _ => "text"
                },
                Method = "PUT"
            };
            template.Options["format"] = Format;
            if (Items.Count > 0)
            {
                var items = Items.Select(item => new { value = item.Id, text = item.Text }).ToArray();
                template.Options["items"] = items;
                template.Options["options"] = JsonSerializer.Serialize(items, _jsonOptions);
            }
            return template.ToJson();
        }
    }
}
