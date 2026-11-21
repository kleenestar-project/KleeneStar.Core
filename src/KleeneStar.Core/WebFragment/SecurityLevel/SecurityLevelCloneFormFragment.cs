using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.SecurityLevel
{
    /// <summary>
    /// Represents the clone form fragment of a security level.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.SecurityLevel._securitylevelid_.Clone>]
    [Cache]
    public sealed class SecurityLevelCloneFormFragment : FragmentControlDataFormClone
    {
        /// <summary>
        /// Gets the input for the name of the security level.
        /// </summary>
        public ControlFormItemInputText SecurityLevelName { get; } = SecurityLevelFormItems.CreateName();

        /// <summary>
        /// Gets the input for the description of the security level.
        /// </summary>
        public ControlFormItemInputText Description { get; } = SecurityLevelFormItems.CreateDescription();

        /// <summary>
        /// Gets the multi-select naming the groups the level clears.
        /// </summary>
        public ControlDataFormItemInputSelection Clearance { get; } = SecurityLevelFormItems.CreateClearance();

        /// <summary>
        /// Gets the input for the rank of the security level.
        /// </summary>
        public ControlFormItemInputText Rank { get; } = SecurityLevelFormItems.CreateRank();

        /// <summary>
        /// Gets the switch marking the level as the one new objects start on.
        /// </summary>
        public ControlFormItemInputCheck IsDefault { get; } = SecurityLevelFormItems.CreateIsDefault();

        /// <summary>
        /// Gets the selection for the state of the security level.
        /// </summary>
        public ControlDataFormItemInputSelection SecurityLevelState { get; } = SecurityLevelFormItems.CreateState();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SecurityLevelCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(SecurityLevelName);
            Add(Description);
            Add(Clearance);
            Add(Rank);
            Add(IsDefault);
            Add(SecurityLevelState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.SecurityLevels.Index>();

            ItemId = renderContext =>
            {
                var securityLevelId = renderContext.Request.GetParameter<SecurityLevelIdParameter>();
                return securityLevelId?.Value?.ToString();
            };
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
