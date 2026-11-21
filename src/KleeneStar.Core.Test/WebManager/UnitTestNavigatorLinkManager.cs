using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.NavigatorLinkManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestNavigatorLinkManager
    {
        /// <summary>
        /// Initializes the in-memory database and CoreHub for a single test case.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);
        }

        /// <summary>
        /// Verifies that <c>Add</c> persists the link and that <c>GetNavigatorLink</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetNavigatorLink_RoundTrip()
        {
            Seed(nameof(Add_Then_GetNavigatorLink_RoundTrip));

            var link = Sample("Handbook", "https://example.com/handbook");
            CoreHub.NavigatorLinkManager.Add(link);

            var loaded = CoreHub.NavigatorLinkManager.GetNavigatorLink(link.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Handbook", loaded.Name);
            Assert.Equal("https://example.com/handbook", loaded.Uri);
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var link = Sample("Initial", "https://example.com/a");
            CoreHub.NavigatorLinkManager.Add(link);

            link.Name = "Renamed";
            link.Uri = "https://example.com/b";
            link.Ordinal = 7;
            CoreHub.NavigatorLinkManager.Update(link);

            var loaded = CoreHub.NavigatorLinkManager.GetNavigatorLink(link.Id);

            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
            Assert.Equal("https://example.com/b", loaded.Uri);
            Assert.Equal(7, loaded.Ordinal);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the link and raises the
        /// <see cref="KleeneStar.Core.WebManager.INavigatorLinkManager.NavigatorLinkRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var link = Sample("DeleteMe", "https://example.com/x");
            CoreHub.NavigatorLinkManager.Add(link);

            NavigatorLink raised = null;
            CoreHub.NavigatorLinkManager.NavigatorLinkRemoved += (_, l) => raised = l;

            CoreHub.NavigatorLinkManager.Remove(link.Id);

            Assert.Null(CoreHub.NavigatorLinkManager.GetNavigatorLink(link.Id));
            Assert.NotNull(raised);
            Assert.Equal(link.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that removing an unknown id is a no-op rather than an error, since the delete
        /// endpoint may be replayed for an entry another session already removed.
        /// </summary>
        [Fact]
        public void Remove_UnknownId_DoesNothing()
        {
            Seed(nameof(Remove_UnknownId_DoesNothing));

            var link = Sample("Keep", "https://example.com/keep");
            CoreHub.NavigatorLinkManager.Add(link);

            CoreHub.NavigatorLinkManager.Remove(Guid.NewGuid());

            Assert.NotNull(CoreHub.NavigatorLinkManager.GetNavigatorLink(link.Id));
        }

        /// <summary>
        /// Verifies that <c>GetNavigatorLinks(IQuery)</c> returns links from the database.
        /// </summary>
        [Fact]
        public void GetNavigatorLinks_ReturnsAllStored()
        {
            Seed(nameof(GetNavigatorLinks_ReturnsAllStored));

            CoreHub.NavigatorLinkManager.Add(Sample("Alpha", "https://example.com/alpha"));
            CoreHub.NavigatorLinkManager.Add(Sample("Beta", "https://example.com/beta"));

            var result = CoreHub.NavigatorLinkManager
                .GetNavigatorLinks(new Query<NavigatorLink>())
                .ToList();

            Assert.True(result.Count >= 2);
            Assert.Contains(result, l => l.Name == "Alpha");
            Assert.Contains(result, l => l.Name == "Beta");
        }

        /// <summary>
        /// Verifies that a hidden link is withheld from the app navigator, which is the whole point
        /// of the state.
        /// </summary>
        [Fact]
        public void GetVisibleNavigatorLinks_ExcludesHidden()
        {
            Seed(nameof(GetVisibleNavigatorLinks_ExcludesHidden));

            CoreHub.NavigatorLinkManager.Add(Sample("Shown", "https://example.com/shown"));

            var hidden = Sample("Concealed", "https://example.com/concealed");
            hidden.State = NavigatorLinkState.Hidden;
            CoreHub.NavigatorLinkManager.Add(hidden);

            var result = CoreHub.NavigatorLinkManager.GetVisibleNavigatorLinks().ToList();

            Assert.Contains(result, l => l.Name == "Shown");
            Assert.DoesNotContain(result, l => l.Name == "Concealed");
        }

        /// <summary>
        /// Verifies that the visible links come back ordered by their ordinal, so the settings page
        /// controls the order of the navigator entries.
        /// </summary>
        [Fact]
        public void GetVisibleNavigatorLinks_OrdersByOrdinal()
        {
            Seed(nameof(GetVisibleNavigatorLinks_OrdersByOrdinal));

            CoreHub.NavigatorLinkManager.Add(Sample("Third", "https://example.com/3", ordinal: 30));
            CoreHub.NavigatorLinkManager.Add(Sample("First", "https://example.com/1", ordinal: 10));
            CoreHub.NavigatorLinkManager.Add(Sample("Second", "https://example.com/2", ordinal: 20));

            var result = CoreHub.NavigatorLinkManager
                .GetVisibleNavigatorLinks()
                .Select(l => l.Name)
                .ToList();

            Assert.Equal(["First", "Second", "Third"], result);
        }

        /// <summary>
        /// Verifies that links sharing an ordinal fall back to their name, so the order stays stable
        /// instead of following an arbitrary storage order.
        /// </summary>
        [Fact]
        public void GetVisibleNavigatorLinks_BreaksOrdinalTieByName()
        {
            Seed(nameof(GetVisibleNavigatorLinks_BreaksOrdinalTieByName));

            CoreHub.NavigatorLinkManager.Add(Sample("Charlie", "https://example.com/c", ordinal: 5));
            CoreHub.NavigatorLinkManager.Add(Sample("Alpha", "https://example.com/a", ordinal: 5));
            CoreHub.NavigatorLinkManager.Add(Sample("Bravo", "https://example.com/b", ordinal: 5));

            var result = CoreHub.NavigatorLinkManager
                .GetVisibleNavigatorLinks()
                .Select(l => l.Name)
                .ToList();

            Assert.Equal(["Alpha", "Bravo", "Charlie"], result);
        }

        /// <summary>
        /// Verifies that an empty configuration yields an empty sequence rather than null, because
        /// the navigator fragment enumerates the result directly.
        /// </summary>
        [Fact]
        public void GetVisibleNavigatorLinks_ReturnsEmptyWhenNoneConfigured()
        {
            Seed(nameof(GetVisibleNavigatorLinks_ReturnsEmptyWhenNoneConfigured));

            var result = CoreHub.NavigatorLinkManager.GetVisibleNavigatorLinks();

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Verifies that adding the same link twice does not duplicate it, so a replayed create
        /// leaves a single entry.
        /// </summary>
        [Fact]
        public void Add_SameId_DoesNotDuplicate()
        {
            Seed(nameof(Add_SameId_DoesNotDuplicate));

            var link = Sample("Once", "https://example.com/once");
            CoreHub.NavigatorLinkManager.Add(link);
            CoreHub.NavigatorLinkManager.Add(link);

            var result = CoreHub.NavigatorLinkManager
                .GetNavigatorLinks(new Query<NavigatorLink>())
                .Count(l => l.Id == link.Id);

            Assert.Equal(1, result);
        }

        /// <summary>
        /// Verifies that <c>Add</c> rejects a missing link instead of writing an empty record.
        /// </summary>
        [Fact]
        public void Add_Null_Throws()
        {
            Seed(nameof(Add_Null_Throws));

            Assert.Throws<ArgumentNullException>(() => CoreHub.NavigatorLinkManager.Add(null));
        }

        /// <summary>
        /// Verifies that <c>Reorder</c> applies the given order, which is what a dragged row set
        /// arrives as.
        /// </summary>
        [Fact]
        public void Reorder_AppliesGivenOrder()
        {
            Seed(nameof(Reorder_AppliesGivenOrder));

            var a = Sample("Alpha", "https://example.com/a", ordinal: 0);
            var b = Sample("Bravo", "https://example.com/b", ordinal: 1);
            var c = Sample("Charlie", "https://example.com/c", ordinal: 2);
            CoreHub.NavigatorLinkManager.Add(a);
            CoreHub.NavigatorLinkManager.Add(b);
            CoreHub.NavigatorLinkManager.Add(c);

            CoreHub.NavigatorLinkManager.Reorder([c.Id, a.Id, b.Id]);

            Assert.Equal
            (
                ["Charlie", "Alpha", "Bravo"],
                CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name)
            );
        }

        /// <summary>
        /// Verifies that <c>Reorder</c> assigns dense ordinals, so repeated arrangements cannot let
        /// the stored values drift apart.
        /// </summary>
        [Fact]
        public void Reorder_AssignsDenseOrdinals()
        {
            Seed(nameof(Reorder_AssignsDenseOrdinals));

            var a = Sample("Alpha", "https://example.com/a", ordinal: 50);
            var b = Sample("Bravo", "https://example.com/b", ordinal: 90);
            CoreHub.NavigatorLinkManager.Add(a);
            CoreHub.NavigatorLinkManager.Add(b);

            CoreHub.NavigatorLinkManager.Reorder([b.Id, a.Id]);

            Assert.Equal
            (
                [0, 1],
                CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Ordinal)
            );
        }

        /// <summary>
        /// Verifies that links the caller did not mention keep their relative order behind the
        /// listed ones, because the table may be arranging only the page it currently shows.
        /// </summary>
        [Fact]
        public void Reorder_KeepsUnmentionedLinksBehind()
        {
            Seed(nameof(Reorder_KeepsUnmentionedLinksBehind));

            var a = Sample("Alpha", "https://example.com/a", ordinal: 0);
            var b = Sample("Bravo", "https://example.com/b", ordinal: 1);
            var c = Sample("Charlie", "https://example.com/c", ordinal: 2);
            CoreHub.NavigatorLinkManager.Add(a);
            CoreHub.NavigatorLinkManager.Add(b);
            CoreHub.NavigatorLinkManager.Add(c);

            CoreHub.NavigatorLinkManager.Reorder([b.Id]);

            Assert.Equal
            (
                ["Bravo", "Alpha", "Charlie"],
                CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name)
            );
        }

        /// <summary>
        /// Verifies that an unknown id in the order is ignored rather than throwing, since the
        /// client may still be showing a row another session removed.
        /// </summary>
        [Fact]
        public void Reorder_IgnoresUnknownId()
        {
            Seed(nameof(Reorder_IgnoresUnknownId));

            var a = Sample("Alpha", "https://example.com/a");
            CoreHub.NavigatorLinkManager.Add(a);

            CoreHub.NavigatorLinkManager.Reorder([Guid.NewGuid(), a.Id]);

            Assert.Equal(["Alpha"], CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name));
        }

        /// <summary>
        /// Verifies that <c>Move</c> swaps a link with the entry above it.
        /// </summary>
        [Fact]
        public void Move_Up_SwapsWithPredecessor()
        {
            Seed(nameof(Move_Up_SwapsWithPredecessor));

            var a = Sample("Alpha", "https://example.com/a", ordinal: 0);
            var b = Sample("Bravo", "https://example.com/b", ordinal: 1);
            CoreHub.NavigatorLinkManager.Add(a);
            CoreHub.NavigatorLinkManager.Add(b);

            CoreHub.NavigatorLinkManager.Move(b.Id, up: true);

            Assert.Equal
            (
                ["Bravo", "Alpha"],
                CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name)
            );
        }

        /// <summary>
        /// Verifies that <c>Move</c> swaps a link with the entry below it.
        /// </summary>
        [Fact]
        public void Move_Down_SwapsWithSuccessor()
        {
            Seed(nameof(Move_Down_SwapsWithSuccessor));

            var a = Sample("Alpha", "https://example.com/a", ordinal: 0);
            var b = Sample("Bravo", "https://example.com/b", ordinal: 1);
            CoreHub.NavigatorLinkManager.Add(a);
            CoreHub.NavigatorLinkManager.Add(b);

            CoreHub.NavigatorLinkManager.Move(a.Id, up: false);

            Assert.Equal
            (
                ["Bravo", "Alpha"],
                CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name)
            );
        }

        /// <summary>
        /// Verifies that moving beyond either end leaves the order untouched, so a repeated click
        /// cannot wrap an entry around to the other end.
        /// </summary>
        /// <param name="up">The direction to move.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Move_AtBoundary_DoesNothing(bool up)
        {
            Seed(nameof(Move_AtBoundary_DoesNothing) + up);

            var a = Sample("Alpha", "https://example.com/a", ordinal: 0);
            var b = Sample("Bravo", "https://example.com/b", ordinal: 1);
            CoreHub.NavigatorLinkManager.Add(a);
            CoreHub.NavigatorLinkManager.Add(b);

            CoreHub.NavigatorLinkManager.Move(up ? a.Id : b.Id, up);

            Assert.Equal
            (
                ["Alpha", "Bravo"],
                CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name)
            );
        }

        /// <summary>
        /// Verifies that moving an unknown link is a no-op.
        /// </summary>
        [Fact]
        public void Move_UnknownId_DoesNothing()
        {
            Seed(nameof(Move_UnknownId_DoesNothing));

            CoreHub.NavigatorLinkManager.Add(Sample("Alpha", "https://example.com/a"));

            CoreHub.NavigatorLinkManager.Move(Guid.NewGuid(), up: true);

            Assert.Equal(["Alpha"], CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name));
        }

        /// <summary>
        /// Verifies that a hidden link keeps its place in the arrangement, so making it visible
        /// again restores it where the operator had put it.
        /// </summary>
        [Fact]
        public void Reorder_IncludesHiddenLinks()
        {
            Seed(nameof(Reorder_IncludesHiddenLinks));

            var a = Sample("Alpha", "https://example.com/a", ordinal: 0);
            var hidden = Sample("Bravo", "https://example.com/b", ordinal: 1);
            hidden.State = NavigatorLinkState.Hidden;
            var c = Sample("Charlie", "https://example.com/c", ordinal: 2);
            CoreHub.NavigatorLinkManager.Add(a);
            CoreHub.NavigatorLinkManager.Add(hidden);
            CoreHub.NavigatorLinkManager.Add(c);

            CoreHub.NavigatorLinkManager.Move(c.Id, up: true);

            Assert.Equal
            (
                ["Alpha", "Charlie", "Bravo"],
                CoreHub.NavigatorLinkManager.GetOrderedNavigatorLinks().Select(l => l.Name)
            );
        }

        /// <summary>
        /// Creates a sample <see cref="NavigatorLink"/> with a fresh GUID.
        /// </summary>
        /// <param name="name">The link label.</param>
        /// <param name="uri">The link address.</param>
        /// <param name="ordinal">The sort order.</param>
        /// <returns>The sample link.</returns>
        private static NavigatorLink Sample(string name, string uri, int ordinal = 0) => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Uri = uri,
            Ordinal = ordinal,
            State = NavigatorLinkState.Active
        };
    }
}
