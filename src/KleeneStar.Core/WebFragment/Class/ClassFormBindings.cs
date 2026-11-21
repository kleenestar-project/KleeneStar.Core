using KleeneStar.Core.WebParameter;
using System;
using System.Collections.Generic;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// The path variable bindings the class dialogs need for the services that are keyed by
    /// the workspace route parameter while the dialog itself is keyed by the class.
    /// </summary>
    /// <remarks>
    /// The edit and the clone dialog stand on <c>/class/{classid}/…</c>, but the name check,
    /// the inherited-class and the parent-class selections are answered by endpoints under
    /// <c>/api/1/classes/{workspacekey}/…</c>. The automatic request binding of the data
    /// islands leaves that placeholder intact on a page that carries no workspace key, and
    /// the client then calls <c>${workspacekey}</c> literally - which is what the clone
    /// dialog used to do. The class the route addresses is therefore loaded to read its
    /// workspace key, and the placeholder is bound by hand.
    /// </remarks>
    internal static class ClassFormBindings
    {
        /// <summary>
        /// Builds the <c>workspacekey</c> binding for a dialog keyed by the class id.
        /// </summary>
        /// <param name="renderContext">The current render context, or null.</param>
        /// <returns>The bindings to apply, empty when no workspace is resolvable.</returns>
        public static IEnumerable<KeyValuePair<string, string>> WorkspaceKeyOfClass(IRenderControlContext renderContext)
        {
            var classParameter = renderContext?.Request?.GetParameter<ClassIdParameter>();

            if (classParameter is null)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            var @class = CoreHub.ClassManager?.GetClass(classParameter);

            if (string.IsNullOrEmpty(@class?.Workspace?.Key))
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            return
            [
                new KeyValuePair<string, string>(WorkspaceKeyParameter.Key, @class.Workspace.Key)
            ];
        }
    }
}
