using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The activity area: the objects people worked on last, each once, with who did what and
    /// when.
    /// </summary>
    /// <remarks>
    /// The area used to print the audit log's last entries as they came - and the audit log is
    /// mostly the installation talking to itself: <i>System started installation</i>,
    /// <i>Admin signed in</i>, six times over. What a reader wants from "what happened" is the
    /// work, so only events a <b>user</b> caused on an <b>object</b>, in the content or
    /// workflow category, are shown (<see cref="IsWork"/>), each object once (its latest
    /// event), and only an object the reader may open - the audit log is not narrowed by
    /// security levels, <see cref="IObjectManager.GetObject(Guid)"/> is, so a classified record
    /// does not leak its title through the feed. The key figure beside the page counts the
    /// same events (<see cref="WorkQuery"/>).
    /// </remarks>
    public static class LandingActivitySection
    {
        /// <summary>
        /// The maximum number of entries shown.
        /// </summary>
        private const int MaxItems = 6;

        /// <summary>
        /// The number of events read at most while collecting distinct visible objects.
        /// </summary>
        private const int ScanLimit = 60;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="auditManager">The audit manager the activity is read from.</param>
        /// <param name="objectManager">The object manager deciding what the reader may see.</param>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The section.</returns>
        public static IControl Build(IAuditManager auditManager, IObjectManager objectManager, IRenderControlContext renderContext)
        {
            var entries = GetEntries(auditManager, objectManager);

            var section = new ControlSection("landing-activity")
            {
                Header = _ => "kleenestar.core:landing.activity.card",
                HeaderIcon = _ => new IconClock(),
                Layout = _ => TypeLayoutSection.Rule
            };

            if (entries.Count == 0)
            {
                section.Add(LandingRow.Empty("landing-activity-empty", "kleenestar.core:landing.activity.empty"));

                return section;
            }

            var list = LandingRow.List("landing-activity-list");

            foreach (var (@event, @object) in entries)
            {
                var kind = ObjectKindCatalog.GetKind(@object.Kind);

                list.Add(LandingRow.Build
                (
                    "landing-activity-" + @event.Id.ToString("N"),
                    WebControl.ObjectIcon.Resolve(@object, kind?.Icon ?? new IconObject()),
                    @object.Summary,
                    ObjectKindCatalog.ResolveDetailUri(@object),
                    meta: LandingHtml.Join(Sentence(@event, renderContext), LandingHtml.Age(@event.Timestamp, renderContext))
                ));
            }

            section.Add(list);

            return section;
        }

        /// <summary>
        /// Builds the query over the events that count as work: caused by a user, on an object,
        /// in the content or workflow category.
        /// </summary>
        /// <returns>The query, newest first.</returns>
        public static IQuery<AuditEvent> WorkQuery()
        {
            return new Query<AuditEvent>()
                .Where(x => x.Origin == AuditOrigin.User)
                .Where(x => x.TargetType == AuditTargetType.Object)
                .Where(x => x.Category == AuditCategory.Content || x.Category == AuditCategory.Workflow)
                .OrderByDesc(x => x.Sequence);
        }

        /// <summary>
        /// Determines whether an event counts as work - the in-memory twin of
        /// <see cref="WorkQuery"/>.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <returns><see langword="true"/> for work.</returns>
        public static bool IsWork(AuditEvent @event)
        {
            return @event is not null
                && @event.Origin == AuditOrigin.User
                && @event.TargetType == AuditTargetType.Object
                && @event.Category is AuditCategory.Content or AuditCategory.Workflow;
        }

        /// <summary>
        /// Reads the latest work events, one per object the reader may open.
        /// </summary>
        /// <param name="auditManager">The audit manager.</param>
        /// <param name="objectManager">The object manager.</param>
        /// <returns>The events with their objects, newest first.</returns>
        private static IReadOnlyList<(AuditEvent Event, Model.Entities.Object Object)> GetEntries(IAuditManager auditManager, IObjectManager objectManager)
        {
            var result = new List<(AuditEvent, Model.Entities.Object)>();
            var seen = new HashSet<Guid>();

            foreach (var @event in auditManager.GetEvents(WorkQuery().WithPaging(0, ScanLimit)).Where(IsWork))
            {
                if (@event.TargetId is not { } id || !seen.Add(id))
                {
                    continue;
                }

                var @object = objectManager.GetObject(id);

                if (@object is null)
                {
                    continue;
                }

                result.Add((@event, @object));

                if (result.Count == MaxItems)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Composes who did what: "Admin User updated the object".
        /// </summary>
        /// <remarks>
        /// The two halves fall in a different order per language, so the pattern carries the
        /// placeholders and is translated before the parts are put in.
        /// </remarks>
        private static string Sentence(AuditEvent @event, IRenderControlContext renderContext)
        {
            var actor = (@event.ActorId.HasValue ? CoreHub.IdentityManager?.GetIdentity(@event.ActorId.Value)?.Name : null)
                ?? @event.ActorName
                ?? I18N.Translate(renderContext, "kleenestar.core:audit.actor.system");

            var predicate = string.Format
            (
                LandingHtml.Culture(renderContext),
                I18N.Translate(renderContext, "kleenestar.core:landing.activity.sentence"),
                I18N.Translate(renderContext, @event.TargetType.Text()),
                I18N.Translate(renderContext, @event.Action.Text())
            );

            return actor + " " + predicate;
        }
    }
}
