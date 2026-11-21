using KleeneStar.Core.WebManager;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// REST endpoint that flips the calling identity's like on an object. The URL is the fixed
    /// <c>/api/1/objects/like</c>; the body names the object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>POST {base}</c> expects <c>{ "object": "SD-45000" }</c> - an object key or id - and
    /// answers <c>{ "value": "7", "active": true }</c>: the new count and whether the caller is
    /// among it. That is exactly what a feed's figure needs to repaint itself, so the surface
    /// that asked for the change does not have to re-query to show it.
    /// </para>
    /// <para>
    /// It toggles rather than taking a state, because the only caller is a control showing the
    /// current state and a reader clicking it. A caller that wanted to set a state would have to
    /// read one first, and would then be racing anybody else clicking.
    /// </para>
    /// <para>
    /// The URL is fixed rather than carrying the object as a segment, like the sibling
    /// <see cref="Move"/>: both are operations on an object named in the body, and neither is a
    /// resource anybody would address on its own.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.like.api.title")]
    [Cache]
    public sealed class Like : IRestApi
    {
        /// <summary>
        /// Serialization options for the payloads: camelCase property names, case-insensitive
        /// binding.
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
        public Like()
        {
        }

        /// <summary>
        /// Handles <c>POST {base}</c>: flips the caller's like on the named object.
        /// </summary>
        /// <param name="request">The incoming request carrying the JSON body.</param>
        /// <returns>
        /// <c>200</c> with the new count and state, <c>404</c> when the object cannot be
        /// resolved, <c>400</c> when the payload is malformed, and <c>401</c> when nobody is
        /// signed in - a like belongs to somebody.
        /// </returns>
        [Method(RequestMethod.POST)]
        public IResponse Toggle(IRequest request)
        {
            var content = (request as Request)?.Content;

            if (content is null || content.Length == 0)
            {
                return new ResponseBadRequest(new StatusMessage("Invalid or empty JSON payload."))
                    .AddHeaderContentType("application/json");
            }

            LikePayload payload;

            try
            {
                payload = JsonSerializer.Deserialize<LikePayload>(Encoding.UTF8.GetString(content), _jsonOptions);
            }
            catch (JsonException)
            {
                return new ResponseBadRequest(new StatusMessage("Invalid or empty JSON payload."))
                    .AddHeaderContentType("application/json");
            }

            var objectEntity = Resolve(payload?.Object);

            if (objectEntity is null)
            {
                return new ResponseNotFound(new StatusMessage("Object not found."))
                    .AddHeaderContentType("application/json");
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == System.Guid.Empty)
            {
                return new ResponseUnauthorized(new StatusMessage("A like belongs to somebody."))
                    .AddHeaderContentType("application/json");
            }

            var liked = !CoreHub.ObjectManager.IsLiked(identityId, objectEntity.Id);

            CoreHub.ObjectManager.SetLike(identityId, objectEntity.Id, liked);

            var result = new
            {
                value = CoreHub.ObjectManager.GetLikeCount(objectEntity.Id).ToString(),
                active = liked
            };

            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result, _jsonOptions))
            }
            .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Resolves an object from a key or an id.
        /// </summary>
        /// <param name="value">The key or id naming the object.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static Model.Entities.Object Resolve(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return System.Guid.TryParse(value, out var id)
                ? CoreHub.ObjectManager.GetObject(id)
                : CoreHub.ObjectManager.GetObjectByKey(value);
        }

        /// <summary>
        /// The body of a like request.
        /// </summary>
        private sealed class LikePayload
        {
            /// <summary>
            /// Gets or sets the key or id of the object being liked.
            /// </summary>
            public string Object { get; set; }
        }
    }
}
