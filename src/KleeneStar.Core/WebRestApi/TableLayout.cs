using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Adapts persisted application layouts to the framework table layout contract.
    /// </summary>
    /// <remarks>
    /// A layout is kept in two places with the same shape: per user and table in the session
    /// store (<see cref="WebManager.ISessionManager.SetTableLayout"/>), and on a saved search,
    /// whose results table opens in the layout it was saved with
    /// (<see cref="Model.Entities.SavedSearch.Columns"/>). Only id, visibility and width are
    /// kept - labels, icons and templates stay the table's own, so a renamed column keeps its
    /// place and a column added later still appears.
    /// </remarks>
    public static class TableLayout
    {
        /// <summary>
        /// The options the layout is written with.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Writes the layout of a column sequence.
        /// </summary>
        /// <param name="columns">The columns in the chosen order.</param>
        /// <returns>The layout, or null for no columns.</returns>
        public static string Serialize(IEnumerable<RestApiTableColumn> columns)
        {
            return columns is null
                ? null
                : Serialize(columns
                    .Where(c => !string.IsNullOrWhiteSpace(c?.Id))
                    .Select(c => new RestApiTableColumnUpdate { Id = c.Id, Visible = c.Visible, Width = c.Width }));
        }

        /// <summary>
        /// Writes a layout that was read before.
        /// </summary>
        /// <param name="layout">The layout entries.</param>
        /// <returns>The layout, or null when there is none.</returns>
        public static string Serialize(IEnumerable<RestApiTableColumnUpdate> layout)
        {
            var entries = layout?.Where(c => !string.IsNullOrWhiteSpace(c?.Id)).ToList();

            return entries is null || entries.Count == 0
                ? null
                : new RestApiTableLayout { Columns = entries }.ToJson();
        }

        /// <summary>
        /// Reads versioned layouts and legacy array snapshots without rewriting stored data.
        /// </summary>
        /// <param name="json">The stored layout.</param>
        /// <returns>The entries, or null when nothing usable is stored.</returns>
        public static IReadOnlyList<RestApiTableColumnUpdate> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var stored = json.TrimStart().StartsWith("[")
                    ? JsonSerializer.Deserialize<List<RestApiTableColumnUpdate>>(json, _jsonOptions)
                    : RestApiTableLayout.Parse(json).Columns.ToList();

                return stored?.Where(c => !string.IsNullOrWhiteSpace(c?.Id)).ToList();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Lays a stored layout over the table's own columns. Columns the layout does not know -
        /// one added in a newer build - follow at the tail in their default state; entries naming
        /// a column the table no longer has are dropped.
        /// </summary>
        /// <param name="stored">The stored layout, may be null.</param>
        /// <param name="defaultColumns">The table's own columns.</param>
        /// <returns>The columns in the stored order, visibility and width.</returns>
        public static IEnumerable<RestApiTableColumn> Apply(IReadOnlyList<RestApiTableColumnUpdate> stored, IEnumerable<RestApiTableColumn> defaultColumns)
        {
            return new RestApiTableLayout { Columns = stored?.ToList() ?? [] }
                .Apply(defaultColumns ?? []);
        }
    }
}
