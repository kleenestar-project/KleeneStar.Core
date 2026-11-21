using KleeneStar.Core.WebFragment.Landing;
using KleeneStar.Model.Entities;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Provides unit tests for <see cref="LandingLabel"/> — the reserved object labels the
    /// landing page reads to fill its pinned area and its help area.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestLandingLabel
    {
        private static readonly Guid WorkspaceId = Guid.Parse("A1B2C3D4-1111-4111-8111-111111111111");
        private static readonly Guid ClassId = Guid.Parse("A1B2C3D4-2222-4222-8222-222222222222");
        private static readonly Guid PinnedId = Guid.Parse("A1B2C3D4-3333-4333-8333-333333333333");
        private static readonly Guid HelpId = Guid.Parse("A1B2C3D4-4444-4444-8444-444444444444");
        private static readonly Guid ArchivedId = Guid.Parse("A1B2C3D4-5555-4555-8555-555555555555");
        private static readonly Guid PlainId = Guid.Parse("A1B2C3D4-6666-4666-8666-666666666666");

        /// <summary>
        /// Seeds four objects: one pinned, one labelled for help, one pinned but archived,
        /// and one carrying no label at all.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-ll", Name = "workspace" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Knowledge", WorkspaceId = WorkspaceId });

            void add(Guid id, string key, string summary, WorkspaceState state = WorkspaceState.Active)
                => db.Objects.Add(new ObjectEntity
                {
                    Id = id,
                    Key = key,
                    Summary = summary,
                    Kind = ObjectKind.Document,
                    State = state,
                    WorkspaceId = WorkspaceId,
                    ClassId = ClassId
                });

            // seeded out of alphabetical order so the summary ordering is actually exercised
            add(PinnedId, "LL-2", "Org chart");
            add(HelpId, "LL-1", "How to work with KleeneStar");
            add(ArchivedId, "LL-3", "Retired guideline", WorkspaceState.Archived);
            add(PlainId, "LL-4", "Ordinary page");

            db.SaveChanges();

            CoreHub.ObjectTagManager.Add(PinnedId, LandingLabel.Pinned, null);
            CoreHub.ObjectTagManager.Add(HelpId, LandingLabel.Help, null);
            CoreHub.ObjectTagManager.Add(ArchivedId, LandingLabel.Pinned, null);
        }

        /// <summary>
        /// A labelled, active object is resolved; an object without the label is not.
        /// </summary>
        [Fact]
        public void Resolve_Returns_Only_Labelled_Objects()
        {
            Seed(nameof(Resolve_Returns_Only_Labelled_Objects));

            var pinned = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.Pinned, 10);

            Assert.Single(pinned);
            Assert.Equal(PinnedId, pinned[0].Id);
        }

        /// <summary>
        /// The two label sets do not bleed into each other: the help label resolves the
        /// help page, not the pinned one.
        /// </summary>
        [Fact]
        public void Resolve_Separates_The_Labels()
        {
            Seed(nameof(Resolve_Separates_The_Labels));

            var help = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.Help, 10);

            Assert.Single(help);
            Assert.Equal(HelpId, help[0].Id);

            var faq = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.Faq, 10);

            Assert.Empty(faq);
        }

        /// <summary>
        /// Labels are matched case-insensitively, so a page labelled in a different casing
        /// still shows up.
        /// </summary>
        [Fact]
        public void Resolve_Matches_The_Label_Case_Insensitively()
        {
            Seed(nameof(Resolve_Matches_The_Label_Case_Insensitively));

            var pinned = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, "pinned", 10);

            Assert.Single(pinned);
            Assert.Equal(PinnedId, pinned[0].Id);
        }

        /// <summary>
        /// An archived object drops off the landing page even while it keeps its label, so
        /// retiring a page does not require remembering to strip the label first.
        /// </summary>
        [Fact]
        public void Resolve_Skips_Archived_Objects()
        {
            Seed(nameof(Resolve_Skips_Archived_Objects));

            var pinned = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.Pinned, 10);

            Assert.DoesNotContain(pinned, x => x.Id == ArchivedId);
        }

        /// <summary>
        /// The cap bounds the result: a section asking for one entry gets one.
        /// </summary>
        [Fact]
        public void Resolve_Honours_The_Cap()
        {
            Seed(nameof(Resolve_Honours_The_Cap));

            CoreHub.ObjectTagManager.Add(PlainId, LandingLabel.Pinned, null);

            var all = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.Pinned, 10);
            Assert.Equal(2, all.Count);

            var capped = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.Pinned, 1);
            Assert.Single(capped);
        }

        /// <summary>
        /// Nothing labelled means an empty list rather than a null reference, so a section
        /// can render its empty state without a guard of its own.
        /// </summary>
        [Fact]
        public void Resolve_Returns_Empty_When_Nothing_Is_Labelled()
        {
            Seed(nameof(Resolve_Returns_Empty_When_Nothing_Is_Labelled));

            var steps = LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.FirstSteps, 10);

            Assert.NotNull(steps);
            Assert.Empty(steps);
        }

        /// <summary>
        /// A missing manager, an empty label or a non-positive cap yield an empty list
        /// instead of throwing — a fragment rendering before the hub is wired must not take
        /// the page down with it.
        /// </summary>
        [Fact]
        public void Resolve_Is_Defensive_About_Its_Arguments()
        {
            Seed(nameof(Resolve_Is_Defensive_About_Its_Arguments));

            Assert.Empty(LandingLabel.Resolve(null, CoreHub.ObjectManager, LandingLabel.Pinned, 10));
            Assert.Empty(LandingLabel.Resolve(CoreHub.ObjectTagManager, null, LandingLabel.Pinned, 10));
            Assert.Empty(LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, "  ", 10));
            Assert.Empty(LandingLabel.Resolve(CoreHub.ObjectTagManager, CoreHub.ObjectManager, LandingLabel.Pinned, 0));
        }
    }
}
