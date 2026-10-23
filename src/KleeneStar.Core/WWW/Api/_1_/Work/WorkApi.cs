using KleeneStar.Core.WebManager;
using KleeneStar.Core.WWW.Api._1_.Automation;
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WWW.Api._1_.Work
{
    /// <summary>
    /// Shared plumbing of the endpoints a person works through: worklogs, estimates, the
    /// milestone of an object, their own capacity.
    /// </summary>
    internal static class WorkApi
    {
        /// <summary>
        /// The body of a work endpoint. Every value is a string, because a page's form sends
        /// nothing else; durations are written as people type them (<c>1h 30m</c>).
        /// </summary>
        internal sealed class Payload
        {
            /// <summary>Gets or sets the object key.</summary>
            public string Object { get; set; }

            /// <summary>Gets or sets a record id (worklog).</summary>
            public string Id { get; set; }

            /// <summary>Gets or sets when the work began (ISO date or date and time).</summary>
            public string Started { get; set; }

            /// <summary>Gets or sets how long it took.</summary>
            public string Duration { get; set; }

            /// <summary>Gets or sets what was done.</summary>
            public string Comment { get; set; }

            /// <summary>Gets or sets the original estimate.</summary>
            public string Original { get; set; }

            /// <summary>Gets or sets the remaining estimate.</summary>
            public string Remaining { get; set; }

            /// <summary>Gets or sets the milestone id, empty for none.</summary>
            public string Milestone { get; set; }

            /// <summary>Gets or sets a weekly capacity.</summary>
            public string Capacity { get; set; }
        }

        /// <summary>
        /// Reads a moment.
        /// </summary>
        /// <param name="value">An ISO date or date and time; empty for now.</param>
        /// <param name="moment">The moment, in UTC.</param>
        /// <returns><see langword="true"/> when the value is empty or a moment.</returns>
        internal static bool TryParseMoment(string value, out DateTime moment)
        {
            moment = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return false;
            }

            // a bare date means that day; noon keeps it on that day in every time zone
            moment = value.Trim().Length <= 10 ? DateTime.SpecifyKind(parsed.ToLocalTime().Date.AddHours(12), DateTimeKind.Local).ToUniversalTime() : parsed;

            return true;
        }

        /// <summary>
        /// Turns a refusal of the planning manager into a response the forms show.
        /// </summary>
        /// <param name="refusal">The refusal.</param>
        /// <returns><c>403</c> or <c>400</c>, each with a message.</returns>
        internal static IResponse Refuse(PlanningRefusedException refusal)
        {
            if (!refusal.Forbidden)
            {
                return AutomationApi.Refuse(refusal.Message);
            }

            return new ResponseForbidden { Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message = refusal.Message }, AutomationApi.JsonOptions)) }
                .AddHeaderContentType("application/json");
        }
    }
}
