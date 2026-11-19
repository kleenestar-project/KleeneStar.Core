using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Transfers
{
    /// <summary>
    /// Starts the move of an object with its subtree into another workspace from the answers of
    /// the transfer wizard (<c>ObjectTransferFormFragment</c>).
    /// </summary>
    /// <remarks>
    /// <c>POST</c> with the answers as form parameters (<see cref="ObjectTransferForm"/>). The
    /// answer is <c>{ data, uri }</c> naming the page of the moved object, which the wizard follows;
    /// a run still processed by the worker names the object's current page, and its summary
    /// arrives in the notification center. A refusal is <c>400 { message }</c>, which the wizard
    /// shows; nobody signed in is <c>403</c>, an object the caller cannot see or change <c>404</c>.
    /// <c>GET</c> answers the empty starting values the wizard loads when it opens.
    /// </remarks>
    [Title("kleenestar.core:transfer.api.title")]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the starting values the wizard loads when it opens (<c>?mode=new</c>): none,
        /// every answer comes from its steps.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The empty starting values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            return new WebExpress.WebApp.WebRestApi.RestApiCrudResultRetrieve().ToResponse();
        }

        /// <summary>
        /// Starts the move.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The address to follow, or the refusal.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Persist(IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return new ResponseForbidden();
            }

            var answers = ObjectTransferForm.Read(request);
            var @object = ObjectTransferForm.ResolveObject(answers);

            if (@object is null || !ContentAuthorization.MayWrite(@object, request))
            {
                return new ResponseNotFound();
            }

            var target = ObjectTransferForm.ResolveTarget(answers, @object);

            if (target is null)
            {
                return JsonApi.Refuse(WebExpress.WebCore.Internationalization.I18N.Translate(request, "kleenestar.core:transfer.refused.workspace"));
            }

            answers.TryGetValue(ObjectTransferForm.KeyField, out var key);

            var started = CoreHub.ObjectOperationManager.StartTransfer(@object.Id, ObjectTransferForm.Options(answers, target),
                string.IsNullOrWhiteSpace(key) ? null : key, identityId,
                x => ContentAuthorization.MayWrite(x, request), classId => ContentAuthorization.MayCreateIn(classId, request));

            if (started.Operation is null)
            {
                return JsonApi.Refuse(Operations._workspacekey_.Index.Explain(started.Plan, request));
            }

            if (started.Operation.State == ObjectOperationState.Failed)
            {
                return JsonApi.Refuse(started.Operation.Error
                    ?? WebExpress.WebCore.Internationalization.I18N.Translate(request, "kleenestar.core:transfer.dialog.failed"));
            }

            // the moved object is read again: a finished run carries its new key
            var moved = CoreHub.ObjectManager.GetObject(@object.Id) ?? @object;

            return new RestApiCreatedResult
            {
                // created keeps objectrefresh.js from reloading while the wizard follows the uri
                Data = new { created = true, id = moved.Id, key = moved.Key, state = started.Operation.State.ToString().ToLowerInvariant() },
                Uri = CoreHub.ObjectKindManager.ResolveDetailUri(moved)?.ToString()
            }
                .ToResponse();
        }
    }
}
