using KleeneStar.Model.Entities;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.WorkspaceManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkspaceManager
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
        /// Verifies that <c>Add</c> persists the workspace and that <c>GetWorkspace</c>
        /// retrieves it by its business id.
        /// </summary>
        [Fact]
        public void Add_Then_GetWorkspace_RoundTrip()
        {
            Seed(nameof(Add_Then_GetWorkspace_RoundTrip));

            var workspace = Sample("alpha");
            CoreHub.WorkspaceManager.Add(workspace);

            var loaded = CoreHub.WorkspaceManager.GetWorkspace(workspace.Id);

            Assert.NotNull(loaded);
            Assert.Equal("alpha", loaded.Key);
        }

        /// <summary>
        /// Verifies that <c>GetWorkspaceByKey</c> resolves a workspace by its key
        /// in a case-insensitive way.
        /// </summary>
        [Fact]
        public void GetWorkspaceByKey_IsCaseInsensitive()
        {
            Seed(nameof(GetWorkspaceByKey_IsCaseInsensitive));

            CoreHub.WorkspaceManager.Add(Sample("alpha"));

            Assert.NotNull(CoreHub.WorkspaceManager.GetWorkspaceByKey("alpha"));
            Assert.NotNull(CoreHub.WorkspaceManager.GetWorkspaceByKey("ALPHA"));
            Assert.Null(CoreHub.WorkspaceManager.GetWorkspaceByKey("beta"));
        }

        /// <summary>
        /// Verifies that <c>Update</c> writes scalar property changes back to the database.
        /// </summary>
        [Fact]
        public void Update_ChangesScalars()
        {
            Seed(nameof(Update_ChangesScalars));

            var workspace = Sample("initial");
            CoreHub.WorkspaceManager.Add(workspace);

            workspace.Name = "Renamed";
            CoreHub.WorkspaceManager.Update(workspace);

            var loaded = CoreHub.WorkspaceManager.GetWorkspace(workspace.Id);
            Assert.NotNull(loaded);
            Assert.Equal("Renamed", loaded.Name);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> deletes the workspace and raises the
        /// <see cref="KleeneStar.Core.WebManager.IWorkspaceManager.WorkspaceRemoved"/> event.
        /// </summary>
        [Fact]
        public void Remove_DeletesAndRaisesEvent()
        {
            Seed(nameof(Remove_DeletesAndRaisesEvent));

            var workspace = Sample("delete-me");
            CoreHub.WorkspaceManager.Add(workspace);

            Workspace raised = null;
            CoreHub.WorkspaceManager.WorkspaceRemoved += (_, w) => raised = w;

            CoreHub.WorkspaceManager.Remove(workspace.Id);

            Assert.Null(CoreHub.WorkspaceManager.GetWorkspace(workspace.Id));
            Assert.NotNull(raised);
            Assert.Equal(workspace.Id, raised.Id);
        }

        /// <summary>
        /// Verifies that <c>Remove</c> is a no-op when the workspace id is unknown.
        /// </summary>
        [Fact]
        public void Remove_Unknown_IsNoOp()
        {
            Seed(nameof(Remove_Unknown_IsNoOp));

            CoreHub.WorkspaceManager.Remove(Guid.NewGuid());
        }

        /// <summary>
        /// Verifies that <c>ReservedWorkspaceKeys</c> blocks well-known URL segments
        /// that would otherwise collide with router endpoints.
        /// </summary>
        [Fact]
        public void ReservedWorkspaceKeys_BlocksRouterSegments()
        {
            Assert.Contains("default", KleeneStar.Core.WebManager.WorkspaceManager.ReservedWorkspaceKeys);
            Assert.Contains("admin", KleeneStar.Core.WebManager.WorkspaceManager.ReservedWorkspaceKeys);
            Assert.Contains("api", KleeneStar.Core.WebManager.WorkspaceManager.ReservedWorkspaceKeys);
            Assert.Contains("workspaces", KleeneStar.Core.WebManager.WorkspaceManager.ReservedWorkspaceKeys);
        }

        /// <summary>
        /// Verifies that <c>RecordVisit</c> creates a bookmark that surfaces in
        /// <c>GetRecentWorkspaces</c> and leaves the favorite flag unset.
        /// </summary>
        [Fact]
        public void RecordVisit_CreatesBookmark_AndSurfacesInRecent()
        {
            Seed(nameof(RecordVisit_CreatesBookmark_AndSurfacesInRecent));
            var ownerId = SeedOwner(nameof(RecordVisit_CreatesBookmark_AndSurfacesInRecent));

            var workspace = Sample("alpha");
            CoreHub.WorkspaceManager.Add(workspace);

            var bookmark = CoreHub.WorkspaceManager.RecordVisit(ownerId, workspace.Id);

            Assert.NotNull(bookmark);
            Assert.False(bookmark.Favorite);

            var recent = CoreHub.WorkspaceManager.GetRecentWorkspaces(ownerId, 10);
            Assert.Single(recent);
            Assert.Equal(workspace.Id, recent[0].Id);
            Assert.False(CoreHub.WorkspaceManager.IsFavorite(ownerId, workspace.Id));
        }

        /// <summary>
        /// Verifies that visiting the same workspace twice updates the single bookmark row
        /// in place rather than creating a duplicate (the composite unique index contract).
        /// </summary>
        [Fact]
        public void RecordVisit_Twice_DoesNotDuplicate()
        {
            Seed(nameof(RecordVisit_Twice_DoesNotDuplicate));
            var ownerId = SeedOwner(nameof(RecordVisit_Twice_DoesNotDuplicate));

            var workspace = Sample("alpha");
            CoreHub.WorkspaceManager.Add(workspace);

            CoreHub.WorkspaceManager.RecordVisit(ownerId, workspace.Id);
            CoreHub.WorkspaceManager.RecordVisit(ownerId, workspace.Id);

            using var db = CoreHubFixture.CreateDbContext(nameof(RecordVisit_Twice_DoesNotDuplicate));
            Assert.Equal(1, db.WorkspaceBookmarks.Count(x => x.OwnerId == ownerId && x.WorkspaceId == workspace.Id));
        }

        /// <summary>
        /// Verifies that <c>RecordVisit</c> with an unknown owner or workspace persists nothing
        /// and returns <c>null</c> (the foreign keys would otherwise reject the write).
        /// </summary>
        [Fact]
        public void RecordVisit_UnknownOwnerOrWorkspace_ReturnsNull()
        {
            Seed(nameof(RecordVisit_UnknownOwnerOrWorkspace_ReturnsNull));
            var ownerId = SeedOwner(nameof(RecordVisit_UnknownOwnerOrWorkspace_ReturnsNull));

            var workspace = Sample("alpha");
            CoreHub.WorkspaceManager.Add(workspace);

            Assert.Null(CoreHub.WorkspaceManager.RecordVisit(Guid.NewGuid(), workspace.Id));
            Assert.Null(CoreHub.WorkspaceManager.RecordVisit(ownerId, Guid.NewGuid()));
        }

        /// <summary>
        /// Verifies that <c>SetFavorite</c> toggles the favorite flag and that the workspace
        /// appears in (and disappears from) <c>GetFavoriteWorkspaces</c> accordingly.
        /// </summary>
        [Fact]
        public void SetFavorite_TogglesFlag_AndSurfacesInFavorites()
        {
            Seed(nameof(SetFavorite_TogglesFlag_AndSurfacesInFavorites));
            var ownerId = SeedOwner(nameof(SetFavorite_TogglesFlag_AndSurfacesInFavorites));

            var workspace = Sample("alpha");
            CoreHub.WorkspaceManager.Add(workspace);

            CoreHub.WorkspaceManager.SetFavorite(ownerId, workspace.Id, true);
            Assert.True(CoreHub.WorkspaceManager.IsFavorite(ownerId, workspace.Id));
            Assert.Contains(CoreHub.WorkspaceManager.GetFavoriteWorkspaces(ownerId), w => w.Id == workspace.Id);

            CoreHub.WorkspaceManager.SetFavorite(ownerId, workspace.Id, false);
            Assert.False(CoreHub.WorkspaceManager.IsFavorite(ownerId, workspace.Id));
            Assert.DoesNotContain(CoreHub.WorkspaceManager.GetFavoriteWorkspaces(ownerId), w => w.Id == workspace.Id);
        }

        /// <summary>
        /// Verifies that <c>GetRecentWorkspaces</c> orders by last-visited descending (newest
        /// first) and honours the count cap.
        /// </summary>
        [Fact]
        public void GetRecentWorkspaces_OrdersByLastVisitedDescending()
        {
            Seed(nameof(GetRecentWorkspaces_OrdersByLastVisitedDescending));
            var ownerId = SeedOwner(nameof(GetRecentWorkspaces_OrdersByLastVisitedDescending));

            var older = Sample("older");
            var middle = Sample("middle");
            var newest = Sample("newest");
            CoreHub.WorkspaceManager.Add(older);
            CoreHub.WorkspaceManager.Add(middle);
            CoreHub.WorkspaceManager.Add(newest);

            var now = DateTime.UtcNow;
            SeedBookmark(nameof(GetRecentWorkspaces_OrdersByLastVisitedDescending), ownerId, older.Id, false, now.AddHours(-10));
            SeedBookmark(nameof(GetRecentWorkspaces_OrdersByLastVisitedDescending), ownerId, middle.Id, false, now.AddHours(-5));
            SeedBookmark(nameof(GetRecentWorkspaces_OrdersByLastVisitedDescending), ownerId, newest.Id, false, now.AddHours(-1));

            var recent = CoreHub.WorkspaceManager.GetRecentWorkspaces(ownerId, 10);
            Assert.Equal([newest.Id, middle.Id, older.Id], recent.Select(w => w.Id).ToList());

            var capped = CoreHub.WorkspaceManager.GetRecentWorkspaces(ownerId, 2);
            Assert.Equal([newest.Id, middle.Id], capped.Select(w => w.Id).ToList());
        }

        /// <summary>
        /// Verifies that <c>GetFavoriteWorkspaces</c> returns only active favorites, ordered by
        /// name — archived favorites and non-favorites are excluded.
        /// </summary>
        [Fact]
        public void GetFavoriteWorkspaces_ReturnsOnlyActiveFavorites_OrderedByName()
        {
            Seed(nameof(GetFavoriteWorkspaces_ReturnsOnlyActiveFavorites_OrderedByName));
            var ownerId = SeedOwner(nameof(GetFavoriteWorkspaces_ReturnsOnlyActiveFavorites_OrderedByName));

            var beta = Sample("beta");
            var alpha = Sample("alpha");
            var visitedOnly = Sample("gamma");
            var archived = new Workspace { Id = Guid.NewGuid(), Key = "delta", Name = "delta", State = WorkspaceState.Archived };
            CoreHub.WorkspaceManager.Add(beta);
            CoreHub.WorkspaceManager.Add(alpha);
            CoreHub.WorkspaceManager.Add(visitedOnly);
            CoreHub.WorkspaceManager.Add(archived);

            var now = DateTime.UtcNow;
            SeedBookmark(nameof(GetFavoriteWorkspaces_ReturnsOnlyActiveFavorites_OrderedByName), ownerId, beta.Id, true, now);
            SeedBookmark(nameof(GetFavoriteWorkspaces_ReturnsOnlyActiveFavorites_OrderedByName), ownerId, alpha.Id, true, now);
            SeedBookmark(nameof(GetFavoriteWorkspaces_ReturnsOnlyActiveFavorites_OrderedByName), ownerId, visitedOnly.Id, false, now);
            SeedBookmark(nameof(GetFavoriteWorkspaces_ReturnsOnlyActiveFavorites_OrderedByName), ownerId, archived.Id, true, now);

            var favorites = CoreHub.WorkspaceManager.GetFavoriteWorkspaces(ownerId);

            // alphabetical, active favorites only (alpha, beta) — gamma (not favorite) and delta (archived) excluded
            Assert.Equal([alpha.Id, beta.Id], favorites.Select(w => w.Id).ToList());
        }

        /// <summary>
        /// Seeds an owning identity into the in-memory database and returns its id. The owner must
        /// exist so the bookmark foreign keys accept the write.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <returns>The id of the seeded identity.</returns>
        private static Guid SeedOwner(string connectionString)
        {
            var ownerId = Guid.NewGuid();

            using var db = CoreHubFixture.CreateDbContext(connectionString);
            db.Identities.Add(new Identity
            {
                Id = ownerId,
                Name = "Owner",
                Email = "owner@kleenestar.org",
                PasswordHash = "$test$"
            });
            db.SaveChanges();

            return ownerId;
        }

        /// <summary>
        /// Seeds a single workspace bookmark with explicit favorite and last-visited values so
        /// ordering tests do not depend on wall-clock resolution.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="ownerId">The owning identity id.</param>
        /// <param name="workspaceId">The bookmarked workspace id.</param>
        /// <param name="favorite">The favorite flag.</param>
        /// <param name="lastVisited">The last-visited timestamp.</param>
        private static void SeedBookmark(string connectionString, Guid ownerId, Guid workspaceId, bool favorite, DateTime lastVisited)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);
            db.WorkspaceBookmarks.Add(new WorkspaceBookmark
            {
                OwnerId = ownerId,
                WorkspaceId = workspaceId,
                Favorite = favorite,
                LastVisited = lastVisited,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        /// <summary>
        /// A workspace without a document class gets its home page in a class <c>Page</c>
        /// created for it, and the page is named as the workspace's home page.
        /// </summary>
        [Fact]
        public void EnsureHome_CreatesDedicatedPageAndClass()
        {
            Seed(nameof(EnsureHome_CreatesDedicatedPageAndClass));

            var workspace = Sample("home1");
            CoreHub.WorkspaceManager.Add(workspace);

            var home = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);

            Assert.NotNull(home);
            Assert.Equal(ObjectKind.Document, home.Kind);
            Assert.Null(home.ParentId);
            Assert.Equal(home.Id, CoreHub.WorkspaceManager.GetWorkspace(workspace.Id).HomeId);
            Assert.Equal(home.Id, CoreHub.WorkspaceManager.GetHome(workspace.Id)?.Id);
            Assert.True(CoreHub.WorkspaceManager.IsHome(home));
            Assert.True(CoreHub.WorkspaceManager.IsHome(workspace.Id, home.Id));

            var host = CoreHub.ClassManager.GetClass(home.ClassId);

            Assert.Equal("Page", host?.Name);
            Assert.Equal(ObjectKind.Document, host?.Kind);
        }

        /// <summary>
        /// Ensuring twice writes one page: the second call answers the page the first wrote.
        /// </summary>
        [Fact]
        public void EnsureHome_IsIdempotent()
        {
            Seed(nameof(EnsureHome_IsIdempotent));

            var workspace = Sample("home2");
            CoreHub.WorkspaceManager.Add(workspace);

            var first = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);
            var second = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);

            Assert.Equal(first.Id, second.Id);

            var documents = CoreHub.ObjectManager.GetObjects(new Query<Model.Entities.Object>()
                .WhereEquals(x => x.WorkspaceId, workspace.Id));

            Assert.Single(documents);
            Assert.Single(CoreHub.ClassManager.GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspace.Id)));
        }

        /// <summary>
        /// The page is written into the document class rendered as prose, not into the
        /// spreadsheet class, and no further class is created.
        /// </summary>
        [Fact]
        public void EnsureHome_UsesTheWorkspacesProseClass()
        {
            Seed(nameof(EnsureHome_UsesTheWorkspacesProseClass));

            var workspace = Sample("home3");
            CoreHub.WorkspaceManager.Add(workspace);

            var sheets = DocumentClass(workspace.Id, "Calculation", ObjectRenderer.Spreadsheet);
            var pages = DocumentClass(workspace.Id, "Knowledge");

            var home = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);

            Assert.Equal(pages.Id, home.ClassId);
            Assert.NotEqual(sheets.Id, home.ClassId);
            Assert.Equal(2, CoreHub.ClassManager.GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspace.Id)).Count());
        }

        /// <summary>
        /// There is no fallback: a workspace whose documents are all pages of its tree has no
        /// home page until one is created for it.
        /// </summary>
        [Fact]
        public void GetHome_WithoutHomePage_IsNull()
        {
            Seed(nameof(GetHome_WithoutHomePage_IsNull));

            var workspace = Sample("home4");
            CoreHub.WorkspaceManager.Add(workspace);

            Document(workspace.Id, "Alpha");
            Document(workspace.Id, "Beta");

            Assert.Null(CoreHub.WorkspaceManager.GetHome(workspace.Id));
        }

        /// <summary>
        /// A stored id that references a document of another workspace or an issue is no home
        /// page.
        /// </summary>
        [Fact]
        public void GetHome_IgnoresWhatIsNotItsDocument()
        {
            Seed(nameof(GetHome_IgnoresWhatIsNotItsDocument));

            var workspace = Sample("home5");
            var other = Sample("home6");
            CoreHub.WorkspaceManager.Add(workspace);
            CoreHub.WorkspaceManager.Add(other);

            var theirs = Document(other.Id, "Theirs");
            var issue = Document(workspace.Id, "An issue", kind: ObjectKind.Issue);

            PointHomeAt(workspace.Id, theirs.Id);
            Assert.Null(CoreHub.WorkspaceManager.GetHome(workspace.Id));

            PointHomeAt(workspace.Id, issue.Id);
            Assert.Null(CoreHub.WorkspaceManager.GetHome(workspace.Id));
            Assert.False(CoreHub.WorkspaceManager.IsHome(issue));
        }

        /// <summary>
        /// A referenced document with children is not adopted: it stays in the tree and a new
        /// home page is created.
        /// </summary>
        [Fact]
        public void EnsureHome_LeavesATreePageInTheTree()
        {
            Seed(nameof(EnsureHome_LeavesATreePageInTheTree));

            var workspace = Sample("home7");
            CoreHub.WorkspaceManager.Add(workspace);

            DocumentClass(workspace.Id, "Knowledge");

            var alpha = Document(workspace.Id, "Alpha");
            var child = Document(workspace.Id, "Aardvark", alpha.Id);

            PointHomeAt(workspace.Id, alpha.Id);

            var home = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);

            Assert.NotEqual(alpha.Id, home.Id);
            Assert.NotNull(CoreHub.ObjectManager.GetObject(alpha.Id));
            Assert.Equal(alpha.Id, CoreHub.ObjectManager.GetObject(child.Id)?.ParentId);
            Assert.False(CoreHub.WorkspaceManager.IsHome(alpha));
            Assert.True(CoreHub.WorkspaceManager.IsHome(home));
        }

        /// <summary>
        /// The home page stands outside the page tree: nothing is hung beneath it, and it is
        /// hung beneath nothing.
        /// </summary>
        [Fact]
        public void SetParent_KeepsTheHomePageOutOfTheTree()
        {
            Seed(nameof(SetParent_KeepsTheHomePageOutOfTheTree));

            var workspace = Sample("home8");
            CoreHub.WorkspaceManager.Add(workspace);

            DocumentClass(workspace.Id, "Knowledge");

            var home = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);
            var page = Document(workspace.Id, "Page");

            Assert.Throws<InvalidOperationException>(() => CoreHub.ObjectManager.SetParent(page.Id, home.Id));
            Assert.Throws<InvalidOperationException>(() => CoreHub.ObjectManager.SetParent(home.Id, page.Id));
        }

        /// <summary>
        /// Creating an object below the home page is refused, like a move below it. Without the
        /// check a child would make the home page a page of the tree, and the next start would
        /// replace it.
        /// </summary>
        [Fact]
        public void Add_BeneathTheHomePage_IsRefused()
        {
            Seed(nameof(Add_BeneathTheHomePage_IsRefused));

            var workspace = Sample("home9");
            CoreHub.WorkspaceManager.Add(workspace);

            DocumentClass(workspace.Id, "Knowledge");

            var home = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);

            Assert.Throws<InvalidOperationException>(() => Document(workspace.Id, "Child", home.Id));
            Assert.Empty(CoreHub.ObjectManager.GetObjects(new Query<Model.Entities.Object>()
                .Where(x => x.ParentId == (Guid?)home.Id)));
        }

        /// <summary>
        /// A parent written through an update rather than a move is held to the same rule, in
        /// both directions.
        /// </summary>
        [Fact]
        public void Update_PuttingTheHomePageIntoTheTree_IsRefused()
        {
            Seed(nameof(Update_PuttingTheHomePageIntoTheTree_IsRefused));

            var workspace = Sample("home10");
            CoreHub.WorkspaceManager.Add(workspace);

            DocumentClass(workspace.Id, "Knowledge");

            var home = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);
            var page = Document(workspace.Id, "Page");

            var child = CoreHub.ObjectManager.GetObject(page.Id);
            child.ParentId = home.Id;
            Assert.Throws<InvalidOperationException>(() => CoreHub.ObjectManager.Update(child));

            var moved = CoreHub.ObjectManager.GetObject(home.Id);
            moved.ParentId = page.Id;
            Assert.Throws<InvalidOperationException>(() => CoreHub.ObjectManager.Update(moved));

            Assert.Null(CoreHub.ObjectManager.GetObject(page.Id)?.ParentId);
            Assert.Null(CoreHub.ObjectManager.GetObject(home.Id)?.ParentId);
        }

        /// <summary>
        /// A referenced document with comments is not adopted, because the home page shows no
        /// comments. A new home page is created.
        /// </summary>
        [Fact]
        public void EnsureHome_LeavesACommentedPageAPage()
        {
            Seed(nameof(EnsureHome_LeavesACommentedPageAPage));

            var workspace = Sample("home11");
            CoreHub.WorkspaceManager.Add(workspace);

            DocumentClass(workspace.Id, "Knowledge");

            var discussed = Document(workspace.Id, "Discussed");

            CoreHub.CommentManager.Add(new Comment
            {
                Id = Guid.NewGuid(),
                ObjectId = discussed.Id,
                AuthorId = Guid.NewGuid(),
                Content = "A thought.",
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            });

            PointHomeAt(workspace.Id, discussed.Id);

            var home = CoreHub.WorkspaceManager.EnsureHome(workspace.Id);

            Assert.NotEqual(discussed.Id, home.Id);
            Assert.False(CoreHub.WorkspaceManager.IsHome(discussed));
            Assert.True(CoreHub.WorkspaceManager.IsHome(home));
        }

        /// <summary>
        /// A referenced document without parent, children and comments is adopted as the home
        /// page.
        /// </summary>
        [Fact]
        public void EnsureHome_TakesOverAnUnusedPage()
        {
            Seed(nameof(EnsureHome_TakesOverAnUnusedPage));

            var workspace = Sample("home12");
            CoreHub.WorkspaceManager.Add(workspace);

            DocumentClass(workspace.Id, "Knowledge");

            var welcome = Document(workspace.Id, "Welcome");

            PointHomeAt(workspace.Id, welcome.Id);

            Assert.Equal(welcome.Id, CoreHub.WorkspaceManager.EnsureHome(workspace.Id)?.Id);
        }

        /// <summary>
        /// Sets <see cref="Workspace.HomeId"/> directly.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="objectId">The document.</param>
        private static void PointHomeAt(Guid workspaceId, Guid objectId)
        {
            var workspace = CoreHub.WorkspaceManager.GetWorkspace(workspaceId);
            workspace.HomeId = objectId;
            CoreHub.WorkspaceManager.Update(workspace);
        }

        /// <summary>
        /// Creates a document class in a workspace.
        /// </summary>
        /// <param name="workspaceId">The owning workspace.</param>
        /// <param name="name">The class name.</param>
        /// <param name="renderer">The renderer, or null to follow the kind (prose).</param>
        /// <returns>The created class.</returns>
        private static Class DocumentClass(Guid workspaceId, string name, string renderer = null)
        {
            var @class = new Class
            {
                Id = Guid.NewGuid(),
                Name = name,
                Kind = ObjectKind.Document,
                Renderer = renderer,
                WorkspaceId = workspaceId,
                State = ClassState.Active,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.ClassManager.Add(@class);

            return @class;
        }

        /// <summary>
        /// Creates a document in a workspace.
        /// </summary>
        /// <param name="workspaceId">The owning workspace.</param>
        /// <param name="summary">The title, which is what the fallback orders by.</param>
        /// <param name="parentId">The parent page, or null for a root.</param>
        /// <param name="kind">The object kind, for the cases that are deliberately not a
        /// document.</param>
        /// <returns>The created object.</returns>
        private static Model.Entities.Object Document(Guid workspaceId, string summary, Guid? parentId = null, string kind = ObjectKind.Document)
        {
            var document = new Model.Entities.Object
            {
                Id = Guid.NewGuid(),
                Key = summary,
                Summary = summary,
                Kind = kind,
                State = WorkspaceState.Active,
                WorkspaceId = workspaceId,
                ParentId = parentId,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            CoreHub.ObjectManager.Add(document);

            return document;
        }

        /// <summary>
        /// Creates a sample <see cref="Workspace"/> with a fresh GUID and the supplied key.
        /// </summary>
        /// <param name="key">The workspace key.</param>
        /// <returns>The sample workspace.</returns>
        private static Workspace Sample(string key) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Name = key,
            State = WorkspaceState.Active
        };
    }
}
