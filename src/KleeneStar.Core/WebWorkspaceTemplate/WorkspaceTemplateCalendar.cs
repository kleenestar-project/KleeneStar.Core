using System;
using System.Collections.Generic;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// One calendar a <see cref="WorkspaceTemplateClass"/> is created with - the hours its
    /// service-level clocks run in.
    /// </summary>
    public sealed class WorkspaceTemplateCalendar
    {
        /// <summary>
        /// Gets the name of the calendar, which is also how an SLA of the same class refers to
        /// it (<see cref="WorkspaceTemplateSla.Calendar"/>).
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets what the calendar covers - an internationalization key or plain text.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Gets the IANA time zone the hours are read in.
        /// </summary>
        public string TimeZone { get; init; } = "UTC";

        /// <summary>
        /// Gets the region whose public holidays the calendar observes, or null.
        /// </summary>
        public string Region { get; init; }

        /// <summary>
        /// Gets whether the calendar is the class's default.
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Gets the working hours per weekday. A weekday that is not listed is not worked.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateBusinessHours> BusinessHours { get; init; } = [];

        /// <summary>
        /// Gets the days the clock does not run although they are working days.
        /// </summary>
        public IReadOnlyList<WorkspaceTemplateHoliday> Holidays { get; init; } = [];
    }

    /// <summary>
    /// The working hours of one weekday of a <see cref="WorkspaceTemplateCalendar"/>.
    /// </summary>
    /// <param name="Day">The weekday.</param>
    /// <param name="Start">When work starts.</param>
    /// <param name="End">When work ends; earlier than the start for a shift across midnight.</param>
    public sealed record WorkspaceTemplateBusinessHours(DayOfWeek Day, TimeOnly Start, TimeOnly End);

    /// <summary>
    /// One holiday of a <see cref="WorkspaceTemplateCalendar"/>.
    /// </summary>
    /// <param name="Date">The date.</param>
    /// <param name="Name">What the day is called.</param>
    public sealed record WorkspaceTemplateHoliday(DateOnly Date, string Name);
}
