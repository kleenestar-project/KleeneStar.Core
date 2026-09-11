using KleeneStar.Core.WebManager;
using KleeneStar.Model;
using KleeneStar.Model.Config;
using System;
using System.Reflection;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.Test
{
    /// <summary>
    /// Test fixture that wires <see cref="CoreHub"/> and <see cref="ModelHub"/> to an isolated
    /// in-memory database and pre-populates the private manager backing fields so that
    /// <see cref="CoreHub.ClassManager"/> and siblings resolve without touching any real
    /// <see cref="IComponentHub"/>.
    /// </summary>
    internal static class CoreHubFixture
    {
        private static readonly (string FieldName, Type ManagerType)[] _managers =
        [
            ("_workspaceManager", typeof(WorkspaceManager)),
            ("_workspaceTemplateManager", typeof(WorkspaceTemplateManager)),
            ("_classManager",     typeof(ClassManager)),
            ("_fieldManager",     typeof(FieldManager)),
            ("_securityLevelManager", typeof(SecurityLevelManager)),
            ("_formManager",      typeof(FormManager)),
            ("_priorityManager",  typeof(PriorityManager)),
            ("_workflowManager",  typeof(WorkflowManager)),
            ("_statusManager",    typeof(StatusManager)),
            ("_objectManager",    typeof(ObjectManager)),
            ("_dashboardManager", typeof(DashboardManager)),
            ("_kanbanBoardManager", typeof(KanbanBoardManager)),
            ("_kindDashboardManager", typeof(KindDashboardManager)),
            ("_tenantManager",    typeof(TenantManager)),
            ("_navigatorLinkManager", typeof(NavigatorLinkManager)),
            ("_maintenanceManager", typeof(MaintenanceManager)),
            ("_brandingManager", typeof(BrandingManager)),
            ("_customQuickfilterManager", typeof(CustomQuickfilterManager)),
            ("_permissionManager", typeof(PermissionManager)),
            ("_identityManager",  typeof(IdentityManager)),
            ("_groupManager",     typeof(GroupManager)),
            ("_slaManager",       typeof(SlaManager)),
            ("_calendarManager",  typeof(CalendarManager)),
            ("_commentManager",   typeof(CommentManager)),
            ("_attachmentManager",typeof(AttachmentManager)),
            ("_watcherManager",   typeof(WatcherManager)),
            ("_shareManager",     typeof(ShareManager)),
            ("_objectTagManager", typeof(ObjectTagManager)),
            ("_objectDraftManager", typeof(ObjectDraftManager)),
            ("_valueManager",     typeof(ValueManager)),
            ("_commitManager",    typeof(CommitManager)),
            ("_templateManager",  typeof(TemplateManager)),
            ("_objectViewManager",typeof(ObjectViewManager)),
            ("_objectRelationManager",typeof(ObjectRelationManager)),
            ("_objectImpactManager",  typeof(ObjectImpactManager)),
            ("_objectProgressManager",typeof(ObjectProgressManager)),
            ("_workflowGuardManager", typeof(WorkflowGuardManager)),
            ("_workflowValidatorManager",typeof(WorkflowValidatorManager)),
            ("_workflowPostFunctionManager",typeof(WorkflowPostFunctionManager)),
            ("_objectRelationTypeManager",typeof(ObjectRelationTypeManager)),
            ("_sessionManager",   typeof(SessionManager)),
            ("_wqlHistoryManager", typeof(WqlHistoryManager)),
            ("_notificationCenterManager", typeof(NotificationCenterManager)),
            ("_identitySessionManager", typeof(IdentitySessionManager)),
            ("_accessTokenManager", typeof(AccessTokenManager)),
            ("_savedSearchManager",typeof(SavedSearchManager)),
            ("_sprintManager",    typeof(SprintManager)),
            ("_auditManager",     typeof(AuditManager)),
        ];

        /// <summary>
        /// Points <see cref="ModelHub"/> at an isolated in-memory database and seeds
        /// <see cref="CoreHub"/>'s private manager backing fields with freshly constructed
        /// instances so that the cached <c>??=</c> accessor bypasses
        /// <see cref="IComponentHub.GetComponentManager{T}"/>.
        /// </summary>
        /// <param name="connectionString">
        /// A unique per-test connection string so parallel-phase state does not leak between
        /// cases in the same <c>[Collection("NonParallelTests")]</c> collection.
        /// </param>
        public static void Initialize(string connectionString)
        {
            ModelHub.DatabaseConfig = new DbConfig
            {
                Assembly = "KleeneStar.Core.Test",
                ConnectionString = connectionString
            };

            foreach (var (fieldName, managerType) in _managers)
            {
                var ctor = managerType.GetConstructor
                (
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    [typeof(IComponentHub), typeof(IHttpServerContext)],
                    null
                ) ?? throw new InvalidOperationException($"Private ctor not found on {managerType.FullName}.");

                var instance = ctor.Invoke([null, null]);

                var field = typeof(CoreHub).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)
                    ?? throw new InvalidOperationException($"Backing field {fieldName} not found on CoreHub.");

                field.SetValue(null, instance);
            }
        }

        /// <summary>
        /// Creates a DbContext against the same in-memory database that <see cref="ModelHub.CreateDbContext"/>
        /// is routed to, for use in the <c>arrange</c> phase when seeding entities.
        /// </summary>
        /// <param name="connectionString">The in-memory database name.</param>
        /// <returns>A configured KleeneStarDbContext instance.</returns>
        public static KleeneStarDbContext CreateDbContext(string connectionString)
        {
            return InMemoryDbContextFactory.Create(connectionString);
        }
    }
}
