using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// REST API scrum backlog endpoint for the objects of a workspace. Sprints come from
    /// the <see cref="CoreHub.SprintManager"/>, items are the active objects of the
    /// workspace projected via <see cref="ObjectBoardProjection"/>. Sprint creation,
    /// editing, activation and deletion as well as item move/rank/estimation are
    /// persisted through the sprint manager.
    /// </summary>
    [Title("kleenestar.core:object.view.scrum.backlog.title")]
    [Cache]
    public sealed class ScrumBacklog : RestApiScrumBacklog<Sprint, Model.Entities.Object>
    {
        /// <summary>
        /// Returns a <see cref="KleeneStarDbContext"/> so the managers can run their
        /// queries against the real database.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the sprints of the workspace addressed by the request route.
        /// </summary>
        /// <param name="query">The query criteria (unused; the route scopes the set).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The sprints of the workspace.</returns>
        protected override IEnumerable<Sprint> RetrieveSprints(IQuery<Sprint> query, IQueryContext context, IRequest request)
        {
            var workspace = ScrumProjection.GetWorkspace(request);

            return workspace is null
                ? []
                : CoreHub.SprintManager.GetSprintsForWorkspace(workspace.Id);
        }

        /// <summary>
        /// Returns the active objects of the workspace addressed by the request route,
        /// ordered by their sprint rank and narrowed by the view's search term and
        /// quickfilter chips.
        /// </summary>
        /// <remarks>
        /// The search and the chips are applied here rather than through the inherited
        /// <c>Filter</c> hooks because <see cref="ScrumProjection.GetItems"/> already
        /// resolves the set in memory and the base passes its <see cref="IQuery{T}"/> to a
        /// retrieval that ignores it. Applying them in memory also matches
        /// <see cref="ScrumSprintKanban.ApplyQuickfilter"/>, so the board and the backlog
        /// read one filter the same way.
        /// </remarks>
        /// <param name="query">The query criteria (unused; the route scopes the set).</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The matching active objects of the workspace.</returns>
        protected override IEnumerable<Model.Entities.Object> RetrieveItems(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var items = ScrumProjection.GetItems(request);

            items = ScrumProjection.ApplySearch(items, request);
            items = ScrumProjection.ApplyQuickfilter(items, request);

            return items;
        }

        /// <summary>
        /// Converts a sprint entity into the REST sprint DTO.
        /// </summary>
        /// <param name="sprint">The sprint entity.</param>
        /// <returns>The REST sprint DTO.</returns>
        protected override RestApiScrumSprintItem ToRestSprint(Sprint sprint)
        {
            return ScrumProjection.ToRestSprint(sprint);
        }

        /// <summary>
        /// Converts an object entity into the REST item DTO.
        /// </summary>
        /// <param name="item">The object entity.</param>
        /// <returns>The REST item DTO.</returns>
        protected override RestApiScrumItem ToRestItem(Model.Entities.Object item)
        {
            return ScrumProjection.ToRestItem(item);
        }

        /// <summary>
        /// Validates a sprint payload: a sprint needs a non-empty name.
        /// </summary>
        /// <param name="existingSprint">The existing sprint, or <see langword="null"/> on create.</param>
        /// <param name="payload">The payload to validate.</param>
        /// <param name="request">The request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult ValidateSprint(Sprint existingSprint, RestApiSprintPayload payload, IRequest request)
        {
            var result = new RestApiValidationResult();

            if (string.IsNullOrWhiteSpace(payload?.Name))
            {
                result.Add("A sprint needs a name.", "name");
            }

            return result;
        }

        /// <summary>
        /// Creates a new sprint in the workspace addressed by the request route.
        /// </summary>
        /// <param name="payload">The sprint payload.</param>
        /// <param name="request">The request.</param>
        /// <param name="newSprint">The created sprint.</param>
        /// <returns>The creation result, or <see langword="null"/> when the workspace
        /// cannot be resolved.</returns>
        protected override IRestApiCrudResultCreate CreateSprint(RestApiSprintPayload payload, IRequest request, out Sprint newSprint)
        {
            newSprint = default;

            var workspace = ScrumProjection.GetWorkspace(request);

            if (workspace is null)
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
                WorkspaceId = workspace.Id,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.SprintManager.AddSprint(newSprint);

            return new RestApiCrudResultCreate
            {
                Data = ScrumProjection.ToRestSprint(newSprint)
            };
        }

        /// <summary>
        /// Updates the metadata or state of an existing sprint. Activating a sprint
        /// completes every other active sprint of the workspace.
        /// </summary>
        /// <param name="existingSprint">The sprint to update.</param>
        /// <param name="payload">The sprint payload.</param>
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
        /// Moves an item into a sprint or back to the backlog, appending at the end of
        /// the target group.
        /// </summary>
        /// <param name="existingItem">The item to move.</param>
        /// <param name="payload">The move payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate MoveItem(Model.Entities.Object existingItem, RestApiScrumMovePayload payload, IRequest request)
        {
            CoreHub.SprintManager.MoveObjectToSprint(existingItem.Id, NormalizeSprintId(payload.SprintId));

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Re-ranks an item within a sprint or the backlog; a payload with a differing
        /// sprint id also moves the item to that group.
        /// </summary>
        /// <param name="existingItem">The item to rank.</param>
        /// <param name="payload">The rank payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate RankItem(Model.Entities.Object existingItem, RestApiScrumRankPayload payload, IRequest request)
        {
            var targetSprintId = payload.SprintId is null
                ? existingItem.SprintId
                : NormalizeSprintId(payload.SprintId);

            CoreHub.SprintManager.MoveObjectToSprint(existingItem.Id, targetSprintId, payload.Rank);

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Updates the assignee and the story-point estimate of an item.
        /// </summary>
        /// <param name="existingItem">The item to update.</param>
        /// <param name="payload">The item payload.</param>
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
        /// Deletes a sprint; the sprint manager moves its items back to the backlog.
        /// </summary>
        /// <param name="existingSprint">The sprint to delete.</param>
        /// <param name="request">The request.</param>
        /// <returns>The delete result.</returns>
        protected override IRestApiCrudResultDelete DeleteSprint(Sprint existingSprint, IRequest request)
        {
            CoreHub.SprintManager.RemoveSprint(existingSprint);

            return new RestApiCrudResultDelete();
        }
    }

    /// <summary>
    /// Shared projection helpers of the scrum endpoints: request-scoped workspace and
    /// item resolution plus the entity-to-DTO mappings.
    /// </summary>
    internal static class ScrumProjection
    {
        private static readonly TimeSpan _lookupTtl = TimeSpan.FromSeconds(5);
        private static readonly object _lookupLock = new();
        private static DateTime _lookupStamp = DateTime.MinValue;
        private static Dictionary<Guid, ObjectBoardClassContext> _classContexts = [];
        private static Dictionary<Guid, StatusCategory> _categories = [];

        /// <summary>
        /// Resolves the workspace addressed by the request route.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        public static Workspace GetWorkspace(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);
        }

        /// <summary>
        /// Returns the active objects of the workspace addressed by the request route,
        /// ordered by their sprint rank.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The active objects of the workspace.</returns>
        public static IEnumerable<Model.Entities.Object> GetItems(IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                return [];
            }

            // the tab views live on the issue overview, so they present the issue kind only
            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspace.Id)
                .WhereEquals(x => x.Kind, Model.Entities.ObjectKind.Issue);

            return CoreHub.ObjectManager.GetObjects(query)
                .Where(x => x.State == WorkspaceState.Active)
                .OrderBy(x => x.SprintRank)
                .ThenBy(x => x.Key);
        }

        /// <summary>
        /// Narrows the items to those whose key or summary contains the view's search term.
        /// An absent or blank term leaves the set unchanged.
        /// </summary>
        /// <param name="items">The candidate objects.</param>
        /// <param name="request">The request carrying the search term in <c>q</c>.</param>
        /// <returns>The matching objects.</returns>
        public static IEnumerable<Model.Entities.Object> ApplySearch(IEnumerable<Model.Entities.Object> items, IRequest request)
        {
            var search = request?.GetParameter("q")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(search))
            {
                return items;
            }

            return items.Where
            (
                x => (x.Key?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                  || (x.Summary?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        }

        /// <summary>
        /// Applies the view's personal-scope quickfilter chips (assigned to me, starred)
        /// selected in the <c>f</c> parameter. Both scopes are per-identity projections. With
        /// no chip selected the set is unchanged.
        /// </summary>
        /// <param name="items">The candidate objects.</param>
        /// <param name="request">The request carrying the selected chips and the caller.</param>
        /// <returns>The matching objects.</returns>
        public static IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> items, IRequest request)
        {
            var filters = request?.GetParameter("f")?.Value?
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
            var selected = new HashSet<string>(filters, StringComparer.OrdinalIgnoreCase);

            if (selected.Count == 0)
            {
                return items;
            }

            var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (selected.Contains(ScrumSprintQuickfilter.MineId))
            {
                items = items.Where(x => x.AssigneeId == ownerId);
            }

            if (selected.Contains(ScrumSprintQuickfilter.StarredId))
            {
                var starredIds = CoreHub.ObjectManager.GetFavoriteObjects(ownerId)
                    .Select(x => x.Id)
                    .ToHashSet();

                items = items.Where(x => starredIds.Contains(x.Id));
            }

            return items;
        }

        /// <summary>
        /// Converts a sprint entity into the REST sprint DTO.
        /// </summary>
        /// <param name="sprint">The sprint entity.</param>
        /// <returns>The REST sprint DTO.</returns>
        public static RestApiScrumSprintItem ToRestSprint(Sprint sprint)
        {
            return new RestApiScrumSprintItem
            {
                Id = sprint.Id.ToString(),
                Name = sprint.Name,
                Goal = sprint.Goal,
                Status = sprint.State.Code(),
                Start = sprint.Start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                End = sprint.End?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Capacity = sprint.Capacity
            };
        }

        /// <summary>
        /// Converts an object entity into the REST item DTO. The item status collapses
        /// the workflow status category ("todo", "doing", "waiting", "done"); objects
        /// outside a sprint read as "backlog".
        /// </summary>
        /// <remarks>
        /// The picture goes through <see cref="ObjectIcon"/> like every other surface that
        /// stands a row for a record, rather than being read off the class captured in
        /// <see cref="ObjectBoardClassContext"/>: that context is rebuilt on a five-second
        /// timer, while <see cref="ObjectIcon"/> drops its entry the moment the class is
        /// edited, so a newly saved class avatar is on the board with the next render.
        /// </remarks>
        /// <param name="item">The object entity.</param>
        /// <returns>The REST item DTO.</returns>
        public static RestApiScrumItem ToRestItem(Model.Entities.Object item)
        {
            var (classContexts, categories) = GetLookups();

            if (!classContexts.TryGetValue(item.ClassId, out var classContext))
            {
                var cls = CoreHub.ClassManager.GetClass(item.ClassId);
                classContext = cls is null ? null : ObjectBoardProjection.BuildClassContext(cls);

                lock (_lookupLock)
                {
                    _classContexts[item.ClassId] = classContext;
                }
            }

            var category = ObjectBoardProjection.ResolveCategory(item.Id, classContext, categories);
            var assignee = item.AssigneeId is Guid assigneeId ? CoreHub.IdentityManager.GetIdentity(assigneeId) : null;

            return new RestApiScrumItem
            {
                Id = item.Id.ToString(),
                Type = classContext?.Class?.Name,
                Icon = ObjectIcon.Uri(item),
                Key = item.Key,
                Title = string.IsNullOrWhiteSpace(item.Summary) ? item.Key : item.Summary,
                Priority = ObjectBoardProjection.ResolvePriorityCode(item.Id, classContext),
                Points = item.StoryPoints ?? 0,
                SprintId = item.SprintId?.ToString(),
                Status = item.SprintId is null ? "backlog" : ObjectBoardProjection.CategoryItemStatus(category),
                Rank = item.SprintRank,
                AssigneeId = assignee?.Id.ToString(),
                AssigneeName = assignee?.Name,
                AssigneeInitials = assignee is null ? null : ObjectBoardProjection.Initials(assignee.Name),
                AssigneeColor = assignee is null ? null : ObjectBoardProjection.AvatarColor(assignee.Id),
                AssigneeImage = ObjectBoardProjection.AvatarImage(assignee)
            };
        }

        /// <summary>
        /// Returns the short-lived class-context and category lookups, rebuilding them
        /// when older than five seconds so field/status edits stay visible while the
        /// per-item projection avoids re-querying the same class data.
        /// </summary>
        /// <returns>The class contexts by class id and the categories by category id.</returns>
        private static (Dictionary<Guid, ObjectBoardClassContext> ClassContexts, Dictionary<Guid, StatusCategory> Categories) GetLookups()
        {
            lock (_lookupLock)
            {
                if (DateTime.UtcNow - _lookupStamp > _lookupTtl)
                {
                    _classContexts = [];
                    _categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id, x => x);
                    _lookupStamp = DateTime.UtcNow;
                }

                return (_classContexts, _categories);
            }
        }

        /// <summary>
        /// Parses a REST date string ("yyyy-MM-dd") into a date, or
        /// <see langword="null"/> when the value is missing or malformed.
        /// </summary>
        /// <param name="value">The REST date string.</param>
        /// <returns>The parsed date, or <see langword="null"/>.</returns>
        public static DateTime? ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                ? date.Date
                : null;
        }
    }
}
