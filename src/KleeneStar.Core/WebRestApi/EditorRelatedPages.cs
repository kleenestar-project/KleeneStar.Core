using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Answers which pages belong to a page: the documents the add-on <c>ks-related</c> lists at
    /// the end of a page, the most relevant first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A page is related to another through what its authors said and through where it stands.
    /// Each connection adds to the relevance of the other page:
    /// </para>
    /// <list type="bullet">
    /// <item>the page references it with an object reference (<c>ks-object</c>):
    /// <see cref="ReferencedWeight"/>;</item>
    /// <item>a relation joins the two, in either direction: <see cref="RelationWeight"/>;</item>
    /// <item>it references the page: <see cref="ReferencingWeight"/>;</item>
    /// <item>it is the parent of the page or stands beside it below the same parent:
    /// <see cref="TreeWeight"/>;</item>
    /// <item>each label the two share: <see cref="LabelWeight"/>.</item>
    /// </list>
    /// <para>
    /// Equal relevance is ordered by the order of the references in the page, then by title.
    /// Only active documents count, never the page itself or its children (a chapter lists
    /// those with <c>ks-children</c>). The tree, the referencing pages and the labels are read
    /// within the workspace of the page, bounded by <see cref="EditorObjectQuery.ScanLimit"/>;
    /// a reference and a relation reach any workspace. Every candidate is read through the
    /// object manager, so a reader sees only the pages they may open.
    /// </para>
    /// </remarks>
    public static class EditorRelatedPages
    {
        /// <summary>
        /// The number of pages the add-on lists unless it is told otherwise.
        /// </summary>
        public const int DefaultMax = 5;

        /// <summary>
        /// The relevance of a page the page references.
        /// </summary>
        public const int ReferencedWeight = 5;

        /// <summary>
        /// The relevance of a page a relation joins to the page.
        /// </summary>
        public const int RelationWeight = 5;

        /// <summary>
        /// The relevance of a page that references the page.
        /// </summary>
        public const int ReferencingWeight = 4;

        /// <summary>
        /// The relevance of the parent and of a page beside the page in the tree.
        /// </summary>
        public const int TreeWeight = 2;

        /// <summary>
        /// The relevance of each label two pages share.
        /// </summary>
        public const int LabelWeight = 1;

        /// <summary>
        /// The name of the object reference add-on in a stored document.
        /// </summary>
        private const string ReferenceAddon = "ks-object";

        /// <summary>
        /// Returns the pages related to a page, the most relevant first.
        /// </summary>
        /// <param name="page">The page, read by the caller.</param>
        /// <param name="max">The most pages to answer.</param>
        /// <returns>The related pages.</returns>
        public static IReadOnlyList<Model.Entities.Object> Find(Model.Entities.Object page, int max)
        {
            if (page is null)
            {
                return [];
            }

            max = Math.Clamp(max, 1, EditorObjectQuery.MaxItems);

            var scores = new Dictionary<Guid, int>();
            var rank = new Dictionary<Guid, int>();
            var objects = new Dictionary<Guid, Model.Entities.Object>();

            void Add(Model.Entities.Object candidate, int weight, bool ordered = false)
            {
                if (candidate is null || candidate.Id == page.Id || candidate.ParentId == page.Id
                    || candidate.Kind != ObjectKind.Document || candidate.State != WorkspaceState.Active)
                {
                    return;
                }

                objects[candidate.Id] = candidate;
                scores[candidate.Id] = scores.GetValueOrDefault(candidate.Id) + weight;

                if (ordered)
                {
                    rank.TryAdd(candidate.Id, rank.Count);
                }
            }

            // the pages the page references, in the order it names them
            foreach (var key in References(page.Description))
            {
                Add(CoreHub.ObjectManager.GetObjectByKey(key), ReferencedWeight, ordered: true);
            }

            // the pages a relation joins to it, from either end
            foreach (var relation in CoreHub.ObjectRelationManager.GetRelations(page.Id))
            {
                var other = relation.SourceObjectId == page.Id ? relation.TargetObjectId : relation.SourceObjectId;

                if (other is { } otherId)
                {
                    Add(CoreHub.ObjectManager.GetObject(otherId), RelationWeight);
                }
            }

            // the pages of the workspace: those referencing it and its place in the tree
            var workspaceId = page.WorkspaceId;
            var documents = CoreHub.ObjectManager.GetObjects(new Query<Model.Entities.Object>()
                .Where(x => x.WorkspaceId == workspaceId)
                .Where(x => x.State == WorkspaceState.Active)
                .WhereEquals(x => x.Kind, ObjectKind.Document)
                .WithPaging(0, EditorObjectQuery.ScanLimit))
                .ToList();

            foreach (var document in documents)
            {
                if (document.Id == page.Id)
                {
                    continue;
                }

                if (References(document.Description).Contains(page.Key, StringComparer.OrdinalIgnoreCase))
                {
                    Add(document, ReferencingWeight);
                }

                if (page.ParentId is { } parentId && (document.Id == parentId || document.ParentId == parentId))
                {
                    Add(document, TreeWeight);
                }
            }

            // the labels the pages share
            var labels = CoreHub.ObjectTagManager.GetTags(page.Id)
                .Select(x => x.Name?.Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrEmpty(x))
                .ToHashSet();

            if (labels.Count > 0)
            {
                var byId = documents.ToDictionary(x => x.Id);
                var ids = byId.Keys.ToList();
                var tags = CoreHub.ObjectTagManager.GetTags(new Query<ObjectTag>()
                    .Where(x => ids.Contains(x.ObjectId)));

                foreach (var tag in tags)
                {
                    if (labels.Contains(tag.Name?.Trim().ToLowerInvariant() ?? string.Empty) && byId.TryGetValue(tag.ObjectId, out var document))
                    {
                        Add(document, LabelWeight);
                    }
                }
            }

            return [.. scores
                .OrderByDescending(x => x.Value)
                .ThenBy(x => rank.TryGetValue(x.Key, out var position) ? position : int.MaxValue)
                .ThenBy(x => objects[x.Key].Summary, StringComparer.CurrentCultureIgnoreCase)
                .Take(max)
                .Select(x => objects[x.Key])];
        }

        /// <summary>
        /// Returns the keys a stored document references through object references, in the
        /// order the document names them.
        /// </summary>
        /// <param name="description">The stored description: an editor document, or text.</param>
        /// <returns>The keys, each once.</returns>
        public static IReadOnlyList<string> References(string description)
        {
            if (string.IsNullOrWhiteSpace(description) || !description.TrimStart().StartsWith('{') || !description.Contains(ReferenceAddon, StringComparison.Ordinal))
            {
                return [];
            }

            var keys = new List<string>();

            try
            {
                using var document = JsonDocument.Parse(description);

                if (document.RootElement.TryGetProperty("doc", out var root))
                {
                    Walk(root, keys);
                }
            }
            catch (JsonException)
            {
                return [];
            }

            return [.. keys.Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        /// <summary>
        /// Collects the references of a node and its children.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="keys">The keys found so far.</param>
        private static void Walk(JsonElement node, List<string> keys)
        {
            if (node.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (node.TryGetProperty("attrs", out var attrs) && attrs.ValueKind == JsonValueKind.Object
                && attrs.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == ReferenceAddon
                && attrs.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(key.GetString()))
            {
                keys.Add(key.GetString().Trim());
            }

            if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    Walk(child, keys);
                }
            }
        }
    }
}
