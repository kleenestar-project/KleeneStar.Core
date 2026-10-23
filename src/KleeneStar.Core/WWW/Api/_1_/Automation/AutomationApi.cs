using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;

namespace KleeneStar.Core.WWW.Api._1_.Automation
{
    /// <summary>
    /// Shared plumbing of the automation endpoints: the workspace the route names, the gate, the
    /// JSON shapes.
    /// </summary>
    /// <remarks>
    /// Rules act on every object of a workspace on its administrators' authority, so reading and
    /// writing them - and running a failed job again - is workspace administration
    /// (<see cref="ContentAuthorization.MayAdministerWorkspace"/>), the same demand the
    /// automation page makes.
    /// </remarks>
    internal static class AutomationApi
    {
        /// <summary>
        /// The serializer profile of the automation endpoints.
        /// </summary>
        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Resolves the workspace the route names and checks that the caller administers it.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="workspace">The workspace, when the caller may proceed.</param>
        /// <returns>The refusal, or <see langword="null"/> when the caller may proceed.</returns>
        internal static IResponse Authorize(IRequest request, out Workspace workspace)
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
        /// Wraps a payload into a JSON response.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <returns>The response.</returns>
        internal static IResponse Json(object payload)
        {
            return new ResponseOK { Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions)) }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Refuses a request with a reason the caller is meant to read - the shape the client
        /// service layer keeps from a failed write and the forms show.
        /// </summary>
        /// <param name="message">The reason.</param>
        /// <returns>The response.</returns>
        internal static IResponse Refuse(string message)
        {
            return new ResponseBadRequest { Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message }, JsonOptions)) }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Reads the body: a JSON document from a client, or the fields of a page's form.
        /// </summary>
        /// <remarks>
        /// The pages' forms submit <c>FormData</c>, whose fields the server has already parsed
        /// into the request's parameters; they are mapped onto the payload by name, so one
        /// endpoint serves the pages and other systems alike.
        /// </remarks>
        /// <typeparam name="T">The payload type, whose properties are strings.</typeparam>
        /// <param name="request">The request.</param>
        /// <returns>The payload, or <see langword="default"/> when there is none.</returns>
        internal static T Read<T>(IRequest request)
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
        /// Projects a rule onto its wire shape.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <returns>The DTO.</returns>
        internal static object ToDto(AutomationRule rule)
        {
            return new
            {
                id = rule.Id,
                name = rule.Name,
                description = rule.Description,
                state = rule.State.ToString().ToLowerInvariant(),
                classId = rule.ClassId,
                trigger = rule.Trigger,
                triggerParameter = rule.TriggerParameter,
                condition = rule.Condition,
                actions = rule.Actions,
                runAsId = rule.RunAsId,
                nextRun = rule.NextRun,
                updated = rule.Updated
            };
        }

        /// <summary>
        /// Projects jobs onto their wire shape.
        /// </summary>
        /// <param name="jobs">The jobs.</param>
        /// <returns>The DTOs.</returns>
        internal static IEnumerable<object> ToDto(IEnumerable<AutomationJob> jobs)
        {
            return jobs.Select(job => new
            {
                id = job.Id,
                ruleId = job.RuleId,
                objectId = job.ObjectId,
                trigger = job.Trigger,
                state = job.State.ToString().ToLowerInvariant(),
                attempts = job.Attempts,
                completedActions = job.CompletedActions,
                nextAttempt = job.NextAttempt,
                lastError = job.LastError,
                created = job.Created,
                updated = job.Updated
            });
        }

        /// <summary>
        /// The JSON body of a rule written through the automation page or a client.
        /// </summary>
        internal sealed class RulePayload
        {
            /// <summary>Gets or sets the id of the rule to change, or empty for a new one.</summary>
            public string Id { get; set; }

            /// <summary>Gets or sets the name.</summary>
            public string Name { get; set; }

            /// <summary>Gets or sets the description.</summary>
            public string Description { get; set; }

            /// <summary>Gets or sets the state, <c>active</c> or <c>disabled</c>.</summary>
            public string State { get; set; }

            /// <summary>Gets or sets the class the rule is narrowed to, or empty for all.</summary>
            public string ClassId { get; set; }

            /// <summary>Gets or sets the trigger key.</summary>
            public string Trigger { get; set; }

            /// <summary>Gets or sets the trigger parameter.</summary>
            public string TriggerParameter { get; set; }

            /// <summary>Gets or sets the WQL condition.</summary>
            public string Condition { get; set; }

            /// <summary>Gets or sets the action script.</summary>
            public string Actions { get; set; }

            /// <summary>Gets or sets the account the rule acts as: a user name, an e-mail address, or empty.</summary>
            public string RunAs { get; set; }
        }

        /// <summary>
        /// The JSON body naming a record by id.
        /// </summary>
        internal sealed class IdPayload
        {
            /// <summary>Gets or sets the id.</summary>
            public string Id { get; set; }
        }

        /// <summary>
        /// Parses an optional id.
        /// </summary>
        /// <param name="value">The id as sent.</param>
        /// <returns>The id, or <see langword="null"/> for an empty or invalid one.</returns>
        internal static Guid? ParseId(string value)
        {
            return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
        }
    }
}
