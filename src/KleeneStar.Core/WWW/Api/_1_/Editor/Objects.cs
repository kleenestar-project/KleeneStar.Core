using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebRestApi;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Editor
{
    /// <summary>
    /// The objects the editor's KleeneStar add-ons show (<c>Assets/js/editoraddons.js</c>):
    /// one object by key (object reference), the documents below a document (child pages), or
    /// the objects a filter selects (object list and figure).
    /// </summary>
    /// <remarks>
    /// An add-on stores only its settings in the document; what it shows is read here every
    /// time the document is opened, so a list in a page is as current as the issue it names.
    /// Every read goes through <c>ObjectManager</c> (<see cref="EditorObjectQuery"/>), so the
    /// answer is narrowed by the reader's permissions and security levels, and an object the
    /// reader may not open is answered as not found rather than described.
    /// <para>
    /// <c>GET ?key=SD-12</c> - one object. <c>GET ?parent=SD-9</c> (key or id) - its children.
    /// <c>GET ?workspace=&amp;class=&amp;kind=&amp;state=open|done&amp;mine=1&amp;wql=&amp;max=</c> -
    /// a filter. A WQL condition that does not parse is refused with 400 and the parser's reason.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:editor.addon.api.title")]
    [Cache]
    public sealed class Objects : IRestApi
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Objects()
        {
        }

        /// <summary>
        /// Answers the objects the request asks for.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The objects as json.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Get(IRequest request)
        {
            var key = Parameter(request, "key");

            if (key is not null)
            {
                var @object = CoreHub.ObjectManager.GetObjectByKey(key);

                return @object is null
                    ? Json(new { items = Array.Empty<object>(), total = 0, truncated = false })
                    : Json(new { items = new[] { Project(new EditorObjectItem(@object, Category(@object)), request) }, total = 1, truncated = false });
            }

            var parent = Parameter(request, "parent");

            if (parent is not null)
            {
                var @object = Guid.TryParse(parent, out var parentId)
                    ? CoreHub.ObjectManager.GetObject(parentId)
                    : CoreHub.ObjectManager.GetObjectByKey(parent);
                var children = EditorObjectQuery.Children(@object, Max(request));

                return Json(new
                {
                    items = children.Select(x => Project(new EditorObjectItem(x, null), request)).ToArray(),
                    total = children.Count,
                    truncated = false
                });
            }

            var wql = Parameter(request, "wql");

            if (!EditorObjectQuery.TryValidate(wql, out var error))
            {
                return new ResponseBadRequest
                {
                    // the parser answers an i18n key; the author reads the sentence
                    Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = I18N.Translate(request, error) }, _jsonOptions))
                }
                    .AddHeaderContentType("application/json");
            }

            var filter = new EditorObjectFilter
            (
                Workspace: Parameter(request, "workspace"),
                Class: Parameter(request, "class"),
                Kind: Parameter(request, "kind"),
                State: Parameter(request, "state"),
                Mine: Parameter(request, "mine") is "1" or "true",
                Wql: wql,
                Max: Max(request)
            );

            var result = EditorObjectQuery.Run(filter, CoreHub.SessionManager.GetCurrentIdentityId(request));

            return Json(new
            {
                items = result.Items.Select(x => Project(x, request)).ToArray(),
                total = result.Total,
                truncated = result.Truncated
            });
        }

        /// <summary>
        /// Projects an object onto what an add-on shows of it.
        /// </summary>
        private static object Project(EditorObjectItem item, IRequest request)
        {
            var @object = item.Object;
            var category = item.Category;

            return new
            {
                key = @object.Key,
                summary = @object.Summary,
                uri = ObjectKindCatalog.ResolveDetailUri(@object)?.ToString(),
                icon = ObjectIcon.Uri(@object),
                kind = @object.Kind,
                workspace = CoreHub.WorkspaceManager.GetWorkspace(@object.WorkspaceId)?.Name,
                updated = @object.Updated.ToString("o", CultureInfo.InvariantCulture),
                state = category is null ? null : new
                {
                    label = ObjectBoardProjection.CategoryLabel(category),
                    tone = (ObjectBoardProjection.CategoryColorCss(category) ?? "wx-color-secondary").Replace("wx-color-", string.Empty)
                }
            };
        }

        /// <summary>
        /// Resolves the status category of a single object.
        /// </summary>
        private static Model.Entities.StatusCategory Category(Model.Entities.Object @object)
        {
            var @class = CoreHub.ClassManager.GetClass(@object.ClassId);
            var context = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
            var categories = CoreHub.StatusManager
                .GetStatusCategories(new WebExpress.WebIndex.Queries.Query<Model.Entities.StatusCategory>())
                .ToDictionary(x => x.Id);

            return ObjectBoardProjection.ResolveCategory(@object.Id, context, categories);
        }

        /// <summary>
        /// Reads the number of objects asked for.
        /// </summary>
        private static int Max(IRequest request)
        {
            return int.TryParse(Parameter(request, "max"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var max)
                ? Math.Clamp(max, 1, EditorObjectQuery.MaxItems)
                : 10;
        }

        /// <summary>
        /// Reads a query parameter, blank as absent.
        /// </summary>
        private static string Parameter(IRequest request, string name)
        {
            var value = request?.GetParameter(name)?.Value;

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Answers a payload as json.
        /// </summary>
        private static IResponse Json(object payload)
        {
            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _jsonOptions))
            }
                .AddHeaderContentType("application/json");
        }
    }
}
