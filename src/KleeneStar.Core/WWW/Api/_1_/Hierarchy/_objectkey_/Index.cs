using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Hierarchy._objectkey_
{
    // The entity types Object/Field/Status collide with System.Object and the
    // KleeneStar.Core.WWW.* namespace segments of the same name; alias them inside the
    // namespace block (see the Calendar namespace-collision note).
    using ObjectEntity = KleeneStar.Model.Entities.Object;
    using Field = KleeneStar.Model.Entities.Field;
    using Status = KleeneStar.Model.Entities.Status;

    /// <summary>
    /// REST endpoint exposing the hierarchy of a single object. The URL is
    /// <c>/api/1/hierarchy/{objectkey}</c>; the <c>{objectkey}</c> URL segment is
    /// declared via <see cref="ObjectKeySegmentAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GET {base}</c> returns the hierarchy panel data of the object — the ancestor
    /// chain (nearest first), the parent, the object itself, the immediate children
    /// (with their resolved workflow status), and the siblings — as a JSON document
    /// with camelCase property names.
    /// </para>
    /// <para>
    /// <c>PUT {base}</c> sets or clears the parent. The request body is a JSON
    /// document of the form <c>{ "parent": "SD-17" }</c> (object key or id) or
    /// <c>{ "parent": null }</c> to detach. Validation is delegated to
    /// <see cref="WebManager.IObjectManager.SetParent"/>: an unknown object yields
    /// <c>404</c>, a rule violation (self-parent, cycle, cross-workspace link, or a
    /// child class the parent's class does not allow) yields <c>400</c> with an
    /// <c>error</c> message. On success the updated hierarchy is returned.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.hierarchy.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Serialization options for the hierarchy payload: camelCase property names,
        /// null members omitted.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Handles <c>GET {base}</c>: returns the hierarchy of the object addressed by
        /// the URL <c>{objectkey}</c> segment.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The hierarchy JSON, or <c>404</c> when the object does not exist.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var entity = ResolveObject(request);

            return entity is null ? new ResponseNotFound() : Json(BuildHierarchy(entity));
        }

        /// <summary>
        /// Handles <c>PUT {base}</c>: sets or clears the parent of the object addressed
        /// by the URL <c>{objectkey}</c> segment and returns the updated hierarchy.
        /// </summary>
        /// <param name="request">The incoming request carrying the JSON body.</param>
        /// <returns>
        /// The updated hierarchy JSON, <c>404</c> when the object does not exist, or
        /// <c>400</c> when the payload is malformed or a hierarchy rule is violated.
        /// </returns>
        [Method(RequestMethod.PUT)]
        public IResponse Update(IRequest request)
        {
            var entity = ResolveObject(request);
            if (entity is null)
            {
                return new ResponseNotFound();
            }

            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(entity, request))
            {
                return new ResponseForbidden();
            }

            HierarchyUpdatePayload payload;

            try
            {
                var content = (request as Request)?.Content;
                payload = content is { Length: > 0 }
                    ? JsonSerializer.Deserialize<HierarchyUpdatePayload>(content, _jsonOptions)
                    : null;
            }
            catch (JsonException)
            {
                return Error("The request body is not a valid JSON document.");
            }

            if (payload is null)
            {
                return Error("The request body must be a JSON document of the form { \"parent\": \"KEY\" } or { \"parent\": null }.");
            }

            Guid? parentId = null;

            if (!string.IsNullOrWhiteSpace(payload.Parent))
            {
                var parent = Guid.TryParse(payload.Parent, out var parsed)
                    ? CoreHub.ObjectManager.GetObject(parsed)
                    : CoreHub.ObjectManager.GetObjectByKey(payload.Parent);

                if (parent is null)
                {
                    return Error($"The parent object '{payload.Parent}' does not exist.");
                }

                parentId = parent.Id;
            }

            try
            {
                var updated = CoreHub.ObjectManager.SetParent(entity.Id, parentId);

                return updated is null ? new ResponseNotFound() : Json(BuildHierarchy(updated));
            }
            catch (InvalidOperationException ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// Resolves the object addressed by the URL <c>{objectkey}</c> segment.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static ObjectEntity ResolveObject(IRequest request)
        {
            var keyParameter = request?.GetParameter<ObjectKeyParameter>();

            return CoreHub.ObjectManager.GetObjectByKey(keyParameter?.Value);
        }

        /// <summary>
        /// Builds the hierarchy projection of the supplied object: ancestors (nearest
        /// first), parent, the object itself, children (with workflow status), and
        /// siblings.
        /// </summary>
        /// <param name="entity">The object whose hierarchy is projected.</param>
        /// <returns>The hierarchy payload.</returns>
        private static HierarchyPayload BuildHierarchy(ObjectEntity entity)
        {
            var statusResolver = new StatusResolver();

            var ancestors = CoreHub.ObjectManager.GetAncestors(entity.Id).ToList();
            var children = CoreHub.ObjectManager.GetChildren(entity.Id)
                .OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var siblings = CoreHub.ObjectManager.GetSiblings(entity.Id)
                .OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new HierarchyPayload
            {
                Ancestors = [.. ancestors.Select(a => ToNode(a, statusResolver))],
                Parent = ancestors.Count > 0 ? ToNode(ancestors[0], statusResolver) : null,
                Current = ToNode(entity, statusResolver),
                Children = [.. children.Select(c => ToNode(c, statusResolver))],
                Siblings = [.. siblings.Select(s => ToNode(s, statusResolver))]
            };
        }

        /// <summary>
        /// Projects a single object onto the lightweight hierarchy node DTO.
        /// </summary>
        /// <param name="entity">The object to project.</param>
        /// <param name="statusResolver">The per-request status resolver.</param>
        /// <returns>The node DTO.</returns>
        private static HierarchyNode ToNode(ObjectEntity entity, StatusResolver statusResolver)
        {
            return new HierarchyNode
            {
                Id = entity.Id.ToString(),
                Key = entity.Key,
                Summary = entity.Summary,
                ClassName = CoreHub.ClassManager.GetClass(entity.ClassId)?.Name,
                Status = statusResolver.Resolve(entity)
            };
        }

        /// <summary>
        /// Wraps a payload into a JSON <c>200</c> response.
        /// </summary>
        /// <param name="payload">The payload to serialize.</param>
        /// <returns>The response.</returns>
        private static IResponse Json(object payload)
        {
            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Wraps an error message into a JSON <c>400</c> response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>The response.</returns>
        private static IResponse Error(string message)
        {
            return new ResponseBadRequest
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = message }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Resolves the workflow status name of objects, caching the per-class lookup
        /// data (workflow field and statuses) so trees with many nodes of the same
        /// class do not re-query per node. Mirrors the resolution of
        /// <c>ObjectMetadataStatusFragment</c>: match by normalized name first, then by
        /// status id.
        /// </summary>
        private sealed class StatusResolver
        {
            private readonly Dictionary<Guid, (Field WorkflowField, List<Status> Statuses)> _byClass = [];

            /// <summary>
            /// Resolves the display status of the supplied object, or
            /// <see langword="null"/> when the class has no workflow field or the
            /// object carries no value yet.
            /// </summary>
            /// <param name="entity">The object whose status is resolved.</param>
            /// <returns>The status name, or <see langword="null"/>.</returns>
            public string Resolve(ObjectEntity entity)
            {
                if (!_byClass.TryGetValue(entity.ClassId, out var context))
                {
                    var workflowField = CoreHub.FieldManager
                        .GetFields(new ClassIdParameter(entity.ClassId))
                        .FirstOrDefault(f => !f.Deprecated
                            && f.State == FieldState.Active
                            && f.FieldType == FieldType.Workflow);

                    var statuses = CoreHub.StatusManager
                        .GetStatuses(new ClassIdParameter(entity.ClassId))
                        .Where(s => s.State == StatusState.Active)
                        .ToList();

                    context = (workflowField, statuses);
                    _byClass[entity.ClassId] = context;
                }

                if (context.WorkflowField is null)
                {
                    return null;
                }

                var data = CoreHub.ValueManager.GetValue(entity.Id, context.WorkflowField.Id)?.Data;
                if (string.IsNullOrWhiteSpace(data))
                {
                    return null;
                }

                var normalized = Normalize(data);
                var status = context.Statuses.FirstOrDefault(s => Normalize(s.Name) == normalized)
                    ?? context.Statuses.FirstOrDefault(s => string.Equals(s.Id.ToString(), data, StringComparison.OrdinalIgnoreCase));

                return status?.Name ?? data;
            }

            /// <summary>
            /// Reduces a string to its lower-cased alphanumeric characters so loosely
            /// formatted status slugs compare against status names.
            /// </summary>
            /// <param name="value">The value to normalize.</param>
            /// <returns>The normalized string.</returns>
            private static string Normalize(string value)
            {
                return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            }
        }

        /// <summary>
        /// The JSON document returned by <c>GET</c>/<c>PUT</c>.
        /// </summary>
        private sealed class HierarchyPayload
        {
            /// <summary>Gets or sets the ancestor chain, nearest first.</summary>
            public IReadOnlyList<HierarchyNode> Ancestors { get; init; }

            /// <summary>Gets or sets the direct parent, or <see langword="null"/>.</summary>
            public HierarchyNode Parent { get; init; }

            /// <summary>Gets or sets the addressed object itself.</summary>
            public HierarchyNode Current { get; init; }

            /// <summary>Gets or sets the immediate children.</summary>
            public IReadOnlyList<HierarchyNode> Children { get; init; }

            /// <summary>Gets or sets the siblings (same workspace and class).</summary>
            public IReadOnlyList<HierarchyNode> Siblings { get; init; }
        }

        /// <summary>
        /// A single node of the hierarchy payload.
        /// </summary>
        private sealed class HierarchyNode
        {
            /// <summary>Gets or sets the object id.</summary>
            public string Id { get; init; }

            /// <summary>Gets or sets the object key (e.g. <c>SD-17</c>).</summary>
            public string Key { get; init; }

            /// <summary>Gets or sets the object summary.</summary>
            public string Summary { get; init; }

            /// <summary>Gets or sets the class name of the object.</summary>
            public string ClassName { get; init; }

            /// <summary>Gets or sets the resolved workflow status name, or <see langword="null"/>.</summary>
            public string Status { get; init; }
        }

        /// <summary>
        /// The JSON body accepted by <c>PUT</c>.
        /// </summary>
        private sealed class HierarchyUpdatePayload
        {
            /// <summary>
            /// Gets or sets the new parent as object key or id, or <see langword="null"/>
            /// to detach the object from its parent.
            /// </summary>
            public string Parent { get; set; }
        }
    }
}
