using KleeneStar.Core.WebRestApi;
using WebExpress.WebApp.WebRestApi;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Provides unit tests for <see cref="RestApiCrudFormDataAvatarExtensions.Detach"/> — the
    /// step that takes the inline picture out of a CRUD payload before the binder can see it.
    /// </summary>
    /// <remarks>
    /// This is the half that was missing from four of the six avatar dialogs. Forgetting it is
    /// silent: the binder hands the data url to <c>RestValueConverterImageIcon</c>, the icon
    /// collapses to <c>http:///</c>, and the request still answers 200 — so the picture is lost
    /// with nothing anywhere reporting a fault. Hence a test on the removal itself rather than
    /// only on the parsing beside it (<c>UnitTestImagePayload</c>).
    /// </remarks>
    public class UnitTestAvatarDetach
    {
        /// <summary>
        /// The shape the avatar control actually posts.
        /// </summary>
        private const string Submitted =
            "file:portrait.png;data:image/png;base64," +
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        /// <summary>
        /// Builds a payload with the lower-cased keys the payload parser produces.
        /// </summary>
        /// <param name="entries">The entries to put in.</param>
        /// <returns>The payload.</returns>
        private static RestApiCrudFormData Payload(params (string Key, object Value)[] entries)
        {
            var payload = new RestApiCrudFormData();

            foreach (var (key, value) in entries)
            {
                payload[key] = value;
            }

            return payload;
        }

        /// <summary>
        /// Verifies the case that matters: the picture is handed back and is <b>gone</b> from
        /// the payload, so <c>base.Update</c> never binds it. The field name is passed as it is
        /// declared on the entity while the payload carries it lower-cased.
        /// </summary>
        [Fact]
        public void Detach_RemovesThePictureFromThePayload()
        {
            var payload = Payload(("icon", Submitted), ("name", "Specification"));

            var sent = payload.Detach("Icon", out var avatar);

            Assert.True(sent);
            Assert.Equal(Submitted, avatar);

            // the binder must not see it any more — this is the whole point of the call
            Assert.False(payload.ContainsKey("icon"));
            Assert.False(payload.ContainsKey("Icon"));

            // everything else the form submitted is left alone
            Assert.Equal("Specification", payload["name"]);
        }

        /// <summary>
        /// Verifies that a payload not carrying the field at all is reported as such and is not
        /// touched. "Absent" means the form does not edit the avatar, which is a different
        /// instruction from an empty value and must not be confused with it.
        /// </summary>
        [Fact]
        public void Detach_AbsentField_ReportsNothingSent()
        {
            var payload = Payload(("name", "Specification"));

            var sent = payload.Detach("Icon", out var avatar);

            Assert.False(sent);
            Assert.Null(avatar);
            Assert.Single(payload);
        }

        /// <summary>
        /// Verifies that an empty value is reported as sent: it is how the dialog says the
        /// picture was removed, and an endpoint that read it as "absent" would leave the old
        /// picture in place against the user's instruction.
        /// </summary>
        [Fact]
        public void Detach_EmptyValue_IsSentAndMeansRemoval()
        {
            var payload = Payload(("icon", ""));

            var sent = payload.Detach("Icon", out var avatar);

            Assert.True(sent);
            Assert.Equal("", avatar);
            Assert.Empty(payload);
        }

        /// <summary>
        /// Verifies that a payload carrying the field under its declared casing is found too —
        /// the lower-casing is a property of the JSON parser, not a guarantee every caller has.
        /// </summary>
        [Fact]
        public void Detach_ExactlyCasedKey_IsFound()
        {
            var payload = Payload(("Icon", Submitted));

            var sent = payload.Detach("Icon", out var avatar);

            Assert.True(sent);
            Assert.Equal(Submitted, avatar);
            Assert.Empty(payload);
        }

        /// <summary>
        /// Verifies that the extension answers false rather than throwing when there is nothing
        /// to read.
        /// </summary>
        [Fact]
        public void Detach_NoPayloadOrNoName_ReportsNothingSent()
        {
            Assert.False(((RestApiCrudFormData)null).Detach("Icon", out var missing));
            Assert.Null(missing);

            Assert.False(Payload(("icon", Submitted)).Detach(null, out var unnamed));
            Assert.Null(unnamed);
        }
    }
}
