using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings.Audit
{
    /// <summary>
    /// One audit event in full, addressed by <c>?event={sequence|id}</c>. Fetched into the
    /// dialog opened from a row of the audit table, and readable on its own as the deep link to
    /// a single event.
    /// </summary>
    /// <remarks>
    /// The event is a page of its own rather than content the table renders up front, for the
    /// same reason the commit detail is: rendering every event's deltas to draw a list of a
    /// thousand events would make the list cost what the whole log costs.
    /// </remarks>
    [WebIcon<IconShieldHalved>]
    [Title("kleenestar.core:audit.detail.title")]
    [Scope<IScopeAdmin>]
    [Cache]
    public sealed class Detail : IPage<VisualTreeWebApp>, IScopeAdmin, IScope
    {
        /// <summary>
        /// The name of the query parameter naming the event: its position in the sequence or its
        /// id.
        /// </summary>
        public const string EventParameter = "event";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Detail()
        {
        }

        /// <summary>
        /// Processing of the resource. The content is contributed entirely by the scoped detail
        /// fragment, so no work is required here.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
        }
    }
}
