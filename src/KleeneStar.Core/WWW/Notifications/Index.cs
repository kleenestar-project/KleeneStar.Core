using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Notifications
{
    /// <summary>
    /// The notification center: every in-app notification addressed to the calling identity,
    /// newest first.
    /// </summary>
    /// <remarks>
    /// The page carries the explanation; the table with its search, quick filters and paging is
    /// contributed by <see cref="WebFragment.Notification.NotificationViewFragment"/> and the
    /// fragments scoped to it. The declared domain is what makes the table refresh by itself
    /// when a notification arrives or is read.
    /// </remarks>
    [Title("kleenestar.core:notification.center.title")]
    [WebIcon<IconBell>]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.UserNotification>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext, "kleenestar.core:notification.center.title");

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:notification.center.description"),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });
        }
    }
}
