using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The CRUD endpoint of a record of a class's structure - a field, form, workflow, priority,
    /// status, agreement, calendar, security level or template - that only the administrators of
    /// the class may change.
    /// </summary>
    /// <remarks>
    /// The administration pages are guarded by <c>RouteAuthorization</c>, but a dialog is only
    /// one caller of its endpoint. This base asks the same question at the endpoint, once for
    /// every structure record: a create is judged on the class the payload names, a clone, an
    /// update and a delete on the class of the record addressed by <c>id</c>, and so are the
    /// dialog reads (<c>mode=new|clone|edit|delete</c>), so the form data of a refused dialog is
    /// not answered either. A plain read is left open - the object pages draw themselves from
    /// the same structure, and the object reads are narrowed where they happen.
    /// </remarks>
    /// <typeparam name="TIndexItem">The structure record.</typeparam>
    public abstract class RestApiCrudClassStructure<TIndexItem> : RestApiCrud<TIndexItem>
        where TIndexItem : IIndexItem
    {
        /// <summary>
        /// Returns the class a record belongs to.
        /// </summary>
        /// <param name="item">The record.</param>
        /// <returns>The class id.</returns>
        protected abstract Guid? ClassOf(TIndexItem item);

        /// <summary>
        /// Determines whether the caller may create a record from nothing. The default judges
        /// the class the payload names (<c>ClassId</c>); a record that is not beneath a class
        /// says what it is beneath instead.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the record may be created.</returns>
        protected virtual bool MayCreate(IRequest request)
        {
            return ContentAuthorization.MayAdminister(ContentAuthorization.ReadPayloadGuid(request, "classid"), request);
        }

        /// <summary>
        /// Answers the dialog reads only to a caller who may administer the class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            var mode = request?.GetParameter("mode")?.Value;
            var id = ContentAuthorization.ReadId(request);

            var authorized = mode switch
            {
                "edit" or "delete" or "clone" => ContentAuthorization.MayAdminister(ClassOfId(id, request), request),
                "new" => id is null
                    ? MayCreate(request)
                    : ContentAuthorization.MayAdminister(ClassOfId(id, request), request),
                _ => true
            };

            return authorized ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Creates or clones a record, once the caller may administer its class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            var id = ContentAuthorization.ReadId(request);
            var authorized = id is null
                ? MayCreate(request)
                : ContentAuthorization.MayAdminister(ClassOfId(id, request), request);

            return authorized
                ? base.Create(request)
                : new ResponseForbidden();
        }

        /// <summary>
        /// Changes a record, once the caller may administer its class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            var id = ContentAuthorization.ReadId(request);

            if (!ContentAuthorization.MayAdminister(ClassOfId(id, request), request))
            {
                return new ResponseForbidden();
            }

            return RefuseManaged(id, request) ?? base.Update(request);
        }

        /// <summary>
        /// Refuses to change a managed copy of a blueprint element locally.
        /// </summary>
        /// <remarks>
        /// A field, status, priority or workflow a follower received from its blueprint is changed
        /// in the blueprint, where the change reaches every follower alike; a local change would
        /// be overwritten by the next synchronization and, until then, make the workspaces
        /// disagree about a structure they are meant to share. The refusal names the reason, so
        /// the dialog can show it.
        /// </remarks>
        /// <param name="id">The record id, may be absent.</param>
        /// <param name="request">The request.</param>
        /// <returns>The refusal, or <see langword="null"/> when the record is the class's own.</returns>
        private IResponse RefuseManaged(Guid? id, IRequest request)
        {
            if (Resolve(id, request) is not Model.Entities.IBlueprintManaged { BlueprintSourceId: not null })
            {
                return null;
            }

            var message = WebExpress.WebCore.Internationalization.I18N.Translate(request, "kleenestar.core:blueprint.managed");

            return RestApiFault.BadRequest(request, new RestApiRefusal(message), message);
        }

        /// <summary>
        /// Deletes a record, once the caller may administer its class.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            var id = ContentAuthorization.ReadId(request);

            if (!ContentAuthorization.MayAdminister(ClassOfId(id, request), request))
            {
                return new ResponseForbidden();
            }

            return RefuseManaged(id, request) ?? base.Delete(request);
        }

        /// <summary>
        /// Resolves the class of the record an id addresses.
        /// </summary>
        /// <param name="id">The record id, may be absent.</param>
        /// <param name="request">The request.</param>
        /// <returns>The class id, or <see langword="null"/> when nothing resolves.</returns>
        private Guid? ClassOfId(Guid? id, IRequest request)
        {
            var item = Resolve(id, request);

            return item is null ? null : ClassOf(item);
        }

        /// <summary>
        /// Resolves the record an id addresses.
        /// </summary>
        /// <param name="id">The record id, may be absent.</param>
        /// <param name="request">The request.</param>
        /// <returns>The record, or <see langword="null"/>.</returns>
        private TIndexItem Resolve(Guid? id, IRequest request)
        {
            if (id is not { } recordId)
            {
                return default;
            }

            using var context = CreateContext();

            return Retrieve(new Query<TIndexItem>().WhereEquals(x => x.Id, recordId), context, request)
                .FirstOrDefault();
        }
    }
}
