using KleeneStar.Core.WebManager;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WWW.Notifications
{
    /// <summary>
    /// Removes the notification named by the <c>id</c> query parameter and returns to the
    /// center. The URL is <c>/notifications/delete?id={guid}</c>.
    /// </summary>
    [Scope<IScopeGeneral>]
    public sealed class Delete : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly INotificationCenterManager _notificationCenterManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="notificationCenterManager">
        /// The manager used to remove the addressed notification. Cannot be null.
        /// </param>
        public Delete(INotificationCenterManager notificationCenterManager)
        {
            _notificationCenterManager = notificationCenterManager;
        }

        /// <summary>
        /// Processing of the resource: removes the addressed notification, then redirects to
        /// the center.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext?.Request;

            if (Guid.TryParse(request?.GetParameter<ParameterId>()?.Value, out var notificationId))
            {
                // the manager only touches rows owned by the calling identity, so a foreign id
                // removes nothing
                _notificationCenterManager.Remove(request, notificationId);
            }

            throw new RedirectException(CoreHub.GetUri<Index>());
        }
    }
}
