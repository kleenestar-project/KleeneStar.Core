using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using CalendarEntity = KleeneStar.Model.Entities.Calendar;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// What an <see cref="AuditEvent"/> is about: which kind of record, which one of them, what
    /// it was called, and which version of it the event produced.
    /// </summary>
    /// <remarks>
    /// The target is the axis along which a trail is read. An event that names only its action
    /// answers "what happened"; an event that names its target answers "what happened to this",
    /// which is the question a forensic reader actually arrives with. The mapping from an entity
    /// to its audit target lives here rather than in the twenty-odd managers that raise events,
    /// for the same reason <see cref="NotificationSubject"/> exists: no manager should have to
    /// know how the audit log classifies its entity.
    /// </remarks>
    /// <param name="Type">The kind of record.</param>
    /// <param name="Id">The durable id of the record, or <c>null</c> when there is none.</param>
    /// <param name="Key">The human-readable name of the record at the time of the event.</param>
    /// <param name="Revision">
    /// The version the record reached through the event, or <c>null</c> when it is not
    /// versioned.
    /// </param>
    public sealed record AuditTarget(AuditTargetType Type, Guid? Id, string Key = null, int? Revision = null)
    {
        /// <summary>
        /// The target used for events that are about the installation itself.
        /// </summary>
        public static AuditTarget Installation { get; } = new(AuditTargetType.Installation, null, "installation");

        /// <summary>
        /// The target used for events that are about nothing in particular.
        /// </summary>
        public static AuditTarget None { get; } = new(AuditTargetType.None, null);

        /// <summary>
        /// Returns this target with a version attached.
        /// </summary>
        /// <param name="revision">The version the record reached.</param>
        /// <returns>The target.</returns>
        public AuditTarget At(int? revision)
        {
            return this with { Revision = revision };
        }

        /// <summary>
        /// Describes an entity as an audit target.
        /// </summary>
        /// <remarks>
        /// An entity of a type the log does not classify still yields a target, with
        /// <see cref="AuditTargetType.None"/> and whatever id and name reflection can find. That
        /// is deliberate: an event about an unclassified record is worth less than a classified
        /// one, but it is worth far more than no event at all, and silently dropping it would
        /// leave a hole the reader cannot see. Adding the missing member of
        /// <see cref="AuditTargetType"/> then upgrades the future events without invalidating
        /// the past ones.
        /// </remarks>
        /// <param name="entity">The record the event is about. May be <c>null</c>.</param>
        /// <returns>The target. Never <c>null</c>.</returns>
        public static AuditTarget Describe(object entity)
        {
            if (entity is null)
            {
                return None;
            }

            return entity switch
            {
                ObjectEntity x => new(AuditTargetType.Object, x.Id, x.Key ?? x.Summary),
                Workspace x => new(AuditTargetType.Workspace, x.Id, x.Name),
                Class x => new(AuditTargetType.Class, x.Id, x.Name),
                Field x => new(AuditTargetType.Field, x.Id, x.Name),
                Form x => new(AuditTargetType.Form, x.Id, x.Name),
                Template x => new(AuditTargetType.Template, x.Id, x.Name),
                Priority x => new(AuditTargetType.Priority, x.Id, x.Name),
                Workflow x => new(AuditTargetType.Workflow, x.Id, x.Name),
                Status x => new(AuditTargetType.Status, x.Id, x.Name),
                Comment x => new(AuditTargetType.Comment, x.Id, x.ObjectId.ToString("D", CultureInfo.InvariantCulture)),
                Attachment x => new(AuditTargetType.Attachment, x.Id, x.FileName),
                ObjectTag x => new(AuditTargetType.Tag, x.Id, x.Name),
                ObjectRelation x => new(AuditTargetType.Relation, x.Id, x.SourceObjectId.ToString("D", CultureInfo.InvariantCulture)),
                ObjectRelationType x => new(AuditTargetType.RelationType, x.Id, x.Key),
                ObjectShare x => new(AuditTargetType.Share, x.Id, x.ObjectId.ToString("D", CultureInfo.InvariantCulture)),
                Sprint x => new(AuditTargetType.Sprint, x.Id, x.Name),
                Identity x => new(AuditTargetType.Identity, x.Id, x.UserName ?? x.Name),
                Group x => new(AuditTargetType.Group, x.Id, x.Name),
                Tenant x => new(AuditTargetType.Tenant, x.Id, x.Name),
                IdentitySession x => new(AuditTargetType.Session, x.Id, x.Device),
                AccessToken x => new(AuditTargetType.AccessToken, x.Id, x.Name),
                PermissionAssignment x => new(AuditTargetType.Permission, x.Id, x.Policy),
                SlaPolicy x => new(AuditTargetType.SlaPolicy, x.Id, x.Name),
                CalendarEntity x => new(AuditTargetType.Calendar, x.Id, x.Name),
                Insight x => new(AuditTargetType.Insight, x.Id, x.Name),
                ObjectView x => new(AuditTargetType.ObjectView, x.Id, x.Name),
                NavigatorLink x => new(AuditTargetType.NavigatorLink, x.Id, x.Name),
                Branding x => new(AuditTargetType.Branding, x.Id, x.Title),
                Maintenance x => new(AuditTargetType.Maintenance, x.Id, null),
                _ => new(AuditTargetType.None, ResolveId(entity), ResolveName(entity))
            };
        }

        /// <summary>
        /// Returns the id of an entity the log does not classify, so its events can still be
        /// grouped into a trail.
        /// </summary>
        /// <param name="entity">The record.</param>
        /// <returns>The id, or <c>null</c>.</returns>
        private static Guid? ResolveId(object entity)
        {
            return entity?.GetType().GetProperty("Id")?.GetValue(entity) as Guid?;
        }

        /// <summary>
        /// Returns the name of an entity the log does not classify, so the event still reads as
        /// being about something.
        /// </summary>
        /// <param name="entity">The record.</param>
        /// <returns>The name, or <c>null</c>.</returns>
        private static string ResolveName(object entity)
        {
            var type = entity?.GetType();

            return type?.GetProperty("Name")?.GetValue(entity) as string
                ?? type?.GetProperty("Key")?.GetValue(entity) as string
                ?? type?.GetProperty("Title")?.GetValue(entity) as string;
        }
    }
}
