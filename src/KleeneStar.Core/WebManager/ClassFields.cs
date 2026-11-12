using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The fields of a class that are in use - for the core and the plugins (automation actions).
    /// </summary>
    public static class ClassFields
    {
        /// <summary>
        /// Returns the active, not deprecated fields of a class.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The fields.</returns>
        public static List<Field> Active(Guid classId)
        {
            return [.. CoreHub.FieldManager
                .GetFields(new ClassIdParameter(classId))
                .Where(x => !x.Deprecated && x.State == FieldState.Active)];
        }
    }
}
