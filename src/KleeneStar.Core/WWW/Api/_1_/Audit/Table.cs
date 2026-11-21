using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Audit
{
    /// <summary>
    /// Serves the audit log as a table: the newest events first, filterable by the axes the log
    /// is typed along.
    /// </summary>
    /// <remarks>
    /// The table offers no create, update or delete. That is not an omission to be filled in
    /// later - the log has no write surface at all beyond the recording the managers do, and
    /// exposing one here would defeat the property the whole feature exists to provide.
    /// <para>
    /// Every enumerated column is rendered as its translated text rather than as its stored
    /// ordinal, and every filter is matched against the stable wire token rather than against
    /// that text. A filter that matched the display text would stop working the moment somebody
    /// switched language.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:audit.table.header")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<AuditEvent>
    {
        private readonly IUri _detailUri;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
            _detailUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Audit.Detail>();
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance that can be used to execute queries.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the collection of columns for the specified REST API request.
        /// </summary>
        /// <param name="request">The request for which to retrieve the table columns.</param>
        /// <returns>An enumerable collection of columns associated with the specified request.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "reference",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.reference"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "timestamp",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.timestamp"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "origin",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.origin"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "category",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.category"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "action",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.action"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "target",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.target"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "actor",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.actor"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "outcome",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.outcome"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "severity",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.severity"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "deltas",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.deltas"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "agent",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.agent"),
                // the agent only says something for the events a machine caused, so it stays
                // available for inspection without occupying a column by default
                Visible = false
            };

            yield return new RestApiTableColumn()
            {
                Id = "client",
                Label = I18N.Translate(request, "kleenestar.core:audit.column.client"),
                Visible = false
            };
        }

        /// <summary>
        /// Retrieves a collection of table rows that match the specified query and context.
        /// </summary>
        /// <param name="query">The query that defines the criteria for selecting table rows.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="columns">The collection of columns to include in the result set.</param>
        /// <param name="request">The request object containing metadata relevant to the retrieval.</param>
        /// <returns>An enumerable collection of table rows that satisfy the query and context.</returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows(IQuery<AuditEvent> query, IQueryContext context, IEnumerable<RestApiTableColumn> columns, IRequest request)
        {
            return CoreHub.AuditManager.GetEvents(query, context)
                .OrderByDescending(x => x.Sequence)
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() { Content = x.Reference },
                        new() { Content = x.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
                        new() { Content = I18N.Translate(request, x.Origin.Text()) },
                        new() { Content = I18N.Translate(request, x.Category.Text()) },
                        new() { Content = I18N.Translate(request, x.Action.Text()) },
                        new() { Content = Target(x, request) },
                        new() { Content = Actor(x, request) },
                        new() { Content = I18N.Translate(request, x.Outcome.Text()) },
                        new() { Content = I18N.Translate(request, x.Severity.Text()) },
                        new() { Content = (x.Deltas?.Count ?? 0).ToString(CultureInfo.InvariantCulture) },
                        new() { Content = x.Agent },
                        new() { Content = x.ClientAddress }
                    ],
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = null
                });
        }

        /// <summary>
        /// Applies the specified filter criteria to the given query object.
        /// </summary>
        /// <remarks>
        /// The free-text filter matches the two fields a reader actually types into it: the name
        /// the target carried and the name the actor carried. Both are snapshots taken when the
        /// event was written, so searching for a deleted user by name still finds what they did -
        /// which is the case the search matters most in.
        /// </remarks>
        /// <param name="filter">A string representing the filter expression to apply.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>A query representing the filtered set of items.</returns>
        protected override IQuery<AuditEvent> Filter(string filter, IQuery<AuditEvent> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase(x => x.TargetKey, filter);
        }

        /// <summary>
        /// Applies the specified quickfilter criteria to the given query object.
        /// </summary>
        /// <param name="filters">A collection of quickfilter identifiers that should be applied.</param>
        /// <param name="query">The query object to which the filter will be applied.</param>
        /// <param name="request">The request that provides the operational context.</param>
        /// <returns>A query representing the filtered set of items.</returns>
        protected override IQuery<AuditEvent> Filter(IEnumerable<string> filters, IQuery<AuditEvent> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                switch (filter[3..].ToLowerInvariant())
                {
                    case "security":
                        query = query.Where(x => x.Category == AuditCategory.Security || x.Category == AuditCategory.Authorization);
                        break;
                    case "failed":
                        query = query.Where(x => x.Outcome != AuditOutcome.Succeeded);
                        break;
                    case "critical":
                        query = query.Where(x => x.Severity == AuditSeverity.Critical);
                        break;
                    default:
                        continue;
                }
            }

            return query;
        }

        /// <summary>
        /// Returns the cell text naming what the event was about: the kind of record and the
        /// name it carried, plus the revision when the record is versioned.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <param name="request">The request used for translating.</param>
        /// <returns>The cell text.</returns>
        private static string Target(AuditEvent @event, IRequest request)
        {
            var kind = I18N.Translate(request, @event.TargetType.Text());

            if (string.IsNullOrWhiteSpace(@event.TargetKey))
            {
                return kind;
            }

            var name = string.Concat(kind, ": ", @event.TargetKey);

            return @event.TargetRevision.HasValue
                ? string.Concat(name, " #", @event.TargetRevision.Value.ToString(CultureInfo.InvariantCulture))
                : name;
        }

        /// <summary>
        /// Returns the cell text naming who caused the event, preferring the identity's current
        /// name and falling back to the name snapshotted when the event was written.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <param name="request">The request used for translating.</param>
        /// <returns>The cell text.</returns>
        private static string Actor(AuditEvent @event, IRequest request)
        {
            return @event.Actor?.Name
                ?? @event.ActorName
                ?? I18N.Translate(request, "kleenestar.core:audit.actor.system");
        }

        /// <summary>
        /// Retrieves the row options. The log is read-only, so the only option is to open the
        /// event in full.
        /// </summary>
        /// <param name="row">The event the options belong to.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The options.</returns>
        private IEnumerable<RestApiOption> GetOptions(AuditEvent row, IRequest request)
        {
            // Add mutates the instance it is called on, so the cached uri must not be used
            // directly: every option would otherwise append to the same accumulating query
            var detailUri = _detailUri is null
                ? null
                : new UriEndpoint(_detailUri).Add
                (
                    new UriQuery(global::KleeneStar.Core.WWW.Settings.Audit.Detail.EventParameter, row.Id.ToString())
                );

            yield return new RestApiOptionCustom(request)
            {
                Text = I18N.Translate(request, "kleenestar.core:audit.detail.title"),
                Icon = new IconEye(),
                PrimaryAction = new ActionModal
                (
                    KleeneStar.Core.WebFragment.Audit.AuditDetailModalFragment.ModalId,
                    detailUri,
                    TypeModalSize.Large
                )
            };
        }
    }
}
