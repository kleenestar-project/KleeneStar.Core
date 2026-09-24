using System;
using System.Text;
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
    /// (to detach). Both <c>node</c> and <c>parent</c> accept an object key or id.
    /// </para>
    /// <para>
    /// The actual re-parenting and its validation (self-parent, cycle, cross-workspace link,
    /// disallowed child class) are delegated to
    /// <see cref="WebManager.IObjectManager.SetParent"/>: a missing/unknown node yields
    /// <c>404</c>, a rule violation yields <c>400</c> with an <c>error</c> message, and a
    /// successful move yields <c>200</c>.
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
        /// <c>200</c> on success, <c>404</c> when the node cannot be resolved, or <c>400</c> when
        /// the payload is malformed, the parent cannot be resolved, or a hierarchy rule is violated.
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
                return Error("The request body is not a valid JSON document.");
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.Node))
            {
                return Error("The request body must be a JSON document of the form { \"node\": \"KEY\", \"parent\": \"KEY\" } or { \"node\": \"KEY\", \"parent\": null }.");
            }

            var node = Resolve(payload.Node);
            if (node is null)
            {
                return new ResponseNotFound();
            }

            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(node, request))
            {
                return new ResponseForbidden();
            }

            Guid? parentId = null;

            if (!string.IsNullOrWhiteSpace(payload.Parent))
            {
                var parent = Resolve(payload.Parent);
                if (parent is null)
                {
                    return Error($"The parent object '{payload.Parent}' does not exist.");
                }

                parentId = parent.Id;
            }

            try
            {
                var updated = CoreHub.ObjectManager.SetParent(node.Id, parentId);

                return updated is null ? new ResponseNotFound() : new ResponseOK();
            }
            catch (InvalidOperationException ex)
            {
                return Error(ex.Message);
            }
        }

        /// <summary>
        /// Resolves an object by its id (when the token is a <see cref="Guid"/>) or otherwise by
        /// its key.
        /// </summary>
        /// <param name="token">The object id or key.</param>
        /// <returns>The object, or <see langword="null"/> when no match exists.</returns>
        private static Model.Entities.Object Resolve(string token)
        {
            return Guid.TryParse(token, out var id)
                ? CoreHub.ObjectManager.GetObject(id)
                : CoreHub.ObjectManager.GetObjectByKey(token);
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
        }
    }
}
