using System;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Reads guid-valued fields out of a CRUD payload.
    /// </summary>
    /// <remarks>
    /// <see cref="RestApiCrudFormDataExtensions.BindTo"/> binds guid properties itself, so this
    /// helper is not needed to fill an entity. It is needed wherever a payload has to be read
    /// without one: to validate a reference before the record is written, to resolve a value the
    /// payload carries under a name no property has, and to derive one reference from another.
    /// </remarks>
    public static class RestApiCrudFormDataGuidExtensions
    {
        /// <summary>
        /// Reads the guid stored under the given field name.
        /// </summary>
        /// <param name="fieldMap">The payload to read from.</param>
        /// <param name="name">
        /// The field name as declared on the entity; the lookup is case-insensitive, matching the
        /// lower-cased keys the payload parser produces.
        /// </param>
        /// <param name="value">
        /// When this method returns, contains the parsed guid, or <see cref="Guid.Empty"/> when the
        /// field is absent, empty or not a guid.
        /// </param>
        /// <returns>True when a guid could be read; otherwise false.</returns>
        public static bool TryGetGuid(this RestApiCrudFormData fieldMap, string name, out Guid value)
        {
            var field = fieldMap?.TryRead<Guid>(name);
            value = field?.Value ?? Guid.Empty;
            return field?.State == RestApiFormFieldState.Valid;
        }

        /// <summary>
        /// Reads an optional guid reference — a foreign key the payload may set, clear or leave
        /// alone.
        /// </summary>
        /// <remarks>
        /// A selection control submits the empty guid for its "none" entry, which means the caller
        /// cleared the reference, while an absent field means the form does not carry it at all
        /// and the stored value must stay. The return value distinguishes the two so a form that
        /// omits a reference cannot silently erase it.
        /// </remarks>
        /// <param name="fieldMap">The payload to read from.</param>
        /// <param name="name">The field name as declared on the entity; matched case-insensitively.</param>
        /// <param name="value">
        /// When this method returns true, contains the referenced id, or null when the payload
        /// clears the reference.
        /// </param>
        /// <returns>True when the payload carries the field at all; otherwise false.</returns>
        public static bool TryGetGuidReference(this RestApiCrudFormData fieldMap, string name, out Guid? value)
        {
            var field = fieldMap?.ReadOptionalGuid(name);
            value = field?.Value;
            return field?.State is RestApiFormFieldState.Valid or RestApiFormFieldState.Empty;
        }
    }
}
