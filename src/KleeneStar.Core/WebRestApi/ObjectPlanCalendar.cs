using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using Calendar = KleeneStar.Model.Entities.Calendar;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Derives the working calendar a Gantt plan counts its durations in from the calendars of
    /// the classes whose objects stand on it.
    /// </summary>
    /// <remarks>
    /// The framework's Gantt only times a plan; which days are worked is the application's to
    /// say (<c>RestApiGantt.RetrieveCalendar</c>). A class carries its own calendars - the one
    /// marked default, otherwise the first active one - and a plan mixes classes, so the plan
    /// counts in working days only where every class on it answers the <b>same</b> week and the
    /// same holidays. One class without a calendar, or two that disagree, and the plan stays in
    /// calendar days: a duration that meant something different on every row would be worse
    /// than one that ignores weekends on all of them.
    /// </remarks>
    internal static class ObjectPlanCalendar
    {
        /// <summary>
        /// Resolves the calendar of a plan made of the objects of the given classes.
        /// </summary>
        /// <param name="classIds">The classes of the objects on the plan.</param>
        /// <returns>
        /// The shared calendar, or <see langword="null"/> when the plan counts calendar days.
        /// </returns>
        public static RestApiGanttCalendar Resolve(IEnumerable<Guid> classIds)
        {
            RestApiGanttCalendar shared = null;

            foreach (var classId in (classIds ?? []).Distinct())
            {
                var calendar = Project(Effective(classId));

                if (calendar is null)
                {
                    return null;
                }

                if (shared is null)
                {
                    shared = calendar;
                }
                else if (!Agree(shared, calendar))
                {
                    return null;
                }
            }

            return shared;
        }

        /// <summary>
        /// Returns the calendar a class is timed by: the active one marked default, otherwise the
        /// first active one by name.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The calendar, or <see langword="null"/> when the class has none.</returns>
        public static Calendar Effective(Guid classId)
        {
            var calendars = CoreHub.CalendarManager
                .GetCalendars(classId)
                .Where(x => x.State == CalendarState.Active)
                .ToList();

            return calendars.FirstOrDefault(x => x.IsDefault)
                ?? calendars.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }

        /// <summary>
        /// Projects a stored calendar onto the wire shape of the Gantt: the weekdays carrying an
        /// enabled business-hour slot, and the enabled holidays of the calendar's region.
        /// </summary>
        /// <param name="calendar">The calendar, may be absent.</param>
        /// <returns>
        /// The projection, or <see langword="null"/> when there is no calendar or it works on no
        /// day at all - the client would replace an empty week with Monday to Friday, which is
        /// a week the administrator never configured.
        /// </returns>
        public static RestApiGanttCalendar Project(Calendar calendar)
        {
            if (calendar is null)
            {
                return null;
            }

            var workingDays = (calendar.BusinessHours ?? [])
                .Where(x => x.Enabled)
                .Select(x => (int)x.DayOfWeek)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            if (workingDays.Length == 0)
            {
                return null;
            }

            var holidays = (calendar.Holidays ?? [])
                .Where(x => x.Enabled && AppliesTo(x, calendar))
                .Select(x => x.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Distinct()
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();

            return new RestApiGanttCalendar
            {
                WorkingDays = workingDays,
                Holidays = holidays
            };
        }

        /// <summary>
        /// Determines whether a holiday is observed by a calendar. A holiday or a calendar
        /// without a region applies everywhere; otherwise the calendar's region has to be the
        /// holiday's or one of its subdivisions, so a nationwide <c>DE</c> holiday is kept by a
        /// <c>DE-BW</c> calendar while a <c>DE-BY</c> one is not.
        /// </summary>
        /// <param name="holiday">The holiday.</param>
        /// <param name="calendar">The calendar it is listed in.</param>
        /// <returns><see langword="true"/> when the calendar observes the holiday.</returns>
        public static bool AppliesTo(Holiday holiday, Calendar calendar)
        {
            var region = holiday?.Region?.Trim();
            var own = calendar?.Region?.Trim();

            if (string.IsNullOrEmpty(region) || string.IsNullOrEmpty(own))
            {
                return true;
            }

            return string.Equals(own, region, StringComparison.OrdinalIgnoreCase)
                || own.StartsWith(region + "-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether two projected calendars count the same days.
        /// </summary>
        /// <param name="left">The first calendar.</param>
        /// <param name="right">The second calendar.</param>
        /// <returns><see langword="true"/> when week and holidays are equal.</returns>
        private static bool Agree(RestApiGanttCalendar left, RestApiGanttCalendar right)
        {
            return left.WorkingDays.SequenceEqual(right.WorkingDays)
                && left.Holidays.SequenceEqual(right.Holidays, StringComparer.Ordinal);
        }
    }
}
