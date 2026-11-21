using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Operations._workspacekey_
{
    /// <summary>
    /// The recursive operations on an object and its subtree, and the move of an object into
    /// another workspace (<c>kleenestar.hierarchy.md</c>).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>GET ?kind=archive|restore|delete|dissolve|clone&amp;object=&lt;key&gt;[&amp;mode=]</c> -
    /// the plan: <c>{ kind, total, subtree, hidden, unwritable, refused }</c> (<c>refused</c> is the
    /// translated reason, or absent).</item>
    /// <item><c>GET ?kind=transfer&amp;object=&lt;key&gt;[&amp;targetWorkspace=&amp;map=&amp;declassify=]</c> -
    /// without a target the workspaces the caller may move into, with one the mapping.</item>
    /// <item><c>POST { kind, object, mode?, key? }</c> or, for a transfer,
    /// <c>{ kind: "transfer", object, targetWorkspace, classMap, declassify, key? }</c> - starts the
    /// run: <c>200 { operation }</c> when it finished within the request, <c>202 { operation }</c>
    /// while the worker runs it, <c>400 { message }</c> refused, <c>403</c> nobody signed in,
    /// <c>404</c> no such object.</item>
    /// <item><c>GET ?operation=&lt;id|key&gt;</c> - the progress
    /// <c>{ id, key, kind, state, done, total, error, result }</c>; <c>result</c> names where the
    /// reader goes next (the copy of a clone, the moved object). Only the identity that started a
    /// run reads it.</item>
    /// </list>
    /// The object is resolved by key, narrowed: what the caller cannot see, they cannot operate on.
    /// </remarks>
    [WorkspaceKeySegment]
    [Title("kleenestar.core:operation.api.title")]
    [Cache]
    public sealed class Index : IRestApi
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers a plan or the progress of a run.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The JSON answer.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var token = request.GetParameter("operation")?.Value;

            if (!string.IsNullOrWhiteSpace(token))
            {
                var operation = CoreHub.ObjectOperationManager.GetOperation(token);

                return operation is null || operation.IdentityId != identityId || identityId == Guid.Empty
                    ? new ResponseNotFound()
                    : Json(Describe(operation, request));
            }

            var root = ResolveRoot(request, request.GetParameter("object")?.Value);

            if (root is null)
            {
                return new ResponseNotFound();
            }

            if (string.Equals(request.GetParameter("kind")?.Value, ObjectOperationKinds.Transfer, StringComparison.OrdinalIgnoreCase))
            {
                return PlanTransfer(request, root);
            }

            var plan = CoreHub.ObjectOperationManager.Plan(request.GetParameter("kind")?.Value, root.Id, request.GetParameter("mode")?.Value, x => ContentAuthorization.MayWrite(x, request));

            return Json(new
            {
                kind = plan.Kind,
                total = plan.Items.Count,
                subtree = plan.Subtree,
                hidden = plan.Hidden,
                unwritable = plan.Unwritable,
                refused = plan.Refused ? Explain(plan, request) : null
            });
        }

        /// <summary>
        /// Starts a run.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The run, or the refusal.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Persist(IRequest request)
        {
            StartPayload payload;

            try
            {
                var content = (request as Request)?.Content;

                payload = content is { Length: > 0 }
                    ? JsonSerializer.Deserialize<StartPayload>(content, _jsonOptions)
                    : null;
            }
            catch (JsonException)
            {
                return ObjectMove.BadRequest("The request body is not a valid JSON document.");
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (identityId == Guid.Empty)
            {
                return new ResponseForbidden();
            }

            var root = ResolveRoot(request, payload?.Object);

            if (root is null)
            {
                return new ResponseNotFound();
            }

            if (string.Equals(payload.Kind, ObjectOperationKinds.Transfer, StringComparison.OrdinalIgnoreCase))
            {
                var options = TransferOptionsOf(payload.TargetWorkspace, payload.ClassMap, payload.Declassify);

                if (options is null)
                {
                    return new ResponseNotFound();
                }

                return Answer(CoreHub.ObjectOperationManager.StartTransfer(root.Id, options, payload.Key, identityId,
                    x => ContentAuthorization.MayWrite(x, request), classId => ContentAuthorization.MayCreateIn(classId, request)), request);
            }

            var started = CoreHub.ObjectOperationManager.Start(payload.Kind, root.Id, payload.Mode, payload.Key, identityId, x => ContentAuthorization.MayWrite(x, request));

            return Answer(started, request);
        }

        /// <summary>
        /// Answers a start: <c>400 { message }</c> refused, <c>200 { operation }</c> finished (or
        /// failed) within the request, <c>202 { operation }</c> while the worker runs it. Shared
        /// with <c>DELETE /api/1/objects?mode=</c>.
        /// </summary>
        /// <param name="started">The outcome of the start.</param>
        /// <param name="request">The request, for the language.</param>
        /// <returns>The response.</returns>
        internal static IResponse Answer(ObjectOperationStart started, IRequest request)
        {
            if (started.Operation is null)
            {
                return Refusal(Explain(started.Plan, request));
            }

            var body = Describe(started.Operation, request);

            return started.Operation.State == ObjectOperationState.Completed || started.Operation.State == ObjectOperationState.Failed
                ? Json(body)
                : Json(body, accepted: true);
        }

        /// <summary>
        /// Resolves the workspace of the route, when the caller may read it.
        /// </summary>
        private static Workspace ResolveWorkspace(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            return string.IsNullOrWhiteSpace(key)
                ? null
                : VisibleWorkspaces().FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the workspaces the caller may read.
        /// </summary>
        private static List<Workspace> VisibleWorkspaces()
        {
            return [.. CoreHub.WorkspaceManager
                .GetWorkspaces(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(new Query<Workspace>()))];
        }

        /// <summary>
        /// Resolves the root by key: an object of the route's workspace the caller may see.
        /// </summary>
        private static Model.Entities.Object ResolveRoot(IRequest request, string key)
        {
            var workspace = ResolveWorkspace(request);

            if (workspace is null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            // by key or id, read narrowed
            var root = ObjectMove.Resolve(key.Trim());

            return root?.WorkspaceId == workspace.Id ? root : null;
        }

        /// <summary>
        /// Translates the refusal of a plan.
        /// </summary>
        internal static string Explain(ObjectOperationPlan plan, IRequest request)
        {
            CultureInfo culture;

            try
            {
                culture = request?.Culture ?? CultureInfo.InvariantCulture;
            }
            catch
            {
                culture = CultureInfo.InvariantCulture;
            }

            return string.Format(culture, I18N.Translate(request, plan?.RefusalKey ?? "kleenestar.core:operation.refused.kind"), plan?.RefusalArguments ?? []);
        }

        /// <summary>
        /// Describes a run for the client.
        /// </summary>
        private static object Describe(ObjectOperation operation, IRequest request)
        {
            string result = null;

            if (operation.State == ObjectOperationState.Completed)
            {
                if (operation.Kind == ObjectOperationKinds.Clone && operation.ReferenceId is Guid copy && CoreHub.ObjectManager.GetObject(copy) is { } clone)
                {
                    result = CoreHub.ObjectKindManager.ResolveDetailUri(clone)?.ToString();
                }
                else if (operation.Kind is not (ObjectOperationKinds.Delete or ObjectOperationKinds.Dissolve)
                    && CoreHub.ObjectManager.GetObject(operation.RootId) is { } root)
                {
                    result = CoreHub.ObjectKindManager.ResolveDetailUri(root)?.ToString();
                }
            }

            return new
            {
                id = operation.Id,
                key = operation.Key,
                kind = operation.Kind,
                state = operation.State.ToString().ToLowerInvariant(),
                done = operation.Done,
                total = operation.Total,
                error = operation.Error,
                result
            };
        }

        /// <summary>
        /// Answers a refusal as <c>400 { message }</c>.
        /// </summary>
        private static IResponse Refusal(string message)
        {
            return new ResponseBadRequest
            {
                Content = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Answers a JSON body, <c>202</c> for a run still in progress.
        /// </summary>
        private static IResponse Json(object body, bool accepted = false)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body, _jsonOptions));

            return accepted
                ? new ResponseAccepted { Content = bytes }.AddHeaderContentType("application/json")
                : new ResponseOK { Content = bytes }.AddHeaderContentType("application/json");
        }

        /// <summary>
        /// The request body of a start.
        /// </summary>
        private sealed class StartPayload
        {
            public string Kind { get; set; }

            public string Object { get; set; }

            public string Mode { get; set; }

            public string Key { get; set; }

            public string TargetWorkspace { get; set; }

            public Dictionary<string, string> ClassMap { get; set; }

            public bool Declassify { get; set; }
        }

        /// <summary>
        /// The plan of a transfer: without a target workspace the workspaces the caller may move
        /// into; with one the mapping (classes with their candidates, dropped fields, states, levels).
        /// </summary>
        private static IResponse PlanTransfer(IRequest request, Model.Entities.Object root)
        {
            var targetToken = request.GetParameter("targetWorkspace")?.Value;

            if (string.IsNullOrWhiteSpace(targetToken))
            {
                var workspaces = VisibleWorkspaces()
                    .Where(x => x.Id != root.WorkspaceId && x.State == WorkspaceState.Active)
                    .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(x => new { id = x.Id, key = x.Key, name = x.Name });

                return Json(new { kind = ObjectOperationKinds.Transfer, workspaces });
            }

            // the class mapping travels as map=source:target,source:target
            var map = (request.GetParameter("map")?.Value ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.Split(':'))
                .Where(x => x.Length == 2)
                .ToDictionary(x => x[0], x => x[1]);

            var options = TransferOptionsOf(targetToken, map,
                string.Equals(request.GetParameter("declassify")?.Value, "true", StringComparison.OrdinalIgnoreCase));

            if (options is null)
            {
                return new ResponseNotFound();
            }

            var plan = CoreHub.ObjectOperationManager.PlanTransfer(root.Id, options,
                x => ContentAuthorization.MayWrite(x, request), classId => ContentAuthorization.MayCreateIn(classId, request));

            return Json(new
            {
                kind = plan.Kind,
                total = plan.Items.Count,
                subtree = plan.Subtree,
                refused = plan.Refused ? Explain(plan, request) : null,
                transfer = plan.Transfer
            });
        }

        /// <summary>
        /// Reads the options of a transfer: the target workspace (key or id, readable for the
        /// caller), the class mapping (ids), declassify.
        /// </summary>
        /// <returns>The options, or <see langword="null"/> when the workspace is unknown.</returns>
        private static TransferOptions TransferOptionsOf(string workspaceToken, IDictionary<string, string> classMap, bool declassify)
        {
            if (string.IsNullOrWhiteSpace(workspaceToken))
            {
                return null;
            }

            var workspace = VisibleWorkspaces().FirstOrDefault(x => x.Id.ToString().Equals(workspaceToken.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Key, workspaceToken.Trim(), StringComparison.OrdinalIgnoreCase));

            if (workspace is null)
            {
                return null;
            }

            var map = (classMap ?? new Dictionary<string, string>())
                .Where(x => Guid.TryParse(x.Key, out _) && Guid.TryParse(x.Value, out _))
                .ToDictionary(x => Guid.Parse(x.Key), x => Guid.Parse(x.Value));

            return new TransferOptions(workspace.Id, map, declassify);
        }
    }
}
