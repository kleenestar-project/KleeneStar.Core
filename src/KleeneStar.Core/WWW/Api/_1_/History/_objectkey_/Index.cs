using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;

namespace KleeneStar.Core.WWW.Api._1_.History._objectkey_
{
    using CommitEntity = KleeneStar.Model.Entities.Commit;
    using ObjectEntity = KleeneStar.Model.Entities.Object;

    /// <summary>
    /// REST endpoint exposing the commit history of a single object. The URL is
    /// <c>/api/1/history/{objectkey}</c>; the <c>{objectkey}</c> URL segment is declared via
    /// <see cref="ObjectKeySegmentAttribute"/> so callers can bind it from the current request's
    /// <see cref="ObjectKeyParameter"/>.
    /// </summary>
    /// <remarks>
    /// The versioning concept lists these routes below <c>/api/1/objects/{objectKey}/history</c>.
    /// They are mounted under <c>/api/1/history/{objectkey}</c> instead, for two reasons: the
    /// <c>objects</c> branch already owns a variable segment (<c>/api/1/objects/{workspacekey}/…</c>)
    /// and a second variable sibling beside it cannot be routed unambiguously, and every other
    /// per-object resource in this application is already mounted the same way — see
    /// <c>/api/1/comments/{objectkey}</c>, <c>/api/1/hierarchy/{objectkey}</c> and
    /// <c>/api/1/assignee/{objectkey}</c>. The set of operations and their payloads are the ones
    /// the concept specifies.
    /// <para>
    /// The routes are:
    /// <list type="bullet">
    /// <item><c>GET {base}</c> — the chain, newest first, with <c>?start=</c> / <c>?count=</c> paging.</item>
    /// <item><c>GET {base}/{number}</c> — one commit including its changed fields.</item>
    /// <item><c>GET {base}/{number}/state</c> — the complete replayed field state at that commit.</item>
    /// <item><c>GET {base}/{from}/{to}/diff</c> — the aggregated difference between two revisions.</item>
    /// <item><c>POST {base}/{number}/restore</c> — reapplies that revision as a new commit.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <see cref="IncludeSubPathsAttribute"/> is REQUIRED: without it the longer paths never
    /// match the endpoint and every call past the plain list answers 404.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.history.api.title")]
    [ObjectKeySegment]
    [IncludeSubPaths(true)]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// The number of commits returned when the caller names no page size.
        /// </summary>
        private const int DefaultPageSize = 50;

        /// <summary>
        /// Serialization options for the history payloads: camelCase property names, null
        /// members omitted.
        /// </summary>
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
        /// Handles the read routes: the chain, a single commit, a replayed state, and a diff.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>
        /// The requested JSON document, <c>400</c> when a revision reference is malformed, or
        /// <c>404</c> when the object, the revision or the route does not exist.
        /// </returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            var segments = GetRelativeSegments(request, @object.Key);

            return segments.Count switch
            {
                0 => RetrieveChain(@object, request),
                1 => RetrieveCommit(@object, segments[0], request),
                2 when Matches(segments[1], "state") => RetrieveState(@object, segments[0], request),
                3 when Matches(segments[2], "diff") => RetrieveDiff(@object, segments[0], segments[1], request),
                _ => new ResponseNotFound()
            };
        }

        /// <summary>
        /// Handles <c>POST {base}/{number}/restore</c>: reapplies the field values the object
        /// held at that revision and appends the resulting <c>restored</c> commit.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>
        /// <c>201</c> with the appended commit, <c>400</c> when the revision reference is
        /// malformed, <c>404</c> when the object or the revision does not exist, or <c>409</c>
        /// when the addressed revision is already the head and restoring it would change nothing.
        /// </returns>
        [Method(RequestMethod.POST)]
        public IResponse Restore(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(@object, request, typeof(global::KleeneStar.Core.WebPermissions.ObjectRestoreStatePermission)))
            {
                return new ResponseForbidden();
            }

            var segments = GetRelativeSegments(request, @object.Key);

            if (segments.Count != 2 || !Matches(segments[1], "restore"))
            {
                return new ResponseNotFound();
            }

            if (!TryParseNumber(segments[0], out var number))
            {
                return Error($"'{segments[0]}' is not a valid revision number.");
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var result = CoreHub.CommitManager.RestoreCommit(@object.Id, number, identityId);

            if (result is null)
            {
                return new ResponseNotFound(new StatusMessage($"revision '{@object.Key}#{number}' not found."));
            }

            if (!result.Changed)
            {
                return Conflict($"revision '{@object.Key}#{number}' is the current state; restoring it would change nothing.");
            }

            return new ResponseCreated
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Map(result.Commit, request), _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Returns the commit chain of an object, newest first, honouring the paging query.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="request">The request carrying the paging query.</param>
        /// <returns>The chain document.</returns>
        private static IResponse RetrieveChain(ObjectEntity @object, IRequest request)
        {
            var all = CoreHub.CommitManager.GetHistory(@object.Id).ToList();

            var start = ParseQuery(request, "start", 0);
            var count = ParseQuery(request, "count", DefaultPageSize);

            var page = all
                .Skip(Math.Max(0, start))
                .Take(count <= 0 ? DefaultPageSize : count)
                .Select(x => Map(x, request))
                .ToList();

            return Json(new HistoryDocument
            {
                Object = @object.Key,
                Count = all.Count,
                Start = Math.Max(0, start),
                Commits = page
            });
        }

        /// <summary>
        /// Returns a single revision of an object's chain.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="reference">The revision reference from the URL.</param>
        /// <returns>The commit document, <c>400</c> or <c>404</c>.</returns>
        private static IResponse RetrieveCommit(ObjectEntity @object, string reference, IRequest request)
        {
            var commit = ResolveCommit(@object, reference, out var error);

            return error ?? (commit is null
                ? new ResponseNotFound(new StatusMessage($"revision '{reference}' not found."))
                : Json(Map(commit, request)));
        }

        /// <summary>
        /// Returns the complete replayed field state of an object at one revision.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="reference">The revision reference from the URL.</param>
        /// <returns>The state document, <c>400</c> or <c>404</c>.</returns>
        private static IResponse RetrieveState(ObjectEntity @object, string reference, IRequest request)
        {
            var commit = ResolveCommit(@object, reference, out var error);

            if (error is not null)
            {
                return error;
            }

            var state = commit is null ? null : CoreHub.CommitManager.GetStateAt(@object.Id, commit.Number);

            if (state is null)
            {
                return new ResponseNotFound(new StatusMessage($"revision '{reference}' not found."));
            }

            return Json(new StateDocument
            {
                Object = @object.Key,
                Reference = state.Reference,
                Number = state.Number,
                Created = Format(state.Timestamp),
                Head = state.IsHead,
                Fields = [.. state.Fields.Select(x => new FieldDocument
                {
                    Name = x.Name,
                    Label = Label(x.Name, x.FieldId, x.Label, request),
                    Field = x.FieldId?.ToString(),
                    Value = Describe(x.Name, x.FieldId, x.Value)
                })]
            });
        }

        /// <summary>
        /// Returns the aggregated difference between two revisions of an object.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="from">The revision the comparison starts at.</param>
        /// <param name="to">The revision the comparison ends at.</param>
        /// <returns>The diff document, <c>400</c> or <c>404</c>.</returns>
        private static IResponse RetrieveDiff(ObjectEntity @object, string from, string to, IRequest request)
        {
            var source = ResolveCommit(@object, from, out var sourceError);

            if (sourceError is not null)
            {
                return sourceError;
            }

            var target = ResolveCommit(@object, to, out var targetError);

            if (targetError is not null)
            {
                return targetError;
            }

            if (source is null || target is null)
            {
                return new ResponseNotFound(new StatusMessage($"revision '{(source is null ? from : to)}' not found."));
            }

            var diff = CoreHub.CommitManager.DiffCommits(@object.Id, source.Number, target.Number);

            if (diff is null)
            {
                return new ResponseNotFound();
            }

            return Json(new DiffDocument
            {
                Object = @object.Key,
                From = diff.From,
                To = diff.To,
                Changes = [.. diff.Changes.Select(x => Map(x, request))]
            });
        }

        /// <summary>
        /// Resolves a revision from the URL. A revision may be addressed by its number
        /// (<c>4</c>) or by its commit id, which is what a deep link into the history dialog
        /// carries.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="reference">The reference from the URL.</param>
        /// <param name="error">The error response when the reference is malformed.</param>
        /// <returns>The commit, or <c>null</c> when there is none.</returns>
        private static CommitEntity ResolveCommit(ObjectEntity @object, string reference, out IResponse error)
        {
            error = null;

            if (Guid.TryParse(reference, out var commitId))
            {
                var byId = CoreHub.CommitManager.GetCommit(commitId);

                return byId?.ObjectId == @object.Id ? byId : null;
            }

            if (!TryParseNumber(reference, out var number))
            {
                error = Error($"'{reference}' is not a valid revision reference.");

                return null;
            }

            return CoreHub.CommitManager.GetCommit(@object.Id, number);
        }

        /// <summary>
        /// Maps a commit onto its wire representation.
        /// </summary>
        /// <param name="commit">The commit.</param>
        /// <param name="request">The request, used to localize the labels.</param>
        /// <returns>The document.</returns>
        private static CommitDocument Map(CommitEntity commit, IRequest request)
        {
            return new CommitDocument
            {
                Id = commit.Id.ToString(),
                Reference = commit.Reference,
                Number = commit.Number,
                Parent = commit.ParentId?.ToString(),
                Type = Model.Entities.CommitTypeExtensions.Token(commit.Type),
                Author = commit.CreatedBy?.Name ?? commit.CreatedByName,
                Created = Format(commit.Created),
                Message = commit.Message,
                Changes = [.. (commit.Changes ?? []).Select(x => Map(x, request))]
            };
        }

        /// <summary>
        /// Maps a change onto its wire representation, resolving the display form of the values.
        /// </summary>
        /// <param name="change">The change.</param>
        /// <param name="request">The request, used to localize the label.</param>
        /// <returns>The document.</returns>
        private static ChangeDocument Map(Model.Entities.Change change, IRequest request)
        {
            return new ChangeDocument
            {
                Name = change.Name,
                Label = Label(change.Name, change.FieldId, change.Field?.Name, request),
                Field = change.FieldId?.ToString(),
                Old = Describe(change.Name, change.FieldId, change.OldValue),
                New = Describe(change.Name, change.FieldId, change.NewValue)
            };
        }

        /// <summary>
        /// Returns the label an attribute is reported under: the localized name of a system
        /// property, or the name of the class field.
        /// </summary>
        /// <remarks>
        /// A payload string is never resolved for us - the serializer ships whatever it is given -
        /// so the resource key of a system property is translated here rather than handed to the
        /// client raw.
        /// </remarks>
        /// <param name="name">The recorded attribute name.</param>
        /// <param name="fieldId">The field id, or <c>null</c> for a system property.</param>
        /// <param name="fieldName">The resolved field name, when the attribute is a class field.</param>
        /// <param name="request">The request, used to localize.</param>
        /// <returns>The label.</returns>
        private static string Label(string name, Guid? fieldId, string fieldName, IRequest request)
        {
            if (fieldId.HasValue)
            {
                return fieldName ?? name;
            }

            var key = ObjectProperty.Text(name);

            return key is null ? name : I18N.Translate(request, key);
        }

        /// <summary>
        /// Turns a recorded value into the form the API reports: a system property's stored
        /// reference id reads as the name it points at, a field payload is reported as stored.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="fieldId">The field id, or <c>null</c> for a system property.</param>
        /// <param name="value">The recorded value.</param>
        /// <returns>The reported value.</returns>
        private static string Describe(string name, Guid? fieldId, string value)
        {
            return fieldId.HasValue ? value : ObjectProperty.Describe(name, value);
        }

        /// <summary>
        /// Resolves the object addressed by the URL <c>{objectkey}</c> segment.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The object, or <c>null</c>.</returns>
        private static ObjectEntity ResolveObject(IRequest request)
        {
            return CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);
        }

        /// <summary>
        /// Returns the path segments below <c>{base}/{objectkey}</c>.
        /// </summary>
        /// <remarks>
        /// The endpoint's own base path ends in a variable segment, so the sub-path is taken
        /// relative to the object key the request resolved rather than to the declared route:
        /// the key is the last segment the endpoint itself owns, and everything after it belongs
        /// to the sub-route.
        /// </remarks>
        /// <param name="request">The request.</param>
        /// <param name="objectKey">The key of the addressed object.</param>
        /// <returns>The remaining segments.</returns>
        private static IReadOnlyList<string> GetRelativeSegments(IRequest request, string objectKey)
        {
            var path = request?.Uri?.PathSegments?
                .Select(x => x?.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "/")
                .ToList() ?? [];

            var anchor = path.FindLastIndex(x => string.Equals(x, objectKey, StringComparison.OrdinalIgnoreCase));

            return anchor < 0 ? [] : [.. path.Skip(anchor + 1)];
        }

        /// <summary>
        /// Reads an integer from the query string, falling back to a default.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="name">The query parameter name.</param>
        /// <param name="fallback">The value used when the parameter is absent or malformed.</param>
        /// <returns>The parsed value.</returns>
        private static int ParseQuery(IRequest request, string name, int fallback)
        {
            var raw = request?.GetParameter(name)?.Value;

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        /// <summary>
        /// Parses a revision number, rejecting anything that is not a positive integer.
        /// </summary>
        /// <param name="raw">The raw segment, with an optional leading <c>#</c>.</param>
        /// <param name="number">The parsed number.</param>
        /// <returns><see langword="true"/> when the segment names a revision.</returns>
        private static bool TryParseNumber(string raw, out int number)
        {
            var trimmed = (raw ?? string.Empty).TrimStart('#');

            return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0;
        }

        /// <summary>
        /// Formats a timestamp as an ISO-8601 round-trip string.
        /// </summary>
        /// <param name="value">The timestamp.</param>
        /// <returns>The formatted timestamp.</returns>
        private static string Format(DateTime value)
        {
            return value.ToString("o", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Compares a path segment against an expected literal case-insensitively.
        /// </summary>
        /// <param name="segment">The segment.</param>
        /// <param name="expected">The expected literal.</param>
        /// <returns><see langword="true"/> when equal.</returns>
        private static bool Matches(string segment, string expected)
        {
            return string.Equals(segment, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Wraps a payload into a JSON <c>200</c> response.
        /// </summary>
        /// <param name="payload">The payload to serialize.</param>
        /// <returns>The response.</returns>
        private static IResponse Json(object payload)
        {
            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Wraps an error message into a JSON <c>400</c> response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>The response.</returns>
        private static IResponse Error(string message)
        {
            return new ResponseBadRequest
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = message }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Wraps an error message into a JSON <c>409</c> response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <returns>The response.</returns>
        private static IResponse Conflict(string message)
        {
            return new ResponseConflict
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = message }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// The document returned for the whole chain of an object.
        /// </summary>
        private sealed class HistoryDocument
        {
            /// <summary>Gets or sets the key of the object.</summary>
            public string Object { get; set; }

            /// <summary>Gets or sets the length of the whole chain, regardless of paging.</summary>
            public int Count { get; set; }

            /// <summary>Gets or sets the offset the returned page starts at.</summary>
            public int Start { get; set; }

            /// <summary>Gets or sets the commits of the page, newest first.</summary>
            public IReadOnlyList<CommitDocument> Commits { get; set; } = [];
        }

        /// <summary>
        /// The document returned for a single commit.
        /// </summary>
        private sealed class CommitDocument
        {
            /// <summary>Gets or sets the commit id.</summary>
            public string Id { get; set; }

            /// <summary>Gets or sets the human-readable revision reference, e.g. <c>INC-00123#4</c>.</summary>
            public string Reference { get; set; }

            /// <summary>Gets or sets the revision number.</summary>
            public int Number { get; set; }

            /// <summary>Gets or sets the id of the predecessor commit.</summary>
            public string Parent { get; set; }

            /// <summary>Gets or sets the commit type token.</summary>
            public string Type { get; set; }

            /// <summary>Gets or sets the display name of the author.</summary>
            public string Author { get; set; }

            /// <summary>Gets or sets the ISO-8601 timestamp of the commit.</summary>
            public string Created { get; set; }

            /// <summary>Gets or sets the commit message.</summary>
            public string Message { get; set; }

            /// <summary>Gets or sets the fields the commit changed.</summary>
            public IReadOnlyList<ChangeDocument> Changes { get; set; } = [];
        }

        /// <summary>
        /// The document returned for a single field modification.
        /// </summary>
        private sealed class ChangeDocument
        {
            /// <summary>Gets or sets the stable attribute name.</summary>
            public string Name { get; set; }

            /// <summary>Gets or sets the display label of the attribute.</summary>
            public string Label { get; set; }

            /// <summary>Gets or sets the field id, absent for a system property.</summary>
            public string Field { get; set; }

            /// <summary>Gets or sets the value before the change.</summary>
            public string Old { get; set; }

            /// <summary>Gets or sets the value after the change.</summary>
            public string New { get; set; }
        }

        /// <summary>
        /// The document returned for a replayed state.
        /// </summary>
        private sealed class StateDocument
        {
            /// <summary>Gets or sets the key of the object.</summary>
            public string Object { get; set; }

            /// <summary>Gets or sets the revision reference the state belongs to.</summary>
            public string Reference { get; set; }

            /// <summary>Gets or sets the revision number.</summary>
            public int Number { get; set; }

            /// <summary>Gets or sets the ISO-8601 timestamp of the revision.</summary>
            public string Created { get; set; }

            /// <summary>Gets or sets whether the revision is the head of the chain.</summary>
            public bool Head { get; set; }

            /// <summary>Gets or sets the complete field set at that revision.</summary>
            public IReadOnlyList<FieldDocument> Fields { get; set; } = [];
        }

        /// <summary>
        /// The document returned for one field of a replayed state.
        /// </summary>
        private sealed class FieldDocument
        {
            /// <summary>Gets or sets the stable attribute name.</summary>
            public string Name { get; set; }

            /// <summary>Gets or sets the display label of the attribute.</summary>
            public string Label { get; set; }

            /// <summary>Gets or sets the field id, absent for a system property.</summary>
            public string Field { get; set; }

            /// <summary>Gets or sets the value at that revision.</summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// The document returned for a difference between two revisions.
        /// </summary>
        private sealed class DiffDocument
        {
            /// <summary>Gets or sets the key of the object.</summary>
            public string Object { get; set; }

            /// <summary>Gets or sets the revision the comparison starts at.</summary>
            public int From { get; set; }

            /// <summary>Gets or sets the revision the comparison ends at.</summary>
            public int To { get; set; }

            /// <summary>Gets or sets the fields that differ between the two revisions.</summary>
            public IReadOnlyList<ChangeDocument> Changes { get; set; } = [];
        }
    }
}
