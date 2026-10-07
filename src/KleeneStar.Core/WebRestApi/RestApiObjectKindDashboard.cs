using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Project-wide base for the object dashboard endpoint of a kind's overview tab control. By
    /// default it is a small, read-only KPI dashboard (total / active / archived) aggregating
    /// the workspace's objects of the <see cref="Kind"/>. Once the board is customized through
    /// its "…" menus (<see cref="UpdtaeColumns"/>, <see cref="UpdateBoard"/>), the persisted
    /// <see cref="KindDashboard"/> (<see cref="CoreHub.KindDashboardManager"/>) takes over and
    /// the board behaves like the standalone dashboard: columns and widgets can be added,
    /// renamed, recolored, reordered, reconfigured and deleted, all persisted through the
    /// endpoint. A concrete subclass only fixes the kind it aggregates (issue, asset, …); each
    /// concrete endpoint registers at its own route, so the base must stay abstract.
    /// </summary>
    public abstract class RestApiObjectKindDashboard : RestApiDashboard
    {
        /// <summary>
        /// The widget types the board offers in its "…" add menu once customized. The server
        /// owns which widgets a board may use; the client resolves each type's render and
        /// display metadata from its widget registry by id. Mirrors the standalone dashboard's
        /// set (<see cref="global::KleeneStar.Core.WWW.Api._1_.Insights._insightid_.View"/>).
        /// </summary>
        private static readonly string[] AvailableWidgetIds =
        [
            "widget_kleenestar_note",
            "widget_info",
            "widget_bignumber",
            "widget_progress",
            "widget_list",
            "widget_chart"
        ];

        /// <summary>
        /// The JSON serializer options used to round-trip the type-specific widget params, which
        /// are stored on the widget as a serialized string dictionary.
        /// </summary>
        private static readonly JsonSerializerOptions ParamsOptions = new();

        /// <summary>
        /// Gets the persisted kind key the dashboard aggregates.
        /// </summary>
        protected abstract string Kind { get; }

        /// <summary>
        /// Returns the persisted board columns when the board has been customized, otherwise
        /// the default KPI columns (total / active / archived) for the kind in the workspace.
        /// </summary>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>The dashboard columns.</returns>
        protected override IEnumerable<RestApiDashboardColumn> RetrieveColumns(IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (workspace is null)
            {
                yield break;
            }

            var board = CoreHub.KindDashboardManager.GetBoard(workspace.Id, Kind, BoardViewScope.Workspace(request, workspace.Id, Kind, ObjectViewType.Dashboard));

            if (board is not null)
            {
                foreach (var column in board.Columns.OrderBy(c => c.Position))
                {
                    yield return new RestApiDashboardColumn
                    {
                        Id = column.Id.ToString(),
                        Label = column.Name,
                        Size = column.Size,
                        Color = column.Color,
                        Widgets = MapWidgets(column.Widgets)
                    };
                }

                yield break;
            }

            using var context = ModelHub.CreateDbContext();

            var query = new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspace.Id)
                .WhereEquals(x => x.Kind, Kind);

            var objects = CoreHub.ObjectManager.GetObjects(query, context).ToList();
            var active = objects.Count(x => x.State == WorkspaceState.Active);
            var archived = objects.Count(x => x.State == WorkspaceState.Archived);

            yield return new RestApiDashboardColumn
            {
                Id = "kpi-total",
                Size = "33%",
                Label = "Total",
                Widgets =
                [
                    new RestApiDashboardWidgetBigNumber
                    {
                        Value = objects.Count.ToString(),
                        Label = "Objects",
                        Color = "#3273A3",
                        Movable = false
                    }
                ]
            };

            yield return new RestApiDashboardColumn
            {
                Id = "kpi-active",
                Size = "33%",
                Label = "Active",
                Widgets =
                [
                    new RestApiDashboardWidgetBigNumber
                    {
                        Value = active.ToString(),
                        Label = "Active",
                        Color = "#A2B284",
                        Movable = false
                    }
                ]
            };

            yield return new RestApiDashboardColumn
            {
                Id = "kpi-archived",
                Size = "33%",
                Label = "Archived",
                Widgets =
                [
                    new RestApiDashboardWidgetBigNumber
                    {
                        Value = archived.ToString(),
                        Label = "Archived",
                        Color = "#76522A",
                        Movable = false
                    }
                ]
            };
        }

        /// <summary>
        /// Reports the widget types the board may add. The client filters its add menu to
        /// exactly this set, resolving each entry's title, icon and description from its widget
        /// registry.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The available widget descriptors.</returns>
        protected override IEnumerable<RestApiDashboardAvailableWidget> RetrieveAvailableWidgets(IRequest request)
        {
            return AvailableWidgetIds.Select(id => new RestApiDashboardAvailableWidget { Id = id });
        }

        /// <summary>
        /// Persists a column-only layout change (add / rename / resize / recolor / reorder /
        /// delete) for the kind dashboard, preserving the widgets of surviving columns.
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

            var board = CoreHub.KindDashboardManager.EnsureBoard(workspace.Id, Kind, BoardViewScope.Workspace(request, workspace.Id, Kind, ObjectViewType.Dashboard));

            var columns = layout.Columns
                .Select(column => new KindDashboardColumn(ParseId(column.Id))
                {
                    Key = ClientKey(column.Id),
                    Name = FallbackName(column.Title, "Column"),
                    Size = column.Size,
                    Color = column.Color
                })
                .ToList();

            CoreHub.KindDashboardManager.SetColumns(board.Id, columns);
        }

        /// <summary>
        /// Persists a full board change (widget add / delete / reconfigure / move) for the kind
        /// dashboard, rebuilding the widgets of every column.
        /// </summary>
        /// <param name="board">The full board carried in the request.</param>
        /// <param name="request">The current HTTP request. Cannot be null.</param>
        protected override void UpdateBoard(IEnumerable<RestApiDashboardBoardColumn> board, IRequest request)
        {
            var workspace = GetWorkspace(request);

            if (board is null || workspace is null)
            {
                return;
            }

            var kindBoard = CoreHub.KindDashboardManager.EnsureBoard(workspace.Id, Kind, BoardViewScope.Workspace(request, workspace.Id, Kind, ObjectViewType.Dashboard));

            var columns = board
                .Select(column => new KindDashboardColumn(ParseId(column.Id))
                {
                    Key = ClientKey(column.Id),
                    Name = FallbackName(column.Title, "Column"),
                    Size = column.Size,
                    Color = column.Color,
                    Widgets = (column.Widgets ?? [])
                        .Select(widget => new KindDashboardWidget(Guid.NewGuid())
                        {
                            Type = widget.Id,
                            Name = FallbackName(widget.Title, widget.Id),
                            Color = widget.Color,
                            Params = SerializeParams(widget.Params)
                        })
                        .ToList()
                })
                .ToList();

            CoreHub.KindDashboardManager.SetBoard(kindBoard.Id, columns);
        }

        /// <summary>
        /// Maps the persisted widgets of a column to their REST API representations, ordered by
        /// position, with each widget's params round-tripping through the client registry type.
        /// </summary>
        /// <param name="widgets">The widgets of a column. Must not be null.</param>
        /// <returns>The REST API widgets ready for serialization.</returns>
        private static List<RestApiDashboardWidget> MapWidgets(IEnumerable<KindDashboardWidget> widgets)
        {
            return widgets
                .OrderBy(w => w.Position)
                .Select(widget => (RestApiDashboardWidget)new RestApiDashboardWidgetGeneric(widget.Type)
                {
                    Title = widget.Name,
                    Color = widget.Color,
                    Movable = true,
                    Params = DeserializeParams(widget.Params)
                })
                .ToList();
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
        /// Parses a client column id into its business id. A client-generated id for a newly
        /// added column (not a GUID) resolves to <see cref="Guid.Empty"/>, signalling a fresh
        /// column.
        /// </summary>
        /// <param name="id">The client column id.</param>
        /// <returns>The parsed GUID, or <see cref="Guid.Empty"/> for a new column.</returns>
        private static Guid ParseId(string id)
        {
            return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
        }

        /// <summary>
        /// Returns the transient client key for a column: the client id of a session-new column
        /// (a non-GUID token), or null once the column is addressed by its business id.
        /// </summary>
        /// <param name="id">The client column id.</param>
        /// <returns>The client key, or null for a column addressed by its business id.</returns>
        private static string ClientKey(string id)
        {
            return Guid.TryParse(id, out _) ? null : id;
        }

        /// <summary>
        /// Returns a non-empty name, falling back to a default when the client cleared it.
        /// </summary>
        /// <param name="title">The title from the payload.</param>
        /// <param name="fallback">The fallback name.</param>
        /// <returns>The name to persist.</returns>
        private static string FallbackName(string title, string fallback)
        {
            return string.IsNullOrWhiteSpace(title) ? fallback : title;
        }

        /// <summary>
        /// Serializes the type-specific widget params into the string dictionary stored on the
        /// widget. Empty params serialize to null so an unconfigured widget stays null.
        /// </summary>
        /// <param name="parameters">The widget params, or null.</param>
        /// <returns>The JSON string, or null when there is nothing to store.</returns>
        private static string SerializeParams(Dictionary<string, string> parameters)
        {
            if (parameters is null || parameters.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(parameters, ParamsOptions);
        }

        /// <summary>
        /// Deserializes the stored widget params back into the string dictionary the client
        /// consumes. A null or blank value yields an empty dictionary; an unreadable value is
        /// ignored.
        /// </summary>
        /// <param name="parameters">The stored JSON string, or null.</param>
        /// <returns>The params dictionary, never null.</returns>
        private static Dictionary<string, string> DeserializeParams(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(parameters, ParamsOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
