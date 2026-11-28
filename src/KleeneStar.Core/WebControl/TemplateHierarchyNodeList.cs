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
    ///    "class": "{class id}", "child": true, "assignee": "{identity id}",
    ///    "links": [{ "link": "{relation type key}[:inverse]", "targets": ["root", "{row id}"] }] }]
    /// </code>
    /// <c>id</c> names a row: the id of a stored node keeps that node, any other text (a new
    /// row) is a key the other rows refer to. <c>parent</c> names an earlier row; a missing
    /// parent places the row below the root. <c>child</c> is true where it is missing.
    /// <c>links</c> joins the object of the row to the objects of other places of the hierarchy:
    /// per entry a relation type read from the row (<c>:inverse</c> for the direction from the
    /// targets to the row) and the targets, each the key of another row or
    /// <see cref="TemplateHierarchyNodeLink.RootTarget"/>. <c>class</c>, <c>assignee</c> and
    /// <c>links</c> are optional.
    /// </remarks>
    public static class TemplateHierarchyNodeList
    {
        /// <summary>
        /// The name of the payload field carrying the nodes.
        /// </summary>
        public const string Field = "Nodes";

        /// <summary>
        /// The suffix of a link that runs from the targets to the row.
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
            /// Gets or sets the account the object is assigned to, or null.
            /// </summary>
            public Guid? Assignee { get; set; }

            /// <summary>
            /// Gets or sets the links of the row; null for none.
            /// </summary>
            public List<LinkRow> Links { get; set; }
        }

        /// <summary>
        /// One link of a row as it travels.
        /// </summary>
        public sealed class LinkRow
        {
            /// <summary>
            /// Gets or sets the relation type key, with <see cref="InverseSuffix"/> for the
            /// direction from the targets to the row.
            /// </summary>
            public string Link { get; set; }

            /// <summary>
            /// Gets or sets the targets: keys of other rows, or
            /// <see cref="TemplateHierarchyNodeLink.RootTarget"/>.
            /// </summary>
            public List<string> Targets { get; set; }
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
                Assignee = x.AssigneeId,
                Links = [.. (x.Links ?? [])
                    .Where(l => l is not null && !string.IsNullOrWhiteSpace(l.Type))
                    .Select(l => new LinkRow
                    {
                        Link = FormatLink(l.Type, l.Inverse),
                        Targets = [.. (l.Targets ?? []).Select(NormalizeTarget).Where(t => t is not null)]
                    })]
            }), _options);
        }

        /// <summary>
        /// Turns rows into nodes. A row naming a stored node of the hierarchy keeps its id; every
        /// other row is given a new one, and the references between the rows (parents and link
        /// targets) are translated to those ids.
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

                return new TemplateHierarchyNode
                {
                    Id = id,
                    ParentNodeId = !string.IsNullOrWhiteSpace(row.Parent) && ids.TryGetValue(row.Parent, out var parent) ? parent : null,
                    TemplateId = row.Template,
                    ClassId = row.Class is Guid c && c != Guid.Empty ? c : null,
                    AsChild = row.Child ?? true,
                    AssigneeId = row.Assignee is Guid a && a != Guid.Empty ? a : null,
                    Links = ToLinks(row, id, ids)
                };
            })];
        }

        /// <summary>
        /// Returns the links of a row with their targets translated to node ids: a link without
        /// a relation type, a target that names no row or the row itself, and a link left
        /// without targets are dropped.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="id">The id of the row's node.</param>
        /// <param name="ids">The node ids by row key.</param>
        /// <returns>The links.</returns>
        private static List<TemplateHierarchyNodeLink> ToLinks(Row row, Guid id, IReadOnlyDictionary<string, Guid> ids)
        {
            var links = new List<TemplateHierarchyNodeLink>();

            foreach (var link in row.Links ?? [])
            {
                var (key, inverse) = ParseLink(link?.Link);

                if (key is null)
                {
                    continue;
                }

                var targets = (link.Targets ?? [])
                    .Select(t => IsRoot(t)
                        ? TemplateHierarchyNodeLink.RootTarget
                        : !string.IsNullOrWhiteSpace(t) && ids.TryGetValue(t.Trim(), out var target) && target != id ? target.ToString() : null)
                    .Where(t => t is not null)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (targets.Count > 0)
                {
                    links.Add(new TemplateHierarchyNodeLink { Type = key, Inverse = inverse, Targets = targets });
                }
            }

            return links;
        }

        /// <summary>
        /// Determines whether a row is linked: it names at least one link with a relation type
        /// and a target.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>True when the row is linked.</returns>
        public static bool IsLinked(Row row)
        {
            return (row?.Links ?? []).Any(l => ParseLink(l?.Link).Key is not null && (l.Targets ?? []).Any(t => !string.IsNullOrWhiteSpace(t)));
        }

        /// <summary>
        /// Determines whether a target names the object created from the root template.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>True for the root.</returns>
        public static bool IsRoot(string target)
        {
            return string.Equals(target?.Trim(), TemplateHierarchyNodeLink.RootTarget, StringComparison.OrdinalIgnoreCase);
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
        /// Determines whether a relation type keeps its target from being finished before its
        /// source (<see cref="RelationEffect.BlocksCompletion"/>).
        /// </summary>
        /// <param name="key">The relation type key.</param>
        /// <returns>True for a blocking type.</returns>
        public static bool IsBlocking(string key)
        {
            return LinkTypes().Any(x => x.Effect == RelationEffect.BlocksCompletion
                && string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
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
        /// Determines whether the blocking links of the rows close a cycle, which would let no
        /// object of the cycle ever be finished.
        /// </summary>
        /// <remarks>
        /// A blocking link without inverse runs from the row to its targets (the row blocks
        /// them), with inverse from the targets to the row (the row is blocked by them). The root
        /// takes part as a place of its own.
        /// </remarks>
        /// <param name="rows">The rows.</param>
        /// <param name="isBlocking">Tells whether a relation type key blocks.</param>
        /// <returns>True when an object is blocked by itself, directly or through others.</returns>
        public static bool HasBlockingCycle(IEnumerable<Row> rows, Func<string, bool> isBlocking)
        {
            var edges = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            void Add(string from, string to)
            {
                if (!edges.TryGetValue(from, out var set))
                {
                    edges[from] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                set.Add(to);
            }

            foreach (var row in (rows ?? []).Where(x => x?.Id is not null))
            {
                foreach (var link in row.Links ?? [])
                {
                    var (key, inverse) = ParseLink(link?.Link);

                    if (key is null || !isBlocking(key))
                    {
                        continue;
                    }

                    foreach (var target in (link.Targets ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => IsRoot(t) ? TemplateHierarchyNodeLink.RootTarget : t.Trim()))
                    {
                        if (inverse)
                        {
                            Add(target, row.Id);
                        }
                        else
                        {
                            Add(row.Id, target);
                        }
                    }
                }
            }

            // depth first search with the colours unvisited, on the path and done
            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool Visit(string place)
            {
                state[place] = 1;

                foreach (var next in edges.TryGetValue(place, out var set) ? set : [])
                {
                    var seen = state.TryGetValue(next, out var s) ? s : 0;

                    if (seen == 1 || (seen == 0 && Visit(next)))
                    {
                        return true;
                    }
                }

                state[place] = 2;

                return false;
            }

            return edges.Keys.ToList().Any(place => !state.ContainsKey(place) && Visit(place));
        }

        /// <summary>
        /// Returns a stored target as it travels: the root, or a node id.
        /// </summary>
        /// <param name="target">The stored target.</param>
        /// <returns>The target, or null when it names nothing.</returns>
        private static string NormalizeTarget(string target)
        {
            return IsRoot(target)
                ? TemplateHierarchyNodeLink.RootTarget
                : Guid.TryParse(target, out var id) ? id.ToString() : null;
        }
    }
}
