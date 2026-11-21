using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebSection;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// Tab template for the Scrum view of the workspace objects index — the sprint and the
    /// backlog in one view.
    /// </summary>
    /// <remarks>
    /// It replaces the former sprint and backlog templates, which showed the same sprint
    /// header twice and forced a tab switch to get from the board to the backlog. Both
    /// <see cref="Model.Entities.ObjectViewType.ScrumSprint"/> and
    /// <see cref="Model.Entities.ObjectViewType.ScrumBacklog"/> map here, so a tab persisted
    /// as either type renders this view and no view type had to be appended to the
    /// ordinal-persisted enum.
    ///
    /// The content is contributed by the fragments scoped to this template: the team
    /// workload (order 0), the sprint burn-down (order 1) and the
    /// <see cref="IssueTabScrumViewFragment"/> view control (order 2), which carries the
    /// board and the backlog as its two switchable views.
    /// </remarks>
    [Section<SectionTabViewPrimary>]
    [Scope<IssueTabFragment>]
    [Order(4)]
    [Cache]
    public sealed class IssueTabScrumTemplateFragment : FragmentControlDataTabTemplate, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public IssueTabScrumTemplateFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // icon, name and description feed the template picker of the tab control;
            // the control emits the raw values, so the i18n keys are translated here
            Icon = _ => new IconBolt();
            Name = renderContext => I18N.Translate(renderContext, "kleenestar.core:object.view.scrum.label");
            Description = renderContext => I18N.Translate(renderContext, "kleenestar.core:object.view.scrum.description");
        }
    }
}
