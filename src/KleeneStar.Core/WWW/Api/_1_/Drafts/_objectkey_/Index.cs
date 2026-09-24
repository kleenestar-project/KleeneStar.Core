using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Drafts._objectkey_
{
    /// <summary>
    /// REST endpoint for the unpublished working copy of the prose of an object. The URL is
    /// <c>/api/1/drafts/{objectkey}</c>. It is what the editor's autosave writes to on every
    /// pause in typing (the framework's <c>EditorFormCtrl</c>), and what tells a freshly opened
    /// editor whether it is resuming a draft or starting from the published text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GET {base}</c> answers the state the editor opens on:
    /// <c>{ "summary": "…", "description": "…", "draft": true, "updated": "2026-09-02T18:12:04Z" }</c>.
    /// <c>draft</c> says where the two texts came from; <c>updated</c> is <c>null</c> unless
    /// they came from a draft.
    /// </para>
    /// <para>
    /// <c>PUT {base}</c> takes <c>{ "summary": "…", "description": "…" }</c> and stores it as
    /// the draft. It writes no commit and leaves the published object untouched - which is the
    /// whole point: an autosave every few seconds must not produce a revision every few
    /// seconds, and a reader must keep seeing the last published text while somebody is
    /// writing.
    /// </para>
    /// <para>
    /// <c>DELETE {base}</c> drops the draft. Leaving the editor is <b>not</b> a delete: an
    /// abandoned draft is kept on purpose so the next edit resumes it. The verb exists for the
    /// explicit "discard my unpublished changes".
    /// </para>
    /// <para>
    /// Publishing is not here. It is a write to the object and belongs to the object's own CRUD
    /// surface - see <see cref="WWW.Api._1_.Prose.Index"/>, which the editor form submits to.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.draft.api.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Serialization options for the payloads: camelCase property names, case-insensitive
        /// binding.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Handles <c>GET {base}</c>: answers the prose the editor is to open on - the draft
        /// when one exists, the published values otherwise.
        /// </summary>
        /// <param name="request">The incoming request carrying the object key.</param>
        /// <returns><c>200</c> with the state, or <c>404</c> when the object does not exist.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Read(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            var (summary, description, isDraft, updated) = CoreHub.ObjectDraftManager.GetEffective(@object.Id);

            return Ok(new DraftState
            {
                Summary = summary,
                Description = description,
                Draft = isDraft,
                Updated = updated
            });
        }

        /// <summary>
        /// Handles <c>PUT {base}</c>: stores the supplied prose as the unpublished draft of the
        /// object.
        /// </summary>
        /// <param name="request">The incoming request carrying the JSON body.</param>
        /// <returns><c>200</c> with the new draft state, <c>404</c> when the object does not
        /// exist, or <c>400</c> when the body is not a JSON document.</returns>
        [Method(RequestMethod.PUT)]
        public IResponse Persist(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(@object, request))
            {
                return new ResponseForbidden();
            }

            DraftPayload payload;

            try
            {
                var content = (request as Request)?.Content;
                payload = content is { Length: > 0 }
                    ? JsonSerializer.Deserialize<DraftPayload>(content, _jsonOptions)
                    : null;
            }
            catch (JsonException)
            {
                return Error("The request body is not a valid JSON document.");
            }

            if (payload is null)
            {
                return Error("The request body must be a JSON document of the form { \"summary\": \"…\", \"description\": \"…\" }.");
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var draft = CoreHub.ObjectDraftManager.Save(@object.Id, payload.Summary, payload.Description, identityId);

            if (draft is null)
            {
                return new ResponseNotFound();
            }

            return Ok(new DraftState
            {
                Summary = draft.Summary,
                Description = draft.Description,
                Draft = true,
                Updated = draft.Updated
            });
        }

        /// <summary>
        /// Handles <c>DELETE {base}</c>: drops the unpublished draft, leaving the published text
        /// as it stands.
        /// </summary>
        /// <param name="request">The incoming request carrying the object key.</param>
        /// <returns><c>204</c> whether or not a draft existed, or <c>404</c> when the object
        /// does not exist.</returns>
        [Method(RequestMethod.DELETE)]
        public IResponse Discard(IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(@object, request))
            {
                return new ResponseForbidden();
            }

            CoreHub.ObjectDraftManager.Discard(@object.Id);

            return new ResponseNoContent();
        }

        /// <summary>
        /// Resolves the object addressed by the URL-bound object key.
        /// </summary>
        /// <param name="request">The request carrying the key.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        private static Model.Entities.Object ResolveObject(IRequest request)
        {
            var keyParameter = request?.GetParameter<ObjectKeyParameter>();

            return CoreHub.ObjectManager.GetObjectByKey(keyParameter?.Value);
        }

        /// <summary>
        /// Wraps a state into a JSON <c>200</c> response.
        /// </summary>
        /// <param name="state">The state to serialize.</param>
        /// <returns>The response.</returns>
        private static IResponse Ok(DraftState state)
        {
            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, _jsonOptions))
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
        /// The JSON body accepted by <c>PUT</c>.
        /// </summary>
        private sealed class DraftPayload
        {
            /// <summary>
            /// Gets or sets the unpublished title.
            /// </summary>
            public string Summary { get; set; }

            /// <summary>
            /// Gets or sets the unpublished rich-text body.
            /// </summary>
            public string Description { get; set; }
        }

        /// <summary>
        /// The JSON document answered by <c>GET</c> and <c>PUT</c>.
        /// </summary>
        private sealed class DraftState
        {
            /// <summary>
            /// Gets or sets the title the editor is to show.
            /// </summary>
            public string Summary { get; set; }

            /// <summary>
            /// Gets or sets the body the editor is to show.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets whether the two texts come from an unpublished draft.
            /// </summary>
            public bool Draft { get; set; }

            /// <summary>
            /// Gets or sets when the draft was last written, or <c>null</c> when there is none.
            /// </summary>
            public DateTime? Updated { get; set; }
        }
    }
}
