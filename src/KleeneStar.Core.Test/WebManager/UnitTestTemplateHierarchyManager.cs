using KleeneStar.Model.Entities;

namespace KleeneStar.Core.Test.WebManager
{
    /// <summary>
    /// Provides unit tests for <see cref="KleeneStar.Core.WebManager.TemplateHierarchyManager"/>.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestTemplateHierarchyManager
    {
        private static readonly Guid WorkspaceId = Guid.Parse("88AABBCC-DDEE-FF00-1122-334455667788");
        private static readonly Guid ClassId = Guid.Parse("99BBCCDD-EEFF-0011-2233-445566778899");

        /// <summary>
        /// Seeds the in-memory database with the workspace and class the templates attach to.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        private static void Seed(string connectionString)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (!db.Workspaces.Any(x => x.Id == WorkspaceId))
            {
                db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-hier", Name = "main" });
            }
            if (!db.Classes.Any(x => x.Id == ClassId))
            {
                db.Classes.Add(new Class { Id = ClassId, Name = "Task", WorkspaceId = WorkspaceId });
            }

            db.SaveChanges();
        }

        /// <summary>
        /// Adds a template of the seeded class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The template.</returns>
        private static Template Template(string name)
        {
            var template = new Template { Id = Guid.NewGuid(), Name = name, ClassId = ClassId, State = TemplateState.Active };

            CoreHub.TemplateManager.AddTemplate(template);

            return template;
        }

        /// <summary>
        /// Adds a hierarchy rooted in a template.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="root">The root template.</param>
        /// <returns>The hierarchy.</returns>
        private static TemplateHierarchy Hierarchy(string name, Template root)
        {
            var hierarchy = new TemplateHierarchy { Name = name, WorkspaceId = WorkspaceId, RootTemplateId = root.Id, State = TemplateState.Active };

            CoreHub.TemplateHierarchyManager.AddHierarchy(hierarchy);

            return hierarchy;
        }

        /// <summary>
        /// The nodes are written as a whole and read depth first: a node carried again keeps its
        /// row, a missing one is removed, the same template may stand twice, a parent must come
        /// earlier, and a predecessor must be a sibling.
        /// </summary>
        [Fact]
        public void SetNodes_WritesTheTreeAsAWhole()
        {
            Seed(nameof(SetNodes_WritesTheTreeAsAWhole));

            var root = Template("Root");
            var first = Template("First");
            var second = Template("Second");
            var hierarchy = Hierarchy("Structure", root);

            var a = new TemplateHierarchyNode { TemplateId = first.Id };
            var b = new TemplateHierarchyNode { TemplateId = second.Id, ParentNodeId = a.Id, AsChild = false };
            var c = new TemplateHierarchyNode { TemplateId = first.Id };
            var d = new TemplateHierarchyNode { TemplateId = second.Id, AssigneeId = Guid.Empty };

            // b is linked to the root and to c; a link without type, a target outside the list
            // and the node itself are dropped, and a link left without targets with them
            b.Links =
            [
                new() { Type = " blocks ", Inverse = true, Targets = ["root", c.Id.ToString(), Guid.NewGuid().ToString(), b.Id.ToString()] },
                new() { Type = "", Targets = [a.Id.ToString()] }
            ];
            d.Links = [new() { Type = "references", Targets = [Guid.NewGuid().ToString()] }];

            CoreHub.TemplateHierarchyManager.SetNodes(hierarchy.Id, [a, b, c, d]);

            var stored = CoreHub.TemplateHierarchyManager.GetNodes(hierarchy.Id);
            Assert.Equal([a.Id, b.Id, c.Id, d.Id], stored.Select(x => x.Id));
            Assert.Equal(a.Id, stored[1].ParentNodeId);
            Assert.False(stored[1].AsChild);

            var link = Assert.Single(stored[1].Links);
            Assert.Equal("blocks", link.Type);
            Assert.True(link.Inverse);
            Assert.Equal(["root", c.Id.ToString()], link.Targets);
            Assert.Empty(stored[3].Links);
            Assert.Null(stored[3].AssigneeId);

            // a parent that comes later moves the node below the root
            var e = new TemplateHierarchyNode { TemplateId = first.Id, ParentNodeId = d.Id };
            CoreHub.TemplateHierarchyManager.SetNodes(hierarchy.Id, [e, d]);

            stored = CoreHub.TemplateHierarchyManager.GetNodes(hierarchy.Id);
            Assert.Equal([e.Id, d.Id], stored.Select(x => x.Id));
            Assert.Null(stored[0].ParentNodeId);
        }

        /// <summary>
        /// A hierarchy is found through its root and through its nodes; removing it removes its
        /// nodes and leaves the templates.
        /// </summary>
        [Fact]
        public void RemoveHierarchy_RemovesTheNodesAndKeepsTheTemplates()
        {
            Seed(nameof(RemoveHierarchy_RemovesTheNodesAndKeepsTheTemplates));

            var root = Template("Root");
            var node = Template("Node");
            var unused = Template("Unused");
            var hierarchy = Hierarchy("Structure", root);
            CoreHub.TemplateHierarchyManager.SetNodes(hierarchy.Id, [new TemplateHierarchyNode { TemplateId = node.Id }]);

            Assert.Equal([hierarchy.Id], CoreHub.TemplateHierarchyManager.GetHierarchyIdsUsing(root.Id));
            Assert.Equal([hierarchy.Id], CoreHub.TemplateHierarchyManager.GetHierarchyIdsUsing(node.Id));
            Assert.Empty(CoreHub.TemplateHierarchyManager.GetHierarchyIdsUsing(unused.Id));

            CoreHub.TemplateHierarchyManager.RemoveHierarchy(hierarchy);

            Assert.Null(CoreHub.TemplateHierarchyManager.GetHierarchy(hierarchy.Id));
            Assert.Empty(CoreHub.TemplateHierarchyManager.GetNodes(hierarchy.Id));
            Assert.NotNull(CoreHub.TemplateManager.GetTemplate(node.Id));
        }

        /// <summary>
        /// The class of a node: the chosen class while the template serves it, else the class the
        /// template is bound to.
        /// </summary>
        [Fact]
        public void ResolveClassId_KeepsAChoiceOnlyWhileItIsServed()
        {
            Seed(nameof(ResolveClassId_KeepsAChoiceOnlyWhileItIsServed));

            Guid further;
            Guid foreign;

            using (var db = CoreHubFixture.CreateDbContext(nameof(ResolveClassId_KeepsAChoiceOnlyWhileItIsServed)))
            {
                further = Guid.NewGuid();
                foreign = Guid.NewGuid();
                db.Classes.Add(new Class { Id = further, Name = "Problem", WorkspaceId = WorkspaceId });
                db.Classes.Add(new Class { Id = foreign, Name = "Change", WorkspaceId = WorkspaceId });
                db.SaveChanges();
            }

            var template = Template("Served twice");
            CoreHub.TemplateManager.SetFurtherClasses(template.Id, [further]);

            Assert.Equal(further, CoreHub.TemplateHierarchyManager.ResolveClassId(further, template));
            Assert.Equal(ClassId, CoreHub.TemplateHierarchyManager.ResolveClassId(foreign, template));
            Assert.Equal(ClassId, CoreHub.TemplateHierarchyManager.ResolveClassId(null, template));
        }
    }
}
