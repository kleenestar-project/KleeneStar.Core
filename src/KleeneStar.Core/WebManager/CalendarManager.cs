using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the lifecycle of <see cref="Calendar"/> entities and their dependent
    /// <see cref="BusinessHourSlot"/>s and <see cref="Holiday"/> entries.
    /// </summary>
    public sealed class CalendarManager : ICalendarManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;

        /// <summary>
        /// Raised when a new calendar has been added to the manager.
        /// </summary>
        public event EventHandler<Calendar> CalendarAdded;

        /// <summary>
        /// Raised when a calendar's scalar properties or child collections have been updated.
        /// </summary>
        public event EventHandler<Calendar> CalendarUpdated;

        /// <summary>
        /// Raised when a calendar has been removed from the manager. The event fires after
        /// the underlying cascade has cleaned up the dependent business-hour slots and
        /// holidays.
        /// </summary>
        public event EventHandler<Calendar> CalendarRemoved;

        /// <summary>
        /// Returns the path-segment names reserved by the calendar router.
        /// </summary>
        public static IEnumerable<string> ReservedCalendarNames =>
        [
            "default", "admin", "system", "assets", "api", "add", "edit",
            "delete", "clone", "settings", "icons"
        ];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private CalendarManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the calendar identified by the supplied id, including its weekly
        /// schedule and holiday list.
        /// </summary>
        /// <param name="calendarId">The calendar id.</param>
        /// <returns>The calendar, or <c>null</c> when no entry matches.</returns>
        public Calendar GetCalendar(Guid calendarId)
        {
            var query = new Query<Calendar>()
                .Where(x => x.Id == calendarId)
                .WithPaging(0, 1);

            return ModelHub.GetCalendars(query).FirstOrDefault();
        }

        /// <summary>
        /// Returns the calendar identified by the supplied URL-bound id parameter.
        /// </summary>
        /// <param name="calendarId">The id parameter parsed from the URL path.</param>
        /// <returns>The calendar, or <c>null</c> when no entry matches.</returns>
        public Calendar GetCalendar(CalendarIdParameter calendarId)
        {
            ArgumentNullException.ThrowIfNull(calendarId);

            var guid = Guid.TryParse(calendarId.Value, out var id) ? id : Guid.Empty;

            return GetCalendar(guid);
        }

        /// <summary>
        /// Returns every calendar attached to the class addressed by the supplied
        /// URL-bound class-id parameter.
        /// </summary>
        /// <param name="classId">The class-id parameter parsed from the URL path.</param>
        /// <returns>The calendars belonging to the class. The collection may be empty.</returns>
        public IEnumerable<Calendar> GetCalendars(ClassIdParameter classId)
        {
            ArgumentNullException.ThrowIfNull(classId);

            var guid = Guid.TryParse(classId.Value, out var id) ? id : Guid.Empty;

            return GetCalendars(guid);
        }

        /// <summary>
        /// Returns every calendar attached to the class with the supplied id.
        /// </summary>
        /// <param name="classId">The class id.</param>
        /// <returns>The calendars belonging to the class. The collection may be empty.</returns>
        public IEnumerable<Calendar> GetCalendars(Guid classId)
        {
            var query = new Query<Calendar>()
                .WhereEquals(x => x.ClassId, classId);

            return ModelHub.GetCalendars(query);
        }

        /// <summary>
        /// Returns the calendars that satisfy the supplied query. The manager opens its
        /// own DbContext for the call.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <returns>The matching calendars.</returns>
        public IEnumerable<Calendar> GetCalendars(IQuery<Calendar> query)
        {
            return ModelHub.GetCalendars(query);
        }

        /// <summary>
        /// Returns the calendars that satisfy the supplied query, executed inside the
        /// supplied <see cref="IQueryContext"/> (expected to be a
        /// <see cref="KleeneStarDbContext"/>).
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <returns>The matching calendars.</returns>
        public IEnumerable<Calendar> GetCalendars(IQuery<Calendar> query, IQueryContext context)
        {
            return ModelHub.GetCalendars(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Adds the supplied calendar to the database, raises <see cref="CalendarAdded"/>,
        /// and emits a UI notification. Returns the manager instance to allow chaining.
        /// </summary>
        /// <param name="calendar">The calendar to add.</param>
        /// <returns>The current manager instance.</returns>
        public ICalendarManager Add(Calendar calendar)
        {
            ArgumentNullException.ThrowIfNull(calendar);

            ModelHub.Add(calendar);

            CalendarAdded?.Invoke(this, calendar);

            TryAddNotification("kleenestar.core:notification.title.created", "kleenestar.core:notification.calendar.created", calendar);

            return this;
        }

        /// <summary>
        /// Persists the supplied calendar's scalar properties and replaces its weekly
        /// schedule and holiday list with the entries on the incoming entity. Raises
        /// <see cref="CalendarUpdated"/> and emits a UI notification.
        /// </summary>
        /// <param name="calendar">The calendar to update.</param>
        /// <returns>The current manager instance.</returns>
        public ICalendarManager Update(Calendar calendar)
        {
            ArgumentNullException.ThrowIfNull(calendar);

            ModelHub.Update(calendar);

            CalendarUpdated?.Invoke(this, calendar);

            TryAddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.calendar.updated", calendar);

            return this;
        }

        /// <summary>
        /// Removes the calendar identified by the supplied id, cascading the deletion to
        /// its weekly schedule and holiday list. Raises <see cref="CalendarRemoved"/>.
        /// No-op when no calendar matches the id.
        /// </summary>
        /// <param name="calendarId">The id of the calendar to remove.</param>
        /// <returns>The current manager instance.</returns>
        public ICalendarManager Remove(Guid calendarId)
        {
            var existing = GetCalendar(calendarId);

            if (existing is not null)
            {
                ModelHub.Remove(existing);
                CalendarRemoved?.Invoke(this, existing);
            }

            return this;
        }

        /// <summary>
        /// Releases unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        // Wraps CoreHub.AddNotification so unit tests with a partially wired host don't crash.
        private static void TryAddNotification(string titleKey, string messageKey, object subject)
        {
            try
            {
                CoreHub.AddNotification(titleKey, messageKey, subject);
            }
            catch
            {
                // notification is best-effort; ignore failures from incomplete host state
            }
        }
    }
}
