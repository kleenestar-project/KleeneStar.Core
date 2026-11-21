using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes <c>Assets/js/kleenestar.core.template.hierarchy.js</c> (<c>kleenestar.core.TemplateHierarchyCtrl</c>),
    /// the node tree of the hierarchy wizard (<see cref="WebControl.TemplateHierarchyInput"/>).
    /// </summary>
    /// <remarks>
    /// Every page, because the wizard opens in a dialog on the template overview as well as on
    /// its own pages, and a dialog brings no scripts of its own. The labels are client
    /// translations of <c>Assets/js/i18n</c>, which <see cref="IncludeEditorAddonsScript"/>
    /// includes on every page.
    /// </remarks>
    [Asset("/assets/js/kleenestar.core.template.hierarchy.js")]
    public sealed class IncludeTemplateHierarchyScript : IInclude
    {
    }
}
