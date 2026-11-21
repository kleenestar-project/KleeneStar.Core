using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.CalendarManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCalendarManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("D31D8A6F-3CB2-4E1B-8A0C-1A82B9D9F1E2");
        private static readonly Guid ClassId = Guid.Parse("AB94CEAF-2D6B-4B07-9C2C-8C5A4E0D7F1A");

        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-cal", Name = "workspace" });
            }

            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.CalendarManager.Add"/> persists the
        /// calendar and that <see cref="KleeneStar.Core.WebManager.CalendarManager.GetCalendar"/>
        /// retrieves it including its children.
        /// </summary>
        [Fact]
        public void Add_Then_GetCalendar_RoundTrip()
        {
            Seed(nameof(Add_Then_GetCalendar_RoundTrip));

            var calendar = SampleCalendar();

            CoreHub.CalendarManager.Add(calendar);
            var loaded = CoreHub.CalendarManager.GetCalendar(calendar.Id);

            Assert.NotNull(loaded);
            Assert.Equal(calendar.Name, loaded.Name);
            Assert.Equal(7, loaded.BusinessHours.Count);
            Assert.Single(loaded.Holidays);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.CalendarManager.GetCalendars(ClassIdParameter)"/>
        /// returns calendars belonging to the supplied class.
        /// </summary>
        [Fact]
        public void GetCalendars_ByClassId_ReturnsCalendarsForClass()
        {
            Seed(nameof(GetCalendars_ByClassId_ReturnsCalendarsForClass));

            CoreHub.CalendarManager.Add(SampleCalendar("Alpha"));
            CoreHub.CalendarManager.Add(SampleCalendar("Beta"));

            var result = CoreHub.CalendarManager.GetCalendars(new ClassIdParameter(ClassId)).ToList();

            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Name == "Alpha");
            Assert.Contains(result, c => c.Name == "Beta");
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.CalendarManager.Update"/> changes scalar properties
        /// and replaces the child collections.
        /// </summary>
        [Fact]
        public void Update_Calendar_ReplacesChildren()
        {
            Seed(nameof(Update_Calendar_ReplacesChildren));

            var calendar = SampleCalendar("Initial");
            CoreHub.CalendarManager.Add(calendar);

            calendar.Name = "Renamed";
            calendar.TimeZone = "UTC";
            calendar.BusinessHours.Clear();
            calendar.BusinessHours.Add(new BusinessHourSlot { DayOfWeek = DayOfWeek.Friday, Enabled = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) });
            calendar.Holidays.Clear();

            CoreHub.CalendarManager.Update(calendar);
            var loaded = CoreHub.CalendarManager.GetCalendar(calendar.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
            Assert.Equal("UTC", loaded.TimeZone);
            Assert.Single(loaded.BusinessHours);
            Assert.Empty(loaded.Holidays);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.CalendarManager.Remove"/> deletes the calendar
        /// and raises the <see cref="KleeneStar.Core.WebManager.ICalendarManager.CalendarRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_Calendar_DeletesItAndRaisesEvent()
        {
            Seed(nameof(Remove_Calendar_DeletesItAndRaisesEvent));

            var calendar = SampleCalendar();
            CoreHub.CalendarManager.Add(calendar);

            Calendar raised = null;
            CoreHub.CalendarManager.CalendarRemoved += (_, c) => raised = c;

            CoreHub.CalendarManager.Remove(calendar.Id);

            Assert.Null(CoreHub.CalendarManager.GetCalendar(calendar.Id));
            Assert.NotNull(raised);
            Assert.Equal(calendar.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.CalendarManager.Remove"/> is a no-op when
        /// the calendar does not exist.
        /// </summary>
        [Fact]
        public void Remove_UnknownCalendar_IsNoOp()
        {
            Seed(nameof(Remove_UnknownCalendar_IsNoOp));

            CoreHub.CalendarManager.Remove(Guid.NewGuid());

            Assert.Empty(CoreHub.CalendarManager.GetCalendars(ClassId));
        }

        /// <summary>
        /// Verifies that <see cref="KleeneStar.Core.WebManager.CalendarManager.ReservedCalendarNames"/>
        /// blocks well-known URL segments.
        /// </summary>
        [Fact]
        public void ReservedCalendarNames_BlocksRouterSegments()
        {
            Assert.Contains("add", KleeneStar.Core.WebManager.CalendarManager.ReservedCalendarNames);
            Assert.Contains("edit", KleeneStar.Core.WebManager.CalendarManager.ReservedCalendarNames);
            Assert.Contains("delete", KleeneStar.Core.WebManager.CalendarManager.ReservedCalendarNames);
            Assert.Contains("api", KleeneStar.Core.WebManager.CalendarManager.ReservedCalendarNames);
        }

        private static Calendar SampleCalendar(string name = null) => new()
        {
            Id = Guid.NewGuid(),
            Name = name ?? "Standard · Europe/Berlin",
            ClassId = ClassId,
            State = CalendarState.Active,
            TimeZone = "Europe/Berlin",
            Region = "DE",
            IsDefault = false,
            BusinessHours =
            {
                new BusinessHourSlot { DayOfWeek = DayOfWeek.Monday,    Enabled = true,  StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) },
                new BusinessHourSlot { DayOfWeek = DayOfWeek.Tuesday,   Enabled = true,  StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) },
                new BusinessHourSlot { DayOfWeek = DayOfWeek.Wednesday, Enabled = true,  StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) },
                new BusinessHourSlot { DayOfWeek = DayOfWeek.Thursday,  Enabled = true,  StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) },
                new BusinessHourSlot { DayOfWeek = DayOfWeek.Friday,    Enabled = true,  StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(18, 0) },
                new BusinessHourSlot { DayOfWeek = DayOfWeek.Saturday,  Enabled = false, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0,  0) },
                new BusinessHourSlot { DayOfWeek = DayOfWeek.Sunday,    Enabled = false, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0,  0) },
            },
            Holidays =
            {
                new Holiday { Date = new DateOnly(2026, 1, 1), Name = "Neujahr", Region = "DE" }
            }
        };
    }
}
