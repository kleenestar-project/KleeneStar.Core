using KleeneStar.Core.WebManager;
using System;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The outcome of one move.
    /// </summary>
    /// <param name="Status">The HTTP status the move answers: 200, 400, 403, 404 or 409.</param>
    /// <param name="Error">The reason of a refusal, or <c>null</c>.</param>
    /// <param name="Moved">The object after the move, or <c>null</c>.</param>
    public sealed record ObjectMoveResult(int Status, string Error, ObjectEntity Moved, string Code = null);

    /// <summary>
    /// The one implementation of "move this object below that one" behind the REST surfaces
    /// that offer it - <c>/api/1/objects/move</c> (the drag-and-drop tree) and
    /// <c>PUT /api/1/hierarchy/{objectkey}</c>.
    /// </summary>
    /// <remarks>
    /// Both used to resolve the operands, ask only whether the caller may change the moved
    /// object, and turn every refusal into a <c>400</c>. They now share the three answers a move
    /// can get besides success: <c>403</c> when the caller may not change the object, the new
    /// parent or the old one (<see cref="ContentAuthorization.MayMove"/>); <c>400</c> when the
    /// move breaks a hierarchy rule (self, cycle, other workspace, depth, allowed children);
    /// <c>409</c> when it conflicts with the moment - the object was moved meanwhile (the
    /// optional <c>expectedParent</c> of the body), or another move of the workspace held the
    /// lock too long.
    /// </remarks>
    public static class ObjectMove
    {
        /// <summary>
        /// Serialization options of the error bodies: camelCase, as every JSON body here.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Resolves an object by its id (when the token is a <see cref="Guid"/>) or otherwise by
        /// its key, narrowed to what the caller may see.
        /// </summary>
        /// <param name="token">The object id or key.</param>
        /// <returns>The object, or <see langword="null"/> when no match exists.</returns>
        public static ObjectEntity Resolve(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return Guid.TryParse(token, out var id)
                ? CoreHub.ObjectManager.GetObject(id)
                : CoreHub.ObjectManager.GetObjectByKey(token);
        }

        /// <summary>
        /// Reads the optional <c>expectedParent</c> member of a move body.
        /// </summary>
        /// <remarks>
        /// Three states, which is why the member is read as a raw element rather than a string:
        /// absent (no expectation - the client does not know or does not care), <c>null</c> (the
        /// client saw the object at the root), and a key or id (the client saw it below that
        /// object). A key that no longer resolves cannot be the object's parent, so it reads as
        /// an expectation that fails.
        /// </remarks>
        /// <param name="element">The member, <see cref="JsonValueKind.Undefined"/> when absent.</param>
        /// <param name="expectedParentId">The expected parent, <see langword="null"/> for the root.</param>
        /// <returns><see langword="true"/> when the body states an expectation.</returns>
        public static bool ReadExpectation(JsonElement element, out Guid? expectedParentId)
        {
            expectedParentId = null;

            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                    return true;
                case JsonValueKind.String:
                    var token = element.GetString();

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        return true;
                    }

                    // an unknown parent never matches: Guid.Empty is no object's id
                    expectedParentId = Resolve(token)?.Id ?? Guid.Empty;

                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Authorizes and carries out a move.
        /// </summary>
        /// <param name="node">The object to move. Must not be null.</param>
        /// <param name="parent">The new parent, or <see langword="null"/> to detach to the root.</param>
        /// <param name="expectation">The body's <c>expectedParent</c> member (see <see cref="ReadExpectation"/>).</param>
        /// <param name="request">The request.</param>
        /// <param name="moved">The object after the move, or <see langword="null"/> when it vanished.</param>
        /// <returns>
        /// <see langword="null"/> when the move was carried out (or changed nothing); otherwise
        /// the refusal to answer with.
        /// </returns>
        public static IResponse Execute(ObjectEntity node, ObjectEntity parent, JsonElement expectation, IRequest request, out ObjectEntity moved)
        {
            var result = Attempt(node, parent, expectation, request);

            moved = result.Moved;

            return result.Status switch
            {
                200 => null,
                403 => new ResponseForbidden(),
                404 => new ResponseNotFound(),
                409 => Conflict(result.Error),
                _ => BadRequest(result.Error)
            };
        }

        /// <summary>
        /// Authorizes and carries out a move, answering the outcome as data - the form a batch
        /// of moves reports per object.
        /// </summary>
        /// <param name="node">The object to move. Must not be null.</param>
        /// <param name="parent">The new parent, or <see langword="null"/> to detach to the root.</param>
        /// <param name="expectation">The body's <c>expectedParent</c> member (see <see cref="ReadExpectation"/>).</param>
        /// <param name="request">The request.</param>
        /// <returns>The outcome.</returns>
        public static ObjectMoveResult Attempt(ObjectEntity node, ObjectEntity parent, JsonElement expectation, IRequest request)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (!ContentAuthorization.MayMove(node, parent, request))
            {
                return new ObjectMoveResult(403, "You may not move this object there.", null);
            }

            try
            {
                var moved = ReadExpectation(expectation, out var expectedParentId)
                    ? CoreHub.ObjectManager.SetParent(node.Id, parent?.Id, expectedParentId)
                    : CoreHub.ObjectManager.SetParent(node.Id, parent?.Id);

                return moved is null
                    ? new ObjectMoveResult(404, "The object does not exist.", null)
                    : new ObjectMoveResult(200, null, moved);
            }
            catch (HierarchyConflictException ex)
            {
                return new ObjectMoveResult(409, ex.Message, null);
            }
            catch (InvalidOperationException ex)
            {
                return new ObjectMoveResult(400, ex.Message, null);
            }
        }

        /// <summary>
        /// Wraps a message into a JSON <c>400</c> response of the form <c>{ "error": … }</c>.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The response.</returns>
        public static IResponse BadRequest(string message)
        {
            return new ResponseBadRequest
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = message }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Wraps a message into a JSON <c>409</c> response of the form <c>{ "error": … }</c>.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The response.</returns>
        public static IResponse Conflict(string message)
        {
            return new ResponseConflict
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = message }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }
    }
}
