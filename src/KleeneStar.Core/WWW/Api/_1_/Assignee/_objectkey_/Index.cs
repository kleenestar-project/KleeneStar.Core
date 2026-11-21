using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Assignee._objectkey_
{
    /// <summary>
    /// REST endpoint behind the assignee row of the people card
    /// (<see cref="ObjectPropertyPeopleCardFragment"/>), the framework's
    /// <see cref="ControlDataAssignment"/>. The URL is <c>/api/1/assignee/{objectkey}</c>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>GET</c> answers <c>{ assignee, self, mayChange }</c> and changes nothing.</item>
    ///   <item><c>PUT</c> with <c>{ userId }</c> assigns the object to that account, without a
    ///   user id to the caller.</item>
    ///   <item><c>DELETE</c> clears the assignment.</item>
    /// </list>
    /// An object the caller cannot see is unknown here, so it cannot be changed either; a change
    /// needs a signed-in caller who may write the object, and only an active account can be
    /// assigned.
    /// </remarks>
    [Title("kleenestar.core:object.assignee.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : RestApiAssignment<Model.Entities.Object>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Resolves the object the address names, through the object manager, so an object the
        /// caller may not read is not found.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        protected override Model.Entities.Object RetrieveItem(IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);
        }

        /// <summary>
        /// Returns the account the object is assigned to.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The assignee, or <see langword="null"/> when the object is unassigned.</returns>
        protected override RestApiAssignmentItem RetrieveAssignee(Model.Entities.Object item, IQueryContext context, IRequest request)
        {
            return ToItem(item?.AssigneeId);
        }

        /// <summary>
        /// Returns the caller, so the row can tell an assignment to them from one to someone else.
        /// </summary>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
        protected override RestApiAssignmentItem RetrieveSelf(IQueryContext context, IRequest request)
        {
            return ToItem(CoreHub.SessionManager.GetCurrentIdentityId(request));
        }

        /// <summary>
        /// Determines whether the caller may change the assignment: a signed-in caller who may
        /// write the object.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the assignment may be changed.</returns>
        protected override bool MayChange(Model.Entities.Object item, IQueryContext context, IRequest request)
        {
            return MayChange(item, request);
        }

        /// <summary>
        /// Assigns the object to an account, or to the caller when no account is named.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="userId">The account to assign, or empty for the caller.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the assignment was written.</returns>
        protected override bool Assign(Model.Entities.Object item, string userId, IQueryContext context, IRequest request)
        {
            if (!MayChange(item, request))
            {
                return false;
            }

            var identityId = string.IsNullOrWhiteSpace(userId)
                ? CoreHub.SessionManager.GetCurrentIdentityId(request)
                : Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

            if (identityId == Guid.Empty || CoreHub.IdentityManager.GetIdentity(identityId) is not { State: IdentityState.Active })
            {
                return false;
            }

            Write(item, identityId);

            return true;
        }

        /// <summary>
        /// Clears the assignment of the object.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        protected override void Unassign(Model.Entities.Object item, IQueryContext context, IRequest request)
        {
            if (MayChange(item, request))
            {
                Write(item, null);
            }
        }

        /// <summary>
        /// Converts an account into the entry the assignment row shows.
        /// </summary>
        /// <param name="identityId">The account, or <see langword="null"/>.</param>
        /// <returns>The entry, or <see langword="null"/> for no or an unknown account.</returns>
        internal static RestApiAssignmentItem ToItem(Guid? identityId)
        {
            var identity = identityId is Guid id && id != Guid.Empty
                ? CoreHub.IdentityManager.GetIdentity(id)
                : null;

            if (identity is null || string.IsNullOrWhiteSpace(identity.Name))
            {
                return null;
            }

            return new RestApiAssignmentItem
            {
                Id = identity.Id.ToString(),
                Name = identity.Name,
                Image = ObjectBoardProjection.AvatarImage(identity),
                Initials = ObjectBoardProjection.Initials(identity.Name)
            };
        }

        /// <summary>
        /// Determines whether the caller may change the assignment of an object.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> for a signed-in caller who may write the object.</returns>
        internal static bool MayChange(Model.Entities.Object item, IRequest request)
        {
            return item is not null
                && CoreHub.SessionManager.GetCurrentIdentityId(request) != Guid.Empty
                && ContentAuthorization.MayWrite(item, request);
        }

        /// <summary>
        /// Stores the assignee of an object.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="assigneeId">The account, or <see langword="null"/> to clear it.</param>
        private static void Write(Model.Entities.Object item, Guid? assigneeId)
        {
            item.AssigneeId = assigneeId;
            item.Updated = DateTime.UtcNow;

            CoreHub.ObjectManager.Update(item);
        }
    }
}
