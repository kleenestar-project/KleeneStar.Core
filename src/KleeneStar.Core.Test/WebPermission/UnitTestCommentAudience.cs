using KleeneStar.Core.WebPermission;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebPermission
{
    /// <summary>
    /// Tests who a comment reaches (<see cref="CommentAudience"/>): a public comment everyone
    /// who reads the object, an internal note the service team alone - an internal account
    /// that may read the object - and never a customer, the requester included.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestCommentAudience
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C0A70000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("C0A70000-0000-0000-0000-000000000002");
        private static readonly Guid ObjectId = Guid.Parse("C0A70000-0000-0000-0000-000000000003");
        private static readonly Guid TenantId = Guid.Parse("C0A70000-0000-0000-0000-000000000004");
        private static readonly Guid AgentId = Guid.Parse("C0A70000-0000-0000-0000-000000000005");
        private static readonly Guid CustomerId = Guid.Parse("C0A70000-0000-0000-0000-000000000006");
        private static readonly Guid LockedAgentId = Guid.Parse("C0A70000-0000-0000-0000-000000000007");

        /// <summary>
        /// Seeds an unadministered workspace with one object, raised by a customer, and three
        /// accounts: an agent, the customer and a locked agent.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        /// <returns>The object.</returns>
        private static ObjectEntity Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
                {
                    var tenant = new Tenant(TenantId) { Name = "Customer Ltd", Description = "customer" };

                    db.Tenants.Add(tenant);
                    db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "SD", Name = "Service desk", Tenants = [tenant] });
                    db.Classes.Add(new Class { Id = ClassId, Name = "Incident", WorkspaceId = WorkspaceId });
                    db.Identities.Add(new Identity { Id = AgentId, Name = "Agent", UserName = "agent", Email = "agent@example.com", PasswordHash = "x" });
                    db.Identities.Add(new Identity { Id = CustomerId, Name = "Customer", UserName = "customer", Email = "customer@example.com", PasswordHash = "x", TenantId = TenantId });
                    db.Identities.Add(new Identity { Id = LockedAgentId, Name = "Locked", UserName = "locked", Email = "locked@example.com", PasswordHash = "x", State = IdentityState.Locked });
                    db.Objects.Add(new ObjectEntity(ObjectId) { Key = "SD-1", Summary = "mail down", WorkspaceId = WorkspaceId, ClassId = ClassId, CreatorId = CustomerId });
                    db.SaveChanges();
                }
            }

            return CoreHub.ObjectManager.GetObject(ObjectId);
        }

        /// <summary>
        /// Builds a comment of the supplied audience on the seeded object.
        /// </summary>
        /// <param name="visibility">The audience.</param>
        /// <returns>The comment.</returns>
        private static Comment Note(CommentVisibility visibility)
        {
            return new Comment { ObjectId = ObjectId, AuthorId = AgentId, Content = "note", Visibility = visibility };
        }

        /// <summary>
        /// An internal note reaches the agent but neither the customer who raised the object
        /// nor an anonymous caller; a public comment reaches all of them.
        /// </summary>
        [Fact]
        public void AnInternalNoteReachesTheServiceTeamOnly()
        {
            var @object = Seed(nameof(AnInternalNoteReachesTheServiceTeamOnly));

            Assert.True(CommentAudience.SeesInternal(@object, AgentId));
            Assert.False(CommentAudience.SeesInternal(@object, CustomerId));
            Assert.False(CommentAudience.SeesInternal(@object, Guid.Empty));

            foreach (var reader in new[] { AgentId, CustomerId, Guid.Empty })
            {
                using (CoreHub.SessionManager.BeginIdentity(reader))
                {
                    Assert.True(CommentAudience.Reaches(Note(CommentVisibility.Public)));
                    Assert.Equal(reader == AgentId, CommentAudience.Reaches(Note(CommentVisibility.InternalTeam)));
                }
            }
        }

        /// <summary>
        /// A locked account is nobody's service team, whatever it used to be.
        /// </summary>
        [Fact]
        public void ALockedAgentSeesNoInternalNote()
        {
            var @object = Seed(nameof(ALockedAgentSeesNoInternalNote));

            Assert.False(CommentAudience.SeesInternal(@object, LockedAgentId));
        }

        /// <summary>
        /// Only the service team may write an internal note; a customer would not see what they
        /// wrote. The system acting on its own is not narrowed.
        /// </summary>
        [Fact]
        public void OnlyTheServiceTeamWritesInternalNotes()
        {
            var @object = Seed(nameof(OnlyTheServiceTeamWritesInternalNotes));

            Assert.True(CommentAudience.MayWriteInternal(@object));

            using (CoreHub.SessionManager.BeginIdentity(AgentId))
            {
                Assert.True(CommentAudience.MayWriteInternal(@object));
            }

            using (CoreHub.SessionManager.BeginIdentity(CustomerId))
            {
                Assert.False(CommentAudience.MayWriteInternal(@object));
            }
        }

        /// <summary>
        /// A count or a teaser says no more than the thread would show: the filter drops the
        /// internal notes for a customer and keeps them for the agent.
        /// </summary>
        [Fact]
        public void FilterCountsWhatTheThreadShows()
        {
            var @object = Seed(nameof(FilterCountsWhatTheThreadShows));
            var comments = new[] { Note(CommentVisibility.Public), Note(CommentVisibility.InternalTeam), Note(CommentVisibility.Public) };

            using (CoreHub.SessionManager.BeginIdentity(CustomerId))
            {
                Assert.Equal(2, CommentAudience.Filter(@object, comments).Count());
            }

            using (CoreHub.SessionManager.BeginIdentity(AgentId))
            {
                Assert.Equal(3, CommentAudience.Filter(@object, comments).Count());
            }
        }
    }
}
