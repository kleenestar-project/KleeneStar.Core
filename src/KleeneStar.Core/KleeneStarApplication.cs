using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebTheme;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Model.Settings;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data.Common;
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

            try
            {
                using var db = ModelHub.CreateDbContext();

                // apply a migration path if necessary
                MigrateWithLegacyDbReset(db, componentHub);

                // run seeding
                KleeneStarDbSeeder.SeedAsync(db).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // surface the failure rather than letting WebExpress swallow it during
                // plugin instantiation (the plugin would then never appear in the sitemap).
                componentHub.LogManager.DefaultLog.Exception(ex);
                throw;
            }
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

        /// <summary>
        /// Applies pending migrations. When the database exists but its schema was
        /// previously created without a <c>__EFMigrationsHistory</c> table (for example
        /// by an older <c>EnsureCreated()</c> code path or by a developer manually
        /// editing the migration files), the first migration throws "table already
        /// exists". In that case the database is reset and the migration is retried —
        /// the seeder will then repopulate every row.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="componentHub">The component hub used to write a warning entry.</param>
        private static void MigrateWithLegacyDbReset(KleeneStarDbContext db, IComponentHub componentHub)
        {
            try
            {
                db.Database.Migrate();
            }
            catch (Exception ex) when (IsAlreadyExistsError(ex))
            {
                componentHub?.LogManager?.DefaultLog?.Warning
                (
                    "Legacy database schema without migrations history detected. " +
                    "Resetting the database and re-running migrations + seed."
                );

                db.Database.EnsureDeleted();
                db.Database.Migrate();
            }
        }

        /// <summary>
        /// Returns whether the supplied exception (or any of its inner exceptions)
        /// reports the "table already exists" condition that providers raise when
        /// the migration tries to (re-)create a table that is already present in
        /// the database.
        /// </summary>
        /// <param name="ex">The exception to inspect.</param>
        /// <returns><c>true</c> when the message chain contains "already exists".</returns>
        private static bool IsAlreadyExistsError(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is DbException &&
                    current.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
