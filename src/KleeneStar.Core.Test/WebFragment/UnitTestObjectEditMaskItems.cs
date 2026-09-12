using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Model.Entities;
using System.Reflection;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.Test.WebFragment
{
    /// <summary>
    /// Tests what the structured edit mask is made of: the fields of the class's edit form,
    /// or the description where the class has none - and nothing about the classification,
    /// which is set in its own dialog and has no place among the answers of a record.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectEditMaskItems
    {
        private static readonly Guid WorkspaceId = Guid.Parse("C3C3C3C3-0000-4000-8000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("C3C3C3C3-0000-4000-8000-000000000002");
        private static readonly Guid LevelId = Guid.Parse("C3C3C3C3-0000-4000-8000-000000000003");
        private static readonly Guid ObjectId = Guid.Parse("C3C3C3C3-0000-4000-8000-000000000004");

        /// <summary>
        /// Seeds a class that classifies its objects with a level nobody is cleared for, and
        /// an unclassified object of it - the constellation the mask used to open with a
        /// warning, although an edit never touches the level.
        /// </summary>
        /// <param name="database">The name of the isolated in-memory database.</param>
        /// <returns>The object.</returns>
        private static Model.Entities.Object Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using var db = CoreHubFixture.CreateDbContext(database);

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-mask", Name = "Mask" });
            db.Classes.Add(new Model.Entities.Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.SecurityLevels.Add(new SecurityLevel
            {
                Id = LevelId,
                Name = "Closed",
                ClassId = ClassId,
                State = SecurityLevelState.Active,
                PermittedGroupIds = []
            });
            db.Objects.Add(new Model.Entities.Object
            {
                Id = ObjectId,
                Key = "MASK-1",
                Summary = "A ticket",
                ClassId = ClassId,
                WorkspaceId = WorkspaceId
            });
            db.SaveChanges();

            return db.Objects.Single(x => x.Id == ObjectId);
        }

        /// <summary>
        /// Builds the items the way the mask does for the given object.
        /// </summary>
        /// <param name="fragment">The mask.</param>
        /// <param name="object">The object, or null.</param>
        /// <returns>The items.</returns>
        private static List<IControlFormItem> Build(ObjectStructuredEditFormFragmentBase fragment, Model.Entities.Object @object)
        {
            var method = typeof(ObjectStructuredEditFormFragmentBase).GetMethod("BuildItems", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return Assert.IsAssignableFrom<IEnumerable<IControlFormItem>>(method!.Invoke(fragment, [@object])).ToList();
        }

        /// <summary>
        /// A class without an edit form leaves the mask with the description alone - no notice
        /// about the classification ahead of it, whoever is asking.
        /// </summary>
        [Fact]
        public void Mask_CarriesNoSecurityLevelNotice()
        {
            var @object = Seed(nameof(Mask_CarriesNoSecurityLevelNotice));
            var fragment = new ObjectEditFormFragment(null!);

            // the anonymous caller is the one cleared for none of the levels, which is what
            // used to put the warning on the mask
            using (CoreHub.SessionManager.BeginIdentity(Guid.Empty))
            {
                var items = Build(fragment, @object);

                Assert.Single(items);
                Assert.Same(fragment.Description, items.Single());
            }
        }

        /// <summary>
        /// The summary titles the mask and is not among its items; it carries the mark the
        /// dialog draws a title input by.
        /// </summary>
        [Fact]
        public void Mask_TitlesItselfByTheSummary()
        {
            var @object = Seed(nameof(Mask_TitlesItselfByTheSummary));
            var fragment = new ObjectEditFormFragment(null!);

            Assert.DoesNotContain(fragment.Summary, Build(fragment, @object));
            Assert.Contains(FormTitleInput.Mark, fragment.Summary.Classes);
            Assert.Null(fragment.Summary.Label);
        }

        /// <summary>
        /// An object that cannot be resolved yields the description as well rather than
        /// failing the render.
        /// </summary>
        [Fact]
        public void Mask_WithoutObject_FallsBackToTheDescription()
        {
            Seed(nameof(Mask_WithoutObject_FallsBackToTheDescription));
            var fragment = new ObjectEditFormFragment(null!);

            var items = Build(fragment, null);

            Assert.Single(items);
            Assert.Same(fragment.Description, items.Single());
        }
    }
}
