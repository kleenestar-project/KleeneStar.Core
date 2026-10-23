using KleeneStar.Core.WebAutomation;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebTheme;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Model.Settings;
using System;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core
{
    /// <summary>
    /// Represents a the KleeneStar application with a specific name, description,
    /// icon, and context path.
    /// </summary>
    [Name("kleenestar.core:app.name")]
    [Description("kleenestar.core:app.description")]
    [Icon("/assets/img/kleenestar.svg")]
    [Theme<LightTheme>]
    [ContextPath("/kleenestar")]
    public sealed class KleeneStarApplication : IApplication
    {
        /// <summary>
        /// The worker running the automation queue, started with the application.
        /// </summary>
        private AutomationWorker _automationWorker;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="applicationContext">The application context.</param>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        public KleeneStarApplication(IApplicationContext applicationContext, IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            CoreHub.HttpServerContext = httpServerContext;
            ModelHub.HttpServerContext = httpServerContext;
            CoreHub.ComponentHub = componentHub;
            ModelHub.ComponentHub = componentHub;
            CoreHub.ApplicationContext = applicationContext;
            ModelHub.ApplicationContext = applicationContext;

            // the provider registry moved out of the identity manager with the token-based
            // authentication: a provider is now owned by the plugin that brought it, and is
            // dropped again when that plugin goes
            CoreHub.ComponentHub.IdentityProviderManager.Register(new WebIdentity.IdentityProvider(), applicationContext);

            // the database settings are the plugin's own section of the settings directory
            // (settings/kleenestar.core.settings.json, overridable from webexpress.settings.json
            // or the environment); a section that is missing, or silent about a value, keeps
            // the built-in sqlite defaults
            ModelHub.DatabaseSettings = DatabaseSettings.From(applicationContext.PluginContext?.Settings);

            // the external sources an administrator configured (Authentication:OpenIdConnect in
            // the same section); a source that cannot be built is logged and skipped, and its
            // accounts cannot sign in until it is fixed
            WebIdentity.OpenIdConnectAuthenticationSource.RegisterConfigured(applicationContext.PluginContext?.Settings, applicationContext, componentHub);

            // a failure here is left to escape: WebExpress logs it, records the application in
            // IApplicationManager.FailedApplications and answers /health and /health/live with
            // 503 until a restart retries the migration and the seed
            using var db = ModelHub.CreateDbContext();

            // migrate and seed under a lock, since replicas sharing the database start at once
            DatabaseStartup.Run(db, componentHub?.ClusterManager, componentHub?.LogManager?.DefaultLog);
        }

        /// <summary>
        /// Called when the application starts working. The call is concurrent.
        /// </summary>
        /// <remarks>
        /// The identity the installation chose is pushed into the application context here, after
        /// the database is migrated and seeded. It cannot happen in the constructor: the
        /// application is not registered with the application manager until the constructor
        /// returns, so there would be no context to rebrand yet.
        /// </remarks>
        public void Run()
        {
            CoreHub.BrandingManager.Apply();

            PublishRelationTypes();

            RecordStartup();

            RecoverInterruptedCreations();

            ConnectBlueprints();

            StartAutomation();
        }

        /// <summary>
        /// Subscribes the automation platform to the events its triggers are raised from and
        /// starts the worker that runs its queue.
        /// </summary>
        /// <remarks>
        /// The queue is in the database, so the jobs an earlier run left - pending, or held by a
        /// worker that died with it - are picked up as soon as the worker starts.
        /// </remarks>
        private void StartAutomation()
        {
            try
            {
                CoreHub.AutomationManager.Connect();
                CoreHub.ApprovalManager.Connect();

                _automationWorker = new AutomationWorker();
            }
            catch (Exception ex)
            {
                CoreHub.HttpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Subscribes the blueprints to the structure managers and brings every follower in step.
        /// </summary>
        /// <remarks>
        /// A synchronization converges, so running it at start heals whatever a crash or a
        /// failed earlier run left behind. A failure must not keep the application from
        /// starting; it is logged and the next change of the blueprint tries again.
        /// </remarks>
        private static void ConnectBlueprints()
        {
            try
            {
                CoreHub.BlueprintManager.Connect();

                foreach (var blueprint in CoreHub.ClassManager.GetClasses(new WebExpress.WebIndex.Queries.Query<Class>()).Where(x => x.IsBlueprint))
                {
                    CoreHub.BlueprintManager.SynchronizeFollowers(blueprint.Id);
                }
            }
            catch (Exception ex)
            {
                CoreHub.HttpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Takes back the object creations a crash interrupted and forgets the completed ones a
        /// retry can no longer refer to.
        /// </summary>
        /// <remarks>
        /// A work structure is written step by step, so a host that died half-way through one
        /// left a fragment and a running record naming it. Nothing that ran it survives a
        /// restart, so the record is abandoned and its objects are removed before anybody can
        /// submit again (<see cref="ObjectCreationRun.RecoverAbandoned"/>). A failure here must
        /// not keep the application from starting; it is logged.
        /// </remarks>
        private void RecoverInterruptedCreations()
        {
            try
            {
                ObjectCreationRun.RecoverAbandoned();
                ModelHub.PruneObjectCreations(DateTime.UtcNow.AddDays(-7));
            }
            catch (Exception ex)
            {
                CoreHub.HttpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Called when the host shuts down. The application holds no resources of its own;
        /// the database contexts are created and disposed per use.
        /// </summary>
        public void Dispose()
        {
            _automationWorker?.Dispose();
        }

        /// <summary>
        /// Lays the administered relation catalog over the framework defaults, so every link
        /// surface, the add dialog and the validation read what this installation defined
        /// rather than what WebExpress ships.
        /// </summary>
        /// <remarks>
        /// This cannot happen in the constructor, for the same reason the audit subscription
        /// cannot: the manager is resolved through the component hub, which hands out no
        /// manager until the application is registered. The registry keeps the framework
        /// defaults until this runs, so a request arriving in between is answered with a
        /// smaller catalog rather than with none.
        /// </remarks>
        private static void PublishRelationTypes()
        {
            try
            {
                CoreHub.ObjectRelationTypeManager.Publish();
            }
            catch (Exception ex)
            {
                // an installation whose relation catalog cannot be read still starts; the
                // surfaces then offer the framework defaults, which is a smaller catalog
                // rather than a broken page
                CoreHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);
            }
        }

        /// <summary>
        /// Subscribes the audit log to the managers it records, and writes the first event of
        /// this run.
        /// </summary>
        /// <remarks>
        /// This cannot happen in the constructor. The audit manager is resolved through the
        /// component hub, which does not hand out managers until the application it belongs to
        /// is registered - and that only happens once the constructor returns. Recording the
        /// startup here also means the event is written after the migration and the seed, so an
        /// installation that failed to come up leaves no entry claiming it did.
        /// <para>
        /// The startup event is what turns a gap in the log into a readable fact. Without it a
        /// restart is indistinguishable from a quiet night, and the sequence numbers on either
        /// side of it say nothing about why nothing happened in between.
        /// </para>
        /// </remarks>
        private static void RecordStartup()
        {
            try
            {
                var audit = CoreHub.AuditManager;

                audit.Connect();

                using var activity = audit.BeginActivity(AuditOrigin.System, Guid.Empty, "kleenestar.host");

                audit.Record
                (
                    AuditCategory.Lifecycle,
                    AuditAction.Started,
                    AuditTarget.Installation,
                    [
                        AuditDelta.Added("provider", ModelHub.DatabaseSettings?.Provider, AuditValueKind.Text),
                        AuditDelta.Added("assembly", ModelHub.DatabaseSettings?.Assembly, AuditValueKind.Text),
                        AuditDelta.Added
                        (
                            "version",
                            typeof(KleeneStarApplication).Assembly.GetName().Version?.ToString(),
                            AuditValueKind.Text
                        )
                    ],
                    AuditOutcome.Succeeded,
                    AuditSeverity.Notice
                );
            }
            catch (Exception ex)
            {
                // an installation that cannot audit its own startup still has to start; the
                // missing entry is visible as a run with no Started event preceding its changes
                CoreHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);
            }
        }
    }
}
