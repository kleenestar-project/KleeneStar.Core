using KleeneStar.Core.WebInsight;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebRestApi;
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
using ScrumProjection = KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_.ScrumProjection;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// The backlog of an insight's Scrum tab: the sprints of the workspaces the insight's
    /// objects live in, and the active objects planned into them or still in a backlog.
    /// </summary>
    /// <remarks>
    /// A sprint belongs to one workspace and an object can only be planned into a sprint of its
    /// own workspace, so an insight that spans workspaces names each sprint with its workspace
    /// key and refuses a move across workspaces. A new sprint needs a workspace to belong to:
    /// it is created when the insight's objects live in exactly one, and refused otherwise.
    /// Every change is a change of the sprint's or the object's workspace and needs the right
    /// to change that workspace's content - an insight grants no rights on what it shows.
    /// </remarks>
    [Title("kleenestar.core:object.view.scrum.backlog.title")]
    [Cache]
    public sealed class ScrumBacklog : RestApiScrumBacklog<Sprint, Model.Entities.Object>
    {
        /// <summary>
        /// Creates the query context of the endpoint.
        /// </summary>
        /// <returns>A database context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Creates a sprint, for a caller who may read the insight.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            return InsightScope.ResolveReadable(request) is null ? new ResponseForbidden() : base.Create(request);
        }

        /// <summary>
        /// Changes a sprint or moves, ranks and estimates an item, for a caller who may read the
        /// insight; the workspace of what changes is checked by the validation.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public override IResponse Update(IRequest request)
        {
            return InsightScope.ResolveReadable(request) is null ? new ResponseForbidden() : base.Update(request);
        }

        /// <summary>
        /// Deletes a sprint, for a caller who may read the insight; the sprint's workspace is
        /// checked when the sprint is resolved.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return InsightScope.ResolveReadable(request) is null ? new ResponseForbidden() : base.Delete(request);
        }

        /// <summary>
        /// Returns the sprints of the workspaces the insight's objects live in.
        /// </summary>
        /// <param name="query">The query criteria (unused; the insight scopes the set).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The sprints.</returns>
        protected override IEnumerable<Sprint> RetrieveSprints(IQuery<Sprint> query, IQueryContext context, IRequest request)
        {
            return Workspaces(request)
                .SelectMany(CoreHub.SprintManager.GetSprintsForWorkspace)
                .ToList();
        }

        /// <summary>
        /// Returns the active objects of the insight, ordered by their sprint rank and narrowed
        /// by the view's search term and quickfilter chips.
        /// </summary>
        /// <param name="query">The query criteria (unused; the insight scopes the set).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The objects.</returns>
        protected override IEnumerable<Model.Entities.Object> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var items = InsightScope.GetObjects(InsightScope.ResolveReadable(request))
                .Where(x => x.State == WorkspaceState.Active)
                .OrderBy(x => x.SprintRank)
                .ThenBy(x => x.Key)
                .AsEnumerable();

            return ObjectKindBoardFilter.Apply(items, request, InsightScope.QuickfilterView).ToList();
        }

        /// <summary>
        /// Converts a sprint into the REST sprint DTO, naming its workspace when the insight
        /// spans several - two workspaces may both be in their "Sprint 4".
        /// </summary>
        /// <param name="sprint">The sprint.</param>
        /// <returns>The REST sprint DTO.</returns>
        protected override RestApiScrumSprintItem ToRestSprint(Sprint sprint)
        {
            var item = ScrumProjection.ToRestSprint(sprint);
            var request = WebExpress.WebCore.WebEx.CurrentRequest;

            if (Workspaces(request).Count > 1)
            {
                var key = CoreHub.WorkspaceManager.GetWorkspace(sprint.WorkspaceId)?.Key;

                item.Name = string.IsNullOrWhiteSpace(key) ? item.Name : $"{key} · {item.Name}";
            }

            return item;
        }

        /// <summary>
        /// Converts an object into the REST item DTO.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <returns>The REST item DTO.</returns>
        protected override RestApiScrumItem ToRestItem(Model.Entities.Object item)
        {
            return ScrumProjection.ToRestItem(item);
        }

        /// <summary>
        /// Validates a sprint: it needs a name, a new one needs exactly one workspace to belong
        /// to, and the caller has to be allowed to change that workspace's content.
        /// </summary>
        /// <param name="existingSprint">The sprint, or <see langword="null"/> on create.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult ValidateSprint(Sprint existingSprint, RestApiSprintPayload payload, IRequest request)
        {
            var result = new RestApiValidationResult();

            if (existingSprint is null && string.IsNullOrWhiteSpace(payload?.Name))
            {
                result.Add("A sprint needs a name.", "name");
            }

            var workspaceId = existingSprint?.WorkspaceId ?? SingleWorkspace(request);

            if (workspaceId is null)
            {
                result.Add(I18N.Translate(request, "kleenestar.core:insight.scrum.validation.workspace"), "name");
            }
            else if (!MayWriteContent(workspaceId.Value, request))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:insight.scrum.validation.permission"), "name");
            }

            return result;
        }

        /// <summary>
        /// Refuses a move into a sprint of another workspace, or by a caller who may not change
        /// the object's workspace.
        /// </summary>
        /// <param name="existingItem">The object.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult ValidateMove(Model.Entities.Object existingItem, RestApiScrumMovePayload payload, IRequest request)
        {
            return ValidatePlacement(existingItem, payload?.SprintId, request);
        }

        /// <summary>
        /// Refuses a rank that moves the object into a sprint of another workspace, or by a
        /// caller who may not change the object's workspace.
        /// </summary>
        /// <param name="existingItem">The object.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult ValidateRank(Model.Entities.Object existingItem, RestApiScrumRankPayload payload, IRequest request)
        {
            return ValidatePlacement(existingItem, payload?.SprintId, request);
        }

        /// <summary>
        /// Refuses an estimate or an assignment by a caller who may not change the object.
        /// </summary>
        /// <param name="existingItem">The object.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult ValidateItem(Model.Entities.Object existingItem, RestApiScrumItemPayload payload, IRequest request)
        {
            var result = new RestApiValidationResult();

            if (!ContentAuthorization.MayWrite(existingItem, request))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:insight.scrum.validation.permission"), "id");
            }

            return result;
        }

        /// <summary>
        /// Creates a sprint in the one workspace the insight's objects live in.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <param name="newSprint">The created sprint.</param>
        /// <returns>The creation result, or <see langword="null"/> when there is no single workspace.</returns>
        protected override IRestApiCrudResultCreate CreateSprint(RestApiSprintPayload payload, IRequest request, out Sprint newSprint)
        {
            newSprint = default;

            if (SingleWorkspace(request) is not Guid workspaceId)
            {
                return null;
            }

            newSprint = new Sprint
            {
                Name = payload.Name?.Trim(),
                Goal = payload.Goal,
                State = SprintStateExtensions.FromCode(payload.Status),
                Start = ScrumProjection.ParseDate(payload.Start),
                End = ScrumProjection.ParseDate(payload.End),
                Capacity = payload.Capacity ?? 0,
                WorkspaceId = workspaceId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.SprintManager.AddSprint(newSprint);

            return new RestApiCrudResultCreate
            {
                Data = ToRestSprint(newSprint)
            };
        }

        /// <summary>
        /// Updates the metadata or state of a sprint.
        /// </summary>
        /// <param name="existingSprint">The sprint.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate UpdateSprint(Sprint existingSprint, RestApiSprintPayload payload, IRequest request)
        {
            existingSprint.Name = string.IsNullOrWhiteSpace(payload.Name) ? existingSprint.Name : payload.Name.Trim();
            existingSprint.Goal = payload.Goal ?? existingSprint.Goal;
            existingSprint.State = payload.Status is null ? existingSprint.State : SprintStateExtensions.FromCode(payload.Status);
            existingSprint.Start = payload.Start is null ? existingSprint.Start : ScrumProjection.ParseDate(payload.Start);
            existingSprint.End = payload.End is null ? existingSprint.End : ScrumProjection.ParseDate(payload.End);
            existingSprint.Capacity = payload.Capacity ?? existingSprint.Capacity;

            CoreHub.SprintManager.UpdateSprint(existingSprint);

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Moves an object into a sprint or back to the backlog.
        /// </summary>
        /// <param name="existingItem">The object.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate MoveItem(Model.Entities.Object existingItem, RestApiScrumMovePayload payload, IRequest request)
        {
            CoreHub.SprintManager.MoveObjectToSprint(existingItem.Id, ParseSprintId(payload.SprintId));

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Re-ranks an object within a sprint or the backlog.
        /// </summary>
        /// <param name="existingItem">The object.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate RankItem(Model.Entities.Object existingItem, RestApiScrumRankPayload payload, IRequest request)
        {
            var target = payload.SprintId is null ? existingItem.SprintId : ParseSprintId(payload.SprintId);

            CoreHub.SprintManager.MoveObjectToSprint(existingItem.Id, target, payload.Rank);

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Updates the assignee and the estimate of an object.
        /// </summary>
        /// <param name="existingItem">The object.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate UpdateItem(Model.Entities.Object existingItem, RestApiScrumItemPayload payload, IRequest request)
        {
            if (payload.Points is not null)
            {
                CoreHub.SprintManager.SetStoryPoints(existingItem.Id, payload.Points);
            }

            if (payload.AssigneeId is not null)
            {
                existingItem.AssigneeId = Guid.TryParse(payload.AssigneeId, out var assigneeId) ? assigneeId : null;
                CoreHub.ObjectManager.Update(existingItem);
            }

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Deletes a sprint, for a caller who may change its workspace's content; its objects go
        /// back to the backlog.
        /// </summary>
        /// <param name="existingSprint">The sprint.</param>
        /// <param name="request">The request.</param>
        /// <returns>The delete result, or <see langword="null"/> when refused.</returns>
        protected override IRestApiCrudResultDelete DeleteSprint(Sprint existingSprint, IRequest request)
        {
            if (!MayWriteContent(existingSprint.WorkspaceId, request))
            {
                return null;
            }

            CoreHub.SprintManager.RemoveSprint(existingSprint);

            return new RestApiCrudResultDelete();
        }

        /// <summary>
        /// Checks that an object may be placed into a sprint (or the backlog) by the caller.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="sprintId">The target sprint id from the payload, blank for the backlog.</param>
        /// <param name="request">The request.</param>
        /// <returns>The validation result.</returns>
        private static RestApiValidationResult ValidatePlacement(Model.Entities.Object item, string sprintId, IRequest request)
        {
            var result = new RestApiValidationResult();

            if (item is null)
            {
                return result;
            }

            if (!MayWriteContent(item.WorkspaceId, request))
            {
                result.Add(I18N.Translate(request, "kleenestar.core:insight.scrum.validation.permission"), "sprintId");

                return result;
            }

            var target = ParseSprintId(sprintId);
            var sprint = target is Guid id ? CoreHub.SprintManager.GetSprint(id) : null;

            if (sprint is not null && sprint.WorkspaceId != item.WorkspaceId)
            {
                result.Add(I18N.Translate(request, "kleenestar.core:insight.scrum.validation.foreign"), "sprintId");
            }

            return result;
        }

        /// <summary>
        /// Returns the workspaces the insight's objects live in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace ids.</returns>
        private static IReadOnlyList<Guid> Workspaces(IRequest request)
        {
            return
            [
                .. InsightScope.GetObjects(InsightScope.ResolveReadable(request))
                    .Select(x => x.WorkspaceId)
                    .Distinct()
            ];
        }

        /// <summary>
        /// Returns the one workspace the insight's objects live in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace id, or <see langword="null"/> for none or several.</returns>
        private static Guid? SingleWorkspace(IRequest request)
        {
            var workspaces = Workspaces(request);

            return workspaces.Count == 1 ? workspaces[0] : null;
        }

        /// <summary>
        /// Determines whether the caller may change the content of a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        private static bool MayWriteContent(Guid workspaceId, IRequest request)
        {
            var workspace = CoreHub.WorkspaceManager.GetWorkspace(workspaceId);

            return workspace is not null
                && PageAuthorization.IsGranted(request, typeof(WebPermissions.WorkspaceWriteContentPermission), PageAuthorization.ChainOf(workspace));
        }

        /// <summary>
        /// Parses a sprint id of a payload; blank, "backlog" or an unparsable value is the backlog.
        /// </summary>
        /// <param name="sprintId">The sprint id.</param>
        /// <returns>The sprint id, or <see langword="null"/> for the backlog.</returns>
        private static Guid? ParseSprintId(string sprintId)
        {
            return Guid.TryParse(sprintId, out var id) && id != Guid.Empty ? id : null;
        }
    }
}
