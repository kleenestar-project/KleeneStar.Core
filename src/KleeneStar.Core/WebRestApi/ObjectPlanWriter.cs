using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebStatusPage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Writes a span a user dragged on the timeline or the calendar back into the date fields
    /// of an object's class. Shared by the Gantt and the scheduler endpoints so the two cannot
    /// drift apart on what a move is allowed to change.
    /// </summary>
    internal static class ObjectPlanWriter
    {
        /// <summary>
        /// Applies a moved span to an object.
        /// </summary>
        /// <remarks>
        /// An edge is only moved when the class models a field to keep it in. An edge the
        /// class does not model is not a plan datum at all — it is the object's creation
        /// instant, which <see cref="ObjectBoardProjection.ResolvePlan"/> stands in with — so a
        /// request that moves it is refused rather than half-applied. Answering success to a
        /// drag that is dropped would leave the bar where the user let go until the next
        /// reload silently put it back.
        /// </remarks>
        /// <param name="entity">The object being moved.</param>
        /// <param name="context">The board context of the object's class.</param>
        /// <param name="start">The requested start, or <see langword="null"/> when unchanged.</param>
        /// <param name="end">The requested end, or <see langword="null"/> when unchanged.</param>
        /// <returns>
        /// <see langword="true"/> when the move was persisted; <see langword="false"/> when it
        /// asked for an edge the class cannot store.
        /// </returns>
        public static bool TryApply(Model.Entities.Object entity, ObjectBoardClassContext context, DateTime? start, DateTime? end)
        {
            if (entity is null || start is null)
            {
                return false;
            }

            var (currentStart, currentEnd) = ObjectBoardProjection.ResolvePlan(entity, context);
            var requestedEnd = end ?? start;

            var movesStart = start.Value.Date != currentStart.Date;
            var movesEnd = requestedEnd.Value.Date != currentEnd.Date;

            if (movesStart && context?.StartDateField is null)
            {
                return false;
            }

            if (movesEnd && context?.EndDateField is null)
            {
                return false;
            }

            if (!movesStart && !movesEnd)
            {
                // an idempotent drop: nothing to write, but nothing refused either
                return true;
            }

            var now = DateTime.UtcNow;

            // dragging a bar moves one or both ends of the plan in a single gesture, so the
            // history records it as one edit
            using (CoreHub.CommitManager.BeginCommit(entity.Id, CommitType.Updated, entity.UpdaterId ?? Guid.Empty))
            {
                if (movesStart)
                {
                    SetFieldValue(entity.Id, context.StartDateField.Id, Format(start.Value), now);
                }

                if (movesEnd)
                {
                    SetFieldValue(entity.Id, context.EndDateField.Id, Format(requestedEnd.Value), now);
                }

                entity.Updated = now;
                CoreHub.ObjectManager.Update(entity);
            }

            return true;
        }

        /// <summary>
        /// Writes a field value of an object, inserting it when the object carries none yet.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <param name="fieldId">The field id.</param>
        /// <param name="data">The value payload.</param>
        /// <param name="timestamp">The mutation timestamp.</param>
        private static void SetFieldValue(Guid objectId, Guid fieldId, string data, DateTime timestamp)
        {
            var existing = CoreHub.ValueManager.GetValue(objectId, fieldId);

            if (existing is null)
            {
                CoreHub.ValueManager.Add(new Value
                {
                    ObjectId = objectId,
                    FieldId = fieldId,
                    Data = data,
                    Created = timestamp,
                    Updated = timestamp
                });

                return;
            }

            existing.Data = data;
            existing.Updated = timestamp;
            CoreHub.ValueManager.Update(existing);
        }

        /// <summary>
        /// Formats a date as the ISO day a date field is persisted as.
        /// </summary>
        /// <param name="value">The date.</param>
        /// <returns>The formatted date.</returns>
        private static string Format(DateTime value)
        {
            return value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses an ISO day from the timeline / calendar wire shape.
        /// </summary>
        /// <param name="value">The formatted date.</param>
        /// <returns>The date, or <see langword="null"/> when absent or unparsable.</returns>
        public static DateTime? ParseDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed.Date
                : null;
        }

        /// <summary>
        /// Builds the <c>409 Conflict</c> a refused move is answered with, naming the edge the
        /// class models no field for so the failure is actionable rather than a bare status.
        /// </summary>
        /// <remarks>
        /// The body is JSON with an <c>error</c> member, matching the shape the portal's
        /// conflicts already use. The message the user sees comes from the service descriptor's
        /// <c>MapError(409, …)</c> instead, which the client resolves through its own
        /// internationalization — this text is for the log and for an API caller.
        /// </remarks>
        /// <param name="entity">The object whose move was refused.</param>
        /// <param name="context">The board context of the object's class.</param>
        /// <returns>The conflict response.</returns>
        public static IResponse Conflict(Model.Entities.Object entity, ObjectBoardClassContext context)
        {
            var missing = context?.StartDateField is null && context?.EndDateField is null
                ? "a start and an end date field"
                : context?.StartDateField is null ? "a start date field" : "an end date field";

            var payload = new
            {
                error = $"'{entity?.Key}' cannot be rescheduled that way: its class '{context?.Class?.Name}' models no {missing}.",
                key = entity?.Key,
                @class = context?.Class?.Name
            };

            return new ResponseConflict
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// The serializer profile of the conflict body: camelCase, matching every other JSON
        /// the object endpoints emit.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
