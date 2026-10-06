using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Editor
{
    /// <summary>
    /// The link targets the editor's link dialog offers on its <i>From the application</i> page
    /// (WebExpress's <c>panels/webexpress.webapp.panel.editor.link.js</c>): the objects the
    /// author may open, so linking an issue or a page is picking it rather than pasting its
    /// address.
    /// </summary>
    /// <remarks>
    /// <c>GET</c> without a search answers the author's recently used objects, <c>GET ?q=</c>
    /// the objects whose key or summary contains the text - the key because that is what a
    /// person pastes from a ticket mail, the summary because that is what they remember. Every
    /// read goes through <c>ObjectManager</c>, so the list is narrowed by the author's
    /// permissions and security levels like every other list; a link in a document still
    /// leads every reader to the object's own page, which asks again. The shape is the one the
    /// framework's library reads: <c>{ items: [{ uri, title, description }] }</c>. Editors
    /// name the endpoint through <see cref="EditorLinkLibrary"/>.
    /// </remarks>
    [Title("kleenestar.core:editor.links.api.title")]
    [Cache]
    public sealed class Links : IRestApi
    {
        /// <summary>
        /// The number of recently used objects offered before anything is typed.
        /// </summary>
        public const int MaxRecent = 20;

        /// <summary>
        /// The number of matches answered to a search.
        /// </summary>
        public const int MaxMatches = 50;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Links()
        {
        }

        /// <summary>
        /// Answers the link targets for the search the request carries.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>The link targets as json.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Get(IRequest request)
        {
            var search = request?.GetParameter("q")?.Value?.Trim();

            var items = Candidates(search, request)
                .Select(Project)
                .Where(x => x is not null)
                .ToArray();

            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { items }, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Returns the objects offered for a search.
        /// </summary>
        /// <param name="search">The text typed, or blank.</param>
        /// <param name="request">The request naming the author.</param>
        /// <returns>The objects.</returns>
        public static IEnumerable<ObjectEntity> Candidates(string search, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                var ownerId = CoreHub.SessionManager.GetCurrentIdentityId(request);

                return CoreHub.ObjectManager.GetRecentObjects(ownerId, MaxRecent);
            }

            var query = new Query<ObjectEntity>()
                .WhereContainsIgnoreCase(x => x.Summary, search)
                .Or(new Query<ObjectEntity>().WhereContainsIgnoreCase(x => x.Key, search))
                .WithPaging(0, MaxMatches);

            return CoreHub.ObjectManager.GetObjects(query);
        }

        /// <summary>
        /// Projects an object onto a link target: its page, its key and summary as the title,
        /// and where it lives as the description.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <returns>The target, or <see langword="null"/> when the object has no page.</returns>
        private static object Project(ObjectEntity @object)
        {
            var uri = ObjectKindCatalog.ResolveDetailUri(@object)?.ToString();

            if (string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            var @class = CoreHub.ClassManager.GetClass(@object.ClassId)?.Name;
            var workspace = CoreHub.WorkspaceManager.GetWorkspace(@object.WorkspaceId)?.Name;

            return new
            {
                uri,
                title = string.IsNullOrWhiteSpace(@object.Summary) ? @object.Key : $"{@object.Key} · {@object.Summary}",
                description = string.Join(" · ", new[] { @class, workspace }.Where(x => !string.IsNullOrWhiteSpace(x)))
            };
        }
    }
}
