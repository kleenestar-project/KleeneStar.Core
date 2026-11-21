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

using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WebFragment.SecurityLevel
{
    /// <summary>
    /// Represents the add form fragment of a security level.
    /// </summary>
    [Title("kleenestar.core:securitylevel.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.SecurityLevels._classid_.Add>]
    [Cache]
    public sealed class SecurityLevelAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Gets the hidden input carrying the class the security level is created in.
        /// </summary>
        /// <remarks>
        /// The form posts to the security level collection, whose route names no class, so the
        /// class of the page the form was opened from has to travel in the payload - a level
        /// cannot be inserted without it.
        /// </remarks>
        public ControlFormItemInputHidden ClassId { get; } = new()
        {
            Name = _ => nameof(SecurityLevelEntity.ClassId)
        };

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
        public SecurityLevelAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Add(ClassId);
            Add(SecurityLevelName);
            Add(Description);
            Add(Clearance);
            Add(Rank);
            Add(IsDefault);
            Add(SecurityLevelState);

            this.DataService<global::KleeneStar.Core.WWW.Api._1_.SecurityLevels.Index>();
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            // the page the form is shown on is class scoped, so the class it creates the
            // security level in is taken from the request and carried in the hidden input
            var classId = renderContext?.Request?.GetParameter<ClassIdParameter>()?.Value;

            if (!string.IsNullOrWhiteSpace(classId))
            {
                renderContext.SetValue(ClassId, new ControlFormInputValueString(classId));
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
