using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebExpress.WebCore;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebApplication;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebEndpoint;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebNotification;

namespace KleeneStar.Core
{
    /// <summary>
    /// Provides utility methods for working with the KleeneStar.
    /// </summary>
    public static class CoreHub
    {
        private static WorkspaceManager _workspaceManager;
        private static WorkspaceTemplateManager _workspaceTemplateManager;
        private static ClassManager _classManager;
        private static FieldManager _fieldManager;
        private static SecurityLevelManager _securityLevelManager;
        private static FormManager _formManager;
        private static PriorityManager _priorityManager;
        private static WorkflowManager _workflowManager;
        private static StatusManager _statusManager;
        private static ObjectManager _objectManager;
        private static DashboardManager _dashboardManager;
        private static KanbanBoardManager _kanbanBoardManager;
        private static KindDashboardManager _kindDashboardManager;
        private static TenantManager _tenantManager;
        private static NavigatorLinkManager _navigatorLinkManager;
        private static MaintenanceManager _maintenanceManager;
        private static BrandingManager _brandingManager;
        private static CustomQuickfilterManager _customQuickfilterManager;
        private static PermissionManager _permissionManager;
        private static IdentityManager _identityManager;
        private static GroupManager _groupManager;
        private static TemplateManager _templateManager;
        private static ObjectViewManager _objectViewManager;
        private static SlaManager _slaManager;
        private static CalendarManager _calendarManager;
        private static CommentManager _commentManager;
        private static AttachmentManager _attachmentManager;
        private static WatcherManager _watcherManager;
        private static ShareManager _shareManager;
        private static ObjectTagManager _objectTagManager;
        private static ObjectDraftManager _objectDraftManager;
        private static ValueManager _valueManager;
        private static CommitManager _commitManager;
        private static ObjectRelationManager _objectRelationManager;
        private static ObjectRelationTypeManager _objectRelationTypeManager;
        private static ObjectImpactManager _objectImpactManager;
        private static ObjectProgressManager _objectProgressManager;
        private static WorkflowGuardManager _workflowGuardManager;
        private static WorkflowValidatorManager _workflowValidatorManager;
        private static WorkflowPostFunctionManager _workflowPostFunctionManager;
        private static SessionManager _sessionManager;

        private static WqlHistoryManager _wqlHistoryManager;
        private static NotificationCenterManager _notificationCenterManager;
        private static IdentitySessionManager _identitySessionManager;
        private static AccessTokenManager _accessTokenManager;
        private static SavedSearchManager _savedSearchManager;
        private static SprintManager _sprintManager;
        private static AuditManager _auditManager;

        /// <summary>
        /// Gets the shared instance of the component hub used for managing and coordinating application components.
        /// </summary>
        public static IComponentHub ComponentHub { get; internal set; }

        /// <summary>
        /// Gets the current application context, which provides access to application-wide services and configurations.
        /// </summary>
        public static IApplicationContext ApplicationContext { get; internal set; }

        /// <summary>
        /// Gets the current HTTP server context for the application.
        /// </summary>
        public static IHttpServerContext HttpServerContext { get; internal set; }

        /// <summary>
        /// Gets the workspace manager responsible for managing workspaces within the application.
        /// </summary>
        public static IWorkspaceManager WorkspaceManager => _workspaceManager ??= ComponentHub.GetComponentManager<WorkspaceManager>();

        /// <summary>
        /// Gets the class manager responsible for managing classes within the workspace.
        /// </summary>
        public static IClassManager ClassManager => _classManager ??= ComponentHub.GetComponentManager<ClassManager>();

        /// <summary>
        /// Gets the field manager responsible for managing fields within the class.
        /// </summary>
        public static IFieldManager FieldManager => _fieldManager ??= ComponentHub.GetComponentManager<FieldManager>();

        /// <summary>
        /// Gets the security level manager responsible for the classifications of the classes
        /// and for deciding who is cleared to see an object carrying one.
        /// </summary>
        public static ISecurityLevelManager SecurityLevelManager => _securityLevelManager ??= ComponentHub.GetComponentManager<SecurityLevelManager>();

        /// <summary>
        /// Gets the form manager responsible for managing forms within the class.
        /// </summary>
        public static IFormManager FormManager => _formManager ??= ComponentHub.GetComponentManager<FormManager>();

        /// <summary>
        /// Gets the priority manager responsible for managing priorities within the class.
        /// </summary>
        public static IPriorityManager PriorityManager => _priorityManager ??= ComponentHub.GetComponentManager<PriorityManager>();

        /// <summary>
        /// Gets the workflow manager responsible for managing workflows within the class.
        /// </summary>
        public static IWorkflowManager WorkflowManager => _workflowManager ??= ComponentHub.GetComponentManager<WorkflowManager>();

        /// <summary>
        /// Gets the workflow state manager responsible for managing workflow states within the class.
        /// </summary>
        public static IStatusManager StatusManager => _statusManager ??= ComponentHub.GetComponentManager<StatusManager>();

        /// <summary>
        /// Gets the object manager responsible for managing objects within the workspace.
        /// </summary>
        public static IObjectManager ObjectManager => _objectManager ??= ComponentHub.GetComponentManager<ObjectManager>();

        /// <summary>
        /// Gets the dashboard manager responsible for managing dashboards within the application.
        /// </summary>
        public static IDashboardManager DashboardManager => _dashboardManager ??= ComponentHub.GetComponentManager<DashboardManager>();

        /// <summary>
        /// Gets the Kanban board manager responsible for the persisted board layout (columns,
        /// swimlanes, board filter) of the workspace object Kanban boards.
        /// </summary>
        public static IKanbanBoardManager KanbanBoardManager => _kanbanBoardManager ??= ComponentHub.GetComponentManager<KanbanBoardManager>();

        /// <summary>
        /// Gets the object-kind dashboard manager responsible for the persisted KPI board
        /// layout (columns, widgets) of the workspace object Dashboard tabs.
        /// </summary>
        public static IKindDashboardManager KindDashboardManager => _kindDashboardManager ??= ComponentHub.GetComponentManager<KindDashboardManager>();

        /// <summary>
        /// Gets the tenant manager used to manage tenant-related operations within the application.
        /// </summary>
        public static ITenantManager TenantManager => _tenantManager ??= ComponentHub.GetComponentManager<TenantManager>();

        /// <summary>
        /// Gets the navigator link manager used to manage the additional links shown in the app navigator.
        /// </summary>
        public static INavigatorLinkManager NavigatorLinkManager => _navigatorLinkManager ??= ComponentHub.GetComponentManager<NavigatorLinkManager>();

        /// <summary>
        /// Gets the maintenance manager used to manage the maintenance notice of the installation.
        /// </summary>
        public static IMaintenanceManager MaintenanceManager => _maintenanceManager ??= ComponentHub.GetComponentManager<MaintenanceManager>();

        /// <summary>
        /// Gets the manager of the installation identity: the title and the icon the application
        /// is presented under.
        /// </summary>
        public static IBrandingManager BrandingManager => _brandingManager ??= ComponentHub.GetComponentManager<BrandingManager>();

        /// <summary>
        /// Gets the manager of the quickfilters the users defined themselves.
        /// </summary>
        public static ICustomQuickfilterManager CustomQuickfilterManager => _customQuickfilterManager ??= ComponentHub.GetComponentManager<CustomQuickfilterManager>();

        /// <summary>
        /// Gets the permission manager, which administers the group-to-policy grants on a resource.
        /// </summary>
        public static IPermissionManager PermissionManager => _permissionManager ??= ComponentHub.GetComponentManager<PermissionManager>();

        /// <summary>
        /// Gets the identity manager used to manage identity-related operations within the application.
        /// </summary>
        public static IIdentityManager IdentityManager => _identityManager ??= ComponentHub.GetComponentManager<IdentityManager>();

        /// <summary>
        /// Gets the group manager used to manage group-related operations within the application.
        /// </summary>
        public static IGroupManager GroupManager => _groupManager ??= ComponentHub.GetComponentManager<GroupManager>();

        /// <summary>
        /// Gets the template manager responsible for managing templates within the workspace.
        /// </summary>
        public static ITemplateManager TemplateManager => _templateManager ??= ComponentHub.GetComponentManager<TemplateManager>();

        /// <summary>
        /// Gets the object view manager responsible for managing the persisted tabs that
        /// wrap the objects index of a workspace.
        /// </summary>
        public static IObjectViewManager ObjectViewManager => _objectViewManager ??= ComponentHub.GetComponentManager<ObjectViewManager>();

        /// <summary>
        /// Gets the SLA manager responsible for managing service-level-agreement policies
        /// attached to classes.
        /// </summary>
        public static ISlaManager SlaManager => _slaManager ??= ComponentHub.GetComponentManager<SlaManager>();

        /// <summary>
        /// Gets the calendar manager responsible for managing working-hours calendars
        /// attached to classes.
        /// </summary>
        public static ICalendarManager CalendarManager => _calendarManager ??= ComponentHub.GetComponentManager<CalendarManager>();

        /// <summary>
        /// Gets the comment manager responsible for managing discussion threads attached
        /// to objects.
        /// </summary>
        public static ICommentManager CommentManager => _commentManager ??= ComponentHub.GetComponentManager<CommentManager>();

        /// <summary>
        /// Gets the attachment manager responsible for the files attached to objects.
        /// </summary>
        public static IAttachmentManager AttachmentManager => _attachmentManager ??= ComponentHub.GetComponentManager<AttachmentManager>();

        /// <summary>
        /// Gets the watcher manager responsible for the per-identity watch relationships
        /// on objects.
        /// </summary>
        public static IWatcherManager WatcherManager => _watcherManager ??= ComponentHub.GetComponentManager<WatcherManager>();

        /// <summary>
        /// Gets the share manager responsible for the per-identity share relationships
        /// on objects (e.g. portal issues shared with additional tenant members).
        /// </summary>
        public static IShareManager ShareManager => _shareManager ??= ComponentHub.GetComponentManager<ShareManager>();

        /// <summary>
        /// Gets the object-tag manager responsible for the tags (labels) attached to objects.
        /// </summary>
        public static IObjectTagManager ObjectTagManager => _objectTagManager ??= ComponentHub.GetComponentManager<ObjectTagManager>();

        /// <summary>
        /// Gets the workspace-template manager responsible for the workspace shapes the installed
        /// plugins define - what a new workspace can be created as, and which classes it starts
        /// with.
        /// </summary>
        public static IWorkspaceTemplateManager WorkspaceTemplateManager => _workspaceTemplateManager ??= ComponentHub.GetComponentManager<WorkspaceTemplateManager>();

        /// <summary>
        /// Gets the object-draft manager responsible for the unpublished working copies of the
        /// prose attributes of objects - what the document and blog editor writes on every
        /// change, and what publishing turns into a revision.
        /// </summary>
        public static IObjectDraftManager ObjectDraftManager => _objectDraftManager ??= ComponentHub.GetComponentManager<ObjectDraftManager>();

        /// <summary>
        /// Gets the value manager responsible for the per-object per-field value rows
        /// that back the typed inputs on the object detail and edit views.
        /// </summary>
        public static IValueManager ValueManager => _valueManager ??= ComponentHub.GetComponentManager<ValueManager>();

        /// <summary>
        /// Gets the commit manager responsible for the append-only commit chains that record
        /// every change made to an object, and for reconstructing and restoring past states
        /// from them.
        /// </summary>
        public static ICommitManager CommitManager => _commitManager ??= ComponentHub.GetComponentManager<CommitManager>();

        /// <summary>
        /// Gets the audit manager responsible for the installation-wide, append-only,
        /// hash-chained record of every event worth reconstructing later, and for replaying
        /// and verifying it.
        /// </summary>
        public static IAuditManager AuditManager => _auditManager ??= ComponentHub.GetComponentManager<AuditManager>();

        /// <summary>
        /// Gets the object-relation manager responsible for the semantic relations an object
        /// holds - to other objects (blocks, causes, duplicate of, ...) and to addresses
        /// outside the installation.
        /// </summary>
        public static IObjectRelationManager ObjectRelationManager => _objectRelationManager ??= ComponentHub.GetComponentManager<ObjectRelationManager>();

        /// <summary>
        /// Gets the relation-type manager responsible for administering the relations an
        /// object relation may carry, and for publishing them into the framework registry
        /// every link surface reads.
        /// </summary>
        public static IObjectRelationTypeManager ObjectRelationTypeManager => _objectRelationTypeManager ??= ComponentHub.GetComponentManager<ObjectRelationTypeManager>();

        /// <summary>
        /// Gets the impact manager responsible for answering what changing an object touches -
        /// the relations of the installation walked transitively rather than one hop at a time.
        /// </summary>
        public static IObjectImpactManager ObjectImpactManager => _objectImpactManager ??= ComponentHub.GetComponentManager<ObjectImpactManager>();

        /// <summary>
        /// Gets the progress manager responsible for rolling the progress of an object up from
        /// the objects it aggregates - the computed half of the relation effect
        /// <c>AggregatesProgress</c>.
        /// </summary>
        public static IObjectProgressManager ObjectProgressManager => _objectProgressManager ??= ComponentHub.GetComponentManager<ObjectProgressManager>();

        /// <summary>
        /// Gets the registry of the conditions that decide whether a workflow transition may be
        /// taken - the core's own guards plus whatever the plugins of the installation added.
        /// </summary>
        public static IWorkflowGuardManager WorkflowGuardManager => _workflowGuardManager ??= ComponentHub.GetComponentManager<WorkflowGuardManager>();

        /// <summary>
        /// Gets the registry of the conditions a workflow transition validates the object
        /// against.
        /// </summary>
        public static IWorkflowValidatorManager WorkflowValidatorManager => _workflowValidatorManager ??= ComponentHub.GetComponentManager<WorkflowValidatorManager>();

        /// <summary>
        /// Gets the registry of the actions a workflow transition performs once it has been
        /// applied.
        /// </summary>
        public static IWorkflowPostFunctionManager WorkflowPostFunctionManager => _workflowPostFunctionManager ??= ComponentHub.GetComponentManager<WorkflowPostFunctionManager>();

        /// <summary>
        /// Gets the session manager responsible for per-identity session/preference
        /// entries (e.g. persisted REST API table column layouts).
        /// </summary>
        public static ISessionManager SessionManager => _sessionManager ??= ComponentHub.GetComponentManager<SessionManager>();

        /// <summary>
        /// Gets the manager of the WQL queries each identity has run, which the query
        /// prompts offer back as their history.
        /// </summary>
        public static IWqlHistoryManager WqlHistoryManager => _wqlHistoryManager ??= ComponentHub.GetComponentManager<WqlHistoryManager>();

        /// <summary>
        /// Gets the notification-center manager responsible for the in-app notifications an
        /// identity can come back to, as listed behind the bell in the header.
        /// </summary>
        public static INotificationCenterManager NotificationCenterManager => _notificationCenterManager ??= ComponentHub.GetComponentManager<NotificationCenterManager>();

        /// <summary>
        /// Gets the identity-session manager responsible for the devices and browsers that
        /// are currently signed in with an identity.
        /// </summary>
        public static IIdentitySessionManager IdentitySessionManager => _identitySessionManager ??= ComponentHub.GetComponentManager<IdentitySessionManager>();

        /// <summary>
        /// Gets the access-token manager responsible for the personal access tokens an
        /// identity created for API access and integrations.
        /// </summary>
        public static IAccessTokenManager AccessTokenManager => _accessTokenManager ??= ComponentHub.GetComponentManager<AccessTokenManager>();

        /// <summary>
        /// Gets the saved-search manager responsible for the per-identity saved searches
        /// that back the global search dropdown and the search-page sidebar.
        /// </summary>
        public static ISavedSearchManager SavedSearchManager => _savedSearchManager ??= ComponentHub.GetComponentManager<SavedSearchManager>();

        /// <summary>
        /// Gets the sprint manager responsible for the Scrum iterations of the
        /// workspaces and the sprint assignment of their objects.
        /// </summary>
        public static ISprintManager SprintManager => _sprintManager ??= ComponentHub.GetComponentManager<SprintManager>();

        /// <summary>
        /// Constructs a URI for the specified endpoint type using the provided parameters.
        /// </summary>
        /// <typeparam name="TEndpoint">
        /// The type of the endpoint for which the URI is being constructed.
        /// </typeparam>
        /// <param name="parameters">
        /// An array of parameters used to customize the URI construction. Can be empty.
        /// </param>
        /// <returns>
        /// An instance of <see cref="IUri"/> representing the constructed URI for the specified endpoint.
        /// </returns>
        public static IUri GetUri<TEndpoint>(params Parameter[] parameters)
            where TEndpoint : IEndpoint
        {
            return ComponentHub.SitemapManager.GetUri<TEndpoint>(ApplicationContext, parameters);
        }

        /// <summary>
        /// Creates and displays a notification with the specified header and message.
        /// </summary>
        /// <remarks>
        /// Both <paramref name="header"/> and <paramref name="message"/> are treated as
        /// internationalization keys (e.g. <c>kleenestar.core:notification.title.created</c>)
        /// and resolved against the application's default culture via
        /// <see cref="I18N.Translate(string)"/>. A string that is not a known key is rendered
        /// verbatim, so plain text continues to work as a fallback. The global notification is
        /// not request-scoped, hence the default culture is used rather than a per-user one.
        /// </remarks>
        /// <param name="header">
        /// The i18n key of the title/heading to display in the notification. Cannot be null.
        /// </param>
        /// <param name="message">
        /// The i18n key of the main content/body text of the notification. Cannot be null.
        /// </param>
        /// <param name="durability">
        /// The duration, in milliseconds, that the notification remains visible. Specify -1
        /// to use the default duration.
        /// </param>
        /// <param name="subject">
        /// What the notification is about — an object key, a name — appended to the entry in
        /// the notification center so a list of otherwise identical messages stays readable.
        /// Optional; the toast does not show it.
        /// </param>
        /// <param name="targetUri">
        /// The path the notification center entry links to. Optional; the toast does not use it.
        /// </param>
        /// <returns>
        /// An object representing the created notification.
        /// </returns>
        /// <summary>
        /// Creates a notification about the supplied record.
        /// </summary>
        /// <remarks>
        /// The name of the record and the page it lives on are derived centrally by
        /// <see cref="NotificationSubject.Describe"/>, so a manager raising a notification does
        /// not have to know where the pages of its own entity are routed. This is the overload
        /// the managers use: a notification that names what it is about and links to it is the
        /// only kind worth keeping in the center.
        /// </remarks>
        /// <param name="header">The i18n key of the title/heading. Cannot be null.</param>
        /// <param name="message">The i18n key of the body text. Cannot be null.</param>
        /// <param name="entity">
        /// The record the notification is about. An unmapped or null record yields an entry
        /// without a link rather than no entry at all.
        /// </param>
        /// <param name="durability">
        /// The duration, in milliseconds, that the notification remains visible. Specify -1
        /// to use the default duration.
        /// </param>
        /// <returns>An object representing the created notification.</returns>
        public static INotification AddNotification(string header, string message, object entity, int durability = 5000)
        {
            var subject = NotificationSubject.Describe(entity);

            return AddNotification(header, message, durability, subject?.Label, subject?.TargetUri, subject?.IconUri);
        }

        public static INotification AddNotification(string header, string message, int durability = -1, string subject = null, string targetUri = null, string subjectIcon = null)
        {
            // best-effort: callers (manager Add/Update/Remove) rely on this returning
            // null silently when the host is not fully initialized (e.g. in unit tests
            // where CoreHub is wired without a real component hub), rather than NREing.
            var notificationManager = ComponentHub?.GetComponentManager<NotificationManager>();
            if (notificationManager is null)
            {
                return null;
            }

            // the toast below is transient. Recording the same event in the notification center
            // is what lets the user find it afterwards, addressed to them and naming what it
            // was about.
            _notificationCenterManager ??= ComponentHub.GetComponentManager<NotificationCenterManager>();
            _notificationCenterManager?.Record(header, message, subject, targetUri, subjectIcon);

            var application = WebEx.ComponentHub?.ApplicationManager?.GetApplication<KleeneStarApplication>();
            if (application is null)
            {
                return null;
            }

            return notificationManager.AddNotification
            (
                applicationContext: application,

                // the icon of the record the notification is about, the same one the entry in
                // the notification center carries. It is what makes a toast recognizable at a
                // glance - and, for a toast that is global anyway (see the note below),
                // recognizable as being about something the reader knows. The application icon
                // is the fallback for the records that carry none, and it used to be the only
                // thing shown: a class whose avatar had just been changed reported the change
                // under the product logo.
                icon: subjectIcon ?? ApplicationContext?.Icon?.ToUri()?.ToString(),

                // the page of the record, so the reader can go straight to what changed. It is
                // the same address the notification center entry links to, and it is null for
                // the records that have no page of their own - the toast is then plain text,
                // as it was before.
                link: targetUri,
                heading: I18N.Translate(header),
                // the toast names what it is about. It has to: the notification API this goes
                // through has no per-session store reachable from here, so every toast is a
                // global one that every connected client replays for as long as it lives. Two
                // actions inside that window are both delivered to both clients, and a toast
                // that says only "the object was updated" is then indistinguishable from
                // somebody else's — which is exactly how a workspace edit came to be reported
                // as an object edit. Naming the subject makes a stray toast recognizable as
                // one; see the remedy note in the memory of this project for the real fix.
                message: Compose(I18N.Translate(message), subject),
                durability: durability
            );
        }

        /// <summary>
        /// Appends the subject to a notification message, so the toast says what it is about.
        /// </summary>
        /// <param name="message">The translated message.</param>
        /// <param name="subject">The name of the record, or <see langword="null"/>.</param>
        /// <returns>The composed message.</returns>
        private static string Compose(string message, string subject)
        {
            return string.IsNullOrWhiteSpace(subject)
                ? message
                : $"{message} ({subject})";
        }

        /// <summary>
        /// The palette the accent color of an entity is drawn from: 32 distinct,
        /// contrast-rich colors.
        /// </summary>
        private static readonly string[] _accentColors =
        [
            "#ca1554", "#25509f", "#008237", "#b76f13", "#404b91", "#368b22", "#953599", "#ed381e",
            "#167ca0", "#d4424f", "#513e21", "#0e6a73", "#8b2443", "#20723d", "#6c2122", "#3b7d8d",
            "#1b6d44", "#903525", "#221f53", "#41775c", "#bd6b82", "#224f44", "#6a3ba1", "#387251",
            "#c26f1b", "#38464a", "#752b5c", "#09897c", "#998f35", "#da4040", "#2a537d", "#146459"
        ];

        /// <summary>
        /// Returns the accent color of an entity, derived from its identifier.
        /// </summary>
        /// <remarks>
        /// The color is a deterministic function of the id, so the same entity is always
        /// shown in the same color — in its generated icon as well as anywhere the id is
        /// rendered as a colored marker, such as the kind swatch of a template card.
        /// </remarks>
        /// <param name="id">The identifier the color is derived from.</param>
        /// <returns>The color as a hexadecimal css value.</returns>
        public static string AccentColor(Guid id)
        {
            var bytes = id.ToByteArray();
            var index = 0;

            for (var i = 0; i < bytes.Length; i++)
            {
                index = (index * 31 + bytes[i]) % _accentColors.Length;
                if (index < 0) { index += _accentColors.Length; } // safety for negatives
            }

            return _accentColors[index];
        }

        /// <summary>
        /// Generates a unique SVG icon for the specified identifier and saves it to the icons directory.
        /// </summary>
        /// <remarks>
        /// The icon color is selected from a palette of 32 distinct colors based on the hash
        /// code of the provided identifier. The generated icon is saved as an SVG file in the
        /// application's icons directory and can be accessed via a URI endpoint. This method
        /// creates the icons directory if it does not already exist. Because the file content
        /// is fully determined by the identifier, an already generated icon is reused as-is
        /// instead of being regenerated and rewritten.
        /// </remarks>
        /// <param name="id">
        /// The unique identifier used to select the icon color and determine the icon file name.
        /// </param>
        /// <returns>
        /// An IIcon instance representing the generated SVG icon, accessible via a relative URI endpoint.
        /// </returns>
        public static ImageIcon GenerateIcon(Guid id)
        {
            // define target icon directory, file name, and the public URI of the icon
            var iconDirectory = Path.Combine(AppContext.BaseDirectory, HttpServerContext?.DataPath, "icons");
            var iconFileName = $"{id}.svg";
            var outputPath = Path.Combine(iconDirectory, iconFileName);
            var icon = new ImageIcon(ApplicationContext.Route.Concat($"/assets/icons/{iconFileName}").ToUri());

            // the icon is a deterministic function of the id: the same id always maps to
            // the same color and therefore the same file content. If it has already been
            // generated, reuse the file on disk instead of re-reading the embedded
            // template, re-running the regex, and rewriting identical bytes — this path
            // runs on every entity create/edit, so the short-circuit avoids redundant I/O.
            if (File.Exists(outputPath))
            {
                return icon;
            }

            var colorHex = AccentColor(id);

            // load the embedded kleenestar.svg resource from assembly. The manifest name is
            // produced from the csproj LogicalName template, whose %(RecursiveDir) token uses
            // the build host's directory separator ('\' on Windows, '/' elsewhere). Normalize
            // both separators to '.' before matching so icon generation works regardless of
            // the platform the assembly was built on.
            var assembly = typeof(WorkspaceManager).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(x => x.Replace('\\', '.').Replace('/', '.')
                    .EndsWith("KleeneStar.Core.Assets.img.kleenestar.svg", StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException("Embedded kleenestar.svg resource not found.");

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException("SVG asset stream not found.");
            using var reader = new StreamReader(stream);
            var svgContent = reader.ReadToEnd();

            // replace the fill attribute in the first <rect> element with the selected color
            var newContent = Regex.Replace(
                svgContent,
                @"(<rect\b[^>]*\bfill\s*=\s*[""']?)[^""'>]+([""']?)",
                $"$1{colorHex}$2",
                RegexOptions.IgnoreCase
            );

            // create the icon directory if it does not exist
            Directory.CreateDirectory(iconDirectory);

            // write the modified SVG to the icon file
            File.WriteAllText(outputPath, newContent);

            return icon;
        }

        /// <summary>
        /// Stores an image an avatar control submitted and returns the icon that serves it.
        /// </summary>
        /// <remarks>
        /// The avatar control posts its picture inline, as
        /// <c>file:&lt;name&gt;;data:&lt;mime&gt;;base64,&lt;payload&gt;</c>. An
        /// <see cref="ImageIcon"/> holds a URI and nothing else, so the payload has to be put
        /// somewhere it can be served from before the entity can point at it. It is written
        /// next to the generated initials icons, under the same <c>assets/icons</c> route that
        /// already serves PNG, JPEG, GIF and WebP.
        ///
        /// The file name carries a short hash of the content, so replacing a picture yields a
        /// new URI: the icon route answers with a one-year immutable cache, and a stable name
        /// would leave every browser showing the old picture. Previous files of the same owner
        /// are removed as the new one is written.
        /// </remarks>
        /// <param name="ownerId">
        /// The entity the picture belongs to; names the file and scopes the cleanup.
        /// </param>
        /// <param name="payload">
        /// The value submitted by the avatar control. An empty or unparsable payload yields
        /// <see langword="null"/>, which the caller reads as "no picture given".
        /// </param>
        /// <returns>
        /// The icon serving the stored picture, or <see langword="null"/> when the payload
        /// carried no image.
        /// </returns>
        public static ImageIcon StoreIcon(Guid ownerId, string payload)
        {
            var image = ImagePayload.Parse(payload);

            if (image is null)
            {
                return null;
            }

            var iconDirectory = Path.Combine(AppContext.BaseDirectory, HttpServerContext?.DataPath, "icons");
            var fileName = $"{ownerId}-{image.Fingerprint}{image.Extension}";
            var outputPath = Path.Combine(iconDirectory, fileName);

            Directory.CreateDirectory(iconDirectory);
            RemoveStoredIcons(ownerId, outputPath);

            File.WriteAllBytes(outputPath, image.Content);

            return new ImageIcon(ApplicationContext.Route.Concat($"/assets/icons/{fileName}").ToUri());
        }

        /// <summary>
        /// Removes the pictures stored for the given owner by <see cref="StoreIcon"/>.
        /// </summary>
        /// <remarks>
        /// Used when a user removes their picture, so the entity falls back to the generated
        /// initials icon and the uploaded file does not stay on disk and reachable. The
        /// generated icon itself is named after the bare id and is therefore left alone.
        /// </remarks>
        /// <param name="ownerId">The entity whose stored pictures are removed.</param>
        /// <param name="keepPath">
        /// A file to spare, used by <see cref="StoreIcon"/> to clear the previous pictures
        /// while writing the new one. <see langword="null"/> removes all of them.
        /// </param>
        public static void RemoveStoredIcons(Guid ownerId, string keepPath = null)
        {
            var iconDirectory = Path.Combine(AppContext.BaseDirectory, HttpServerContext?.DataPath, "icons");

            if (!Directory.Exists(iconDirectory))
            {
                return;
            }

            foreach (var stale in Directory.EnumerateFiles(iconDirectory, $"{ownerId}-*")
                .Where(x => !string.Equals(x, keepPath, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    File.Delete(stale);
                }
                catch (IOException)
                {
                    // a file still held open elsewhere is left for the next write to clean up;
                    // failing here would lose the picture the user just uploaded
                }
            }
        }
    }
}
