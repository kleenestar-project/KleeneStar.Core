using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WebWorkflow;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// The data service of the transition dialog
    /// (<see cref="WebFragment.Object.ObjectTransitionFormFragment"/>): it loads the object's
    /// values into the form the transition shows, and on submit moves the object and stores what
    /// was filled in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog posts the field and the state it moves to as
    /// <see cref="FieldKey"/> and <see cref="StateKey"/> next to the values of the form. The move
    /// is decided first: <see cref="WebManager.IWorkflowManager.ExecuteTransition"/> hands the
    /// values to the validators as the screen of the transition, so a move that is refused leaves
    /// the record untouched and the dialog open with the reason.
    /// </para>
    /// <para>
    /// The values are written into the same commit as the state change, so the history shows one
    /// action rather than a transition followed by an unrelated edit. Workflow fields are never
    /// written from the dialog: a state moves only through a transition, and the form's copy of
    /// the old state would otherwise undo the one just made.
    /// </para>
    /// </remarks>
    [Cache]
    public sealed class Transition : RestApiCrud<Model.Entities.Object>
    {
        /// <summary>
        /// The payload key carrying the workflow field the transition moves.
        /// </summary>
        public const string FieldKey = "TransitionFieldId";

        /// <summary>
        /// The payload key carrying the state the transition moves to.
        /// </summary>
        public const string StateKey = "TransitionStateId";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Transition()
        {
        }

        /// <summary>
        /// Answers the dialog's read only to a caller who may move the object.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.GET)]
        public override IResponse Retrieve(IRequest request)
        {
            return MayTransition(request) ? base.Retrieve(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Moves the object, once the caller may.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return MayTransition(request) ? base.Update(request) : new ResponseForbidden();
        }

        /// <summary>
        /// Creates a query context backed by the application's database.
        /// </summary>
        /// <returns>The database context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the objects matching the query, as the caller may see them.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The objects.</returns>
        protected override IEnumerable<Model.Entities.Object> Retrieve(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.GetObjects(query, context);
        }

        /// <summary>
        /// Retrieves the object together with its field values, so the form of the transition
        /// starts from what the record holds.
        /// </summary>
        /// <param name="query">The query addressing the object.</param>
        /// <param name="request">The request.</param>
        /// <returns>The object's data.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Object> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ObjectManager.GetObjects(query, context).FirstOrDefault();
            var result = RetrieveForUpdate(request, data);

            if (data is not null)
            {
                Index.MergeFieldValues(result, data.Id, data.ClassId, request);
            }

            return result;
        }

        /// <summary>
        /// Runs the transition the dialog was opened for and stores the values of its form.
        /// </summary>
        /// <param name="existingItem">The object being moved.</param>
        /// <param name="payload">The values of the form, with the field and the target state.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result of the update.</returns>
        /// <exception cref="RestApiRefusal">The move was refused; the reason is the message.</exception>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Object existingItem, RestApiCrudFormData payload, IRequest request)
        {
            if (!TryTake(payload, FieldKey, out var fieldId) || !TryTake(payload, StateKey, out var stateId))
            {
                throw new RestApiRefusal(I18N.Translate(request, "kleenestar.core:object.property.workflow.transition.notfound"));
            }

            foreach (var name in WorkflowFieldNames(existingItem.ClassId))
            {
                payload.Remove(name);
            }

            var screen = payload.ToDictionary
            (
                x => x.Key,
                x => Index.SerializePayloadValue(x.Value),
                StringComparer.OrdinalIgnoreCase
            );
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            using var scope = CoreHub.CommitManager.BeginCommit(existingItem.Id, CommitType.Transitioned, identityId);

            var result = CoreHub.WorkflowManager.ExecuteTransition(existingItem.Id, fieldId, stateId, identityId, screen);

            if (!result.Succeeded)
            {
                scope.Abort();

                throw new RestApiRefusal(Explain(result, request));
            }

            Index.UpsertFieldValues(existingItem, payload, request);

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Determines whether the caller may move the object the request addresses.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may run a transition on it.</returns>
        private static bool MayTransition(IRequest request)
        {
            var target = ContentAuthorization.ReadId(request) is { } id
                ? CoreHub.ObjectManager.GetObject(id)
                : null;

            return ContentAuthorization.MayWrite(target, request, typeof(WebPermissions.TransitionExecutePermission));
        }

        /// <summary>
        /// Removes an id the dialog posted from the payload, so it is not mistaken for a value.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="key">The key the id travels under.</param>
        /// <param name="id">The id.</param>
        /// <returns><see langword="true"/> when the payload carried a valid id.</returns>
        private static bool TryTake(RestApiCrudFormData payload, string key, out Guid id)
        {
            id = Guid.Empty;

            return payload.Remove(key, out var value) && Guid.TryParse(value?.ToString(), out id);
        }

        /// <summary>
        /// Returns the names of the workflow fields of a class, which the dialog must not write.
        /// </summary>
        /// <param name="classId">The class of the object.</param>
        /// <returns>The field names.</returns>
        private static IEnumerable<string> WorkflowFieldNames(Guid classId)
        {
            return CoreHub.FieldManager
                .GetFields(new WebParameter.ClassIdParameter(classId))
                .Where(f => f.FieldType == FieldType.Workflow)
                .Select(f => f.Name);
        }

        /// <summary>
        /// Builds the sentence a refused move is shown with in the dialog. A validator names what
        /// is missing, so its findings are spelled out instead of the bare "rejected" the toast
        /// uses - the person is still looking at the form that can supply them.
        /// </summary>
        /// <param name="result">The outcome reported by the workflow manager.</param>
        /// <param name="request">The request, for the culture.</param>
        /// <returns>The translated sentence.</returns>
        private static string Explain(WebManager.WorkflowTransitionResult result, IRequest request)
        {
            if (result.Outcome != WebManager.WorkflowTransitionOutcome.ValidationFailed || result.ValidationErrors is not { Count: > 0 })
            {
                return WorkflowTransitionNotice.Explain(result, request);
            }

            return I18N.Translate(request, result.Message) + " " +
                string.Join(" ", result.ValidationErrors.Select(x => I18N.Translate(request, x)));
        }
    }
}
