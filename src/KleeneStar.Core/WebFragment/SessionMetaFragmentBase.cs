using System;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment
{
    /// <summary>
    /// Tells the page when its sign-in's access token ends, for <c>sessionrefresh.js</c> to
    /// renew it in time. Renders nothing itself.
    /// </summary>
    /// <remarks>
    /// The access token is in a cookie the page cannot read, and WebExpress renews nothing on
    /// its own, so without this a signed-in user turned anonymous when the token ran out. The
    /// deadline is written into <c>&lt;meta name="kleenestar.session"&gt;</c> (unix seconds) and
    /// the refresh endpoint into <c>kleenestar.session.refresh</c> - only for a sign-in: a
    /// personal access token is not renewed, and an anonymous page has nothing to keep. A
    /// fragment is registered per application, so the core and the portal each derive one.
    /// </remarks>
    public abstract class SessionMetaFragmentBase : FragmentControlPanel
    {
        /// <summary>
        /// The name of the meta element carrying the deadline.
        /// </summary>
        public const string MetaName = "kleenestar.session";

        /// <summary>
        /// The name of the meta element carrying the refresh endpoint.
        /// </summary>
        public const string RefreshMetaName = "kleenestar.session.refresh";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected SessionMetaFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Writes the deadline of the caller's sign-in into the page head, and renders nothing.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns><see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var credential = CoreHub.SessionManager?.GetCurrentCredential(renderContext?.Request);

            if (credential is null || credential.Personal || credential.Expires is not { } expires || visualTree is null)
            {
                return null;
            }

            var deadline = new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc)).ToUnixTimeSeconds();
            var application = credential.Application?.ApplicationId;

            visualTree.AddMeta(MetaName, deadline.ToString(System.Globalization.CultureInfo.InvariantCulture));
            visualTree.AddMeta
            (
                RefreshMetaName,
                string.IsNullOrEmpty(application)
                    ? "/api/auth/refresh"
                    : "/api/auth/refresh?application=" + Uri.EscapeDataString(application)
            );

            return null;
        }
    }
}
