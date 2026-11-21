using KleeneStar.Core.WebManager;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW.Notifications
{
    /// <summary>
    /// Marks every notification of the calling identity as seen and returns to the center.
    /// The URL is <c>/notifications/readall</c>.
    /// </summary>
    [Scope<IScopeGeneral>]
    public sealed class ReadAll : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly INotificationCenterManager _notificationCenterManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="notificationCenterManager">
        /// The manager used to mark the notifications. Cannot be null.
        /// </param>
        public ReadAll(INotificationCenterManager notificationCenterManager)
        {
            _notificationCenterManager = notificationCenterManager;
        }

        /// <summary>
        /// Processing of the resource: marks everything as seen, then redirects to the center.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            _notificationCenterManager.MarkAllRead(renderContext?.Request);

            throw new RedirectException(CoreHub.GetUri<Index>());
        }
    }
}
