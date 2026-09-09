using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base for the object Kanban endpoint of a kind's overview tab control.
    /// By default, columns are the workflow status categories (To Do, In Progress, Waiting,
    /// Done) and swimlanes are the classes of the workspace that have at least one active
    /// object of the <see cref="Kind"/>. Once the board is customized through its "…" menus
    /// (<see cref="UpdtaeColumns"/>, <see cref="UpdateSwimlanes"/>), the persisted
    /// <see cref="KanbanBoard"/> (<see cref="CoreHub.KanbanBoardManager"/>) takes over: its
    /// columns/swimlanes own their display name, color and order independently of the shared
    /// <see cref="StatusCategory"/>/<see cref="Class"/> rows they place cards by by. Each
    /// active object of the <see cref="Kind"/> becomes a card placed by its workflow-field
    /// value. A concrete subclass only fixes the kind it lists (issue, asset, …); each
    /// concrete endpoint registers at its own route, so the base must stay abstract.
    /// </summary>
    public abstract class RestApiObjectKindKanban : RestApiKanban<Model.Entities.Object>
    {
        /// <summary>
        /// The id of the synthetic swimlane a card falls into when its class was excluded from
        /// (or never added to) a customized board's swimlane list.
        /// </summary>
        private const string OtherSwimlaneId = "other";

        /// <summary>
        /// Gets the persisted kind key the board is scoped to.
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Resolves an optional sprint the board is additionally scoped to: when non-null,
        /// only objects committed to that sprint become cards (and only their classes form
        /// swimlanes). The default returns <see langword="null"/>, so the board shows every
        /// object of the kind in the workspace. The sprint board of the Scrum tab overrides
        /// this to show the active sprint only.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The sprint id to scope to, or <see langword="null"/> for the whole workspace.</returns>
        protected virtual Guid? ResolveSprint(IRequest request) => null;

        /// <summary>
        /// Applies an optional in-memory quickfilter to the board's objects (both the cards
        /// and the swimlane population). The default is a no-op, so the board shows every
        /// object of the kind/sprint. The sprint board overrides this to honour the personal
        /// scope chips (assigned to me, starred) that a WebIndex query cannot express.
        /// </summary>
        /// <param name="objects">The objects that would become cards / populate swimlanes.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The filtered objects.</returns>
        protected virtual IEnumerable<Model.Entities.Object> ApplyQuickfilter(IEnumerable<Model.Entities.Object> objects, IRequest request) => objects;

        /// <summary>
        /// Returns a <see cref="KleeneStarDbContext"/> so <see cref="CoreHub.ObjectManager"/>
        /// can run its queries; the base class' default query context would cast to null
        /// in the manager and trigger an NRE downstream.
        /// </summary>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the persisted board columns when the board has been customized, otherwise
        /// one column per workflow status category, ordered To Do, In Progress, Waiting, Done.
        /// </summary>
        protected override IEnumerable<RestApiKanbanColumn> RetrieveColumns(IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                yield break;
            }

            var board = CoreHub.KanbanBoardManager.GetBoard(workspace.Id, Kind);

            if (board?.Columns is { Count: > 0 })
            {
                foreach (var column in board.Columns.OrderBy(c => c.Position))
                {
                    yield return new RestApiKanbanColumn
                    {
                        Id = column.Id.ToString(),
                        Label = column.Name,
                        Color = column.Color,
                        ColorCss = ResolveCategoryColorCss(column.CategoryId)
                    };
                }

                yield break;
            }

            foreach (var category in ObjectBoardProjection.GetOrderedCategories())
            {
                yield return new RestApiKanbanColumn
                {
                    Id = category.Id.ToString(),
                    Label = ObjectBoardProjection.CategoryLabel(category),
                    ColorCss = ObjectBoardProjection.CategoryColorCss(category)
                };
            }
        }

        /// <summary>
        /// Returns the persisted board swimlanes when the board has been customized (plus a
        /// synthetic "Other" swimlane when an active object's class was excluded from the
        /// list), otherwise one swimlane per class that has at least one active object of the
        /// kind in the workspace, ordered by class name.
        /// </summary>
        protected override IEnumerable<RestApiKanbanSwimlane> RetrieveSwimlanes(IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                yield break;
            }

            var board = CoreHub.KanbanBoardManager.GetBoard(workspace.Id, Kind);

            if (board?.Swimlanes is { Count: > 0 })
            {
                foreach (var swimlane in board.Swimlanes.OrderBy(s => s.Position))
                {
                    yield return new RestApiKanbanSwimlane
                    {
                        Id = swimlane.Id.ToString(),
                        Label = swimlane.Name,
                        Color = swimlane.Color,
                        Filter = swimlane.Filter,
                        Expanded = true
                    };
                }

                if (HasUnconfiguredActiveObjects(workspace.Id, board, request))
                {
                    yield return new RestApiKanbanSwimlane
                    {
                        Id = OtherSwimlaneId,
                        Label = "Other",
                        Expanded = true
                    };
                }

                yield break;
            }

            var populatedClassIds = GetActiveObjects(workspace.Id, ResolveSprint(request), request)
                .Select(x => x.ClassId)
                .ToHashSet();

            var classes = CoreHub.ClassManager
                .GetClasses(new Query<Model.Entities.Class>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                .Where(x => populatedClassIds.Contains(x.Id))
                .OrderBy(x => x.Name);

            foreach (var cls in classes)
            {
                yield return new RestApiKanbanSwimlane
                {
                    Id = cls.Id.ToString(),
                    Label = cls.Name,
                    Expanded = true
                };
            }
        }

        /// <summary>
        /// Returns one card per active object of the kind, placed in the column/swimlane its
        /// workflow status category / class resolve to. Objects without a resolvable workflow
        /// value fall into the first (To Do) column; once the board is customized, an object
        /// whose category/class was excluded from the board falls into the first configured
        /// column / the synthetic "Other" swimlane instead.
        /// </summary>
        protected override IEnumerable<RestApiKanbanCard> RetrieveCards(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                yield break;
            }

            var board = CoreHub.KanbanBoardManager.GetBoard(workspace.Id, Kind);

            var categories = ObjectBoardProjection.GetOrderedCategories();
            var categoriesById = categories.ToDictionary(x => x.Id, x => x);
            var defaultCategory = categories.FirstOrDefault();

            Dictionary<Guid, string> columnIdByCategoryId = null;
            var fallbackColumnId = defaultCategory?.Id.ToString();

            if (board?.Columns is { Count: > 0 } boardColumns)
            {
                columnIdByCategoryId = boardColumns
                    .Where(c => c.CategoryId.HasValue)
                    .GroupBy(c => c.CategoryId!.Value)
                    .ToDictionary(g => g.Key, g => g.First().Id.ToString());

                fallbackColumnId = boardColumns.OrderBy(c => c.Position).First().Id.ToString();
            }

            Dictionary<Guid, string> swimlaneIdByClassId = null;

            if (board?.Swimlanes is { Count: > 0 } boardSwimlanes)
            {
                swimlaneIdByClassId = boardSwimlanes
                    .Where(s => s.ClassId.HasValue)
                    .GroupBy(s => s.ClassId!.Value)
                    .ToDictionary(g => g.Key, g => g.First().Id.ToString());
            }

            var contextByClass = new Dictionary<Guid, ObjectBoardClassContext>();
            var identityById = new Dictionary<Guid, Identity>();
            var sprintId = ResolveSprint(request);

            query = query
                .WhereEquals(x => x.WorkspaceId, workspace.Id)
                .WhereEquals(x => x.Kind, Kind);

            var cards = CoreHub.ObjectManager.GetObjects(query, context)
                .Where(x => x.State == WorkspaceState.Active)
                .Where(x => sprintId is null || x.SprintId == sprintId);
            cards = ApplyQuickfilter(cards, request);

            foreach (var entity in cards)
            {
                if (!contextByClass.TryGetValue(entity.ClassId, out var classContext))
                {
                    var cls = CoreHub.ClassManager.GetClass(entity.ClassId);
                    classContext = cls is null ? null : ObjectBoardProjection.BuildClassContext(cls);
                    contextByClass[entity.ClassId] = classContext;
                }

                var category = ObjectBoardProjection.ResolveCategory(entity.Id, classContext, categoriesById)
                    ?? defaultCategory;

                var columnId = ResolveColumnId(category, columnIdByCategoryId, fallbackColumnId);
                var swimlaneId = ResolveSwimlaneId(entity.ClassId, swimlaneIdByClassId);

                yield return BuildCard(entity, classContext, columnId, swimlaneId, identityById);
            }
        }

        /// <summary>
        /// Applies a column layout change (add / rename / recolor / reorder / delete) submitted
        /// through the board "…" menu. A column carrying an existing id keeps the workflow
        /// category it already places cards by; a genuinely new column claims the next global
        /// <see cref="StatusCategory"/> not yet represented on the board, or none when every
        /// category is already in use (it then stays a decorative, cardless bucket, since the
        /// generic column-add flow carries no category picker).
        /// </summary>
        /// <param name="layout">The layout payload carrying the full ordered column list.</param>
        /// <param name="request">The current HTTP request. Cannot be null.</param>
        protected override void UpdtaeColumns(RestApiDashboardLayout layout, IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null || layout?.Columns is null)
            {
                return;
            }

            var board = CoreHub.KanbanBoardManager.EnsureBoard(workspace.Id, Kind);
            var existingById = board.Columns.ToDictionary(c => c.Id);
            var existingByKey = board.Columns.Where(c => c.Key is not null).ToDictionary(c => c.Key);

            var usedCategoryIds = board.Columns
                .Where(c => c.CategoryId.HasValue)
                .Select(c => c.CategoryId!.Value)
                .ToHashSet();

            var availableCategories = new Queue<StatusCategory>
            (
                ObjectBoardProjection.GetOrderedCategories().Where(c => !usedCategoryIds.Contains(c.Id))
            );

            var columns = layout.Columns.Select(column =>
            {
                var id = ParseId(column.Id);
                var key = ClientKey(column.Id);
                var existing = ResolveExisting(id, key, existingById, existingByKey);

                var categoryId = existing?.CategoryId;
                if (existing is null && availableCategories.Count > 0)
                {
                    categoryId = availableCategories.Dequeue().Id;
                }

                return new KanbanBoardColumn(id == Guid.Empty ? Guid.NewGuid() : id)
                {
                    BoardId = board.Id,
                    Key = key,
                    Name = FallbackName(column.Title, "Column"),
                    Color = column.Color,
                    CategoryId = categoryId
                };
            }).ToList();

            CoreHub.KanbanBoardManager.SetColumns(board.Id, columns);
        }

        /// <summary>
        /// Applies a swimlane layout change (add / rename / recolor / reorder / delete) submitted
        /// through the board "…" menu. A swimlane carrying an existing id keeps the class it already
        /// places cards by; a genuinely new swimlane claims the next class of the workspace not
        /// yet represented on the board (regardless of whether it currently has active
        /// objects), or none when every class is already in use.
        /// </summary>
        /// <param name="layout">
        /// The layout payload whose <see cref="RestApiDashboardLayout.Swimlanes"/> carries the
        /// new swimlane list.
        /// </param>
        /// <param name="request">The current HTTP request. Cannot be null.</param>
        protected override void UpdateSwimlanes(RestApiDashboardLayout layout, IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null || layout?.Swimlanes is null)
            {
                return;
            }

            var board = CoreHub.KanbanBoardManager.EnsureBoard(workspace.Id, Kind);
            var existingById = board.Swimlanes.ToDictionary(s => s.Id);
            var existingByKey = board.Swimlanes.Where(s => s.Key is not null).ToDictionary(s => s.Key);

            var usedClassIds = board.Swimlanes
                .Where(s => s.ClassId.HasValue)
                .Select(s => s.ClassId!.Value)
                .ToHashSet();

            var availableClasses = new Queue<Model.Entities.Class>
            (
                CoreHub.ClassManager
                    .GetClasses(new Query<Model.Entities.Class>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                    .Where(c => !usedClassIds.Contains(c.Id))
                    .OrderBy(c => c.Name)
            );

            var swimlanes = layout.Swimlanes.Select(swimlane =>
            {
                var id = ParseId(swimlane.Id);
                var key = ClientKey(swimlane.Id);
                var existing = ResolveExisting(id, key, existingById, existingByKey);

                var classId = existing?.ClassId;
                if (existing is null && availableClasses.Count > 0)
                {
                    classId = availableClasses.Dequeue().Id;
                }

                return new KanbanBoardSwimlane(id == Guid.Empty ? Guid.NewGuid() : id)
                {
                    BoardId = board.Id,
                    Key = key,
                    Name = FallbackName(swimlane.Title, "Swimlane"),
                    Color = swimlane.Color,
                    Filter = swimlane.Filter,
                    ClassId = classId
                };
            }).ToList();

            CoreHub.KanbanBoardManager.SetSwimlanes(board.Id, swimlanes);
        }

        /// <summary>
        /// Persists the board-level WQL filter submitted through the board settings dialog. The
        /// filter is echoed back on the next load; it is not yet applied to narrow the card
        /// query (no WQL query engine is wired up for object boards).
        /// </summary>
        /// <param name="layout">
        /// The layout payload whose <see cref="RestApiDashboardLayout.Filter"/> carries the
        /// submitted WQL filter.
        /// </param>
        /// <param name="request">The current HTTP request. Cannot be null.</param>
        protected override void UpdateSettings(RestApiDashboardLayout layout, IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                return;
            }

            var board = CoreHub.KanbanBoardManager.EnsureBoard(workspace.Id, Kind);

            CoreHub.KanbanBoardManager.SetFilter(board.Id, layout?.Filter);
        }

        /// <summary>
        /// Seeds the board settings dialog with the persisted board filter when the request
        /// (e.g. a full page reload) carries none of its own.
        /// </summary>
        /// <param name="wql">The WQL filter carried on the request, or null.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The active WQL filter, or null when the board has none.</returns>
        protected override string RetrieveFilter(string wql, IRequest request)
        {
            if (!string.IsNullOrWhiteSpace(wql))
            {
                return wql;
            }

            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                return null;
            }

            return CoreHub.KanbanBoardManager.GetBoard(workspace.Id, Kind)?.Filter;
        }

        /// <summary>
        /// Builds the kanban card of a single object, including the assignee avatar data
        /// and the priority/story-point footer chips.
        /// </summary>
        /// <param name="entity">The object to project.</param>
        /// <param name="classContext">The board context of the object's class.</param>
        /// <param name="columnId">The resolved column id the card is placed in.</param>
        /// <param name="swimlaneId">The resolved swimlane id the card is placed in.</param>
        /// <param name="identityById">A request-scoped identity cache.</param>
        /// <returns>The kanban card.</returns>
        private static RestApiKanbanCard BuildCard
        (
            Model.Entities.Object entity,
            ObjectBoardClassContext classContext,
            string columnId,
            string swimlaneId,
            Dictionary<Guid, Identity> identityById
        )
        {
            var assignee = ResolveIdentity(entity.AssigneeId, identityById);

            var card = new RestApiKanbanCard
            {
                // the card id is the object id, which the board writes onto the card as
                // data-card-id and a master-detail resolves from there
                Id = entity.Id.ToString(),
                Label = string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : entity.Summary,
                Html = $"<strong>{WebUtility.HtmlEncode(entity.Key)}</strong><br/>{WebUtility.HtmlEncode(entity.Summary)}",
                ColumnId = columnId,
                SwimlaneId = swimlaneId,
                AssigneeId = assignee?.Id.ToString(),
                AssigneeName = assignee?.Name,
                AssigneeInitials = assignee is null ? null : ObjectBoardProjection.Initials(assignee.Name),
                AssigneeColor = assignee is null ? null : ObjectBoardProjection.AvatarColor(assignee.Id),
                AssigneeImage = ObjectBoardProjection.AvatarImage(assignee),
                Footer = BuildFooter(entity, classContext).ToList()
            };

            return card;
        }

        /// <summary>
        /// Builds the footer chips of a card: the priority code (when the object carries
        /// a priority value) and the story-point estimate (when estimated).
        /// </summary>
        /// <param name="entity">The object to project.</param>
        /// <param name="classContext">The board context of the object's class.</param>
        /// <returns>The footer chips.</returns>
        private static IEnumerable<RestApiKanbanCardChip> BuildFooter(Model.Entities.Object entity, ObjectBoardClassContext classContext)
        {
            var priority = ObjectBoardProjection.ResolvePriorityCode(entity.Id, classContext);

            if (!string.IsNullOrWhiteSpace(priority))
            {
                yield return new RestApiKanbanCardChip
                {
                    Label = priority,
                    Icon = new IconFlag(),
                    Color = new PropertyColorBackgroundBadge(PriorityBadgeColor(priority)),
                    Title = "Priority"
                };
            }

            if (entity.StoryPoints is int points)
            {
                yield return new RestApiKanbanCardChip
                {
                    Label = points.ToString(),
                    Icon = new IconScaleBalanced(),
                    Color = new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Secondary),
                    Title = "Story points"
                };
            }
        }

        /// <summary>
        /// Maps a priority display code to the badge color of its chip.
        /// </summary>
        /// <param name="priority">The priority display code.</param>
        /// <returns>The badge color.</returns>
        private static TypeColorBackgroundBadge PriorityBadgeColor(string priority)
        {
            return priority switch
            {
                "P1" => TypeColorBackgroundBadge.Danger,
                "P2" => TypeColorBackgroundBadge.Warning,
                "P3" => TypeColorBackgroundBadge.Info,
                "P4" => TypeColorBackgroundBadge.Secondary,
                _ => TypeColorBackgroundBadge.Secondary
            };
        }

        /// <summary>
        /// Resolves the workspace addressed by the request route.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        private static Workspace GetWorkspace(IRequest request)
        {
            var workspaceKey = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);
        }

        /// <summary>
        /// Returns the active objects of the kind in the workspace, optionally narrowed to
        /// the objects committed to <paramref name="sprintId"/>.
        /// </summary>
        /// <param name="workspaceId">The workspace id.</param>
        /// <param name="sprintId">The sprint to scope to, or <see langword="null"/> for the whole workspace.</param>
        /// <param name="request">The request that provides the operational context for the quickfilter.</param>
        /// <returns>The active objects.</returns>
        private IEnumerable<Model.Entities.Object> GetActiveObjects(Guid workspaceId, Guid? sprintId, IRequest request)
        {
            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspaceId)
                .WhereEquals(x => x.Kind, Kind);

            var objects = CoreHub.ObjectManager.GetObjects(query)
                .Where(x => x.State == WorkspaceState.Active)
                .Where(x => sprintId is null || x.SprintId == sprintId);

            return ApplyQuickfilter(objects, request);
        }

        /// <summary>
        /// Determines whether any active object's class was excluded from (or never added to)
        /// a customized board's swimlane list, meaning the "Other" catch-all swimlane must be
        /// shown so the object's card is not silently dropped.
        /// </summary>
        /// <param name="workspaceId">The workspace id.</param>
        /// <param name="board">The customized board.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns><see langword="true"/> when at least one active object has no configured swimlane.</returns>
        private bool HasUnconfiguredActiveObjects(Guid workspaceId, KanbanBoard board, IRequest request)
        {
            var configuredClassIds = board.Swimlanes
                .Where(s => s.ClassId.HasValue)
                .Select(s => s.ClassId!.Value)
                .ToHashSet();

            return GetActiveObjects(workspaceId, ResolveSprint(request), request)
                .Any(x => !configuredClassIds.Contains(x.ClassId));
        }

        /// <summary>
        /// Resolves the column a resolved status category places a card in: the board column
        /// bound to the category, the board's first column when the category is not (or no
        /// longer) represented on a customized board, or the category id itself on the default,
        /// uncustomized board.
        /// </summary>
        /// <param name="category">The object's resolved status category, or null.</param>
        /// <param name="columnIdByCategoryId">
        /// The category-to-column lookup of a customized board, or null on the default board.
        /// </param>
        /// <param name="fallbackColumnId">
        /// The column id to fall back to (the board's first column, or the first default
        /// category) when the category carries no match.
        /// </param>
        /// <returns>The resolved column id.</returns>
        private static string ResolveColumnId(StatusCategory category, Dictionary<Guid, string> columnIdByCategoryId, string fallbackColumnId)
        {
            if (columnIdByCategoryId is null)
            {
                return category?.Id.ToString();
            }

            if (category is not null && columnIdByCategoryId.TryGetValue(category.Id, out var columnId))
            {
                return columnId;
            }

            return fallbackColumnId;
        }

        /// <summary>
        /// Resolves the swimlane a card's class places it in: the board swimlane bound to the
        /// class, the synthetic "Other" swimlane when the class is not (or no longer)
        /// represented on a customized board, or the class id itself on the default,
        /// uncustomized board.
        /// </summary>
        /// <param name="classId">The card's class id.</param>
        /// <param name="swimlaneIdByClassId">
        /// The class-to-swimlane lookup of a customized board, or null on the default board.
        /// </param>
        /// <returns>The resolved swimlane id.</returns>
        private static string ResolveSwimlaneId(Guid classId, Dictionary<Guid, string> swimlaneIdByClassId)
        {
            if (swimlaneIdByClassId is null)
            {
                return classId.ToString();
            }

            return swimlaneIdByClassId.TryGetValue(classId, out var swimlaneId) ? swimlaneId : OtherSwimlaneId;
        }

        /// <summary>
        /// Resolves a client column/swimlane id (from a layout update payload) to the existing
        /// board row it addresses: first by business id, then by the transient client key a
        /// session-new row keeps until the next reload.
        /// </summary>
        /// <typeparam name="TRow">The board row type (<see cref="KanbanBoardColumn"/> or <see cref="KanbanBoardSwimlane"/>).</typeparam>
        /// <param name="id">The parsed business id, or <see cref="Guid.Empty"/> for a client-generated id.</param>
        /// <param name="key">The client key, or null when <paramref name="id"/> is a business id.</param>
        /// <param name="existingById">The board's existing rows, keyed by business id.</param>
        /// <param name="existingByKey">The board's existing rows that still carry a client key.</param>
        /// <returns>The matched existing row, or null when the row is genuinely new.</returns>
        private static TRow ResolveExisting<TRow>(Guid id, string key, Dictionary<Guid, TRow> existingById, Dictionary<string, TRow> existingByKey)
            where TRow : class
        {
            if (id != Guid.Empty && existingById.TryGetValue(id, out var byId))
            {
                return byId;
            }

            if (!string.IsNullOrEmpty(key) && existingByKey.TryGetValue(key, out var byKey))
            {
                return byKey;
            }

            return null;
        }

        /// <summary>
        /// Returns the color CSS class of the workflow status category a column is bound to, or
        /// the neutral fallback when the column is unbound (every category was already in use
        /// when it was added).
        /// </summary>
        /// <param name="categoryId">The category id a column is bound to, or null.</param>
        /// <returns>The CSS class.</returns>
        private static string ResolveCategoryColorCss(Guid? categoryId)
        {
            if (categoryId is not Guid id)
            {
                return "wx-color-secondary";
            }

            var category = ObjectBoardProjection.GetOrderedCategories().FirstOrDefault(c => c.Id == id);

            return category is not null ? ObjectBoardProjection.CategoryColorCss(category) : "wx-color-secondary";
        }

        /// <summary>
        /// Returns a non-empty display name, falling back to a default when the client cleared it.
        /// </summary>
        /// <param name="title">The title from the payload.</param>
        /// <param name="fallback">The fallback name.</param>
        /// <returns>The name to persist.</returns>
        private static string FallbackName(string title, string fallback)
        {
            return string.IsNullOrWhiteSpace(title) ? fallback : title;
        }

        /// <summary>
        /// Parses a client column/swimlane id into its business id. A client-generated id for a
        /// newly added row (not a GUID) resolves to <see cref="Guid.Empty"/>, signalling a fresh row.
        /// </summary>
        /// <param name="id">The client id.</param>
        /// <returns>The parsed GUID, or <see cref="Guid.Empty"/> for a new row.</returns>
        private static Guid ParseId(string id)
        {
            return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
        }

        /// <summary>
        /// Returns the transient client key for a column/swimlane: the client id of a
        /// session-new row (a non-GUID token), or null once the row is addressed by its
        /// business id.
        /// </summary>
        /// <param name="id">The client id.</param>
        /// <returns>The client key, or null for a row addressed by its business id.</returns>
        private static string ClientKey(string id)
        {
            return Guid.TryParse(id, out _) ? null : id;
        }

        /// <summary>
        /// Resolves an identity through a request-scoped cache.
        /// </summary>
        /// <param name="identityId">The identity id, or <see langword="null"/>.</param>
        /// <param name="identityById">The cache.</param>
        /// <returns>The identity, or <see langword="null"/>.</returns>
        private static Identity ResolveIdentity(Guid? identityId, Dictionary<Guid, Identity> identityById)
        {
            if (identityId is not Guid id)
            {
                return null;
            }

            if (!identityById.TryGetValue(id, out var identity))
            {
                identity = CoreHub.IdentityManager.GetIdentity(id);
                identityById[id] = identity;
            }

            return identity;
        }
    }
}
