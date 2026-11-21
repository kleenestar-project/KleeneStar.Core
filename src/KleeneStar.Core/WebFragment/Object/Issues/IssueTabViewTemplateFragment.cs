using KleeneStar.Model.Entities;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Tab template of the issues view inside the workspace tab control: the curated
    /// list of the most recently updated issues with search, personal quickfilters
    /// (starred, assigned to me, created by me, archived), and pagination. The content
    /// is contributed by the <see cref="IssueTabViewFragment"/> scoped to this template.
    /// </summary>
    [Section<SectionTabViewPrimary>]
    [Scope<IssueTabFragment>]
    [Order(0)]
    [Cache]
    public sealed class IssueTabViewTemplateFragment : FragmentControlDataTabTemplate, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabViewTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // icon, name and description feed the template picker of the tab control;
            // the control emits the raw values, so the i18n keys are translated here
            Icon = _ => ObjectViewType.Issues.Icon();
            Name = renderContext => I18N.Translate(renderContext, ObjectViewType.Issues.Text());
            Description = renderContext => I18N.Translate(renderContext, ObjectViewType.Issues.Description());
        }
    }
}
