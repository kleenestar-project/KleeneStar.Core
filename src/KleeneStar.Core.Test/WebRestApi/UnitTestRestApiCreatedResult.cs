using KleeneStar.Core.WebRestApi;
using System.Text;
using System.Text.Json;

namespace KleeneStar.Core.Test.WebRestApi
{
    /// <summary>
    /// Tests the answer of a create that the success action <c>follow-uri</c> follows.
    /// </summary>
    public class UnitTestRestApiCreatedResult
    {
        /// <summary>
        /// The address stands at the top level, where the rest form reads it; the record stays
        /// under <c>data</c>; there is no message, which would keep the dialog open.
        /// </summary>
        [Fact]
        public void AnswerCarriesTheAddressAtTheTopLevel()
        {
            var result = new RestApiCreatedResult
            {
                Uri = "/kleenestar/issue/sd-1",
                Data = new { created = true, key = "SD-1" }
            };

            var response = result.ToResponse();
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString((byte[])response.Content));

            Assert.Equal("/kleenestar/issue/sd-1", json.RootElement.GetProperty("uri").GetString());
            Assert.True(json.RootElement.GetProperty("data").GetProperty("created").GetBoolean());
            Assert.False(json.RootElement.TryGetProperty("message", out _));
        }
    }
}
