using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Represents a control fragment that provides a button link for adding a new object within the workspace.
    /// </summary>
    [Section<SectionAppQuickcreatePreferences>]
    [Condition<global::KleeneStar.Core.WebIdentity.SignedInCondition>]
    [Scope<IScopeGeneral>]
    [Cache]
    public sealed class ObjectAddButtonFragment : FragmentControlSplitButtonItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation. 
        /// Cannot be null.
        /// </param>
        public ObjectAddButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.core:object.add.label";
            Icon = _ => new IconPlus();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorBackground(TypeColorBackground.Highlight);
            PrimaryAction = _ => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Add>(),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
