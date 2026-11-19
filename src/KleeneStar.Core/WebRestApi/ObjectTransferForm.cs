using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebParameter;
using WebExpress.WebIndex.Queries;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The answers of the transfer wizard (<c>ObjectTransferFormFragment</c>) and what they name:
    /// the object, the target workspace and the class mapping.
    /// </summary>
    /// <remarks>
    /// The rendered steps receive the answers as JSON, the final submit as form parameters;
    /// <see cref="Read"/> maps both onto one dictionary. A class mapping counts only when the
    /// hidden <see cref="ContextField"/> names the chosen target workspace, because a mapping
    /// chosen for one workspace names classes of that workspace.
    /// </remarks>
    public static class ObjectTransferForm
    {
        /// <summary>
        /// The hidden input naming the key of the moved object.
        /// </summary>
        public const string ObjectField = "Object";

        /// <summary>
        /// The tile selection naming the target workspace by id.
        /// </summary>
        public const string WorkspaceField = "TargetWorkspace";

        /// <summary>
        /// The hidden input carrying the idempotency key of the run.
        /// </summary>
        public const string KeyField = "Key";

        /// <summary>
        /// The prefix of a class mapping input, followed by the id of the source class.
        /// </summary>
        public const string ClassMapPrefix = "ClassMap.";

        /// <summary>
        /// The hidden input of the class step naming the workspace it was rendered for.
        /// </summary>
        public const string ContextField = "TransferContext";

        /// <summary>
        /// Reads the answers of a request: a JSON object, or the form parameters.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The answers, keyed without case.</returns>
        public static IReadOnlyDictionary<string, string> Read(IRequest request)
        {
            var answers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var content = (request as Request)?.Content;

            if (content is { Length: > 0 }
                && request.Header?.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(content) ?? [];

                    foreach (var (key, value) in payload)
                    {
                        answers[key] = value.ValueKind switch
                        {
                            JsonValueKind.String => value.GetString(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                            JsonValueKind.Array => string.Join(";", value.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())),
                            _ => value.GetRawText()
                        };
                    }
                }
                catch (JsonException)
                {
                }

                return answers;
            }

            foreach (var parameter in request?.Parameters ?? [])
            {
                var key = parameter switch
                {
                    IParameterStatic known => known.GetKey(),
                    IParameterDynamic named => named.Key,
                    _ => null
                };

                if (!string.IsNullOrWhiteSpace(key))
                {
                    answers[key] = parameter.Value;
                }
            }

            return answers;
        }

        /// <summary>
        /// Resolves the object the answers name, read narrowed: what the caller cannot see, they
        /// cannot move.
        /// </summary>
        /// <param name="answers">The answers.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        public static ObjectEntity ResolveObject(IReadOnlyDictionary<string, string> answers)
        {
            return answers.TryGetValue(ObjectField, out var key) && !string.IsNullOrWhiteSpace(key)
                ? ObjectMove.Resolve(key.Trim())
                : null;
        }

        /// <summary>
        /// Returns the workspaces the object may be moved into: the active workspaces the caller
        /// may read, without the object's own, ordered by name.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The workspaces.</returns>
        public static IReadOnlyList<Workspace> TargetWorkspaces(ObjectEntity @object)
        {
            return [.. CoreHub.WorkspaceManager
                .GetWorkspaces(WebPermission.ContentVisibility.Restrict(new Query<Workspace>()))
                .Where(x => x.Id != @object.WorkspaceId && x.State == WorkspaceState.Active)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Resolves the target workspace the answers name.
        /// </summary>
        /// <param name="answers">The answers.</param>
        /// <param name="object">The moved object.</param>
        /// <returns>The workspace, or <see langword="null"/> when none or none offered is named.</returns>
        public static Workspace ResolveTarget(IReadOnlyDictionary<string, string> answers, ObjectEntity @object)
        {
            if (!answers.TryGetValue(WorkspaceField, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            token = token.Trim();

            return TargetWorkspaces(@object).FirstOrDefault(x => x.Id.ToString().Equals(token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Key, token, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the context value of the class step for a target workspace.
        /// </summary>
        /// <param name="target">The target workspace.</param>
        /// <returns>The context value.</returns>
        public static string Context(Workspace target)
        {
            return target.Id.ToString("N");
        }

        /// <summary>
        /// Builds the options of the transfer from the answers. The wizard asks no question about
        /// security levels: a level without counterpart in the chosen class is removed, and the
        /// review names every such level before the move.
        /// </summary>
        /// <param name="answers">The answers.</param>
        /// <param name="target">The target workspace.</param>
        /// <returns>The options.</returns>
        public static TransferOptions Options(IReadOnlyDictionary<string, string> answers, Workspace target)
        {
            var map = new Dictionary<Guid, Guid>();
            var current = answers.TryGetValue(ContextField, out var context)
                && string.Equals(context, Context(target), StringComparison.OrdinalIgnoreCase);

            if (current)
            {
                foreach (var (key, value) in answers)
                {
                    if (key.StartsWith(ClassMapPrefix, StringComparison.OrdinalIgnoreCase)
                        && Guid.TryParse(key[ClassMapPrefix.Length..], out var source)
                        && Guid.TryParse(value, out var mapped))
                    {
                        map[source] = mapped;
                    }
                }
            }

            return new TransferOptions(target.Id, map, Declassify: true);
        }

        /// <summary>
        /// Plans the transfer for the caller of the request.
        /// </summary>
        /// <param name="object">The moved object.</param>
        /// <param name="options">The options.</param>
        /// <param name="request">The request.</param>
        /// <returns>The plan.</returns>
        public static ObjectOperationPlan Plan(ObjectEntity @object, TransferOptions options, IRequest request)
        {
            return CoreHub.ObjectOperationManager.PlanTransfer(@object.Id, options,
                x => ContentAuthorization.MayWrite(x, request), classId => ContentAuthorization.MayCreateIn(classId, request));
        }
    }
}
