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
    /// Tab template for the <see cref="ObjectViewType.Gantt"/> view of the workspace objects
    /// index. The timeline content is contributed by the <see cref="IssueTabGanttFragment"/>
    /// scoped to this template.
    /// </summary>
    [Section<SectionTabViewPrimary>]
    [Scope<IssueTabFragment>]
    [Order(5)]
    [Cache]
    public sealed class IssueTabGanttTemplateFragment : FragmentControlDataTabTemplate, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabGanttTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // icon, name and description feed the template picker of the tab control;
            // the control emits the raw values, so the i18n keys are translated here
            Icon = _ => ObjectViewType.Gantt.Icon();
            Name = renderContext => I18N.Translate(renderContext, ObjectViewType.Gantt.Text());
            Description = renderContext => I18N.Translate(renderContext, ObjectViewType.Gantt.Description());
        }
    }
}
