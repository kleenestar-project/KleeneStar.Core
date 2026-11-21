using KleeneStar.Core.WebAttribute;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Calendar._calendarid_
{
    /// <summary>
    /// Declares the calendar id as the variable segment of this api branch.
    /// </summary>
    /// <remarks>
    /// The segment attribute belongs on the endpoint that stands for the folder itself; the
    /// endpoints beside it inherit the variable segment from here.
    /// </remarks>
    [CalendarIdSegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers that the branch itself carries nothing to retrieve.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A not-found response.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            return new ResponseNotFound();
        }
    }
}
