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
    /// Tab template for the <see cref="ObjectViewType.Scheduler"/> view of the workspace
    /// objects index. The calendar content is contributed by the
    /// <see cref="IssueTabSchedulerFragment"/> scoped to this template.
    /// </summary>
    [Section<SectionTabViewPrimary>]
    [Scope<IssueTabFragment>]
    [Order(6)]
    [Cache]
    public sealed class IssueTabSchedulerTemplateFragment : FragmentControlDataTabTemplate, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabSchedulerTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // icon, name and description feed the template picker of the tab control;
            // the control emits the raw values, so the i18n keys are translated here
            Icon = _ => ObjectViewType.Scheduler.Icon();
            Name = renderContext => I18N.Translate(renderContext, ObjectViewType.Scheduler.Text());
            Description = renderContext => I18N.Translate(renderContext, ObjectViewType.Scheduler.Description());
        }
    }
}
