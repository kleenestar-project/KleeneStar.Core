using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;

namespace KleeneStar.Core.WWW.Api._1_.Priorities
{
    /// <summary>
    /// Moves a priority one position within the order of its class.
    /// </summary>
    /// <remarks>
    /// The table itself persists a whole arranged order through its configure endpoint, which is
    /// what dragging a row produces. This endpoint serves the row option menu instead, where only a
    /// single entry and a direction are known, and it exists so that the order can also be changed
    /// without a pointer.
    /// </remarks>
    [Title("Priority move")]
    public sealed class Move : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Move()
        {
        }

        /// <summary>
        /// Moves the priority identified by the <c>id</c> parameter towards the start or the end,
        /// depending on the <c>direction</c> parameter.
        /// </summary>
        /// <param name="request">The request providing the id and the direction.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        public IResponse MovePriority(IRequest request)
        {
            var id = request?.GetParameter("id")?.Value;
            var direction = request?.GetParameter("direction")?.Value;

            if (!Guid.TryParse(id, out var priorityId))
            {
                return new ResponseBadRequest(new StatusMessage("A valid id is required."));
            }

            var up = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase);

            if (!up && !string.Equals(direction, "down", StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseBadRequest(new StatusMessage("The direction must be 'up' or 'down'."));
            }

            // reordering a scale is administering the class it belongs to
            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayAdminister(CoreHub.PriorityManager.GetPriority(priorityId)?.ClassId, request))
            {
                return new ResponseForbidden();
            }

            CoreHub.PriorityManager.Move(priorityId, up);

            return new ResponseOK();
        }
    }
}
