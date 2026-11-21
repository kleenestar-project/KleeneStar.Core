using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Objects._workspacekey_
{
    /// <summary>
    /// Scrum team endpoint of the Scrum sprint tab: the people working in the workspace's
    /// active sprint together with the story points committed to each of them and the
    /// share already completed (issues whose workflow status resolves to "done"). Backs
    /// the data service of the <c>ControlDataScrumTeam</c> control, which queries
    /// <c>GET {uri}</c> once and renders the avatars with their points/progress and a modal
    /// breakdown.
    /// </summary>
    [Title("kleenestar.core:object.view.scrum.sprint.team.title")]
    [Cache]
    public sealed class ScrumSprintTeam : IRestApi
    {
        /// <summary>
        /// The JSON serializer options; the <see cref="RestApiScrumTeamMember"/> property
        /// names are fixed by their attributes, camel-case covers any unattributed member.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// The workflow item status of a completed issue, as returned by
        /// <see cref="ObjectBoardProjection.CategoryItemStatus"/>.
        /// </summary>
        private const string DoneStatus = "done";

        /// <summary>
        /// Handles <c>GET {base}</c>: returns the members of the active sprint together with
        /// their committed and completed story points, most-loaded first. Returns an empty
        /// array when no sprint is active.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The scrum team members as a JSON array.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Get(IRequest request)
        {
            var members = BuildMembers(request);
            var json = JsonSerializer.Serialize(members, _jsonOptions);

            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(json)
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Builds the scrum team members from the active sprint's committed issues: one
        /// member per assignee, with the summed committed points and the summed points of
        /// the issues already in the "done" status category. Unassigned issues do not
        /// belong to a person and are excluded.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The team members, ordered by committed points (desc) then name.</returns>
        private static IReadOnlyList<RestApiScrumTeamMember> BuildMembers(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);

            if (workspace is null)
            {
                return [];
            }

            var activeSprint = CoreHub.SprintManager.GetActiveSprint(workspace.Id);

            if (activeSprint is null)
            {
                return [];
            }

            var items = CoreHub.SprintManager.GetSprintObjects(workspace.Id, activeSprint.Id)
                .Where(x => string.Equals(x.Kind, Model.Entities.ObjectKind.Issue, StringComparison.OrdinalIgnoreCase)
                    && x.State == WorkspaceState.Active);

            var categories = ObjectBoardProjection.GetOrderedCategories();
            var categoriesById = categories.ToDictionary(x => x.Id, x => x);
            var classContexts = new Dictionary<Guid, ObjectBoardClassContext>();

            // sum committed and completed points per assignee
            var byAssignee = new Dictionary<Guid, (int Points, int Completed)>();

            foreach (var item in items)
            {
                if (item.AssigneeId is not Guid assigneeId)
                {
                    continue;
                }

                if (!classContexts.TryGetValue(item.ClassId, out var classContext))
                {
                    var cls = CoreHub.ClassManager.GetClass(item.ClassId);
                    classContext = cls is null ? null : ObjectBoardProjection.BuildClassContext(cls);
                    classContexts[item.ClassId] = classContext;
                }

                var category = ObjectBoardProjection.ResolveCategory(item.Id, classContext, categoriesById);
                var isDone = string.Equals(ObjectBoardProjection.CategoryItemStatus(category), DoneStatus, StringComparison.OrdinalIgnoreCase);
                var points = item.StoryPoints ?? 0;

                var current = byAssignee.TryGetValue(assigneeId, out var value) ? value : (Points: 0, Completed: 0);
                byAssignee[assigneeId] = (current.Points + points, current.Completed + (isDone ? points : 0));
            }

            return byAssignee
                .Select(kv => ToMember(kv.Key, kv.Value.Points, kv.Value.Completed))
                .OrderByDescending(x => x.Points)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Projects an assignee's committed/completed totals into a scrum team member DTO,
        /// resolving the person's name, initials, avatar colour, and image.
        /// </summary>
        /// <param name="assigneeId">The assignee identity id.</param>
        /// <param name="points">The committed story points.</param>
        /// <param name="completed">The completed story points.</param>
        /// <returns>The scrum team member.</returns>
        private static RestApiScrumTeamMember ToMember(Guid assigneeId, int points, int completed)
        {
            var identity = CoreHub.IdentityManager.GetIdentity(assigneeId);
            var name = identity?.Name ?? assigneeId.ToString();

            return new RestApiScrumTeamMember
            {
                Id = assigneeId.ToString(),
                Name = name,
                Initials = ObjectBoardProjection.Initials(name),
                Color = ObjectBoardProjection.AvatarColor(assigneeId),
                Image = identity?.Avatar?.Uri?.ToString(),
                Points = points,
                Completed = completed
            };
        }
    }
}
