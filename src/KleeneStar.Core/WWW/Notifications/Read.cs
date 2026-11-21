using KleeneStar.Core.WebManager;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WWW.Notifications
{
    /// <summary>
    /// Marks the notification named by the <c>id</c> query parameter as seen and continues to
    /// whatever it was about. The URL is <c>/notifications/read?id={guid}</c>.
    /// </summary>
    /// <remarks>
    /// This is the target of the notification itself rather than a separate button: opening a
    /// notification is what marks it read, and the redirect then lands on the object it
    /// announced. A notification without a target returns to the center.
    /// </remarks>
    [Scope<IScopeGeneral>]
    public sealed class Read : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        private readonly INotificationCenterManager _notificationCenterManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="notificationCenterManager">
        /// The manager used to resolve and mark the addressed notification. Cannot be null.
        /// </param>
        public Read(INotificationCenterManager notificationCenterManager)
        {
            _notificationCenterManager = notificationCenterManager;
        }

        /// <summary>
        /// Processing of the resource: marks the addressed notification as seen, then
        /// redirects to what it was about.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext?.Request;
            var id = request?.GetParameter<ParameterId>()?.Value;
            var target = request?.GetParameter("target")?.Value;

            if (Guid.TryParse(id, out var notificationId))
            {
                // the manager only touches rows owned by the calling identity, so a foreign id
                // marks nothing
                _notificationCenterManager.MarkRead(request, notificationId);
            }

            throw new RedirectException(ResolveTarget(target));
        }

        /// <summary>
        /// Returns where to go after the notification was marked as seen.
        /// </summary>
        /// <remarks>
        /// Only a path of this host is accepted. The target travels through the query string,
        /// so anything else would turn the link into an open redirect — a notification that
        /// forwards to a foreign site is exactly the shape a phishing link wants.
        /// </remarks>
        /// <param name="target">The target carried by the link, or <see langword="null"/>.</param>
        /// <returns>The URI to redirect to.</returns>
        private static IUri ResolveTarget(string target)
        {
            if (!string.IsNullOrWhiteSpace(target) &&
                target.StartsWith('/') &&
                !target.StartsWith("//", StringComparison.Ordinal))
            {
                return new UriEndpoint(target);
            }

            return CoreHub.GetUri<Index>();
        }
    }
}
