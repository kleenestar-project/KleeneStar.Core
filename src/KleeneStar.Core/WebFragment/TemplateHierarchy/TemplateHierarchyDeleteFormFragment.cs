using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.TemplateHierarchy
{
    /// <summary>
    /// The dialog that deletes a template hierarchy; the templates it references stay.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.TemplateHierarchy._hierarchyid_.Delete>]
    [Cache]
    public sealed class TemplateHierarchyDeleteFormFragment : FragmentControlDataFormDelete
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public TemplateHierarchyDeleteFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.TemplateHierarchies.Index>();
            ItemId = renderContext => renderContext.Request.GetParameter<TemplateHierarchyIdParameter>()?.Value?.ToString();
        }
    }
}
