using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// What the free-form JSON endpoints of the workspace administration share - approvals,
    /// catalogs, planning, webhooks, backups, and the automation plugin's: the workspace of the
    /// route and the demand to administer it, reading a body, answering JSON or a refusal.
    /// Public, so the plugins answer the same way.
    /// </summary>
    /// <remarks>
    /// The pages' <c>ControlModalForm</c>s post <c>FormData</c>, not JSON; <see cref="Read{T}"/>
    /// maps either onto a payload of strings.
    /// </remarks>
    public static class JsonApi
    {
        /// <summary>
        /// The serializer options of the answers and payloads: camel case, read without regard to case.
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Resolves the workspace of the route and demands its administration.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="workspace">The workspace, when the route names one.</param>
        /// <returns><see langword="null"/> when the caller may go on; otherwise the refusal (404, 403).</returns>
        public static IResponse Authorize(IRequest request, out Workspace workspace)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            workspace = string.IsNullOrWhiteSpace(key) ? null : CoreHub.WorkspaceManager.GetWorkspaceByKey(key);

            if (workspace is null)
            {
                return new ResponseNotFound();
            }

            return ContentAuthorization.MayAdministerWorkspace(workspace.Id, request)
                ? null
                : new ResponseForbidden();
        }

        /// <summary>
        /// Answers a payload as JSON.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <returns>The response.</returns>
        public static IResponse Json(object payload)
        {
            return new ResponseOK { Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions)) }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Answers a refusal with a message the client shows (400).
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The response.</returns>
        public static IResponse Refuse(string message)
        {
            return new ResponseBadRequest { Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message }, JsonOptions)) }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Reads the body of a request - JSON, or the parsed form parameters.
        /// </summary>
        /// <typeparam name="T">The payload type.</typeparam>
        /// <param name="request">The request.</param>
        /// <returns>The payload, or the default when there is none or it does not parse.</returns>
        public static T Read<T>(IRequest request)
        {
            var content = (request as Request)?.Content;

            if (content is not { Length: > 0 })
            {
                return default;
            }

            try
            {
                if (request.Header?.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) != false)
                {
                    return JsonSerializer.Deserialize<T>(content, JsonOptions);
                }

                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var parameter in request.Parameters ?? [])
                {
                    var key = parameter switch
                    {
                        IParameterStatic known => known.GetKey(),
                        IParameterDynamic named => named.Key,
                        _ => null
                    };

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        fields[key] = parameter.Value;
                    }
                }

                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(fields), JsonOptions);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        /// <summary>
        /// Parses an id; blank, malformed and the empty guid are no id.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The id, or <see langword="null"/>.</returns>
        public static Guid? ParseId(string value)
        {
            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }

        /// <summary>
        /// A payload that names a record by its id.
        /// </summary>
        public sealed class IdPayload
        {
            /// <summary>Gets or sets the id.</summary>
            public string Id { get; set; }
        }
    }
}
