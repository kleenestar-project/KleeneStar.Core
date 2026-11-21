using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebApp.WebRelation;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Reads and writes the nodes of a template hierarchy in the form the hierarchy wizard posts
    /// them (<see cref="TemplateHierarchyInput"/>) and an API client sends them.
    /// </summary>
    /// <remarks>
    /// The nodes travel as one field, <see cref="Field"/>, holding a JSON array depth first,
    /// either as a string (the wizard posts it through a hidden input) or as the array itself:
    /// <code>
    /// [{ "id": "…", "parent": "{id of an earlier row}", "template": "{template id}",
    ///    "class": "{class id}", "child": true, "link": "{relation type key}[:inverse]",
    ///    "assignee": "{identity id}", "waitsFor": "{id of a sibling row}" }]
    /// </code>
    /// <c>id</c> names a row: the id of a stored node keeps that node, any other text (a new
    /// row) is a key the other rows refer to. <c>parent</c> names an earlier row; a missing
    /// parent places the row below the root. <c>child</c> is true where it is missing;
    /// <c>link</c> names the relation type the object is linked with, <c>:inverse</c> for the
    /// direction from the created object to its parent. <c>class</c>, <c>link</c>,
    /// <c>assignee</c> and <c>waitsFor</c> are optional.
    /// </remarks>
    public static class TemplateHierarchyNodeList
    {
        /// <summary>
        /// The name of the payload field carrying the nodes.
        /// </summary>
        public const string Field = "Nodes";

        /// <summary>
        /// The suffix of a link that runs from the created object to its parent.
        /// </summary>
        public const string InverseSuffix = ":inverse";

        /// <summary>
        /// The serializer settings of the list: camel case, without the empty references.
        /// </summary>
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// One row of the list as it travels.
        /// </summary>
        public sealed class Row
        {
            /// <summary>
            /// Gets or sets the key of the row: the id of a stored node, or a key of a new row.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Gets or sets the key of the parent row, or null for a row below the root.
            /// </summary>
            public string Parent { get; set; }

            /// <summary>
            /// Gets or sets the referenced template.
            /// </summary>
            public Guid Template { get; set; }

            /// <summary>
            /// Gets or sets the class the object is created in, or null for the bound class.
            /// </summary>
            public Guid? Class { get; set; }

            /// <summary>
            /// Gets or sets whether the object is created as a child; null counts as true.
            /// </summary>
            public bool? Child { get; set; }

            /// <summary>
            /// Gets or sets the link: a relation type key, with <see cref="InverseSuffix"/> for
            /// the inverse direction; null or empty for no link.
            /// </summary>
            public string Link { get; set; }

            /// <summary>
            /// Gets or sets the account the object is assigned to, or null.
            /// </summary>
            public Guid? Assignee { get; set; }

            /// <summary>
            /// Gets or sets the key of the sibling row whose object has to be finished first.
            /// </summary>
            public string WaitsFor { get; set; }
        }

        /// <summary>
        /// Reads the list from a payload value.
        /// </summary>
        /// <param name="value">The value: JSON text, or a JSON element holding the array or the text.</param>
        /// <param name="rows">The rows in their order; empty for a blank value.</param>
        /// <returns>False when the value is no list of rows.</returns>
        public static bool TryParse(object value, out IReadOnlyList<Row> rows)
        {
            rows = [];

            var text = value switch
            {
                null => null,
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                JsonElement { ValueKind: JsonValueKind.Array } element => element.GetRawText(),
                JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
                _ => value.ToString()
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<Row>>(text, _options);

                if (parsed is null || parsed.Any(x => x is null))
                {
                    return false;
                }

                // a row without a key cannot be referenced; it gets one of its own
                foreach (var row in parsed.Where(x => string.IsNullOrWhiteSpace(x.Id)))
                {
                    row.Id = Guid.NewGuid().ToString();
                }

                rows = parsed;

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes stored nodes as the list the wizard starts from.
        /// </summary>
        /// <param name="nodes">The nodes depth first.</param>
        /// <returns>The JSON array.</returns>
        public static string Serialize(IEnumerable<TemplateHierarchyNode> nodes)
        {
            return JsonSerializer.Serialize((nodes ?? []).Select(x => new Row
            {
                Id = x.Id.ToString(),
                Parent = x.ParentNodeId?.ToString(),
                Template = x.TemplateId,
                Class = x.ClassId,
                Child = x.AsChild,
                Link = FormatLink(x.RelationTypeKey, x.RelationInverse),
                Assignee = x.AssigneeId,
                WaitsFor = x.PredecessorId?.ToString()
            }), _options);
        }

        /// <summary>
        /// Turns rows into nodes. A row naming a stored node of the hierarchy keeps its id; every
        /// other row is given a new one, and the references between the rows are translated to
        /// those ids.
        /// </summary>
        /// <param name="rows">The rows depth first.</param>
        /// <param name="stored">The nodes the hierarchy holds now.</param>
        /// <returns>The nodes depth first.</returns>
        public static IReadOnlyList<TemplateHierarchyNode> ToNodes(IEnumerable<Row> rows, IEnumerable<TemplateHierarchyNode> stored)
        {
            var known = (stored ?? []).Select(x => x.Id).ToHashSet();
            var list = (rows ?? []).Where(x => x is not null).ToList();
            var ids = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in list)
            {
                var key = row.Id ?? string.Empty;

                if (ids.ContainsKey(key))
                {
                    continue;
                }

                ids[key] = Guid.TryParse(key, out var id) && known.Contains(id) && !ids.ContainsValue(id)
                    ? id
                    : Guid.NewGuid();
            }

            var used = new HashSet<Guid>();

            return [.. list.Select(row =>
            {
                // a key repeated by mistake must not merge two rows into one node
                var id = ids[row.Id ?? string.Empty];
                id = used.Add(id) ? id : Guid.NewGuid();
                used.Add(id);

                var (relationKey, inverse) = ParseLink(row.Link);

                return new TemplateHierarchyNode
                {
                    Id = id,
                    ParentNodeId = !string.IsNullOrWhiteSpace(row.Parent) && ids.TryGetValue(row.Parent, out var parent) ? parent : null,
                    TemplateId = row.Template,
                    ClassId = row.Class is Guid c && c != Guid.Empty ? c : null,
                    AsChild = row.Child ?? true,
                    RelationTypeKey = relationKey,
                    RelationInverse = inverse,
                    AssigneeId = row.Assignee is Guid a && a != Guid.Empty ? a : null,
                    PredecessorId = !string.IsNullOrWhiteSpace(row.WaitsFor) && ids.TryGetValue(row.WaitsFor, out var p) ? p : null
                };
            })];
        }

        /// <summary>
        /// Returns the relation types a node may be linked with: the active types of the object
        /// relation system, without those that aggregate progress (the child of a node is joined
        /// that way already), in the order of the catalog.
        /// </summary>
        /// <returns>The relation types.</returns>
        public static IReadOnlyList<ObjectRelationType> LinkTypes()
        {
            return [.. (CoreHub.ObjectRelationTypeManager?.GetRelationTypes() ?? [])
                .Where(x => x.Active
                    && x.Effect != RelationEffect.AggregatesProgress
                    && string.Equals(x.System, RelationSystem.Object, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Order)];
        }

        /// <summary>
        /// Determines whether a link names a relation type of <see cref="LinkTypes"/> in a
        /// direction it has: a symmetric type has no inverse direction.
        /// </summary>
        /// <param name="key">The relation type key.</param>
        /// <param name="inverse">Whether the link is inverse.</param>
        /// <returns>True when the link may be stored.</returns>
        public static bool IsLinkable(string key, bool inverse)
        {
            var type = LinkTypes().FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

            return type is not null && (!inverse || !type.Symmetric);
        }

        /// <summary>
        /// Splits a link into the relation type key and the direction.
        /// </summary>
        /// <param name="link">The link: a key, with <see cref="InverseSuffix"/> for the inverse direction.</param>
        /// <returns>The key, or null for no link, and whether the link is inverse.</returns>
        public static (string Key, bool Inverse) ParseLink(string link)
        {
            var text = link?.Trim();

            if (string.IsNullOrEmpty(text))
            {
                return (null, false);
            }

            var inverse = text.EndsWith(InverseSuffix, StringComparison.OrdinalIgnoreCase);
            var key = inverse ? text[..^InverseSuffix.Length].Trim() : text;

            return string.IsNullOrEmpty(key) ? (null, false) : (key, inverse);
        }

        /// <summary>
        /// Joins a relation type key and the direction into a link.
        /// </summary>
        /// <param name="key">The relation type key, or null.</param>
        /// <param name="inverse">Whether the link is inverse.</param>
        /// <returns>The link, or null for no key.</returns>
        public static string FormatLink(string key, bool inverse)
        {
            return string.IsNullOrWhiteSpace(key) ? null : inverse ? key + InverseSuffix : key;
        }

        /// <summary>
        /// Determines whether the waits-for references of the rows close a cycle, which would let
        /// no object of the cycle ever be finished.
        /// </summary>
        /// <param name="rows">The rows.</param>
        /// <returns>True when a row waits for itself, directly or through others.</returns>
        public static bool HasWaitingCycle(IEnumerable<Row> rows)
        {
            var next = (rows ?? [])
                .Where(x => x?.Id is not null)
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().WaitsFor, StringComparer.OrdinalIgnoreCase);

            foreach (var start in next.Keys)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
                var cursor = next[start];

                while (!string.IsNullOrWhiteSpace(cursor) && next.TryGetValue(cursor, out var following))
                {
                    if (!seen.Add(cursor))
                    {
                        return true;
                    }

                    cursor = following;
                }
            }

            return false;
        }
    }
}
