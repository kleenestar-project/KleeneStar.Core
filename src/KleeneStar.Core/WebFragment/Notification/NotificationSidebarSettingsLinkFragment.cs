using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Notification
{
    /// <summary>
    /// Sidebar entry leading to the profile page where the user decides which events reach
    /// them and on which channel.
    /// </summary>
    /// <remarks>
    /// The center answers "what happened"; that page answers "what should reach me at all".
    /// Somebody looking at a list they consider too long is one click away from changing it.
    /// </remarks>
    [Section<SectionSidebarSecondary>]
    [Scope<global::KleeneStar.Core.WWW.Notifications.Index>]
    [Order(0)]
    [Cache]
    public sealed class NotificationSidebarSettingsLinkFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context associated with the fragment.</param>
        public NotificationSidebarSettingsLinkFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Icon = _ => new IconGear();
            Text = _ => "kleenestar.core:notification.center.settings";
            Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Profile.Notifications>();
        }

        /// <summary>
        /// Convert the fragment to HTML.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }
    }
}
