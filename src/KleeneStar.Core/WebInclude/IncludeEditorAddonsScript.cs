using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Core.WebInclude
{
    /// <summary>
    /// Includes the KleeneStar add-ons of the editor on every page of the application, with
    /// the client translations they are labelled with. Written like the framework's own add-ons
    /// (<c>editor/addons/*.js</c>): the shared helpers <c>kleenestar.core.EditorAddons</c>, then one
    /// file per add-on with its registration and its control (<c>kleenestar.core.…AddonCtrl</c>).
    /// </summary>
    /// <remarks>
    /// Every page, because a document is edited in dialogs and read in panes on pages that are
    /// no object page, and an add-on the registry does not know is drawn as nothing. The
    /// translations come first: the add-ons resolve their labels when they register. The same
    /// two translation files are inlined into the dashboard view as well
    /// (<c>DashboardWidgetScript</c>); registering them twice merges, it does not conflict.
    /// </remarks>
    [Asset("/assets/js/i18n/en.js")]
    [Asset("/assets/js/i18n/de.js")]
    [Asset("/assets/js/editor/addons/common.js")]
    [Asset("/assets/js/editor/addons/object.js")]
    [Asset("/assets/js/editor/addons/list.js")]
    [Asset("/assets/js/editor/addons/count.js")]
    [Asset("/assets/js/editor/addons/news.js")]
    [Asset("/assets/js/editor/addons/children.js")]
    [Asset("/assets/js/editor/addons/legacy.js")]
    [Asset("/assets/js/editor/addons/callout.js")]
    public sealed class IncludeEditorAddonsScript : IInclude
    {
    }
}
