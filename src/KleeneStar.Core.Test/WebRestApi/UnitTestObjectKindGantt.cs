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

            public IEnumerable<RestApiGanttTask> Tasks(IRequest request) => RetrieveTasks(request);

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

            CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = "parent",
                Label = "is parent of",
                InverseLabel = "is child of",
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.OneToMany,
                Effect = RelationEffect.AggregatesProgress,
                Active = true,
                Order = 3
            });
        }

        /// <summary>
        /// Stores a relation between two seeded objects.
        /// </summary>
        private static ObjectRelation Relate(string typeKey, Guid source, Guid target, RelationStatus status = RelationStatus.Active, DateTime? created = null)
        {
            var relation = new ObjectRelation
            {
                Id = Guid.NewGuid(),
                System = RelationSystem.Object,
                TypeKey = typeKey,
                Status = status,
                SourceObjectId = source,
                TargetObjectId = target,
                Created = created ?? DateTime.UtcNow
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
        /// Verifies that a sub-task hangs under the order aggregating it, even when it is of
        /// another class, while a relation that does not aggregate, or is obsolete, groups nothing.
        /// </summary>
        [Fact]
        public void Tasks_HangUnderTheAggregatingOrder()
        {
            Seed(nameof(Tasks_HangUnderTheAggregatingOrder));

            Relate("parent", FirstId, BugId);
            Relate("blocks", FirstId, SecondId);
            Relate("parent", FirstId, ThirdId, RelationStatus.Obsolete);

            var parents = ParentsOf(new Plan().Tasks(CreateRequest()));

            Assert.Equal(FirstId.ToString(), parents[BugId.ToString()]);
            Assert.Equal($"class:{TaskClassId}", parents[SecondId.ToString()]);
            Assert.Equal($"class:{TaskClassId}", parents[ThirdId.ToString()]);
            Assert.Equal($"class:{TaskClassId}", parents[FirstId.ToString()]);
        }

        /// <summary>
        /// Verifies that the model's own hierarchy wins over an aggregation, and that an object
        /// aggregated twice hangs under the order of the older relation.
        /// </summary>
        [Fact]
        public void Tasks_PreferTheParentThenTheOldestAggregation()
        {
            var name = nameof(Tasks_PreferTheParentThenTheOldestAggregation);
            Seed(name);

            using (var db = CoreHubFixture.CreateDbContext(name))
            {
                db.Objects.Single(x => x.Id == SecondId).ParentId = ThirdId;
                db.SaveChanges();
            }

            Relate("parent", FirstId, SecondId);
            Relate("parent", ThirdId, BugId, created: DateTime.UtcNow);
            Relate("parent", FirstId, BugId, created: DateTime.UtcNow.AddDays(-1));

            var parents = ParentsOf(new Plan().Tasks(CreateRequest()));

            Assert.Equal(ThirdId.ToString(), parents[SecondId.ToString()]);
            Assert.Equal(FirstId.ToString(), parents[BugId.ToString()]);
        }

        /// <summary>
        /// Verifies that an aggregation closing a cycle is not drawn as nesting: the object stays
        /// in its class group instead of being lifted out of every group by the client.
        /// </summary>
        [Fact]
        public void Tasks_SkipAnAggregationThatClosesACycle()
        {
            Seed(nameof(Tasks_SkipAnAggregationThatClosesACycle));

            Relate("parent", FirstId, SecondId, created: DateTime.UtcNow.AddMinutes(-2));
            Relate("parent", SecondId, ThirdId, created: DateTime.UtcNow.AddMinutes(-1));
            Relate("parent", ThirdId, FirstId, created: DateTime.UtcNow);

            var parents = ParentsOf(new Plan().Tasks(CreateRequest()));

            Assert.Equal(FirstId.ToString(), parents[SecondId.ToString()]);
            Assert.Equal(SecondId.ToString(), parents[ThirdId.ToString()]);
            Assert.Equal($"class:{TaskClassId}", parents[FirstId.ToString()]);
        }

        /// <summary>
        /// Returns the parent bar of every object bar of a plan, keyed by the bar id.
        /// </summary>
        private static Dictionary<string, string> ParentsOf(IEnumerable<RestApiGanttTask> tasks)
        {
            return tasks
                .Where(x => !x.Id.StartsWith("class:", StringComparison.Ordinal))
                .ToDictionary(x => x.Id, x => x.ParentId);
        }

        /// <summary>
        /// Verifies that every concrete plan endpoint of the core accepts the sub-paths the
        /// control writes to (<c>/tasks/{id}</c>, <c>/links[/{id}]</c>). The insight plan lacked
        /// the attribute: it loaded its links, and every drawn, retyped or deleted one answered 404.
        /// </summary>
        [Fact]
        public void Endpoints_IncludeTheSubPathsTheControlWritesTo()
        {
            var endpoints = typeof(RestApiObjectKindGantt).Assembly
                .GetTypes()
                .Where(x => !x.IsAbstract && typeof(RestApiObjectKindGantt).IsAssignableFrom(x))
                .ToList();

            Assert.NotEmpty(endpoints);

            foreach (var endpoint in endpoints)
            {
                var attribute = endpoint
                    .GetCustomAttributes(typeof(WebExpress.WebCore.WebAttribute.IncludeSubPathsAttribute), false)
                    .OfType<WebExpress.WebCore.WebAttribute.IncludeSubPathsAttribute>()
                    .SingleOrDefault();

                Assert.True(attribute?.IncludeSubPaths == true, $"{endpoint.FullName} does not include its sub-paths.");
            }
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
