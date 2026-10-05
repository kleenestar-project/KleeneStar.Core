using KleeneStar.Core.WebInsight;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebScope;

namespace KleeneStar.Core.WebFragment.Insight.Views
{
    /// <summary>
    /// Base of the tab templates of an insight: the icon, the name and the description the
    /// template picker offers are those of the view type the template draws, read from the
    /// <see cref="InsightViewTypeCatalog"/>, so a type is described in one place.
    /// </summary>
    public abstract class InsightTabTemplateFragmentBase : FragmentControlDataTabTemplate, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        /// <param name="key">The key of the view type the template draws.</param>
        protected InsightTabTemplateFragmentBase(IFragmentContext fragmentContext, string key)
            : base(fragmentContext)
        {
            // the picker emits the raw values, so the i18n keys are translated here
            Icon = _ => InsightViewTypeCatalog.Get(key)?.Icon;
            Name = renderContext => I18N.Translate(renderContext, InsightViewTypeCatalog.Get(key)?.Label ?? key);
            Description = renderContext => I18N.Translate(renderContext, InsightViewTypeCatalog.Get(key)?.Description ?? string.Empty);
        }
    }
}
