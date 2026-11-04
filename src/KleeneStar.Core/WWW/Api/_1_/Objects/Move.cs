using KleeneStar.Core.WebRestApi;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// REST endpoint that re-parents an object inside its workspace. The URL is the fixed
    /// <c>/api/1/objects/move</c>; the body carries both operands so the client-side tree
    /// (see <c>Assets/js/objectmovetree.js</c>) can persist a drag-and-drop move with a
    /// single request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>POST {base}</c> expects a JSON document of the form
    /// <c>{ "node": "SD-17", "parent": "SD-3" }</c> or <c>{ "node": "SD-17", "parent": null }</c>
    /// (to detach). Both <c>node</c> and <c>parent</c> accept an object key or id. An optional
    /// <c>expectedParent</c> (key, id or <c>null</c> for the root) names the parent the client
    /// saw; when the object has another one by now, the move is refused with <c>409</c> instead
    /// of taking the object from a place the user never saw it in.
    /// </para>
    /// <para>
    /// Authorization and the re-parenting are <see cref="ObjectMove.Execute"/>: <c>403</c> when
    /// the caller may not change the object, its new parent or its old one; <c>400</c> with an
    /// <c>error</c> message for a rule violation (self-parent, cycle, cross-workspace link,
    /// depth, disallowed child class); <c>409</c> for a conflict; <c>404</c> when the node does
    /// not resolve; <c>200</c> on success.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.move.api.title")]
    [Cache]
    public sealed class Move : IRestApi
    {
        /// <summary>
        /// Serialization options for the payloads: camelCase property names, null members omitted,
        /// case-insensitive binding.
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
        public Move()
        {
        }

        /// <summary>
        /// Handles <c>POST {base}</c>: re-parents the <c>node</c> object under the <c>parent</c>
        /// object (or detaches it when <c>parent</c> is <see langword="null"/>).
        /// </summary>
        /// <param name="request">The incoming request carrying the JSON body.</param>
        /// <returns>
        /// <c>200</c> on success, <c>404</c> when the node cannot be resolved, <c>403</c> when the
        /// caller may not move it there, <c>409</c> on a conflict, or <c>400</c> when the payload is
        /// malformed, the parent cannot be resolved, or a hierarchy rule is violated.
        /// </returns>
        [Method(RequestMethod.POST)]
        public IResponse Persist(IRequest request)
        {
            MovePayload payload;

            try
            {
                var content = (request as Request)?.Content;
                payload = content is { Length: > 0 }
                    ? JsonSerializer.Deserialize<MovePayload>(content, _jsonOptions)
                    : null;
            }
            catch (JsonException)
            {
                return ObjectMove.BadRequest("The request body is not a valid JSON document.");
            }

            var batch = payload?.Nodes is { Count: > 0 };

            if (payload is null || (!batch && string.IsNullOrWhiteSpace(payload.Node)))
            {
                return ObjectMove.BadRequest("The request body must be a JSON document of the form { \"node\": \"KEY\", \"parent\": \"KEY\" }, { \"node\": \"KEY\", \"parent\": null } or { \"nodes\": [\"KEY\", …], \"parent\": … }.");
            }

            Model.Entities.Object parent = null;

            if (!string.IsNullOrWhiteSpace(payload.Parent))
            {
                parent = ObjectMove.Resolve(payload.Parent);

                if (parent is null)
                {
                    return ObjectMove.BadRequest($"The parent object '{payload.Parent}' does not exist.");
                }
            }

            if (batch)
            {
                return MoveAll(payload, parent, request);
            }

            var node = ObjectMove.Resolve(payload.Node);
            if (node is null)
            {
                return new ResponseNotFound();
            }

            return ObjectMove.Execute(node, parent, payload.ExpectedParent, request, out _)
                ?? new ResponseOK();
        }

        /// <summary>
        /// Moves several objects below one parent, each on its own: one refused move does not
        /// keep the others in place, and the answer reports every object.
        /// </summary>
        /// <param name="payload">The body.</param>
        /// <param name="parent">The resolved parent, or <see langword="null"/> for the root.</param>
        /// <param name="request">The request.</param>
        /// <returns><c>200</c> with <c>{ moved, refused, results: [{ node, status, error }] }</c>.</returns>
        private static IResponse MoveAll(MovePayload payload, Model.Entities.Object parent, IRequest request)
        {
            var results = new System.Collections.Generic.List<object>();
            var moved = 0;

            // the other open pages hear of the batch once, not once per object
            using var notices = global::KleeneStar.Core.WebManager.ObjectChangeNotices.Batch();

            foreach (var token in payload.Nodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(System.StringComparer.OrdinalIgnoreCase))
            {
                var node = ObjectMove.Resolve(token);

                if (node is null)
                {
                    results.Add(new { node = token, status = 404, error = "The object does not exist." });
                    continue;
                }

                var expectation = payload.ExpectedParents is { } expected
                    && expected.TryGetValue(token, out var stated)
                        ? stated
                        : default;

                var result = ObjectMove.Attempt(node, parent, expectation, request);

                if (result.Status == 200)
                {
                    moved++;
                }

                results.Add(new { node = token, status = result.Status, error = result.Error, code = result.Code });
            }

            var body = new { moved, refused = results.Count - moved, results };

            return new ResponseOK
            {
                Content = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// The JSON body accepted by <c>POST</c>.
        /// </summary>
        private sealed class MovePayload
        {
            /// <summary>
            /// Gets or sets the object to move, as object key or id.
            /// </summary>
            public string Node { get; set; }

            /// <summary>
            /// Gets or sets the new parent as object key or id, or <see langword="null"/> to detach
            /// the object from its parent.
            /// </summary>
            public string Parent { get; set; }

            /// <summary>
            /// Gets or sets the parent the client saw: a key or id, <c>null</c> for the root, or
            /// absent (<see cref="JsonValueKind.Undefined"/>) when the client states none.
            /// </summary>
            public JsonElement ExpectedParent { get; set; }

            /// <summary>
            /// Gets or sets several objects to move below <see cref="Parent"/> at once, as keys
            /// or ids; takes the place of <see cref="Node"/>.
            /// </summary>
            public System.Collections.Generic.List<string> Nodes { get; set; }

            /// <summary>
            /// Gets or sets, for a batch, the parent the client saw per object (key or id as
            /// in <see cref="Nodes"/> -> key, id or <c>null</c>).
            /// </summary>
            public System.Collections.Generic.Dictionary<string, JsonElement> ExpectedParents { get; set; }
        }
    }
}
