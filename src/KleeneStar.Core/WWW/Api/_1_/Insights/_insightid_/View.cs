using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Insights._insightid_
{
    /// <summary>
    /// Provides the REST API endpoint that returns and persists the editable board (columns and
    /// widgets) of an insight of the dashboard type, consumed by the <c>ControlDataDashboard</c>
    /// control on the insight page. It reports the widget types the board may add, and persists column
    /// changes (add / rename / resize / recolor / reorder / delete) as well as full board changes
    /// (widget add / delete / reconfigure / move).
    /// </summary>
    [Title("kleenestar.core:insight.view.label")]
    [Cache]
    public sealed class View : RestApiDashboard
    {
        /// <summary>
        /// The widget types the board offers in its "…" add menu. The server owns which widgets a
        /// board may use; the client resolves each type's render and display metadata from its
        /// widget registry by id. Every id here is registered client-side (the framework
        /// <c>widgets/default.js</c> widgets plus the KleeneStar <c>widgets/kleenestar.js</c> ones).
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
        /// The JSON serializer options used to round-trip the type-specific widget params, which are
        /// stored on the widget as a serialized string dictionary.
        /// </summary>
        private static readonly JsonSerializerOptions ParamsOptions = new();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public View()
        {
        }

        /// <summary>
        /// Retrieves the column and widget layout for the dashboard identified by the
        /// <c>insightid</c> path segment in the current request.
        /// </summary>
        /// <param name="request">The current HTTP request. Cannot be null.</param>
        /// <returns>
        /// The dashboard columns in their persisted order, each with its widgets. Returns without
        /// yielding when no insight matches the id.
        /// </returns>
        protected override IEnumerable<RestApiDashboardColumn> RetrieveColumns(IRequest request)
        {
            // the board is the insight's content; a caller who may not read it gets none
            var insight = global::KleeneStar.Core.WebInsight.InsightScope.ResolveReadable(request);

            if (insight == null)
            {
                yield break;
            }

            var viewId = BoardViewScope.Insight(request, insight.Id, InsightViewTypes.Dashboard);

            foreach (var column in insight.Columns.Where(c => c.ViewId == viewId).OrderBy(c => c.Position))
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
        }

        /// <summary>
        /// Reports the widget types the board may add. The client filters its add menu to exactly
        /// this set, resolving each entry's title, icon and description from its widget registry.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <returns>The available widget descriptors.</returns>
        protected override IEnumerable<RestApiDashboardAvailableWidget> RetrieveAvailableWidgets(IRequest request)
        {
            // the server owns which widget types the board may add; the client resolves each entry's
            // title, description and icon from its widget registry (the app widgets from the inlined
            // kleenestar.core i18n, the framework widgets from the framework i18n).
            return AvailableWidgetIds.Select(id => new RestApiDashboardAvailableWidget { Id = id });
        }

        /// <summary>
        /// Persists a column-only layout change (add / rename / resize / recolor / reorder / delete)
        /// for the dashboard identified by the request, preserving the widgets of surviving columns.
        /// </summary>
        /// <param name="layout">The layout payload carrying the full ordered column list.</param>
        /// <param name="request">The current HTTP request. Cannot be null.</param>
        protected override void UpdtaeColumns(RestApiDashboardLayout layout, IRequest request)
        {
            if (layout?.Columns is null || !TryGetInsightId(request, out var insightId))
            {
                return;
            }

            DemandArrange(request);

            var columns = layout.Columns
                .Select(column => new DashboardColumn(ParseColumnId(column.Id))
                {
                    Key = ColumnKey(column.Id),
                    Name = FallbackColumnName(column.Title),
                    Size = column.Size,
                    Color = column.Color
                })
                .ToList();

            CoreHub.InsightManager.SetColumns(insightId, columns, BoardViewScope.Insight(request, insightId, InsightViewTypes.Dashboard));
        }

        /// <summary>
        /// Persists a full board change (widget add / delete / reconfigure / move) for the dashboard
        /// identified by the request, rebuilding the widgets of every column. Widget types unknown to
        /// the server are stored verbatim via <see cref="RestApiDashboardWidgetGeneric"/>, so any
        /// widget's settings survive the round trip.
        /// </summary>
        /// <param name="board">The full board carried in the request.</param>
        /// <param name="request">The current HTTP request. Cannot be null.</param>
        protected override void UpdateBoard(IEnumerable<RestApiDashboardBoardColumn> board, IRequest request)
        {
            if (board is null || !TryGetInsightId(request, out var insightId))
            {
                return;
            }

            DemandArrange(request);

            var columns = board
                .Select(column => new DashboardColumn(ParseColumnId(column.Id))
                {
                    Key = ColumnKey(column.Id),
                    Name = FallbackColumnName(column.Title),
                    Size = column.Size,
                    Color = column.Color,
                    Widgets = (column.Widgets ?? [])
                        .Select(widget => new Widget(Guid.NewGuid())
                        {
                            Type = widget.Id,
                            Name = FallbackWidgetName(widget.Title, widget.Id),
                            Color = widget.Color,
                            Params = SerializeParams(widget.Params)
                        })
                        .ToList()
                })
                .ToList();

            CoreHub.InsightManager.SetBoard(insightId, columns, BoardViewScope.Insight(request, insightId, InsightViewTypes.Dashboard));
        }

        /// <summary>
        /// Maps the persisted widgets of a column to their REST API representations, ordered by
        /// position. Widgets carrying a client registry type are emitted as generic widgets whose
        /// params round-trip; legacy WQL-backed widgets (no type) have their content re-delivered as
        /// an info card, since that content is not carried in the round-tripped params.
        /// </summary>
        /// <param name="widgets">The widgets of a column. Must not be null.</param>
        /// <returns>The REST API widgets ready for serialization.</returns>
        private static List<RestApiDashboardWidget> MapWidgets(IEnumerable<Widget> widgets)
        {
            var result = new List<RestApiDashboardWidget>();

            foreach (var widget in widgets.OrderBy(w => w.Position))
            {
                if (!string.IsNullOrWhiteSpace(widget.Type))
                {
                    result.Add(new RestApiDashboardWidgetGeneric(widget.Type)
                    {
                        Title = widget.Name,
                        Color = widget.Color,
                        Movable = true,
                        Params = DeserializeParams(widget.Params)
                    });
                }
                else
                {
                    result.Add(new RestApiDashboardWidgetInfo
                    {
                        Title = widget.Name,
                        Description = widget.Wql,
                        Color = widget.Color ?? "blue",
                        Movable = true
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Refuses a change of the board to a caller who may not change the insight's content.
        /// The framework's save entry points are not virtual; the refusal reaches the user as the
        /// board's own error message.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <exception cref="RestApiRefusal">The caller may not arrange the insight.</exception>
        private static void DemandArrange(IRequest request)
        {
            var insight = global::KleeneStar.Core.WebInsight.InsightScope.Resolve(request);

            if (!global::KleeneStar.Core.WebInsight.InsightScope.MayArrange(insight, request))
            {
                throw new RestApiRefusal(WebExpress.WebCore.Internationalization.I18N.Translate(request, "kleenestar.core:insight.refused.arrange"));
            }
        }

        /// <summary>
        /// Resolves the insight id from the request path segment.
        /// </summary>
        /// <param name="request">The current HTTP request.</param>
        /// <param name="insightId">The parsed insight id when the parse succeeds.</param>
        /// <returns><see langword="true"/> when an insight id is present and valid.</returns>
        private static bool TryGetInsightId(IRequest request, out Guid insightId)
        {
            return Guid.TryParse(request?.GetParameter<InsightIdParameter>()?.Value, out insightId);
        }

        /// <summary>
        /// Parses a client column id into its business id. A client-generated id for a newly added
        /// column (not a GUID) resolves to <see cref="Guid.Empty"/>, signalling a fresh column.
        /// </summary>
        /// <param name="id">The client column id.</param>
        /// <returns>The parsed GUID, or <see cref="Guid.Empty"/> for a new column.</returns>
        private static Guid ParseColumnId(string id)
        {
            return Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty;
        }

        /// <summary>
        /// Returns the transient client key for a column: the client id of a session-new column (a
        /// non-GUID token), or null once the column is addressed by its business id. The key lets the
        /// server correlate the same freshly added column across a board update and a later column
        /// update, before the client has reloaded and learned the persisted id.
        /// </summary>
        /// <param name="id">The client column id.</param>
        /// <returns>The client key, or null for a column addressed by its business id.</returns>
        private static string ColumnKey(string id)
        {
            return Guid.TryParse(id, out _) ? null : id;
        }

        /// <summary>
        /// Returns a non-empty column name, falling back to a default when the client cleared it.
        /// </summary>
        /// <param name="title">The column title from the payload.</param>
        /// <returns>The column name to persist.</returns>
        private static string FallbackColumnName(string title)
        {
            return string.IsNullOrWhiteSpace(title) ? "Column" : title;
        }

        /// <summary>
        /// Returns a non-empty widget name, falling back to the widget type id when the client
        /// cleared the name.
        /// </summary>
        /// <param name="title">The widget title from the payload.</param>
        /// <param name="type">The widget type id from the payload.</param>
        /// <returns>The widget name to persist.</returns>
        private static string FallbackWidgetName(string title, string type)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return string.IsNullOrWhiteSpace(type) ? "Widget" : type;
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
        /// Deserializes the stored widget params back into the string dictionary the client consumes.
        /// A null or blank value yields an empty dictionary; an unreadable value is ignored.
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
