using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Maintenance
{
    /// <summary>
    /// Shows the maintenance instruction text as a toast at the top of every page.
    /// </summary>
    /// <remarks>
    /// The fragment is gated by <see cref="MaintenanceNoticeCondition"/>, so it contributes nothing
    /// while no announcement is active. The text is read on each render rather than captured in the
    /// constructor, because the fragment instance is cached while the notice behind it is not.
    /// The text is written in the prose editor, so what is stored is the editor's document rather
    /// than a sentence; it is handed to <see cref="ControlContent"/>, the framework's reading view
    /// of such a value - printed as text, the toast showed the serialization. The framework ships
    /// no fragment base for that control, so this one checks its context itself, the way
    /// <see cref="FragmentControlText"/> does.
    /// </remarks>
    [Section<SectionToastNotificationPrimary>]
    [Scope<IScopeGeneral>]
    [Scope<IScopeAdmin>]
    [Condition<MaintenanceNoticeCondition>]
    [Cache]
    public sealed class MaintenanceToastFragment : ControlContent, IFragmentControl<ControlContent>
    {
        /// <summary>
        /// Returns the context of the fragment.
        /// </summary>
        public IFragmentContext FragmentContext { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public MaintenanceToastFragment(IFragmentContext fragmentContext)
            : base(fragmentContext?.FragmentId?.ToString()?.Replace(".", "-"))
        {
            FragmentContext = fragmentContext;

            Content = _ => CoreHub.MaintenanceManager?.GetMaintenance()?.Message;
            Format = _ => TypeFormatContent.RichText;
        }

        /// <summary>
        /// Converts the fragment to an HTML representation, or to nothing when its context
        /// does not apply to the request.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Check(renderContext?.Request))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
