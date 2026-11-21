using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for the count helpers the landing page's key figures rest on -
    /// <c>CountObjects</c>, <c>CountIdentities</c>, <c>CountGroups</c> and
    /// <c>CountEvents</c>. Each answers a filtered query with a number and must agree with
    /// what the matching <c>Get…</c> call returns.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestManagerCounts
    {
        private static readonly Guid WorkspaceId = Guid.Parse("B0C1D2E3-1111-4111-8111-111111111111");
        private static readonly Guid ClassId = Guid.Parse("B0C1D2E3-2222-4222-8222-222222222222");

        /// <summary>
        /// Seeds three issues (one of them archived), two documents, two identities (one of
        /// them locked) and two groups.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-cnt", Name = "workspace" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });

            void add(string key, string kind, WorkspaceState state)
                => db.Objects.Add(new ObjectEntity
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Summary = key,
                    Kind = kind,
                    State = state,
                    WorkspaceId = WorkspaceId,
                    ClassId = ClassId
                });

            add("CNT-1", ObjectKind.Issue, WorkspaceState.Active);
            add("CNT-2", ObjectKind.Issue, WorkspaceState.Active);
            add("CNT-3", ObjectKind.Issue, WorkspaceState.Archived);
            add("CNT-4", ObjectKind.Document, WorkspaceState.Active);
            add("CNT-5", ObjectKind.Document, WorkspaceState.Active);

            db.Identities.Add(new Identity { Id = Guid.NewGuid(), Name = "Active One", UserName = "one", Email = "one@example.test", PasswordHash = "x", State = IdentityState.Active });
            db.Identities.Add(new Identity { Id = Guid.NewGuid(), Name = "Locked One", UserName = "two", Email = "two@example.test", PasswordHash = "x", State = IdentityState.Locked });

            db.Groups.Add(new Group { Id = Guid.NewGuid(), Name = "Team A", State = GroupState.Active });
            db.Groups.Add(new Group { Id = Guid.NewGuid(), Name = "Team B", State = GroupState.Active });

            db.SaveChanges();
        }

        /// <summary>
        /// The object count applies both filters of a query and matches the row count the
        /// same query returns.
        /// </summary>
        [Fact]
        public void CountObjects_Applies_The_Filters()
        {
            Seed(nameof(CountObjects_Applies_The_Filters));

            IQuery<ObjectEntity> build() => new Query<ObjectEntity>()
                .WhereEquals(x => x.Kind, ObjectKind.Issue)
                .Where(x => x.State == WorkspaceState.Active);

            Assert.Equal(2, CoreHub.ObjectManager.CountObjects(build()));
            Assert.Equal(CoreHub.ObjectManager.GetObjects(build()).Count(), CoreHub.ObjectManager.CountObjects(build()));
        }

        /// <summary>
        /// An unfiltered count reports every object, kinds and states alike.
        /// </summary>
        [Fact]
        public void CountObjects_Without_Filters_Counts_Everything()
        {
            Seed(nameof(CountObjects_Without_Filters_Counts_Everything));

            Assert.Equal(5, CoreHub.ObjectManager.CountObjects(new Query<ObjectEntity>()));
        }

        /// <summary>
        /// A query matching nothing counts zero rather than throwing.
        /// </summary>
        [Fact]
        public void CountObjects_Returns_Zero_For_An_Empty_Result()
        {
            Seed(nameof(CountObjects_Returns_Zero_For_An_Empty_Result));

            var query = new Query<ObjectEntity>()
                .WhereEquals(x => x.Kind, ObjectKind.Blog);

            Assert.Equal(0, CoreHub.ObjectManager.CountObjects(query));
        }

        /// <summary>
        /// Paging on the query narrows the count to the page — which is exactly why the
        /// callers are told to leave it off. Pinned here so the contract the landing page
        /// relies on cannot change unnoticed.
        /// </summary>
        [Fact]
        public void CountObjects_Counts_The_Page_When_Paging_Is_Set()
        {
            Seed(nameof(CountObjects_Counts_The_Page_When_Paging_Is_Set));

            var paged = new Query<ObjectEntity>().WithPaging(0, 1);

            Assert.Equal(1, CoreHub.ObjectManager.CountObjects(paged));
        }

        /// <summary>
        /// The identity count applies its filter and matches the row count.
        /// </summary>
        [Fact]
        public void CountIdentities_Applies_The_Filter()
        {
            Seed(nameof(CountIdentities_Applies_The_Filter));

            IQuery<Identity> build() => new Query<Identity>()
                .Where(x => x.State == IdentityState.Active);

            Assert.Equal(1, CoreHub.IdentityManager.CountIdentities(build()));
            Assert.Equal(2, CoreHub.IdentityManager.CountIdentities(new Query<Identity>()));
        }

        /// <summary>
        /// The group count applies its filter and matches the row count.
        /// </summary>
        [Fact]
        public void CountGroups_Applies_The_Filter()
        {
            Seed(nameof(CountGroups_Applies_The_Filter));

            IQuery<Group> build() => new Query<Group>()
                .Where(x => x.State == GroupState.Active);

            Assert.Equal(2, CoreHub.GroupManager.CountGroups(build()));
            Assert.Equal(CoreHub.GroupManager.GetGroups(build()).Count(), CoreHub.GroupManager.CountGroups(build()));
        }

        /// <summary>
        /// The audit count answers a time-windowed query: events inside the window are
        /// counted, older ones are not.
        /// </summary>
        [Fact]
        public void CountEvents_Applies_The_Time_Window()
        {
            Seed(nameof(CountEvents_Applies_The_Time_Window));

            var now = DateTime.UtcNow;

            using (var db = CoreHubFixture.CreateDbContext(nameof(CountEvents_Applies_The_Time_Window)))
            {
                void add(long sequence, DateTime timestamp)
                    => db.AuditEvents.Add(new AuditEvent
                    {
                        Id = Guid.NewGuid(),
                        Sequence = sequence,
                        Timestamp = timestamp,
                        Category = AuditCategory.Content,
                        Action = AuditAction.Created,
                        Outcome = AuditOutcome.Succeeded,
                        Severity = AuditSeverity.Info,
                        // the chain hash is required; its value is irrelevant to a count
                        Hash = sequence.ToString("x64")
                    });

                add(1, now.AddDays(-1));
                add(2, now.AddDays(-2));
                add(3, now.AddDays(-30));

                db.SaveChanges();
            }

            var since = now.AddDays(-7);
            var recent = new Query<AuditEvent>().Where(x => x.Timestamp >= since);

            Assert.Equal(2, CoreHub.AuditManager.CountEvents(recent));
            Assert.Equal(3, CoreHub.AuditManager.CountEvents(new Query<AuditEvent>()));
        }
    }
}
