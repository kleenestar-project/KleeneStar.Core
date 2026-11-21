using WebExpress.WebApp.WebData;

namespace KleeneStar.Core.WebFragment.Object.Issues
{
    /// <summary>
    /// The type-safe identity of the issue calendar resource shared inside the scheduler tab's
    /// <see cref="WebExpress.WebApp.WebControl.ControlViewState"/>.
    /// </summary>
    /// <remarks>
    /// The calendar renders it, and the search and the quickfilter of the tab header write
    /// into the state it maps onto the endpoint's <c>q</c> and <c>f</c> parameters — so a chip
    /// or a search term re-queries the calendar without a bind wire, which the schedule
    /// control has none of. All three resolve the ViewState by this type rather than by
    /// ancestry, so they may sit in sibling fragments.
    /// </remarks>
    public sealed class IssueSchedulerResource : IDataResource
    {
    }
}
