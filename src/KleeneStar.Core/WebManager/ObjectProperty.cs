using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The system properties of an <see cref="ObjectEntity"/> that the commit history records
    /// alongside the class fields, together with the reading, writing and formatting of them.
    /// </summary>
    /// <remarks>
    /// An object carries two kinds of state: the class fields, stored as <see cref="Value"/> rows
    /// and addressed by a <see cref="Field"/> id, and the properties on the object row itself —
    /// its summary, its assignee, where it sits in the hierarchy. A history that recorded only
    /// the first would miss half of what a user changes, so both travel through the same
    /// <see cref="Change"/> entries; a system property is the one whose
    /// <see cref="Change.FieldId"/> is <c>null</c>.
    /// <para>
    /// Values are recorded in their storage form — a reference as its id, an enumeration as its
    /// name — so a restore can write them back without guessing. <see cref="Describe"/> turns
    /// that form into the text the history modal shows.
    /// </para>
    /// </remarks>
    public static class ObjectProperty
    {
        /// <summary>The key of the object, e.g. <c>INC-00123</c>.</summary>
        public const string Key = "key";

        /// <summary>The one-line summary of the object.</summary>
        public const string Summary = "summary";

        /// <summary>The long description of the object.</summary>
        public const string Description = "description";

        /// <summary>The lifecycle state (active / archived) of the object.</summary>
        public const string State = "state";

        /// <summary>The object kind that decides which detail view presents the object.</summary>
        public const string Kind = "kind";

        /// <summary>The containing parent object.</summary>
        public const string Parent = "parent";

        /// <summary>The identity the object is assigned to.</summary>
        public const string Assignee = "assignee";

        /// <summary>The sprint the object is committed to.</summary>
        public const string Sprint = "sprint";

        /// <summary>The ordering rank of the object within its sprint or backlog.</summary>
        public const string SprintRank = "sprintrank";

        /// <summary>The story-point estimate of the object.</summary>
        public const string StoryPoints = "storypoints";

        /// <summary>The security level the object is classified with.</summary>
        public const string SecurityLevel = "securitylevel";

        /// <summary>Whether the customer portal keeps the object to its participants.</summary>
        public const string PortalRestricted = "portalrestricted";

        /// <summary>
        /// The system properties in the order the history presents them.
        /// </summary>
        public static readonly IReadOnlyList<string> All =
        [
            Key,
            Summary,
            Description,
            State,
            Kind,
            Parent,
            Assignee,
            Sprint,
            SprintRank,
            StoryPoints,
            SecurityLevel,
            PortalRestricted
        ];

        /// <summary>
        /// Returns whether the supplied name addresses a system property.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns><see langword="true"/> when the name is one of <see cref="All"/>.</returns>
        public static bool IsSystem(string name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && All.Contains(name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the localized label key of a system property, suitable for passing to
        /// <c>I18N.Translate</c>.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <returns>The translation key, or <c>null</c> when the name is not a system property.</returns>
        public static string Text(string name)
        {
            return (name ?? string.Empty).ToLowerInvariant() switch
            {
                Key => "kleenestar.core:object.history.property.key",
                Summary => "kleenestar.core:object.history.property.summary",
                Description => "kleenestar.core:object.history.property.description",
                State => "kleenestar.core:object.history.property.state",
                Kind => "kleenestar.core:object.history.property.kind",
                Parent => "kleenestar.core:object.history.property.parent",
                Assignee => "kleenestar.core:object.history.property.assignee",
                Sprint => "kleenestar.core:object.history.property.sprint",
                SprintRank => "kleenestar.core:object.history.property.sprintrank",
                StoryPoints => "kleenestar.core:object.history.property.storypoints",
                SecurityLevel => "kleenestar.core:object.history.property.securitylevel",
                PortalRestricted => "kleenestar.core:object.history.property.portalrestricted",
                _ => null
            };
        }

        /// <summary>
        /// Reads a system property from an object in its storage form.
        /// </summary>
        /// <param name="object">The object to read from.</param>
        /// <param name="name">The attribute name.</param>
        /// <returns>The storage form, or <c>null</c> when the property is unset or unknown.</returns>
        public static string Read(ObjectEntity @object, string name)
        {
            if (@object is null)
            {
                return null;
            }

            return (name ?? string.Empty).ToLowerInvariant() switch
            {
                Key => Blank(@object.Key),
                Summary => Blank(@object.Summary),
                Description => Blank(@object.Description),
                State => @object.State.ToString(),
                Kind => Blank(@object.Kind),
                Parent => @object.ParentId?.ToString(),
                Assignee => @object.AssigneeId?.ToString(),
                Sprint => @object.SprintId?.ToString(),
                SprintRank => @object.SprintRank == 0 ? null : @object.SprintRank.ToString(CultureInfo.InvariantCulture),
                StoryPoints => @object.StoryPoints?.ToString(CultureInfo.InvariantCulture),
                SecurityLevel => @object.SecurityLevelId?.ToString(),
                PortalRestricted => @object.PortalRestricted ? bool.TrueString : null,
                _ => null
            };
        }

        /// <summary>
        /// Writes a system property back onto an object from its storage form. Used by the
        /// restore path, which reapplies a recorded state rather than a user's input.
        /// </summary>
        /// <remarks>
        /// <see cref="Key"/> is deliberately not writable: the key is how every link, comment and
        /// external reference addresses the object, so rewinding it would break references the
        /// history has no claim over. Restoring an object therefore restores what it contained,
        /// not what it is called. <see cref="SecurityLevel"/> and <see cref="PortalRestricted"/>
        /// are recorded but not restored either: who may see a record is decided in its own
        /// dialog, which checks the clearance to assign it, and restoring the content must not
        /// widen the audience as a side effect.
        /// </remarks>
        /// <param name="object">The object to write to.</param>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The storage form to write, or <c>null</c> to clear.</param>
        /// <returns><see langword="true"/> when the property was written.</returns>
        public static bool Write(ObjectEntity @object, string name, string value)
        {
            if (@object is null)
            {
                return false;
            }

            switch ((name ?? string.Empty).ToLowerInvariant())
            {
                case Summary:
                    @object.Summary = value;
                    return true;

                case Description:
                    @object.Description = value;
                    return true;

                case State:
                    @object.State = Enum.TryParse<WorkspaceState>(value, true, out var state)
                        ? state
                        : WorkspaceState.Active;
                    return true;

                case Kind:
                    @object.Kind = ObjectKind.Normalize(value);
                    return true;

                case Parent:
                    @object.ParentId = ParseGuid(value);
                    return true;

                case Assignee:
                    @object.AssigneeId = ParseGuid(value);
                    return true;

                case Sprint:
                    @object.SprintId = ParseGuid(value);
                    return true;

                case SprintRank:
                    @object.SprintRank = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) ? rank : 0;
                    return true;

                case StoryPoints:
                    @object.StoryPoints = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var points) ? points : null;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Turns the storage form of a system property into the text the history shows: a
        /// referenced identity, object or sprint reads as its name rather than as its id.
        /// </summary>
        /// <remarks>
        /// A reference whose target has since been deleted keeps its raw id rather than
        /// disappearing — the history says what the value was, and an id the reader can look up
        /// is more honest than an empty cell.
        /// </remarks>
        /// <param name="name">The attribute name.</param>
        /// <param name="value">The storage form.</param>
        /// <returns>The display text, or an empty string when the property was unset.</returns>
        public static string Describe(string name, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            switch ((name ?? string.Empty).ToLowerInvariant())
            {
                case Assignee:
                    return ParseGuid(value) is Guid identityId
                        ? CoreHub.IdentityManager.GetIdentity(identityId)?.Name ?? value
                        : value;

                case Parent:
                    return ParseGuid(value) is Guid parentId
                        ? CoreHub.ObjectManager.GetObject(parentId)?.Key ?? value
                        : value;

                case Sprint:
                    return ParseGuid(value) is Guid sprintId
                        ? CoreHub.SprintManager.GetSprint(sprintId)?.Name ?? value
                        : value;

                case SecurityLevel:
                    return ParseGuid(value) is Guid levelId
                        ? CoreHub.SecurityLevelManager.GetSecurityLevel(levelId)?.Name ?? value
                        : value;

                default:
                    return value;
            }
        }

        /// <summary>
        /// Returns <c>null</c> for a blank string so an unset property and an empty one are
        /// recorded as the same absence.
        /// </summary>
        /// <param name="value">The value to normalize.</param>
        /// <returns>The value, or <c>null</c>.</returns>
        private static string Blank(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// Parses a stored reference id, returning <c>null</c> for anything that is not one.
        /// </summary>
        /// <param name="value">The stored value.</param>
        /// <returns>The id, or <c>null</c>.</returns>
        private static Guid? ParseGuid(string value)
        {
            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }
    }
}
