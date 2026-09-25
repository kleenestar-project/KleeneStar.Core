using KleeneStar.Core.WebFragment.Class;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPermissions;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebPermission
{
    /// <summary>
    /// Decides, for any page of the application, which permission it demands and on which
    /// resource chain - the one place that knows what a route is guarded by.
    /// </summary>
    /// <remarks>
    /// A page used to be guarded only if its author called <see cref="PageAuthorization.Demand"/>
    /// in its <c>Process</c>, and most never did: the class page answered anybody who knew its
    /// address. The rule is read off the route instead, so a page added tomorrow under an
    /// existing area is guarded without its author knowing the rule exists:
    /// <list type="bullet">
    /// <item><b>The resource</b> is the most specific one the route names: an object (by key),
    /// else a class (directly, or through a field, form, workflow, status, priority, agreement,
    /// calendar, security level or template of it), else a workspace (by key).</item>
    /// <item><b>The permission</b> follows from the area the page lives in (its namespace under
    /// <c>WWW</c>): the class administration areas and the workspace's own dialogs
    /// <em>administer</em> (<see cref="ClassUpdatePermission"/> /
    /// <see cref="WorkspaceUpdatePermission"/> - only the workspace administrators, and a grant
    /// on that very class); an object's page reads it, and its edit, delete, clone, draft and
    /// classification dialogs change it; any other page naming a workspace reads its content.</item>
    /// </list>
    /// A page naming no resource - the settings, the profile, the search, the start page - is on
    /// no chain and is left to its own guard. An administrative area naming no resource is not:
    /// administration fails closed (<see cref="PermissionImplication"/>).
    /// </remarks>
    public static class RouteAuthorization
    {
        /// <summary>
        /// The prefix every page id of the core carries (a page id is its full type name, lower-cased).
        /// </summary>
        private const string Prefix = "kleenestar.core.www.";

        /// <summary>
        /// The areas that administer the structure of a class or of a workspace.
        /// </summary>
        private static readonly HashSet<string> AdministrationAreas = new(StringComparer.OrdinalIgnoreCase)
        {
            "class", "classes", "field", "fields", "form", "forms", "priority", "priorities",
            "status", "statuses", "sla", "slas", "calendar", "calendars", "securitylevel",
            "securitylevels", "workflow", "workflows", "template", "templates", "relations"
        };

        /// <summary>
        /// The dialogs of an object page that change the object rather than show it.
        /// </summary>
        private static readonly HashSet<string> ObjectWritePages = new(StringComparer.OrdinalIgnoreCase)
        {
            "edit", "delete", "clone", "draft", "securitylevel"
        };

        /// <summary>
        /// The dialogs of a workspace that administer it.
        /// </summary>
        private static readonly HashSet<string> WorkspaceAdministrationPages = new(StringComparer.OrdinalIgnoreCase)
        {
            "edit", "delete", "clone", "avatar", "permissions", "home"
        };

        /// <summary>
        /// Returns the demand a page makes: the permission and the chain it is asked on.
        /// </summary>
        /// <param name="pageId">The page's id - its full type name, lower-cased.</param>
        /// <param name="request">The request whose route names the resource.</param>
        /// <returns>The demand, or <see langword="null"/> when the page is on no chain this rule
        /// guards.</returns>
        public static (Type Permission, PermissionResource[] Chain)? Resolve(string pageId, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(pageId) || request is null
                || !pageId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var segments = pageId[Prefix.Length..].Split('.');
            var area = segments[0];
            var page = segments[^1];

            // the REST endpoints guard themselves and answer a status, not a redirect
            if (string.Equals(area, "api", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (AdministrationAreas.Contains(area))
            {
                var @class = ResolveClass(request);

                return @class is not null
                    ? (typeof(ClassUpdatePermission), PageAuthorization.ChainOf(@class))
                    : (typeof(ClassUpdatePermission), PageAuthorization.ChainOf(ResolveWorkspace(request)));
            }

            var @object = ResolveObject(request);

            if (@object is not null)
            {
                var permission = page switch
                {
                    _ when ObjectWritePages.Contains(page) => typeof(ObjectUpdatePermission),
                    "historyrestore" => typeof(ObjectRestoreStatePermission),
                    _ => typeof(ObjectReadPermission)
                };

                return (permission, PageAuthorization.ChainOf(@object));
            }

            var workspace = ResolveWorkspace(request);

            if (workspace is null)
            {
                return null;
            }

            if (WorkspaceAdministrationPages.Contains(page)
                && (string.Equals(area, "workspaces", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(area, "documents", StringComparison.OrdinalIgnoreCase)))
            {
                return (typeof(WorkspaceUpdatePermission), PageAuthorization.ChainOf(workspace));
            }

            return string.Equals(page, "organize", StringComparison.OrdinalIgnoreCase)
                ? (typeof(WorkspaceWriteContentPermission), PageAuthorization.ChainOf(workspace))
                : (typeof(WorkspaceReadContentPermission), PageAuthorization.ChainOf(workspace));
        }

        /// <summary>
        /// Determines whether the caller of a request may open a page.
        /// </summary>
        /// <param name="pageId">The page's id.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the page may render.</returns>
        public static bool IsGranted(string pageId, IRequest request)
        {
            // a new workspace is on no chain yet, so no grant can say who may create one - but
            // an anonymous visitor may not: whoever creates it is who the template sets it up for
            if (string.Equals(pageId, Prefix + "workspaces.add", StringComparison.OrdinalIgnoreCase))
            {
                return IsSignedIn(request);
            }

            // notifications belong to an account and dashboards to the signed-in callers of the
            // installation (they are on no workspace chain); a dashboard's own grants decide beyond
            var area = AreaOf(pageId);

            if (string.Equals(area, "notifications", StringComparison.OrdinalIgnoreCase))
            {
                return IsSignedIn(request);
            }

            if (string.Equals(area, "dashboards", StringComparison.OrdinalIgnoreCase)
                || string.Equals(area, "dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return MayOpenDashboardPage(pageId, request);
            }

            // a saved search belongs to its owner and is shared through its own grants; it is on
            // no workspace chain either
            if (string.Equals(area, "savedsearches", StringComparison.OrdinalIgnoreCase)
                || string.Equals(area, "savedsearch", StringComparison.OrdinalIgnoreCase))
            {
                return MayOpenSavedSearchPage(pageId, request);
            }

            // the overview lists what the caller may read and offers to create one; with neither
            // there is nothing for them on it
            if (string.Equals(pageId, Prefix + "workspaces.index", StringComparison.OrdinalIgnoreCase))
            {
                return MayListWorkspaces(request);
            }

            return Resolve(pageId, request) is not { } demand
                || PageAuthorization.IsGranted(request, demand.Permission, demand.Chain);
        }

        /// <summary>
        /// Returns the area of a page - the first namespace segment under <c>WWW</c>.
        /// </summary>
        /// <param name="pageId">The page id.</param>
        /// <returns>The area, or <see langword="null"/> for a page outside the core.</returns>
        private static string AreaOf(string pageId)
        {
            return !string.IsNullOrWhiteSpace(pageId) && pageId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                ? pageId[Prefix.Length..].Split('.')[0]
                : null;
        }

        /// <summary>
        /// Decides a dashboard page: signed in, and - for a page naming a dashboard - the
        /// dashboard permission the page needs on its own grants.
        /// </summary>
        /// <param name="pageId">The page id.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the page may render.</returns>
        private static bool MayOpenDashboardPage(string pageId, IRequest request)
        {
            var id = Parse(request.GetParameter<DashboardIdParameter>()?.Value);
            var permission = pageId[(pageId.LastIndexOf('.') + 1)..].ToLowerInvariant() switch
            {
                "edit" => typeof(DashboardUpdatePermission),
                "delete" => typeof(DashboardDeletePermission),
                "clone" => typeof(DashboardClonePermission),
                "permission" => typeof(DashboardManageProfilesPermission),
                _ => typeof(DashboardReadPermission)
            };

            return WebRestApi.ContentAuthorization.MayUseDashboard(id, request, permission);
        }

        /// <summary>
        /// Decides a saved-search dialog: signed in, and - for a dialog naming a saved search -
        /// the saved-search permission the dialog needs, which the owner always holds and anybody
        /// else only through a grant on it.
        /// </summary>
        /// <param name="pageId">The page id.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the page may render.</returns>
        private static bool MayOpenSavedSearchPage(string pageId, IRequest request)
        {
            if (!IsSignedIn(request))
            {
                return false;
            }

            var id = Parse(request.GetParameter<SavedSearchIdParameter>()?.Value);

            if (id is not { } savedSearchId)
            {
                return true;
            }

            var permission = pageId[(pageId.LastIndexOf('.') + 1)..].ToLowerInvariant() switch
            {
                "edit" => typeof(SavedSearchUpdatePermission),
                "delete" => typeof(SavedSearchDeletePermission),
                "permission" => typeof(SavedSearchManageProfilesPermission),
                _ => typeof(SavedSearchReadPermission)
            };

            return CoreHub.SavedSearchManager.IsGranted(
                CoreHub.SavedSearchManager.GetSavedSearch(savedSearchId),
                CoreHub.SessionManager.GetCurrentIdentityId(request),
                permission);
        }

        /// <summary>
        /// Determines whether the caller of a request is signed in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> for a caller that resolves to an account.</returns>
        public static bool IsSignedIn(IRequest request)
        {
            return CoreHub.SessionManager.GetCurrentIdentityId(request) != Guid.Empty;
        }

        /// <summary>
        /// Determines whether the workspace overview - and the menu that leads to it - has
        /// anything for the caller: a workspace they may read, or the right to create one.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the overview is offered.</returns>
        public static bool MayListWorkspaces(IRequest request)
        {
            return IsSignedIn(request) || ContentVisibility.MayReadAnyWorkspace();
        }

        /// <summary>
        /// Refuses a page whose caller may not open it. For the pages that act in their
        /// <c>Process</c> and redirect before any fragment - and so before the page guard - renders.
        /// </summary>
        /// <param name="renderContext">The render context of the page.</param>
        /// <exception cref="WebExpress.WebCore.WebMessage.ForbiddenException">The caller is refused.</exception>
        public static void Demand(WebExpress.WebCore.WebPage.IRenderContext renderContext)
        {
            if (!IsGranted(renderContext?.PageContext?.EndpointId?.ToString(), renderContext?.Request))
            {
                throw PageAuthorization.Refuse();
            }
        }

        /// <summary>
        /// Resolves the object the route names by key, whatever the caller's clearance - whether
        /// they may see the record is the security level's question, asked where it is shown.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static Model.Entities.Object ResolveObject(IRequest request)
        {
            var key = request.GetParameter<ObjectKeyParameter>()?.Value;

            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            using (CoreHub.SecurityLevelManager.BeginUnrestricted())
            {
                return CoreHub.ObjectManager.GetObjectByKey(key);
            }
        }

        /// <summary>
        /// Resolves the class the route names, directly or through one of the records of its
        /// structure.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The class, or <see langword="null"/>.</returns>
        internal static Model.Entities.Class ResolveClass(IRequest request)
        {
            var @class = ClassSidebarCondition.ResolveClass(request);

            if (@class is not null)
            {
                return @class;
            }

            Guid? classId = null;

            classId ??= Parse(request.GetParameter<FieldIdParameter>()?.Value) is { } fieldId
                ? CoreHub.FieldManager.GetField(fieldId)?.ClassId
                : null;

            classId ??= Parse(request.GetParameter<PriorityIdParameter>()?.Value) is { } priorityId
                ? CoreHub.PriorityManager.GetPriority(priorityId)?.ClassId
                : null;

            classId ??= Parse(request.GetParameter<WorkflowStateIdParameter>()?.Value) is { } statusId
                ? CoreHub.StatusManager.GetStatus(statusId)?.ClassId
                : null;

            classId ??= Parse(request.GetParameter<SecurityLevelIdParameter>()?.Value) is { } securityLevelId
                ? CoreHub.SecurityLevelManager.GetSecurityLevel(securityLevelId)?.ClassId
                : null;

            classId ??= Parse(request.GetParameter<TemplateIdParameter>()?.Value) is { } templateId
                ? CoreHub.TemplateManager.GetTemplate(templateId)?.ClassId
                : null;

            return classId is { } id ? CoreHub.ClassManager.GetClass(id) : null;
        }

        /// <summary>
        /// Resolves the workspace the route names by key.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        private static Model.Entities.Workspace ResolveWorkspace(IRequest request)
        {
            var key = request.GetParameter<WorkspaceKeyParameter>()?.Value;

            return string.IsNullOrWhiteSpace(key) ? null : CoreHub.WorkspaceManager.GetWorkspaceByKey(key);
        }

        /// <summary>
        /// Parses a route segment as an id.
        /// </summary>
        /// <param name="value">The segment value.</param>
        /// <returns>The id, or <see langword="null"/>.</returns>
        private static Guid? Parse(string value)
        {
            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }
    }
}
