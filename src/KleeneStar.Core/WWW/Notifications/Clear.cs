using KleeneStar.Core.WebManager;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;

namespace KleeneStar.Core.WWW.Notifications
{
    /// <summary>
    /// Removes every notification of the calling identity and returns to the center. The URL
    /// is <c>/notifications/clear</c>.
    /// </summary>
    [Scope<IScopeGeneral>]
    public sealed class Clear : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly INotificationCenterManager _notificationCenterManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="notificationCenterManager">
        /// The manager used to remove the notifications. Cannot be null.
        /// </param>
        public Clear(INotificationCenterManager notificationCenterManager)
        {
            _notificationCenterManager = notificationCenterManager;
        }

        /// <summary>
        /// Processing of the resource: empties the center, then redirects back to it.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            _notificationCenterManager.Clear(renderContext?.Request);

            throw new RedirectException(CoreHub.GetUri<Index>());
        }
    }
}
