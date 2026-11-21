using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Api._1_.Notifications
{
    /// <summary>
    /// Serves the notification center table: the in-app notifications addressed to the calling
    /// identity, newest first.
    /// </summary>
    /// <remarks>
    /// The rows are always confined to the caller's own notifications. The <c>scope</c> query
    /// parameter narrows them further to the unread ones, which is how the sidebar switches
    /// between "all" and "unread" without a second endpoint.
    /// </remarks>
    [Title("kleenestar.core:notification.center.title")]
    [Cache]
    public sealed class Table : KleeneStarRestApiTable<UserNotification>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Table()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>An IQueryContext instance.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the collection of columns.
        /// </summary>
        /// <param name="request">The triggering request.</param>
        /// <returns>The default columns.</returns>
        protected override IEnumerable<RestApiTableColumn> RetrieveDefaultColumns(IRequest request)
        {
            yield return new RestApiTableColumn()
            {
                Id = "event",
                Label = I18N.Translate(request, "kleenestar.core:notification.center.column.event"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "message",
                Label = I18N.Translate(request, "kleenestar.core:notification.center.column.message"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "subject",
                Label = I18N.Translate(request, "kleenestar.core:notification.center.column.subject"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "actor",
                Label = I18N.Translate(request, "kleenestar.core:notification.center.column.actor"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "state",
                Label = I18N.Translate(request, "kleenestar.core:notification.center.column.state"),
                Visible = true
            };

            yield return new RestApiTableColumn()
            {
                Id = "created",
                Label = I18N.Translate(request, "kleenestar.core:notification.center.column.created"),
                Visible = true
            };
        }

        /// <summary>
        /// Retrieves the table rows matching the query.
        /// </summary>
        /// <param name="query">The query criteria carrying search, filters and paging.</param>
        /// <param name="context">The context in which the query is executed.</param>
        /// <param name="columns">The columns the rows are built for.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The matching rows.</returns>
        protected override IEnumerable<RestApiTableRow> RetrieveRows
        (
            IQuery<UserNotification> query,
            IQueryContext context,
            IEnumerable<RestApiTableColumn> columns,
            IRequest request
        )
        {
            var notifications = CoreHub.NotificationCenterManager
                .GetNotifications(request, unreadOnly: IsUnreadScope(request));

            // the projection below translates keys, formats dates and builds routes, none of
            // which an expression tree can carry — so the rows are materialized first and the
            // shaping happens in memory
            return query.Apply(notifications.AsQueryable())
                .AsEnumerable()
                .Select(x => new RestApiTableRow
                {
                    Id = x.Id.ToString(),
                    Cells =
                    [
                        new RestApiTableCell() { Content = I18N.Translate(request, x.TitleKey) },
                        new() { Content = I18N.Translate(request, x.MessageKey) },
                        new() { Content = x.Subject },
                        new() { Content = DescribeActor(x, request) },
                        new()
                        {
                            Content = I18N.Translate
                            (
                                request,
                                x.Read
                                    ? "kleenestar.core:notification.center.state.read"
                                    : "kleenestar.core:notification.center.state.unread"
                            )
                        },
                        new() { Content = x.Created.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) }
                    ],
                    // the icon of the record the notification is about, falling back to the
                    // picture of the person who caused it — a row is recognized by the thing it
                    // concerns, not by the fact that something happened
                    Image = x.SubjectIcon ?? ResolveActorImage(x),
                    Icon = ResolveEventIcon(x),
                    Options = GetOptions(x, request).Select(o => o.ToJson()),
                    Uri = GetUri(x, request)?.ToString()
                });
        }

        /// <summary>
        /// Applies the free-text search to the query. A notification is found by what it says
        /// and by what it is about, which is what a reader remembers of it.
        /// </summary>
        /// <param name="filter">The search term, or null when nothing was typed.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<UserNotification> Filter(string filter, IQuery<UserNotification> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter == "null")
            {
                return query;
            }

            return query.WhereContainsIgnoreCase(x => x.Subject, filter);
        }

        /// <summary>
        /// Applies the quick filters to the query. They select by what happened, which is the
        /// distinction a full center is usually scanned along.
        /// </summary>
        /// <param name="filters">The active quick filter ids.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<UserNotification> Filter(IEnumerable<string> filters, IQuery<UserNotification> query, IRequest request)
        {
            foreach (var filter in filters.Where(f => f.StartsWith("qf_", StringComparison.OrdinalIgnoreCase)))
            {
                switch (filter[3..].ToLowerInvariant())
                {
                    case "unread":
                        query = query.Where(x => !x.Read);
                        break;
                    case "read":
                        query = query.Where(x => x.Read);
                        break;
                    case "created":
                        query = query.Where(x => x.TitleKey.EndsWith(".created"));
                        break;
                    case "updated":
                        query = query.Where(x => x.TitleKey.EndsWith(".updated"));
                        break;
                    case "deleted":
                        query = query.Where(x => x.TitleKey.EndsWith(".deleted"));
                        break;
                    default:
                        continue;
                }
            }

            return query;
        }

        /// <summary>
        /// Returns the name of the identity that caused the event.
        /// </summary>
        /// <remarks>
        /// An event without a person behind it — a scheduled job, an SLA that ran out — is
        /// named as the system rather than left blank, so the column reads as an answer instead
        /// of a gap.
        /// </remarks>
        /// <param name="notification">The notification the row shows.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The name of the actor.</returns>
        private static string DescribeActor(UserNotification notification, IRequest request)
        {
            if (!notification.ActorId.HasValue)
            {
                return I18N.Translate(request, "kleenestar.core:notification.center.actor.system");
            }

            return CoreHub.IdentityManager.GetIdentity(notification.ActorId.Value)?.Name
                ?? I18N.Translate(request, "kleenestar.core:notification.center.actor.system");
        }

        /// <summary>
        /// Returns the picture of the identity that caused the event, used when the record the
        /// notification is about carries no icon of its own.
        /// </summary>
        /// <param name="notification">The notification the row shows.</param>
        /// <returns>The path of the picture, or <see langword="null"/>.</returns>
        private static string ResolveActorImage(UserNotification notification)
        {
            if (!notification.ActorId.HasValue)
            {
                return null;
            }

            return CoreHub.IdentityManager.GetIdentity(notification.ActorId.Value)?.Avatar?.Uri?.ToString();
        }

        /// <summary>
        /// Returns the glyph describing what happened, shown when neither the record nor the
        /// actor offers a picture.
        /// </summary>
        /// <param name="notification">The notification the row shows.</param>
        /// <returns>The CSS class of the icon.</returns>
        private static string ResolveEventIcon(UserNotification notification)
        {
            var key = notification.TitleKey ?? string.Empty;

            if (key.EndsWith(".created", StringComparison.OrdinalIgnoreCase))
            {
                return new IconPlus().Class;
            }

            if (key.EndsWith(".updated", StringComparison.OrdinalIgnoreCase))
            {
                return new IconPen().Class;
            }

            if (key.EndsWith(".deleted", StringComparison.OrdinalIgnoreCase))
            {
                return new IconTrashCan().Class;
            }

            return new IconBell().Class;
        }

        /// <summary>
        /// Returns whether the request asks for the unread notifications only.
        /// </summary>
        /// <param name="request">The triggering request.</param>
        /// <returns><c>true</c> when the caller asked for the unread scope.</returns>
        private static bool IsUnreadScope(IRequest request)
        {
            return string.Equals(request?.GetParameter("scope")?.Value, "unread", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Retrieves the row options: mark a single notification as seen, or remove it.
        /// </summary>
        /// <param name="row">The notification the options act on.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The options of the row.</returns>
        private static IEnumerable<RestApiOption> GetOptions(UserNotification row, IRequest request)
        {
            if (!row.Read)
            {
                yield return new RestApiOptionCustom(request)
                {
                    Text = I18N.Translate(request, "kleenestar.core:notification.center.markread"),
                    Icon = new IconCheck(),
                    Uri = BuildUri<global::KleeneStar.Core.WWW.Notifications.Read>(row, request)
                };
            }

            yield return new RestApiOptionDelete(request)
            {
                Icon = new IconTrashCan(),
                Uri = BuildUri<global::KleeneStar.Core.WWW.Notifications.Delete>(row, request)
            };
        }

        /// <summary>
        /// Returns the target a row navigates to: opening a notification marks it as seen and
        /// continues to what it announced.
        /// </summary>
        /// <param name="row">The notification the row shows.</param>
        /// <param name="request">The triggering request.</param>
        /// <returns>The row target.</returns>
        private static IUri GetUri(UserNotification row, IRequest request)
        {
            var uri = BuildUri<global::KleeneStar.Core.WWW.Notifications.Read>(row, request);

            return string.IsNullOrWhiteSpace(row.TargetUri)
                ? uri
                : uri?.Add(new UriQuery("target", row.TargetUri));
        }

        /// <summary>
        /// Builds the route of an action page addressing the given notification.
        /// </summary>
        /// <typeparam name="TEndpoint">The action page to address.</typeparam>
        /// <param name="row">The notification the action acts on.</param>
        /// <param name="request">The triggering request, used to bind the route.</param>
        /// <returns>The bound route, or <see langword="null"/>.</returns>
        private static IUri BuildUri<TEndpoint>(UserNotification row, IRequest request)
            where TEndpoint : WebExpress.WebCore.WebEndpoint.IEndpoint
        {
            return CoreHub.GetUri<TEndpoint>()?
                .BindParameters(request)
                .Add(new UriQuery("id", row.Id.ToString()));
        }
    }
}
