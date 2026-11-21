using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object.Assets
{
    /// <summary>
    /// Tab template of the curated asset view inside the workspace asset tab control: the
    /// most recently updated assets with search, personal quickfilters (starred, assigned
    /// to me, created by me, archived), and pagination. The content is contributed by the
    /// <see cref="AssetTabViewFragment"/> scoped to this template.
    /// </summary>
    [Section<SectionTabViewPrimary>]
    [Scope<AssetTabFragment>]
    [Order(0)]
    [Cache]
    public sealed class AssetTabViewTemplateFragment : FragmentControlDataTabTemplate, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AssetTabViewTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // icon, name and description feed the template picker of the tab control;
            // the control emits the raw values, so the i18n keys are translated here
            Icon = _ => ObjectViewType.Assets.Icon();
            Name = renderContext => I18N.Translate(renderContext, ObjectViewType.Assets.Text());
            Description = renderContext => I18N.Translate(renderContext, ObjectViewType.Assets.Description());
        }
    }
}
