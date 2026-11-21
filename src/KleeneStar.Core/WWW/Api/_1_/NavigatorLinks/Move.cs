using KleeneStar.Core.WebRestApi;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;

namespace KleeneStar.Core.WWW.Api._1_.NavigatorLinks
{
    /// <summary>
    /// Moves a navigator link one position within the app navigator.
    /// </summary>
    /// <remarks>
    /// The table itself persists a whole arranged order through its configure endpoint, which is
    /// what dragging a row produces. This endpoint serves the row option menu instead, where only a
    /// single entry and a direction are known, and it exists so that the order can also be changed
    /// without a pointer.
    /// </remarks>
    [Title("Navigator link move")]
    public sealed class Move : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Move()
        {
        }

        /// <summary>
        /// Moves the link identified by the <c>id</c> parameter towards the start or the end,
        /// depending on the <c>direction</c> parameter.
        /// </summary>
        /// <param name="request">The request providing the id and the direction.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        public IResponse MoveLink(IRequest request)
        {
            // the order is the navigator of every user; only an administrator arranges it
            if (!AccountAuthorization.IsAdministrator(request))
            {
                return new ResponseForbidden();
            }

            var id = request?.GetParameter("id")?.Value;
            var direction = request?.GetParameter("direction")?.Value;

            if (!Guid.TryParse(id, out var linkId))
            {
                return new ResponseBadRequest(new StatusMessage("A valid id is required."));
            }

            var up = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase);

            if (!up && !string.Equals(direction, "down", StringComparison.OrdinalIgnoreCase))
            {
                return new ResponseBadRequest(new StatusMessage("The direction must be 'up' or 'down'."));
            }

            CoreHub.NavigatorLinkManager.Move(linkId, up);

            return new ResponseOK();
        }
    }
}
