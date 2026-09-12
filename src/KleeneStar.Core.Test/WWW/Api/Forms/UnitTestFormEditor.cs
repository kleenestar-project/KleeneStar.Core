using KleeneStar.Core.Test;
using KleeneStar.Model.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WWW.Api.Forms
{
    /// <summary>
    /// Tests the save of the visual form editor
    /// (<see cref="KleeneStar.Core.WWW.Api._1_.Forms.FormEditor"/>): the structure the editor
    /// sends is stored through the model, a field node is resolved by whatever identifies it,
    /// and what cannot be stored faithfully is refused rather than dropped.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestFormEditor
    {
        private static readonly Guid WorkspaceId = Guid.Parse("F0E10000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("F0E10000-0000-0000-0000-000000000002");
        private static readonly Guid FormId = Guid.Parse("F0E10000-0000-0000-0000-000000000003");
        private static readonly Guid TitleFieldId = Guid.Parse("F0E10000-0000-0000-0000-000000000004");
        private static readonly Guid PriorityFieldId = Guid.Parse("F0E10000-0000-0000-0000-000000000005");

        /// <summary>
        /// Seeds a class with two fields and an empty form on it.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (db.Forms.Any(x => x.Id == FormId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-fe", Name = "workspace" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.Forms.Add(new Form { Id = FormId, Name = "Standard", Description = "The standard form", FormType = FormType.Default, ClassId = ClassId });
            db.Fields.AddRange
            (
                new Field { Id = TitleFieldId, Name = "Title", FieldType = FieldType.Text, ClassId = ClassId, State = FieldState.Active },
                new Field { Id = PriorityFieldId, Name = "Priority", FieldType = FieldType.Priority, ClassId = ClassId, State = FieldState.Active }
            );
            db.SaveChanges();
        }

        /// <summary>
        /// Invokes the protected save of the endpoint.
        /// </summary>
        /// <param name="item">The structure as the editor would send it.</param>
        /// <returns>The structure the endpoint answers.</returns>
        private static RestApiFormEditorItem Update(RestApiFormEditorItem item)
        {
            return Invoke<RestApiFormEditorItem>("UpdateItem", [FormId.ToString(), item, null, null]);
        }

        /// <summary>
        /// Invokes the protected load of the endpoint.
        /// </summary>
        /// <returns>The structure the endpoint answers.</returns>
        private static RestApiFormEditorItem Retrieve()
        {
            return Invoke<RestApiFormEditorItem>("RetrieveItem", [FormId.ToString(), null, null]);
        }

        /// <summary>
        /// Invokes a protected member of the sealed endpoint, unwrapping the reflection envelope
        /// so a test can assert on the exception the endpoint throws.
        /// </summary>
        private static T Invoke<T>(string name, object[] args)
        {
            var endpoint = new KleeneStar.Core.WWW.Api._1_.Forms.FormEditor();
            var method = typeof(KleeneStar.Core.WWW.Api._1_.Forms.FormEditor)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            try
            {
                return (T)method.Invoke(endpoint, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Builds the payload of a field node the way the editor does for a node it created
        /// itself: a generated id, the label of the catalog entry it was picked from.
        /// </summary>
        private static RestApiFormEditorFieldItem PickedField(string label, string id = null)
        {
            return new RestApiFormEditorFieldItem { Id = id ?? "n" + Guid.NewGuid().ToString("N")[..8], Label = label, Type = "string" };
        }

        /// <summary>
        /// The structure the editor sends is stored: tabs in order, a group with its layout, a
        /// field picked from the catalog (identified by its label) and one whose id the client
        /// kept from the catalog (identified by the field id). The answer carries the stored
        /// version and the editor's layout ids.
        /// </summary>
        [Fact]
        public void UpdateItem_StoresTheStructureAndAnswersTheStoredVersion()
        {
            Seed(nameof(UpdateItem_StoresTheStructureAndAnswersTheStoredVersion));

            var saved = Update(new RestApiFormEditorItem
            {
                FormId = FormId.ToString(),
                FormName = "Standard",
                FormDescription = "  edited  ",
                Version = 0,
                Tabs =
                [
                    new RestApiFormEditorTabItem
                    {
                        Id = "t1",
                        Name = "Main",
                        Children =
                        [
                            new RestApiFormEditorGroupItem
                            {
                                Id = "g1",
                                Label = "Head",
                                Layout = "col-vertical",
                                Children = [PickedField("Title")]
                            },
                            PickedField("Priority", PriorityFieldId.ToString())
                        ]
                    },
                    new RestApiFormEditorTabItem { Id = "t2", Name = "Details", Children = [] }
                ]
            });

            Assert.Equal(1, saved.Version);
            Assert.Equal("edited", saved.FormDescription);

            var tabs = saved.Tabs.ToList();
            Assert.Equal(2, tabs.Count);
            Assert.Equal("Main", tabs[0].Name);
            Assert.Equal("Details", tabs[1].Name);

            var children = tabs[0].Children.ToList();
            var group = Assert.IsType<RestApiFormEditorGroupItem>(children[0]);
            Assert.Equal("col-vertical", group.Layout);
            var title = Assert.IsType<RestApiFormEditorFieldItem>(Assert.Single(group.Children));
            Assert.Equal("Title", title.Label);
            var priority = Assert.IsType<RestApiFormEditorFieldItem>(children[1]);
            Assert.Equal("Priority", priority.Label);

            // what the model holds is the reference, not the label
            var stored = CoreHub.FormManager.GetFormWithStructure(FormId);
            Assert.Equal(1, stored.Version);
            var storedGroup = Assert.IsType<FormGroupElement>(stored.Tabs[0].Elements[0]);
            Assert.Equal(FormGroupLayout.ColumnVertical, storedGroup.Layout);
            Assert.Equal(TitleFieldId, Assert.IsType<FormFieldRefElement>(Assert.Single(storedGroup.Children)).FieldId);
            Assert.Equal(PriorityFieldId, Assert.IsType<FormFieldRefElement>(stored.Tabs[0].Elements[1]).FieldId);
        }

        /// <summary>
        /// A node loaded from the stored structure carries the element id, and that id resolves
        /// it even when the editor changed its label - the label is presentation of the field's
        /// name, and the element says which field.
        /// </summary>
        [Fact]
        public void UpdateItem_ResolvesALoadedNodeByItsElement()
        {
            Seed(nameof(UpdateItem_ResolvesALoadedNodeByItsElement));

            Update(new RestApiFormEditorItem
            {
                FormName = "Standard",
                Version = 0,
                Tabs = [new RestApiFormEditorTabItem { Name = "Main", Children = [PickedField("Priority")] }]
            });

            var loaded = Retrieve();
            var node = Assert.IsType<RestApiFormEditorFieldItem>(Assert.Single(Assert.Single(loaded.Tabs).Children));
            Assert.True(Guid.TryParse(node.Id, out _));

            node.Label = "Urgency";

            var saved = Update(loaded);

            Assert.Equal(2, saved.Version);
            var stored = CoreHub.FormManager.GetFormWithStructure(FormId);
            Assert.Equal(PriorityFieldId, Assert.IsType<FormFieldRefElement>(Assert.Single(stored.Tabs[0].Elements)).FieldId);

            // the label the editor invented is not stored: the field's name comes back
            Assert.Equal("Priority", Assert.IsType<RestApiFormEditorFieldItem>(Assert.Single(saved.Tabs.First().Children)).Label);
        }

        /// <summary>
        /// A field node that names nothing the class defines - a node the editor built and then
        /// renamed - is refused with the label, and nothing is written.
        /// </summary>
        [Fact]
        public void UpdateItem_RefusesAFieldTheClassDoesNotDefine()
        {
            Seed(nameof(UpdateItem_RefusesAFieldTheClassDoesNotDefine));

            var ex = Assert.Throws<InvalidOperationException>(() => Update(new RestApiFormEditorItem
            {
                FormName = "Standard",
                Version = 0,
                Tabs = [new RestApiFormEditorTabItem { Name = "Main", Children = [PickedField("Title"), PickedField("Severity")] }]
            }));

            Assert.Contains("Severity", ex.Message);
            Assert.Contains("Ticket", ex.Message);

            var stored = CoreHub.FormManager.GetFormWithStructure(FormId);
            Assert.Equal(0, stored.Version);
            Assert.Empty(stored.Tabs);
        }

        /// <summary>
        /// The version the editor loaded is the one it may overwrite: a second editor that saved
        /// in between makes the first one's save stale, and the stale save is refused instead of
        /// silently undoing the other's work.
        /// </summary>
        [Fact]
        public void UpdateItem_RefusesAStaleVersion()
        {
            Seed(nameof(UpdateItem_RefusesAStaleVersion));

            Update(new RestApiFormEditorItem { FormName = "Standard", Version = 0, Tabs = [] });

            Assert.Throws<DbUpdateConcurrencyException>(() =>
                Update(new RestApiFormEditorItem { FormName = "Standard", Version = 0, Tabs = [] }));

            var saved = Update(new RestApiFormEditorItem { FormName = "Standard", Version = 1, Tabs = [] });
            Assert.Equal(2, saved.Version);
        }

        /// <summary>
        /// A save that carries no tabs clears the structure, a blank name does not rename the
        /// form, a blank tab name is given one, and an unknown layout falls back to vertical.
        /// </summary>
        [Fact]
        public void UpdateItem_NormalizesWhatTheEditorLeavesBlank()
        {
            Seed(nameof(UpdateItem_NormalizesWhatTheEditorLeavesBlank));

            var saved = Update(new RestApiFormEditorItem
            {
                FormName = "   ",
                Version = 0,
                Tabs =
                [
                    new RestApiFormEditorTabItem
                    {
                        Name = " ",
                        Children = [new RestApiFormEditorGroupItem { Label = "Box", Layout = "diagonal", Children = null }]
                    }
                ]
            });

            Assert.Equal("Standard", saved.FormName);
            var tab = Assert.Single(saved.Tabs);
            Assert.Equal("Tab 1", tab.Name);
            var group = Assert.IsType<RestApiFormEditorGroupItem>(Assert.Single(tab.Children));
            Assert.Equal("vertical", group.Layout);
            Assert.Empty(group.Children);

            var cleared = Update(new RestApiFormEditorItem { FormName = "Standard", Version = 1, Tabs = null });

            Assert.Equal(2, cleared.Version);
            Assert.Empty(cleared.Tabs);
            Assert.Empty(CoreHub.FormManager.GetFormWithStructure(FormId).Tabs);
        }

        /// <summary>
        /// The load answers the stored version, not a constant, so the first save of a fresh
        /// form carries the version the store expects.
        /// </summary>
        [Fact]
        public void RetrieveItem_AnswersTheStoredVersion()
        {
            Seed(nameof(RetrieveItem_AnswersTheStoredVersion));

            Assert.Equal(0, Retrieve().Version);

            Update(new RestApiFormEditorItem { FormName = "Standard", Version = 0, Tabs = [] });

            Assert.Equal(1, Retrieve().Version);
            Assert.Equal("Ticket", Retrieve().ClassName);
        }

        /// <summary>
        /// A save that names an unknown form is refused, not answered with an empty structure.
        /// </summary>
        [Fact]
        public void UpdateItem_RefusesAnUnknownForm()
        {
            Seed(nameof(UpdateItem_RefusesAnUnknownForm));

            Assert.Throws<InvalidOperationException>(() =>
                Invoke<RestApiFormEditorItem>("UpdateItem", [Guid.NewGuid().ToString(), new RestApiFormEditorItem { Tabs = [] }, null, null]));

            Assert.Throws<ArgumentException>(() =>
                Invoke<RestApiFormEditorItem>("UpdateItem", ["not-a-guid", new RestApiFormEditorItem { Tabs = [] }, null, null]));
        }
    }
}
