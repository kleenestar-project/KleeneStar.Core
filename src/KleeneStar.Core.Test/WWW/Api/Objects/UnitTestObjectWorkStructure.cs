using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System.Reflection;
using WebExpress.WebApp.WebRelation;
using WebExpress.WebUI.WebIcon;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using TemplateEntity = KleeneStar.Model.Entities.Template;

namespace KleeneStar.Core.Test.WWW.Api.Objects
{
    /// <summary>
    /// Tests the work structures a composite template creates - across workspaces, with
    /// assignees and dependencies - and the creation run that makes them idempotent and atomic.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestObjectWorkStructure
    {
        private static readonly Guid HrWorkspaceId = Guid.Parse("57A0C000-0000-4000-8000-000000000001");
        private static readonly Guid ItWorkspaceId = Guid.Parse("57A0C000-0000-4000-8000-000000000002");
        private static readonly Guid OrderClassId = Guid.Parse("57A0C000-0000-4000-8000-000000000003");
        private static readonly Guid TaskClassId = Guid.Parse("57A0C000-0000-4000-8000-000000000004");
        private static readonly Guid OnboardingTemplateId = Guid.Parse("57A0C000-0000-4000-8000-000000000005");
        private static readonly Guid AccountTemplateId = Guid.Parse("57A0C000-0000-4000-8000-000000000006");
        private static readonly Guid DeskTemplateId = Guid.Parse("57A0C000-0000-4000-8000-000000000007");
        private static readonly Guid AgentId = Guid.Parse("57A0C000-0000-4000-8000-000000000008");
        private static readonly Guid AccountEntryId = Guid.Parse("57A0C000-0000-4000-8000-000000000009");
        private static readonly Guid DeskEntryId = Guid.Parse("57A0C000-0000-4000-8000-00000000000A");
        private static readonly Guid HierarchyId = Guid.Parse("57A0C000-0000-4000-8000-00000000000B");

        /// <summary>
        /// Seeds an HR and an IT workspace, an order class in HR and a task class in IT, and an
        /// onboarding hierarchy whose account request is filed at the IT desk while the desk
        /// set-up stays in HR and waits for the account.
        /// </summary>
        /// <param name="database">The per-test in-memory database name.</param>
        private static void Seed(string database)
        {
            CoreHubFixture.Initialize(database);

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                if (!db.Workspaces.Any(x => x.Id == HrWorkspaceId))
                {
                    db.Workspaces.Add(new Workspace { Id = HrWorkspaceId, Key = "HR", Name = "HR" });
                    db.Workspaces.Add(new Workspace { Id = ItWorkspaceId, Key = "IT", Name = "IT" });
                    db.Classes.Add(new Class { Id = OrderClassId, Name = "Order", WorkspaceId = HrWorkspaceId });
                    db.Classes.Add(new Class { Id = TaskClassId, Name = "Task", WorkspaceId = ItWorkspaceId });
                    db.Identities.Add(new Identity { Id = AgentId, Name = "Agent", UserName = "agent", Email = "agent@example.com", PasswordHash = "x" });

                    db.Templates.Add(new TemplateEntity(OnboardingTemplateId) { Name = "Onboarding", ClassId = OrderClassId, State = TemplateState.Active, Icon = ImageIcon.FromString("/kleenestar/assets/icons/task.svg") });
                    db.Templates.Add(new TemplateEntity(AccountTemplateId) { Name = "Create account", ClassId = TaskClassId, State = TemplateState.Active, Icon = ImageIcon.FromString("/kleenestar/assets/icons/task.svg") });
                    db.Templates.Add(new TemplateEntity(DeskTemplateId) { Name = "Set up desk", ClassId = OrderClassId, State = TemplateState.Active, Icon = ImageIcon.FromString("/kleenestar/assets/icons/task.svg") });

                    // the onboarding hierarchy references both templates; the desk set-up waits for the account
                    db.TemplateHierarchies.Add(new TemplateHierarchy(HierarchyId) { Name = "Onboarding", WorkspaceId = HrWorkspaceId, RootTemplateId = OnboardingTemplateId, State = TemplateState.Active, Created = DateTime.UtcNow, Updated = DateTime.UtcNow });
                    db.TemplateHierarchyNodes.Add(new TemplateHierarchyNode { Id = AccountEntryId, HierarchyId = HierarchyId, TemplateId = AccountTemplateId, AssigneeId = AgentId, Order = 1 });
                    db.TemplateHierarchyNodes.Add(new TemplateHierarchyNode
                    {
                        Id = DeskEntryId,
                        HierarchyId = HierarchyId,
                        TemplateId = DeskTemplateId,
                        Order = 2,
                        Links = [new TemplateHierarchyNodeLink { Type = "blocks", Inverse = true, Targets = [AccountEntryId.ToString()] }]
                    });

                    db.SaveChanges();
                }
            }

            void publish(string key, RelationEffect effect, int order) => CoreHub.ObjectRelationTypeManager.Store(new ObjectRelationType
            {
                Id = Guid.NewGuid(),
                Key = key,
                Label = key,
                InverseLabel = "inverse " + key,
                System = RelationSystem.Object,
                Cardinality = RelationCardinality.ManyToMany,
                Effect = effect,
                Active = true,
                Order = order
            });

            publish("parent", RelationEffect.AggregatesProgress, 1);
            publish("blocks", RelationEffect.BlocksCompletion, 2);
            publish("references", RelationEffect.None, 3);
        }

        /// <summary>
        /// Creates the order the structure hangs on.
        /// </summary>
        /// <returns>The order.</returns>
        private static ObjectEntity Order()
        {
            var order = new ObjectEntity(Guid.NewGuid()) { Key = "HR-" + Random.Shared.Next(1000, 9999), Summary = "Onboarding Jane", WorkspaceId = HrWorkspaceId, ClassId = OrderClassId };

            CoreHub.ObjectManager.Add(order);

            return order;
        }

        /// <summary>
        /// Instantiates the nodes of the onboarding hierarchy below an order.
        /// </summary>
        /// <param name="order">The order.</param>
        /// <param name="run">The run the objects are tracked by.</param>
        private static void CreateChildren(ObjectEntity order, ObjectCreationRun run)
        {
            var method = typeof(global::KleeneStar.Core.WWW.Api._1_.Objects.Index)
                .GetMethod("CreateStructure", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method!.Invoke(null, [order, CoreHub.TemplateHierarchyManager.GetNodes(HierarchyId), null, run]);
        }

        /// <summary>
        /// Reads the objects created from a template, unrestricted.
        /// </summary>
        /// <param name="summary">The summary they carry.</param>
        /// <returns>The objects.</returns>
        private static ObjectEntity Single(string summary)
        {
            return Assert.Single(CoreHub.ObjectManager.GetObjects(new WebExpress.WebIndex.Queries.Query<ObjectEntity>()), x => x.Summary == summary);
        }

        /// <summary>
        /// A child whose class lives in another workspace is filed there, joined to the order by
        /// the aggregating relation rather than placed below it, and assigned as the template
        /// says; a same-workspace child is placed below the order; the dependency between the
        /// two becomes a blocking relation.
        /// </summary>
        [Fact]
        public void CreateChildren_BuildsTheStructureAcrossWorkspaces()
        {
            Seed(nameof(CreateChildren_BuildsTheStructureAcrossWorkspaces));

            var order = Order();
            var run = ObjectCreationRun.Claim(null, OnboardingTemplateId, Guid.Empty, out _);

            CreateChildren(order, run);

            var account = Single("Create account");
            var desk = Single("Set up desk");

            Assert.Equal(ItWorkspaceId, account.WorkspaceId);
            Assert.StartsWith("IT-", account.Key);
            Assert.Null(account.ParentId);
            Assert.Equal(AgentId, account.AssigneeId);

            Assert.Equal(HrWorkspaceId, desk.WorkspaceId);
            Assert.Equal(order.Id, desk.ParentId);

            var relations = CoreHub.ObjectRelationManager.GetRelations(order.Id).ToList();
            Assert.Contains(relations, x => x.TypeKey == "parent" && x.TargetObjectId == account.Id);
            Assert.Contains(relations, x => x.TypeKey == "parent" && x.TargetObjectId == desk.Id);
            Assert.Contains(CoreHub.ObjectRelationManager.GetRelations(desk.Id), x => x.TypeKey == "blocks" && x.SourceObjectId == account.Id && x.TargetObjectId == desk.Id);

            Assert.Equal([account.Id, desk.Id], run.Objects.OrderBy(x => x == desk.Id).ToList());

            // the order now reports the progress of the whole structure
            Assert.True(CoreHub.ObjectProgressManager.GetProgress(order.Id).Aggregated);
        }

        /// <summary>
        /// The nodes reference templates: one template may stand in a hierarchy twice and both
        /// nodes create an object; a template serving several classes creates its object in the
        /// class the node chose; an archived template creates nothing, nor do the nodes below it;
        /// a nested node is created below the object of its parent node.
        /// </summary>
        [Fact]
        public void CreateChildren_FollowsTheReferences()
        {
            var database = nameof(CreateChildren_FollowsTheReferences);
            Seed(database);

            var trainingId = Guid.NewGuid();
            var retiredId = Guid.NewGuid();

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                // the training serves the order class and, further, the task class of IT
                db.Templates.Add(new TemplateEntity(trainingId) { Name = "Training", ClassId = OrderClassId, State = TemplateState.Active, Icon = ImageIcon.FromString("/kleenestar/assets/icons/task.svg") });
                db.Templates.Add(new TemplateEntity(retiredId) { Name = "Retired step", ClassId = OrderClassId, State = TemplateState.Archived, Icon = ImageIcon.FromString("/kleenestar/assets/icons/task.svg") });
                db.TemplateClasses.Add(new TemplateClass { TemplateId = trainingId, ClassId = TaskClassId });
                var secondDesk = Guid.NewGuid();
                var retired = Guid.NewGuid();
                db.TemplateHierarchyNodes.Add(new TemplateHierarchyNode { HierarchyId = HierarchyId, TemplateId = trainingId, ClassId = TaskClassId, Order = 3 });
                db.TemplateHierarchyNodes.Add(new TemplateHierarchyNode { Id = secondDesk, HierarchyId = HierarchyId, TemplateId = DeskTemplateId, Order = 4 });
                db.TemplateHierarchyNodes.Add(new TemplateHierarchyNode { HierarchyId = HierarchyId, ParentNodeId = secondDesk, TemplateId = AccountTemplateId, Order = 5 });
                db.TemplateHierarchyNodes.Add(new TemplateHierarchyNode { Id = retired, HierarchyId = HierarchyId, TemplateId = retiredId, Order = 6 });
                db.TemplateHierarchyNodes.Add(new TemplateHierarchyNode { HierarchyId = HierarchyId, ParentNodeId = retired, TemplateId = DeskTemplateId, Order = 7 });
                db.SaveChanges();
            }

            var order = Order();
            var run = ObjectCreationRun.Claim(null, OnboardingTemplateId, Guid.Empty, out _);

            CreateChildren(order, run);

            var objects = CoreHub.ObjectManager.GetObjects(new WebExpress.WebIndex.Queries.Query<ObjectEntity>()).ToList();
            var training = Assert.Single(objects, x => x.Summary == "Training");

            Assert.Equal(TaskClassId, training.ClassId);
            Assert.Equal(ItWorkspaceId, training.WorkspaceId);
            Assert.Equal(2, objects.Count(x => x.Summary == "Set up desk"));
            Assert.DoesNotContain(objects, x => x.Summary == "Retired step");

            // the second desk set-up carries an account request of its own, related below it
            var accounts = objects.Where(x => x.Summary == "Create account").Select(x => x.Id).ToList();
            var desks = objects.Where(x => x.Summary == "Set up desk").Select(x => x.Id).ToList();
            Assert.Equal(2, accounts.Count);
            Assert.Contains(desks, desk => CoreHub.ObjectRelationManager.GetRelations(desk).Any(r => r.TypeKey == "parent" && r.SourceObjectId == desk && r.TargetObjectId is Guid target && accounts.Contains(target)));
        }

        /// <summary>
        /// A node that is a link only is created where its class lives, without a place below the
        /// parent and without the aggregating relation, and is related as the node says; a node
        /// that is both a sub-object and a link carries both relations.
        /// </summary>
        [Fact]
        public void CreateChildren_LinksAsTheNodeSays()
        {
            var database = nameof(CreateChildren_LinksAsTheNodeSays);
            Seed(database);

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                foreach (var node in db.TemplateHierarchyNodes.Where(x => x.HierarchyId == HierarchyId))
                {
                    // the account references the root object; the desk is referenced by it and
                    // still waits for the account
                    if (node.Id == AccountEntryId)
                    {
                        node.AsChild = false;
                        node.Links = [new TemplateHierarchyNodeLink { Type = "references", Targets = [TemplateHierarchyNodeLink.RootTarget] }];
                    }
                    else
                    {
                        node.Links = [.. node.Links, new TemplateHierarchyNodeLink { Type = "references", Inverse = true, Targets = [TemplateHierarchyNodeLink.RootTarget] }];
                    }
                }

                db.SaveChanges();
            }

            var order = Order();
            var run = ObjectCreationRun.Claim(null, OnboardingTemplateId, Guid.Empty, out _);

            CreateChildren(order, run);

            var account = Single("Create account");
            var desk = Single("Set up desk");
            var relations = CoreHub.ObjectRelationManager.GetRelations(order.Id).ToList();

            Assert.Null(account.ParentId);
            Assert.DoesNotContain(relations, x => x.TypeKey == "parent" && x.TargetObjectId == account.Id);
            Assert.Contains(relations, x => x.TypeKey == "references" && x.SourceObjectId == account.Id && x.TargetObjectId == order.Id);

            Assert.Equal(order.Id, desk.ParentId);
            Assert.Contains(relations, x => x.TypeKey == "parent" && x.TargetObjectId == desk.Id);
            Assert.Contains(relations, x => x.TypeKey == "references" && x.SourceObjectId == order.Id && x.TargetObjectId == desk.Id);
        }

        /// <summary>
        /// A failed creation takes back what it wrote - objects and relations - and its key may
        /// run again.
        /// </summary>
        [Fact]
        public void RollBack_TakesBackWhatTheRunWrote()
        {
            Seed(nameof(RollBack_TakesBackWhatTheRunWrote));

            var order = Order();
            var run = ObjectCreationRun.Claim("rollback-key", OnboardingTemplateId, Guid.Empty, out _);

            run.TrackObject(order.Id);
            CreateChildren(order, run);

            run.RollBack(new InvalidOperationException("the database went away"));

            Assert.Null(CoreHub.ObjectManager.GetObject(order.Id));
            Assert.DoesNotContain(CoreHub.ObjectManager.GetObjects(new WebExpress.WebIndex.Queries.Query<ObjectEntity>()), x => x.Summary is "Create account" or "Set up desk");
            Assert.Empty(CoreHub.ObjectRelationManager.GetRelations(order.Id));

            Assert.NotNull(ObjectCreationRun.Claim("rollback-key", OnboardingTemplateId, Guid.Empty, out _));
        }

        /// <summary>
        /// A second submission under a completed key is not run again: the claim answers with
        /// the record of the first, naming the object it created. One under a running key is
        /// not run beside it.
        /// </summary>
        [Fact]
        public void Claim_HeldKey_AnswersFromTheFirstSubmission()
        {
            Seed(nameof(Claim_HeldKey_AnswersFromTheFirstSubmission));

            var running = ObjectCreationRun.Claim("double-click", null, Guid.Empty, out _);

            Assert.NotNull(running);
            Assert.Null(ObjectCreationRun.Claim("double-click", null, Guid.Empty, out var whileRunning));
            Assert.Equal(ObjectCreationState.Running, whileRunning.State);

            var order = Order();
            running.TrackObject(order.Id);
            running.Complete(order.Id);

            Assert.Null(ObjectCreationRun.Claim("double-click", null, Guid.Empty, out var completed));
            Assert.Equal(ObjectCreationState.Completed, completed.State);
            Assert.Equal(order.Id, completed.RootObjectId);
        }

        /// <summary>
        /// A creation a crash interrupted is taken back on the next start: its record names what
        /// it wrote.
        /// </summary>
        [Fact]
        public void RecoverAbandoned_TakesBackAnInterruptedCreation()
        {
            var database = nameof(RecoverAbandoned_TakesBackAnInterruptedCreation);
            Seed(database);

            var order = Order();

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                db.ObjectCreations.Add(new ObjectCreation
                {
                    Id = Guid.NewGuid(),
                    Key = "crashed",
                    State = ObjectCreationState.Running,
                    ObjectIds = order.Id.ToString(),
                    Created = DateTime.UtcNow - ObjectCreationRun.AbandonedAfter - TimeSpan.FromMinutes(1)
                });
                db.SaveChanges();
            }

            Assert.Equal(1, ObjectCreationRun.RecoverAbandoned());
            Assert.Null(CoreHub.ObjectManager.GetObject(order.Id));

            using (var db = CoreHubFixture.CreateDbContext(database))
            {
                Assert.Equal(ObjectCreationState.RolledBack, db.ObjectCreations.Single(x => x.Key == "crashed").State);
            }
        }
    }
}
