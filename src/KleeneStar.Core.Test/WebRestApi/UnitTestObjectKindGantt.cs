using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using Calendar = KleeneStar.Model.Entities.Calendar;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Provides unit tests for the object Gantt beyond the bars: the dependencies it draws from
    /// the blocking relations, the relations a drawn, changed or deleted link writes, and the
    /// working calendar it counts durations in.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectKindGantt
    {
        private static readonly Guid WorkspaceId = Guid.Parse("6A000000-0000-4000-8000-000000000001");
        private static readonly Guid TaskClassId = Guid.Parse("6A000000-0000-4000-8000-000000000002");
        private static readonly Guid BugClassId = Guid.Parse("6A000000-0000-4000-8000-000000000003");
        private static readonly Guid FirstId = Guid.Parse("6A000000-0000-4000-8000-000000000011");
        private static readonly Guid SecondId = Guid.Parse("6A000000-0000-4000-8000-000000000012");
        private static readonly Guid ThirdId = Guid.Parse("6A000000-0000-4000-8000-000000000013");
        private static readonly Guid BugId = Guid.Parse("6A000000-0000-4000-8000-000000000014");

        /// <summary>
        /// A plan over the objects of the seeded workspace, exposing the hooks under test.
        /// </summary>
        private sealed class Plan : RestApiObjectKindGantt
        {
            protected override string Kind => ObjectKind.Issue;

            protected override IQuery<ObjectEntity> ResolveScope(IRequest request)
            {
                return new Query<ObjectEntity>().WhereEquals(x => x.WorkspaceId, WorkspaceId);
            }

            public IEnumerable<RestApiGanttLink> Links(IRequest request) => RetrieveLinks(request);

            public RestApiGanttCalendar Calendar(IRequest request) => RetrieveCalendar(request);

            public RestApiGanttLink Create(RestApiGanttLink link, IRequest request) => CreateLink(link, request);

            public RestApiGanttLink Change(string id, RestApiGanttLink link, IRequest request) => UpdateLink(id, link, request);

            public bool Drop(string id, IRequest request) => DeleteLink(id, request);
        }

        /// <summary>
        /// Seeds a workspace with two classes, three tasks and a bug, and publishes a blocking
        /// and a referencing relation type.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using (var db = CoreHubFixture.CreateDbContext(connectionString))
            {
                if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
                {
                    db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-plan", Name = "plan" });
                    db.Classes.Add(new Class { Id = TaskClassId, Name = "Task", WorkspaceId = WorkspaceId });
                    db.Classes.Add(new Class { Id = BugClassId, Name = "Bug", WorkspaceId = WorkspaceId });

                    db.Objects.Add(new ObjectEntity { Id = FirstId, Key = "PLN-1", Summary = "first", WorkspaceId = WorkspaceId, ClassId = TaskClassId, Created = DateTime.UtcNow });
                    db.Objects.Add(new ObjectEntity { Id = SecondId, Key = "PLN-2", Summary = "second", WorkspaceId = WorkspaceId, ClassId = TaskClassId, Created = DateTime.UtcNow });
                    db.Objects.Add(new ObjectEntity { Id = ThirdId, Key = "PLN-3", Summary = "third", WorkspaceId = WorkspaceId, ClassId = TaskClassId, Created = DateTime.UtcNow });
                    db.Objects.Add(new ObjectEntity { Id = BugId, Key = "PLN-4", Summary = "bug", WorkspaceId = WorkspaceId, ClassId = BugClassId, Created = DateTime.UtcNow });

                    db.SaveChanges();
                }
            }

            CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = "blocks",
                Label = "blocks",
                InverseLabel = "is blocked by",
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToMany,
                Effect = RelationEffect.BlocksCompletion,
                Active = true,
                Order = 1
            });

            CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = "references",
                Label = "references",
                InverseLabel = "is referenced by",
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToMany,
                Active = true,
                Order = 2
            });
        }

        /// <summary>
        /// Stores a relation between two seeded objects.
        /// </summary>
        private static ObjectRelation Relate(string typeKey, Guid source, Guid target, RelationStatus status = RelationStatus.Active)
        {
            var relation = new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Object,
                TypeKey = typeKey,
                Status = status,
                SourceObjectId = source,
                TargetObjectId = target
            };

            CoreHub.ObjectRelationManager.Add(relation);

            return relation;
        }

        /// <summary>
        /// Verifies that the plan draws exactly the active blocking relations between two of its
        /// bars, from the blocker to the blocked, finish-to-start unless the relation says
        /// otherwise.
        /// </summary>
        [Fact]
        public void Links_AreTheBlockingRelationsAmongTheBars()
        {
            Seed(nameof(Links_AreTheBlockingRelationsAmongTheBars));

            var blocks = Relate("blocks", FirstId, SecondId);
            Relate("references", FirstId, ThirdId);
            Relate("blocks", SecondId, ThirdId, RelationStatus.Obsolete);

            var links = new Plan().Links(CreateRequest()).ToList();

            var link = Assert.Single(links);
            Assert.Equal(blocks.Id.ToString(), link.Id);
            Assert.Equal(FirstId.ToString(), link.From);
            Assert.Equal(SecondId.ToString(), link.To);
            Assert.Equal("FS", link.Type);
        }

        /// <summary>
        /// Verifies that a drawn link is stored as a blocking relation carrying the Gantt type,
        /// and that the plan reads it back under the relation's id.
        /// </summary>
        [Fact]
        public void Create_StoresABlockingRelation()
        {
            Seed(nameof(Create_StoresABlockingRelation));

            var plan = new Plan();
            var created = plan.Create(new RestApiGanttLink { From = FirstId.ToString(), To = SecondId.ToString(), Type = "SS" }, CreateRequest());

            var stored = CoreHub.ObjectRelationManager.GetRelation(Guid.Parse(created.Id));

            Assert.NotNull(stored);
            Assert.Equal("blocks", stored.TypeKey);
            Assert.Equal(FirstId, stored.SourceObjectId);
            Assert.Equal(SecondId, stored.TargetObjectId);
            Assert.Equal("SS", stored.Metadata[RestApiObjectKindGantt.LinkTypeMetadata]);
            Assert.Equal("SS", Assert.Single(plan.Links(CreateRequest())).Type);
        }

        /// <summary>
        /// Verifies that a link closing a blocking cycle is refused, also when the cycle runs
        /// through a third object.
        /// </summary>
        [Fact]
        public void Create_RefusesACycle()
        {
            Seed(nameof(Create_RefusesACycle));

            Relate("blocks", FirstId, SecondId);
            Relate("blocks", SecondId, ThirdId);

            Assert.Throws<RestApiRefusal>(() => new Plan().Create(new RestApiGanttLink { From = ThirdId.ToString(), To = FirstId.ToString(), Type = "FS" }, CreateRequest()));
        }

        /// <summary>
        /// Verifies that a class group cannot be linked: it is a heading, not an object.
        /// </summary>
        [Fact]
        public void Create_RefusesAClassGroup()
        {
            Seed(nameof(Create_RefusesAClassGroup));

            Assert.Throws<RestApiRefusal>(() => new Plan().Create(new RestApiGanttLink { From = $"class:{TaskClassId}", To = SecondId.ToString(), Type = "FS" }, CreateRequest()));
        }

        /// <summary>
        /// Verifies that a link the catalog rejects - a second identical blocking relation - is
        /// refused rather than stored twice.
        /// </summary>
        [Fact]
        public void Create_RefusesADuplicate()
        {
            Seed(nameof(Create_RefusesADuplicate));

            Relate("blocks", FirstId, SecondId);

            Assert.Throws<RestApiRefusal>(() => new Plan().Create(new RestApiGanttLink { From = FirstId.ToString(), To = SecondId.ToString(), Type = "FS" }, CreateRequest()));
            Assert.Single(CoreHub.ObjectRelationManager.GetRelations(FirstId));
        }

        /// <summary>
        /// Verifies that changing a link changes its type and leaves its ends, and that a change
        /// re-pointing it is refused.
        /// </summary>
        [Fact]
        public void Change_WritesTheTypeAndKeepsTheEnds()
        {
            Seed(nameof(Change_WritesTheTypeAndKeepsTheEnds));

            var relation = Relate("blocks", FirstId, SecondId);
            var plan = new Plan();

            var changed = plan.Change(relation.Id.ToString(), new RestApiGanttLink { From = FirstId.ToString(), To = SecondId.ToString(), Type = "FF" }, CreateRequest());

            Assert.Equal("FF", changed.Type);
            Assert.Equal("FF", CoreHub.ObjectRelationManager.GetRelation(relation.Id).Metadata[RestApiObjectKindGantt.LinkTypeMetadata]);

            Assert.Throws<RestApiRefusal>(() => plan.Change(relation.Id.ToString(), new RestApiGanttLink { From = FirstId.ToString(), To = ThirdId.ToString(), Type = "FF" }, CreateRequest()));
            Assert.Equal(SecondId, CoreHub.ObjectRelationManager.GetRelation(relation.Id).TargetObjectId);
        }

        /// <summary>
        /// Verifies that a relation the plan does not draw cannot be changed or deleted through it.
        /// </summary>
        [Fact]
        public void ChangeAndDrop_IgnoreARelationThatIsNoDependency()
        {
            Seed(nameof(ChangeAndDrop_IgnoreARelationThatIsNoDependency));

            var relation = Relate("references", FirstId, SecondId);
            var plan = new Plan();

            Assert.Null(plan.Change(relation.Id.ToString(), new RestApiGanttLink { From = FirstId.ToString(), To = SecondId.ToString(), Type = "FF" }, CreateRequest()));
            Assert.False(plan.Drop(relation.Id.ToString(), CreateRequest()));
            Assert.NotNull(CoreHub.ObjectRelationManager.GetRelation(relation.Id));
        }

        /// <summary>
        /// Verifies that deleting a link removes the relation behind it.
        /// </summary>
        [Fact]
        public void Drop_RemovesTheRelation()
        {
            Seed(nameof(Drop_RemovesTheRelation));

            var relation = Relate("blocks", FirstId, SecondId);

            Assert.True(new Plan().Drop(relation.Id.ToString(), CreateRequest()));
            Assert.Null(CoreHub.ObjectRelationManager.GetRelation(relation.Id));
        }

        /// <summary>
        /// Verifies that the plan counts working days when every class on it is timed by the same
        /// week and holidays, observing only the holidays of the calendar's region.
        /// </summary>
        [Fact]
        public void Calendar_IsSharedWhenTheClassesAgree()
        {
            var name = nameof(Calendar_IsSharedWhenTheClassesAgree);
            Seed(name);
            AddCalendar(name, TaskClassId, "DE-BW");
            AddCalendar(name, BugClassId, "DE-BW");

            var calendar = new Plan().Calendar(CreateRequest());

            Assert.NotNull(calendar);
            Assert.Equal([1, 2, 3, 4, 5], calendar.WorkingDays);
            Assert.Equal(["2026-10-03"], calendar.Holidays);
        }

        /// <summary>
        /// Verifies that the plan stays in calendar days when the classes disagree or one of them
        /// has no calendar.
        /// </summary>
        [Fact]
        public void Calendar_IsAbsentWhenTheClassesDisagree()
        {
            var name = nameof(Calendar_IsAbsentWhenTheClassesDisagree);
            Seed(name);
            AddCalendar(name, TaskClassId, "DE-BW");

            Assert.Null(new Plan().Calendar(CreateRequest()));

            AddCalendar(name, BugClassId, "DE-BY");

            Assert.Null(new Plan().Calendar(CreateRequest()));
        }

        /// <summary>
        /// Adds an active default calendar working Monday to Friday, with a nationwide holiday and
        /// one of another region.
        /// </summary>
        private static void AddCalendar(string connectionString, Guid classId, string region)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            var id = Guid.NewGuid();

            db.Calendars.Add(new Calendar
            {
                Id = id,
                Name = "Business hours",
                Region = region,
                State = CalendarState.Active,
                IsDefault = true,
                ClassId = classId,
                BusinessHours =
                [
                    .. Enum.GetValues<DayOfWeek>().Select(day => new BusinessHourSlot
                    {
                        Id = Guid.NewGuid(),
                        CalendarId = id,
                        DayOfWeek = day,
                        Enabled = day is not DayOfWeek.Saturday and not DayOfWeek.Sunday,
                        StartTime = new TimeOnly(8, 0),
                        EndTime = new TimeOnly(17, 0)
                    })
                ],
                Holidays =
                [
                    new Holiday { Id = Guid.NewGuid(), CalendarId = id, Date = new DateOnly(2026, 10, 3), Name = "Unity Day", Region = "DE", Enabled = true },
                    new Holiday { Id = Guid.NewGuid(), CalendarId = id, Date = new DateOnly(2026, 11, 1), Name = "All Saints", Region = "DE-BY", Enabled = true },
                    new Holiday { Id = Guid.NewGuid(), CalendarId = id, Date = new DateOnly(2026, 12, 31), Name = "disabled", Region = null, Enabled = false }
                ]
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Builds a plan request carrying a culture.
        /// </summary>
        private static IRequest CreateRequest()
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method = "GET",
                Protocol = "HTTP/1.1",
                Scheme = "http",
                RawTarget = "/kleenestar/api/1/objects/ws-plan/gantt",
                QueryString = string.Empty,
                Headers = new HeaderDictionary { ["Host"] = "localhost", ["Accept-Language"] = "en" }
            });
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                LocalIpAddress = System.Net.IPAddress.Loopback,
                RemoteIpAddress = System.Net.IPAddress.Loopback
            });
            features.Set<IHttpRequestIdentifierFeature>(new HttpRequestIdentifierFeature
            {
                TraceIdentifier = nameof(UnitTestObjectKindGantt)
            });

            return new WebExpress.WebCore.WebMessage.HttpContext(features, null!).Request;
        }
    }
}
