using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Answers whether a REST request may change an object, or administer the structure of a
    /// class - the endpoint half of the rule <see cref="RouteAuthorization"/> applies to pages.
    /// </summary>
    /// <remarks>
    /// Reading needs no gate here: every object read passes through the object manager, which
    /// already leaves out what the caller may not read (<see cref="ContentVisibility"/>), so an
    /// endpoint asked for such an object answers it as not found. Changing does: an endpoint
    /// that resolved a record it may show must still ask whether the caller may change it, and
    /// answers a refusal as <c>403</c> - a REST caller has no page to be redirected to.
    /// <para>
    /// A record that does not resolve is not an authorization question: the gates answer
    /// <see langword="true"/> and leave the endpoint to report it as not found.
    /// </para>
    /// </remarks>
    public static class ContentAuthorization
    {
        /// <summary>
        /// Determines whether the caller may change an object.
        /// </summary>
        /// <param name="object">The object, may be absent.</param>
        /// <param name="request">The request.</param>
        /// <param name="permission">The permission the change needs; defaults to
        /// <see cref="ObjectUpdatePermission"/>.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        public static bool MayWrite(ObjectEntity @object, IRequest request, Type permission = null)
        {
            return @object is null
                || PageAuthorization.IsGranted(request, permission ?? typeof(ObjectUpdatePermission), PageAuthorization.ChainOf(@object));
        }

        /// <summary>
        /// Determines whether the caller may change the content of the workspace the route
        /// names - a sprint, a board's lanes and filter, a plan dragged on a timeline.
        /// </summary>
        /// <param name="request">The request whose route names the workspace.</param>
        /// <returns><see langword="true"/> when the change may proceed, or when the route names
        /// no workspace.</returns>
        public static bool MayWriteContent(IRequest request)
        {
            var key = request?.GetParameter<WebParameter.WorkspaceKeyParameter>()?.Value;
            var workspace = string.IsNullOrWhiteSpace(key) ? null : CoreHub.WorkspaceManager.GetWorkspaceByKey(key);

            return workspace is null
                || PageAuthorization.IsGranted(request, typeof(WorkspaceWriteContentPermission), PageAuthorization.ChainOf(workspace));
        }

        /// <summary>
        /// Determines whether the caller may create an object of a class.
        /// </summary>
        /// <param name="classId">The class the object is created in, may be absent.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the object may be created.</returns>
        public static bool MayCreateIn(Guid? classId, IRequest request)
        {
            var @class = classId is { } id && id != Guid.Empty ? CoreHub.ClassManager.GetClass(id) : null;

            return @class is null
                || PageAuthorization.IsGranted(request, typeof(ObjectUpdatePermission), PageAuthorization.ChainOf(@class));
        }

        /// <summary>
        /// Determines whether the caller may administer the structure of a class - the class
        /// itself and its fields, forms, workflows, priorities, statuses, agreements, calendars,
        /// security levels and templates.
        /// </summary>
        /// <param name="classId">The class, may be absent.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        public static bool MayAdminister(Guid? classId, IRequest request)
        {
            var @class = classId is { } id && id != Guid.Empty ? CoreHub.ClassManager.GetClass(id) : null;

            return @class is null
                || PageAuthorization.IsGranted(request, typeof(ClassUpdatePermission), PageAuthorization.ChainOf(@class));
        }

        /// <summary>
        /// Determines whether the caller may administer the classes of a workspace - create one
        /// in it.
        /// </summary>
        /// <param name="workspaceId">The workspace, may be absent.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the change may proceed.</returns>
        public static bool MayAdministerWorkspace(Guid? workspaceId, IRequest request)
        {
            var workspace = workspaceId is { } id && id != Guid.Empty ? CoreHub.WorkspaceManager.GetWorkspace(id) : null;

            return workspace is null
                || PageAuthorization.IsGranted(request, typeof(ClassCreatePermission), PageAuthorization.ChainOf(workspace));
        }

        /// <summary>
        /// Determines whether the caller may act on an insight. Insights belong to no
        /// workspace and are for signed-in callers; an insight's own grants (its permission
        /// dialog) decide beyond that, with the usual reading that an unadministered one is open.
        /// </summary>
        /// <param name="insightId">The insight, or <see langword="null"/> for a new one.</param>
        /// <param name="request">The request.</param>
        /// <param name="permission">The insight permission the action needs.</param>
        /// <returns><see langword="true"/> when the action may proceed.</returns>
        public static bool MayUseInsight(Guid? insightId, IRequest request, Type permission)
        {
            if (!RouteAuthorization.IsSignedIn(request))
            {
                return false;
            }

            return insightId is not { } id
                || PageAuthorization.IsGranted(request, permission, new PermissionResource(PermissionScope.Insight, id.ToString()));
        }

        /// <summary>
        /// The scope id of the installation-wide insight resource: the grants that say who may
        /// create insights, administered in the permission dialog of the insight overview.
        /// </summary>
        /// <remarks>
        /// A new insight is on no resource yet, so "who may create one" needs a resource of its
        /// own. It lives in the insight scope under an id no insight can carry (not a guid), so
        /// the readers of per-insight grants - <c>GetRefusedIds</c> and <c>GetGrantedIds</c>,
        /// which parse the id as a guid - pass it by.
        /// </remarks>
        public const string InsightCreationScopeId = "creation";

        /// <summary>
        /// The installation-wide insight resource (<see cref="InsightCreationScopeId"/>).
        /// </summary>
        public static PermissionResource InsightCreationResource => new(PermissionScope.Insight, InsightCreationScopeId);

        /// <summary>
        /// Determines whether the caller may create an insight: signed in, and granted
        /// <see cref="InsightCreatePermission"/> on the installation-wide insight resource -
        /// open to every signed-in caller while nobody administered it, like any resource.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may create an insight.</returns>
        public static bool MayCreateInsight(IRequest request)
        {
            return RouteAuthorization.IsSignedIn(request)
                && PageAuthorization.IsGranted(request, typeof(InsightCreatePermission), InsightCreationResource);
        }

        /// <summary>
        /// Determines whether the caller may administer who creates insights. The resource
        /// belongs to the installation, not to anybody's insight, so this fails closed on the
        /// administrators group (<see cref="AccountAuthorization.IsAdministrator(IRequest)"/>).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller may change the creation grants.</returns>
        public static bool MayAdministerInsightCreation(IRequest request)
        {
            return RouteAuthorization.IsSignedIn(request) && AccountAuthorization.IsAdministrator(request);
        }

        /// <summary>
        /// Determines whether the caller may move an insight from one state to another: taking
        /// an active insight out of use needs <see cref="InsightArchivePermission"/>, putting
        /// one back into use <see cref="InsightRestorePermission"/>. Staying in the same state
        /// asks nothing beyond the update itself.
        /// </summary>
        /// <param name="insightId">The insight.</param>
        /// <param name="from">The state it has.</param>
        /// <param name="to">The state it is to have.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the change of state may proceed.</returns>
        public static bool MayChangeInsightState(Guid insightId, InsightState from, InsightState to, IRequest request)
        {
            if (from == to)
            {
                return true;
            }

            var permission = to.IsActive()
                ? typeof(InsightRestorePermission)
                : typeof(InsightArchivePermission);

            return MayUseInsight(insightId, request, permission);
        }

        /// <summary>
        /// Reads the <c>id</c> query parameter of a CRUD request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The id, or <see langword="null"/>.</returns>
        public static Guid? ReadId(IRequest request)
        {
            return Guid.TryParse(request?.GetParameter<ParameterId>()?.Value, out var id) && id != Guid.Empty ? id : null;
        }

        /// <summary>
        /// Reads a guid field of a request's payload - JSON or form data - by name, ignoring case,
        /// before the endpoint parses it. A create names the class or workspace it creates in
        /// only there, and the gate has to know it before anything is written.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="name">The field name.</param>
        /// <returns>The value, or <see langword="null"/> when absent or not an id.</returns>
        public static Guid? ReadPayloadGuid(IRequest request, string name)
        {
            var value = default(string);

            if ((request as Request)?.Content is { Length: > 0 } content
                && request.Header?.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    using var document = JsonDocument.Parse(content);

                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var property = document.RootElement.EnumerateObject()
                            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

                        value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    }
                }
                catch (JsonException)
                {
                    // a payload that does not parse is the endpoint's to refuse
                }
            }

            value ??= request?.GetParameter(name)?.Value ?? request?.GetParameter(name.ToLowerInvariant())?.Value;

            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }
    }
}
