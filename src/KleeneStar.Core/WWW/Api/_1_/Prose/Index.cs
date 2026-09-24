using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Prose
{
    /// <summary>
    /// The CRUD surface the prose editor of the document and blog kinds loads and publishes
    /// through. The URL is <c>/api/1/prose</c>, addressed by object id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It exists beside <see cref="WWW.Api._1_.Objects.Index"/> rather than inside it because
    /// the two answer different questions about the same row. The object endpoint reads and
    /// writes what is <i>published</i>, which is what every list, board and issue form needs.
    /// This one reads what is <i>being written</i> - the draft when there is one - and its
    /// write is a <b>publish</b>: it turns the unpublished draft into the published text and
    /// drops the draft. Folding that into the object endpoint would make every ordinary save of
    /// an issue go looking for a draft.
    /// </para>
    /// <para>
    /// The autosave in between does not come here; it goes to
    /// <see cref="WWW.Api._1_.Drafts._objectkey_.Index"/>, which writes no commit.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.prose.api.title")]
    [Cache]
    public sealed class Index : RestApiCrud<Model.Entities.Object>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Creates the query context the objects are read in.
        /// </summary>
        /// <returns>The query context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns the objects matching the supplied query.
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request providing the operational context.</param>
        /// <returns>The matching objects.</returns>
        protected override IEnumerable<Model.Entities.Object> Retrieve(IQuery<Model.Entities.Object> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager.GetObjects(query, context);
        }

        /// <summary>
        /// Returns the prose the editor is to open on. When the object carries an unpublished
        /// draft, the draft's title and body replace the published ones in the payload, so
        /// re-opening the editor resumes where the author stopped rather than starting over
        /// from what the readers currently see.
        /// </summary>
        /// <param name="query">The query criteria addressing the object.</param>
        /// <param name="request">The request providing the operational context.</param>
        /// <returns>The retrieve result.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<Model.Entities.Object> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.ObjectManager.GetObjects(query, context)
                .FirstOrDefault();

            var result = RetrieveForUpdate(request, data);

            if (data is not null && result?.Data is IDictionary<string, object> payload)
            {
                var (summary, description, _, _) = CoreHub.ObjectDraftManager.GetEffective(data.Id);

                payload[nameof(Model.Entities.Object.Summary)] = summary;
                payload[nameof(Model.Entities.Object.Description)] = description;
            }

            return result;
        }

        /// <summary>
        /// Publishes the prose: the submitted title and body become the published state of the
        /// object as one commit, and the draft row is dropped.
        /// </summary>
        /// <remarks>
        /// The payload is trusted over the draft here rather than merged with it, because the
        /// editor submits exactly what it is showing - which is the draft it loaded plus
        /// whatever was typed since the last autosave. Publishing what the author is looking at
        /// is the only reading that cannot surprise them.
        /// </remarks>
        /// <param name="existingItem">The object being published.</param>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="request">The request providing the operational context.</param>
        /// <returns>The update result.</returns>
        protected override IRestApiCrudResultUpdate Update(Model.Entities.Object existingItem, RestApiCrudFormData payload, IRequest request)
        {
            // publishing is changing the object; the framework's entry point takes a refusal
            // thrown from here as the failure the editor already reports
            if (!global::KleeneStar.Core.WebRestApi.ContentAuthorization.MayWrite(existingItem, request))
            {
                throw new UnauthorizedAccessException("The object may be published by those who may change it only.");
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            var summary = ReadText(payload, nameof(Model.Entities.Object.Summary));
            var description = ReadText(payload, nameof(Model.Entities.Object.Description));

            CoreHub.ObjectDraftManager.Publish(existingItem.Id, summary, description, identityId);

            return new RestApiCrudResultUpdate();
        }

        /// <summary>
        /// Reads a text field out of the submitted payload.
        /// </summary>
        /// <remarks>
        /// The payload parser lower-cases every property name it reads off the wire, so a lookup
        /// spelled the way the entity spells it misses every time - silently, because a missing
        /// key is indistinguishable from an unsent field.
        /// </remarks>
        /// <param name="payload">The submitted form data.</param>
        /// <param name="name">The field name as declared on the entity.</param>
        /// <returns>The text, or <see langword="null"/> when the payload does not carry the
        /// field.</returns>
        private static string ReadText(RestApiCrudFormData payload, string name)
        {
            return payload is not null && payload.TryGetValue(name.ToLowerInvariant(), out var value)
                ? value?.ToString()
                : null;
        }
    }
}
