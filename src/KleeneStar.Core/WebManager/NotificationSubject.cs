using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// What a notification is about: the name the entry carries and the page it opens.
    /// </summary>
    /// <remarks>
    /// A notification that says only "an object was created" is not worth keeping — the reader
    /// cannot tell which one, and cannot get to it. Every manager therefore hands the record it
    /// just changed to <see cref="CoreHub.AddNotification"/>, and the mapping from an entity to
    /// its name and its route lives here rather than in twenty-odd managers, none of which
    /// should have to know where the pages of their entity live.
    /// </remarks>
    /// <param name="Label">The name shown in the notification.</param>
    /// <param name="TargetUri">The path the notification links to, or <see langword="null"/>.</param>
    /// <param name="IconUri">
    /// The path the icon of the record is served from, or <see langword="null"/> when the
    /// record carries none.
    /// </param>
    public sealed record NotificationSubject(string Label, string TargetUri, string IconUri = null)
    {
        /// <summary>
        /// Describes the given entity for a notification.
        /// </summary>
        /// <remarks>
        /// An entity of an unknown type still yields its name, so the entry says what it is
        /// about even when nothing knows where to link. A route that cannot be resolved — which
        /// is the normal case outside a running host, for instance in the unit tests — is
        /// likewise reported as a subject without a link rather than as a failure: a
        /// notification is worth recording even when it cannot be followed.
        /// </remarks>
        /// <param name="entity">The record the notification is about. May be null.</param>
        /// <returns>
        /// The subject, or <see langword="null"/> when the entity says nothing useful.
        /// </returns>
        public static NotificationSubject Describe(object entity)
        {
            if (entity is null)
            {
                return null;
            }

            try
            {
                return entity switch
                {
                    Model.Entities.Object x => new(x.Key ?? x.Summary, ResolveObjectPath(x), Icon(x.Icon)),
                    Workspace x => new(x.Name, Path<global::KleeneStar.Core.WWW.Workspaces._workspacekey_.Index>(new WorkspaceKeyParameter(x.Key)), Icon(x.Icon)),
                    Class x => new(x.Name, Path<global::KleeneStar.Core.WWW.Class._classid_.Index>(new ClassIdParameter(x.Id)), Icon(x.Icon)),
                    Field x => new(x.Name, Path<global::KleeneStar.Core.WWW.Field._fieldid_.Index>(new FieldIdParameter(x.Id))),
                    Form x => new(x.Name, Path<global::KleeneStar.Core.WWW.Form._formid_.Index>(new FormIdParameter(x.Id))),
                    Workflow x => new(x.Name, Path<global::KleeneStar.Core.WWW.Workflow._workflowid_.Index>(new WorkflowIdParameter(x.Id))),
                    Priority x => new(x.Name, Path<global::KleeneStar.Core.WWW.Priority._priorityid_.Index>(new PriorityIdParameter(x.Id))),
                    Status x => new(x.Name, Path<global::KleeneStar.Core.WWW.Status._statusid_.Index>(new WorkflowStateIdParameter(x.Id))),
                    SlaPolicy x => new(x.Name, Path<global::KleeneStar.Core.WWW.Sla._slaid_.Index>(new SlaIdParameter(x.Id))),
                    Calendar x => new(x.Name, Path<global::KleeneStar.Core.WWW.Calendar._calendarid_.Index>(new CalendarIdParameter(x.Id))),
                    Insight x => new(x.Name, Path<global::KleeneStar.Core.WWW.Insight._insightid_.Index>(new InsightIdParameter(x.Id))),
                    Template x => new(x.Name, Path<global::KleeneStar.Core.WWW.Template._templateid_.Index>(new TemplateIdParameter(x.Id)), Icon(x.Icon)),
                    Identity x => new(x.Name, Path<global::KleeneStar.Core.WWW.Settings.Identity._identityid_.Index>(new IdentityIdParameter(x.Id)), Icon(x.Avatar)),
                    Tenant x => new(x.Name, Path<global::KleeneStar.Core.WWW.Settings.Tenant._tenantid_.Index>(new TenantIdParameter(x.Id)), Icon(x.Icon)),
                    Group x => new(x.Name, Path<global::KleeneStar.Core.WWW.Settings.Group._groupid_.Index>(new GroupIdParameter(x.Id))),
                    NavigatorLink x => new(x.Name, Path<global::KleeneStar.Core.WWW.Settings.NavigatorLinks.Index>()),
                    SavedSearch x => new(x.Name, Path<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Index>(new SavedSearchIdParameter(x.Id))),
                    AccessToken x => new(x.Name, Path<global::KleeneStar.Core.WWW.Profile.Tokens.Index>()),
                    IdentitySession x => new(x.Device, Path<global::KleeneStar.Core.WWW.Profile.Sessions.Index>()),
                    Maintenance => new(null, Path<global::KleeneStar.Core.WWW.Settings.Maintenance>()),
                    Sprint x => new(x.Name, null),
                    string x => new(x, null),
                    _ => null
                };
            }
            catch (Exception)
            {
                // the route table is not reachable outside a running host; the caller still gets
                // a subject through the fallback below rather than losing the notification
                return new NotificationSubject(DescribeName(entity), null);
            }
        }

        /// <summary>
        /// Returns the path an image icon is served from.
        /// </summary>
        /// <remarks>
        /// Every entity of this application carries a generated icon in its own accent colour,
        /// and an identity carries the picture its owner chose. Showing that in the
        /// notification instead of a generic glyph is what lets a list of entries be scanned:
        /// the reader recognizes the thing, not the fact that something was created.
        /// </remarks>
        /// <param name="icon">The icon of the record, or <see langword="null"/>.</param>
        /// <returns>The path, or <see langword="null"/> when the record carries no icon.</returns>
        private static string Icon(WebExpress.WebUI.WebIcon.ImageIcon icon)
        {
            return icon?.Uri?.ToString();
        }

        /// <summary>
        /// Returns the name of an entity whose route is unknown, so an unmapped type still
        /// reads as something rather than as a blank.
        /// </summary>
        /// <param name="entity">The record the notification is about.</param>
        /// <returns>The name, or <see langword="null"/>.</returns>
        private static string DescribeName(object entity)
        {
            return entity?.GetType().GetProperty("Name")?.GetValue(entity) as string
                ?? entity?.GetType().GetProperty("Key")?.GetValue(entity) as string;
        }

        /// <summary>
        /// Returns the path of the detail page of an object, which follows from its kind.
        /// </summary>
        /// <param name="object">The object the notification is about.</param>
        /// <returns>The path, or <see langword="null"/>.</returns>
        private static string ResolveObjectPath(Model.Entities.Object @object)
        {
            if (string.IsNullOrWhiteSpace(@object?.Key))
            {
                return null;
            }

            var kind = ObjectKindCatalog.GetKind(ObjectKind.Normalize(@object.Kind));

            return kind?.DetailUri(@object.Key)?.ToString();
        }

        /// <summary>
        /// Returns the path of an endpoint, bound to the supplied route parameters.
        /// </summary>
        /// <typeparam name="TEndpoint">The page to address.</typeparam>
        /// <param name="parameters">The route parameters to bind.</param>
        /// <returns>The path, or <see langword="null"/> when the route is unknown.</returns>
        private static string Path<TEndpoint>(params WebExpress.WebCore.WebParameter.IParameter[] parameters)
            where TEndpoint : WebExpress.WebCore.WebEndpoint.IEndpoint
        {
            var uri = CoreHub.GetUri<TEndpoint>();

            foreach (var parameter in parameters)
            {
                uri = uri?.BindParameters(parameter);
            }

            return uri?.ToString();
        }
    }
}
