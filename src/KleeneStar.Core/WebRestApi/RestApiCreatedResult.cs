using System.Text;
using System.Text.Json;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The answer of a create or clone that names the page of the new record.
    /// </summary>
    /// <remarks>
    /// The JSON answer is <c>{ "data": …, "uri": … }</c>. <c>uri</c> stands at the top level,
    /// where the framework's rest form reads it for <see cref="TypeRestFormSuccess.FollowUri"/>;
    /// <c>data</c> carries the record as <see cref="RestApiCrudResultCreate.Data"/> does. The
    /// answer has no <c>message</c>, because a message keeps the dialog open to show it.
    /// </remarks>
    public sealed class RestApiCreatedResult : RestApiCrudResultCreate
    {
        /// <summary>
        /// The serializer options of the answer.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Gets or sets the address of the page of the new record.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Converts the result into its JSON response.
        /// </summary>
        /// <returns>The response.</returns>
        public override Response ToResponse()
        {
            var json = JsonSerializer.Serialize(new { data = Data, uri = Uri }, JsonOptions);

            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(json)
            }
                .AddHeaderContentType("application/json");
        }
    }
}
