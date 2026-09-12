using KleeneStar.Core.Test;
using KleeneStar.Model.Entities;
using System.Reflection;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.Test.WWW.Api.Workflows
{
    /// <summary>
    /// Tests the rule configuration of
    /// <see cref="KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor"/>: the catalogs the
    /// editor's pickers offer are the registered rules, and what the editor lists on a
    /// transition is stored as the expressions the workflow manager evaluates.
    /// </summary>
    [Collection("NonParallelTests")]
    public class UnitTestWorkflowEditorRules
    {
        private static readonly Guid WorkspaceId = Guid.Parse("E0E10000-0000-0000-0000-000000000001");
        private static readonly Guid ClassId = Guid.Parse("E0E10000-0000-0000-0000-000000000002");
        private static readonly Guid WorkflowId = Guid.Parse("E0E10000-0000-0000-0000-000000000003");
        private static readonly Guid CategoryId = Guid.Parse("E0E10000-0000-0000-0000-000000000004");
        private static readonly Guid OpenId = Guid.Parse("E0E10000-0000-0000-0000-000000000005");
        private static readonly Guid DoneId = Guid.Parse("E0E10000-0000-0000-0000-000000000006");
        private static readonly Guid TransitionId = Guid.Parse("E0E10000-0000-0000-0000-000000000007");

        /// <summary>
        /// Seeds a two-state workflow with one transition that carries the given rules.
        /// </summary>
        /// <param name="connectionString">The per-test in-memory database name.</param>
        /// <param name="guards">The stored guard expression.</param>
        /// <param name="validators">The stored validator expression.</param>
        /// <param name="postFunctions">The stored post function keys.</param>
        private static void Seed(string connectionString, string guards = null, string validators = null, List<string> postFunctions = null)
        {
            CoreHubFixture.Initialize(connectionString);

            using var db = CoreHubFixture.CreateDbContext(connectionString);

            if (db.Workflows.Any(x => x.Id == WorkflowId))
            {
                return;
            }

            db.Workspaces.Add(new Workspace { Id = WorkspaceId, Key = "ws-wr", Name = "main" });
            db.Classes.Add(new Class { Id = ClassId, Name = "Ticket", WorkspaceId = WorkspaceId });
            db.StatusCategories.Add(new StatusCategory { Id = CategoryId, Name = "Open", Color = "#abcdef", IsDefault = true });

            db.Statuses.AddRange
            (
                new Status { Id = OpenId, Name = "Open", ClassId = ClassId, CategoryId = CategoryId, State = StatusState.Active },
                new Status { Id = DoneId, Name = "Done", ClassId = ClassId, CategoryId = CategoryId, State = StatusState.Active }
            );

            db.Workflows.Add(new Workflow
            {
                Id = WorkflowId,
                Name = "Lifecycle",
                ClassId = ClassId,
                State = WorkflowState.Active,
                WorkflowStatuses =
                [
                    new WorkflowStatus { StatusId = OpenId, IsStart = true },
                    new WorkflowStatus { StatusId = DoneId, IsEnd = true }
                ]
            });

            db.Transitions.Add(new Transition(TransitionId)
            {
                Name = "close",
                WorkflowId = WorkflowId,
                SourceId = OpenId,
                TargetId = DoneId,
                State = TransitionState.Active,
                GuardExpression = guards,
                ValidatorExpression = validators,
                PostFunctionKeys = postFunctions ?? []
            });

            db.SaveChanges();
        }

        /// <summary>
        /// Invokes a protected member of the sealed endpoint.
        /// </summary>
        private static T Invoke<T>(string name, params object[] args)
        {
            var endpoint = new KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor();
            var method = typeof(KleeneStar.Core.WWW.Api._1_.Workflows.WorkflowEditor)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            return (T)method.Invoke(endpoint, args);
        }

        /// <summary>
        /// Reads the transitions as the editor loads them.
        /// </summary>
        private static List<RestApiWorkflowTransition> Transitions(string connectionString)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            return Invoke<IEnumerable<RestApiWorkflowTransition>>("RetrieveTransitions", WorkflowId.ToString(), db, null).ToList();
        }

        /// <summary>
        /// Saves the workflow with its one transition carrying the given rule lists, the way
        /// the editor posts them: the key in the entry's type, an id of the editor's own.
        /// </summary>
        private static void Save(string connectionString, string[] guards = null, string[] validators = null, string[] postFunctions = null)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            var workflow = new RestApiWorkflowResult
            {
                Id = WorkflowId.ToString(),
                States =
                [
                    new RestApiWorkflowState { Id = OpenId.ToString(), Label = "Open", IsStart = true },
                    new RestApiWorkflowState { Id = DoneId.ToString(), Label = "Done", IsEnd = true }
                ],
                Transitions =
                [
                    new RestApiWorkflowTransition
                    {
                        Id = TransitionId.ToString(),
                        From = OpenId.ToString(),
                        To = DoneId.ToString(),
                        Label = "close",
                        Guards = guards?.Select((key, i) => new RestApiWorkflowGuard { Id = "guards_" + i, Type = key, Label = "picked" }).ToList(),
                        Validators = validators?.Select((key, i) => new RestApiWorkflowValidator { Id = "validators_" + i, Type = key, Label = "picked" }).ToList(),
                        PostFunctions = postFunctions?.Select((key, i) => new RestApiWorkflowPostFunction { Id = "postfunctions_" + i, Type = key, Label = "picked" }).ToList()
                    }
                ]
            };

            Invoke<object>("Update", WorkflowId.ToString(), workflow, db, null);
        }

        /// <summary>
        /// Reads the stored transition back.
        /// </summary>
        private static Transition Stored(string connectionString)
        {
            using var db = CoreHubFixture.CreateDbContext(connectionString);

            return db.Transitions.Single(t => t.Id == TransitionId);
        }

        /// <summary>
        /// The validator catalog is the registry: every registered validator, addressed by its
        /// key in the one field the editor preserves, in the order the rules ask for.
        /// </summary>
        [Fact]
        public void RetrieveValidations_OffersTheRegisteredValidators()
        {
            Seed(nameof(RetrieveValidations_OffersTheRegisteredValidators));

            var catalog = Invoke<IEnumerable<RestApiWorkflowValidator>>("RetrieveValidations", WorkflowId.ToString(), null, null).ToList();

            Assert.NotEmpty(catalog);
            Assert.Equal(CoreHub.WorkflowValidatorManager.Rules.Count(), catalog.Count);
            Assert.All(catalog, x => Assert.Equal(x.Id, x.Type));
            Assert.Equal("summary.set", catalog[0].Type);
            Assert.Contains(catalog, x => x.Type == "assignee.set");

            // no request, no culture: the label is the key of the translation, never empty
            Assert.All(catalog, x => Assert.False(string.IsNullOrWhiteSpace(x.Label)));
        }

        /// <summary>
        /// The guard and post function catalogs come from their registries the same way.
        /// </summary>
        [Fact]
        public void RetrieveGuardsAndPostFunctions_OfferTheRegisteredRules()
        {
            Seed(nameof(RetrieveGuardsAndPostFunctions_OfferTheRegisteredRules));

            var guards = Invoke<IEnumerable<RestApiWorkflowGuard>>("RetrieveGuards", WorkflowId.ToString(), null, null).ToList();
            var postFunctions = Invoke<IEnumerable<RestApiWorkflowPostFunction>>("RetrievePostFunctions", WorkflowId.ToString(), null, null).ToList();

            Assert.Equal(CoreHub.WorkflowGuardManager.Rules.Count(), guards.Count);
            Assert.Equal(CoreHub.WorkflowPostFunctionManager.Rules.Count(), postFunctions.Count);
            Assert.All(guards, x => Assert.Equal(x.Id, x.Type));
            Assert.All(postFunctions, x => Assert.Equal(x.Id, x.Type));
        }

        /// <summary>
        /// The validators the editor lists on a transition are stored as one conjunction - a
        /// list of rules means all of them - and the workflow reads them from there.
        /// </summary>
        [Fact]
        public void Update_StoresTheListedValidatorsAsAConjunction()
        {
            Seed(nameof(Update_StoresTheListedValidatorsAsAConjunction));

            Save(nameof(Update_StoresTheListedValidatorsAsAConjunction), validators: ["summary.set", "assignee.set"]);

            var stored = Stored(nameof(Update_StoresTheListedValidatorsAsAConjunction));

            Assert.Equal("summary.set;assignee.set", stored.ValidatorExpression);
            Assert.Null(stored.GuardExpression);
            Assert.Empty(stored.PostFunctionKeys);

            var loaded = Assert.Single(Transitions(nameof(Update_StoresTheListedValidatorsAsAConjunction)));
            var keys = loaded.Validators.Select(x => x.Type).ToList();

            Assert.Equal(["summary.set", "assignee.set"], keys);
            Assert.All(loaded.Validators, x => Assert.Equal(x.Id, x.Type));
        }

        /// <summary>
        /// Guards and post functions travel the same way; the post functions keep their order,
        /// because they run in it.
        /// </summary>
        [Fact]
        public void Update_StoresGuardsAndOrderedPostFunctions()
        {
            Seed(nameof(Update_StoresGuardsAndOrderedPostFunctions));

            var guardKey = CoreHub.WorkflowGuardManager.Rules.First().Key;
            var postKeys = CoreHub.WorkflowPostFunctionManager.Rules.Select(x => x.Key).Take(2).Reverse().ToArray();

            Save(nameof(Update_StoresGuardsAndOrderedPostFunctions), guards: [guardKey], postFunctions: postKeys);

            var stored = Stored(nameof(Update_StoresGuardsAndOrderedPostFunctions));

            Assert.Equal(guardKey, stored.GuardExpression);
            Assert.Equal(postKeys, stored.PostFunctionKeys);

            var loaded = Assert.Single(Transitions(nameof(Update_StoresGuardsAndOrderedPostFunctions)));

            Assert.Equal(postKeys, loaded.PostFunctions.Select(x => x.Type).ToArray());
        }

        /// <summary>
        /// Duplicates and blanks are normalized rather than stored as written, an entry without
        /// a type names no rule however its id reads, and an empty list clears the rule.
        /// </summary>
        [Fact]
        public void Update_NormalizesTheRuleListAndClearsOnEmpty()
        {
            Seed(nameof(Update_NormalizesTheRuleListAndClearsOnEmpty), validators: "summary.set");

            using (var db = CoreHubFixture.CreateDbContext(nameof(Update_NormalizesTheRuleListAndClearsOnEmpty)))
            {
                var workflow = new RestApiWorkflowResult
                {
                    Id = WorkflowId.ToString(),
                    States =
                    [
                        new RestApiWorkflowState { Id = OpenId.ToString(), Label = "Open", IsStart = true },
                        new RestApiWorkflowState { Id = DoneId.ToString(), Label = "Done", IsEnd = true }
                    ],
                    Transitions =
                    [
                        new RestApiWorkflowTransition
                        {
                            Id = TransitionId.ToString(),
                            From = OpenId.ToString(),
                            To = DoneId.ToString(),
                            Label = "close",
                            Validators =
                            [
                                new RestApiWorkflowValidator { Id = "validators_1", Type = " assignee.set " },
                                new RestApiWorkflowValidator { Id = "validators_2", Type = "" },
                                new RestApiWorkflowValidator { Id = "description.set", Type = null },
                                new RestApiWorkflowValidator { Id = "validators_3", Type = "ASSIGNEE.SET" },
                                null
                            ],
                            Guards = []
                        }
                    ]
                };

                Invoke<object>("Update", WorkflowId.ToString(), workflow, db, null);
            }

            var stored = Stored(nameof(Update_NormalizesTheRuleListAndClearsOnEmpty));

            Assert.Equal("assignee.set", stored.ValidatorExpression);
            Assert.Null(stored.GuardExpression);

            Save(nameof(Update_NormalizesTheRuleListAndClearsOnEmpty), validators: []);

            Assert.Null(Stored(nameof(Update_NormalizesTheRuleListAndClearsOnEmpty)).ValidatorExpression);
        }

        /// <summary>
        /// An expression the editor cannot express - a disjunction written by other means - is
        /// shown as the rules it mentions and kept as written while that list is not edited,
        /// so opening the transition does not flatten it. Editing the list does.
        /// </summary>
        [Fact]
        public void Update_KeepsADisjunctionUntilTheListIsEdited()
        {
            Seed(nameof(Update_KeepsADisjunctionUntilTheListIsEdited), validators: "summary.set;assignee.set|description.set");

            var loaded = Assert.Single(Transitions(nameof(Update_KeepsADisjunctionUntilTheListIsEdited)));

            Assert.Equal(["summary.set", "assignee.set", "description.set"], loaded.Validators.Select(x => x.Type).ToList());

            // the editor sends the list back unchanged, in whatever order it holds it
            Save(nameof(Update_KeepsADisjunctionUntilTheListIsEdited), validators: ["description.set", "summary.set", "assignee.set"]);

            Assert.Equal("summary.set;assignee.set|description.set", Stored(nameof(Update_KeepsADisjunctionUntilTheListIsEdited)).ValidatorExpression);

            // one rule removed: the list is what the administrator now means
            Save(nameof(Update_KeepsADisjunctionUntilTheListIsEdited), validators: ["summary.set", "description.set"]);

            Assert.Equal("summary.set;description.set", Stored(nameof(Update_KeepsADisjunctionUntilTheListIsEdited)).ValidatorExpression);
        }

        /// <summary>
        /// A rule of an uninstalled plugin is still listed - under its key, because there is no
        /// label to translate - so the transition shows which rule it lost rather than hiding it.
        /// </summary>
        [Fact]
        public void RetrieveTransitions_ShowsAnUnregisteredRuleUnderItsKey()
        {
            Seed(nameof(RetrieveTransitions_ShowsAnUnregisteredRuleUnderItsKey), validators: "plugin.gone;summary.set");

            var loaded = Assert.Single(Transitions(nameof(RetrieveTransitions_ShowsAnUnregisteredRuleUnderItsKey)));
            var gone = loaded.Validators.Single(x => x.Type == "plugin.gone");

            Assert.Equal("plugin.gone", gone.Label);
        }

        /// <summary>
        /// A transition the editor creates carries its rules from the first save.
        /// </summary>
        [Fact]
        public void Update_StoresTheRulesOfANewTransition()
        {
            Seed(nameof(Update_StoresTheRulesOfANewTransition));

            using (var db = CoreHubFixture.CreateDbContext(nameof(Update_StoresTheRulesOfANewTransition)))
            {
                var workflow = new RestApiWorkflowResult
                {
                    Id = WorkflowId.ToString(),
                    States =
                    [
                        new RestApiWorkflowState { Id = OpenId.ToString(), Label = "Open", IsStart = true },
                        new RestApiWorkflowState { Id = DoneId.ToString(), Label = "Done", IsEnd = true }
                    ],
                    Transitions =
                    [
                        new RestApiWorkflowTransition
                        {
                            Id = "e_new",
                            From = DoneId.ToString(),
                            To = OpenId.ToString(),
                            Label = "reopen",
                            Validators = [new RestApiWorkflowValidator { Id = "validators_1", Type = "screen.note.given" }]
                        }
                    ]
                };

                Invoke<object>("Update", WorkflowId.ToString(), workflow, db, null);
            }

            using var verify = CoreHubFixture.CreateDbContext(nameof(Update_StoresTheRulesOfANewTransition));
            var reopen = verify.Transitions.Single(t => t.WorkflowId == WorkflowId && t.Name == "reopen");

            Assert.Equal("screen.note.given", reopen.ValidatorExpression);
        }
    }
}
