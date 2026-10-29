using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebWorkspaceTemplate;
using System;
using System.Text;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Workspaces
{
    /// <summary>
    /// Draws the banner a workspace's home page and opening post start with, at
    /// <c>/api/1/workspaces/banner?workspace={id}&amp;caption={text}</c>, as an SVG image.
    /// </summary>
    /// <remarks>
    /// The pages used to carry the banner as an inline <c>data:</c> image. Since WebExpress
    /// 2026-10-29 the reading view cleans stored markup through the editor's url policy, which
    /// keeps an image address only when it is http(s) or relative, so the picture lost its
    /// source and the pages showed a broken image. The page now stores this address instead
    /// (<see cref="WorkspaceTemplateContent"/>), and the banner is drawn from the workspace as it
    /// is - its key and accent colour - with the caption the page was written with, in the
    /// creator's language.
    /// <para>
    /// A workspace the caller may not read answers 404, like one that does not exist, so the
    /// address tells nobody which workspaces there are. The caption is text, escaped into the
    /// drawing; it cannot add markup to it.
    /// </para>
    /// <para>
    /// It is a REST endpoint rather than a resource although it answers an image: only a REST
    /// endpoint's <c>_1_</c> folder routes as <c>1</c>, a resource beside it would sit at
    /// <c>/api/_1_/…</c>.
    /// </para>
    /// </remarks>
    public sealed class Banner : IRestApi
    {
        /// <summary>
        /// The query parameter naming the workspace.
        /// </summary>
        public const string WorkspaceParameter = "workspace";

        /// <summary>
        /// The query parameter carrying the line set under the key.
        /// </summary>
        public const string CaptionParameter = "caption";

        /// <summary>
        /// The longest caption drawn; the banner has room for one line.
        /// </summary>
        private const int MaxCaptionLength = 120;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Banner()
        {
        }

        /// <summary>
        /// Draws the banner of the workspace the request names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The SVG image, or 404 for a workspace that is unknown or not readable.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Get(IRequest request)
        {
            if (!Guid.TryParse(request?.GetParameter(WorkspaceParameter)?.Value, out var workspaceId))
            {
                return new ResponseNotFound();
            }

            var workspace = CoreHub.WorkspaceManager?.GetWorkspace(workspaceId);

            if (workspace is null || !ContentVisibility.MayRead(workspace.Id))
            {
                return new ResponseNotFound();
            }

            var caption = request.GetParameter(CaptionParameter)?.Value ?? string.Empty;

            if (caption.Length > MaxCaptionLength)
            {
                caption = caption[..MaxCaptionLength];
            }

            var data = Encoding.UTF8.GetBytes(WorkspaceTemplateContent.DrawBanner(workspace, caption));

            var response = new ResponseOK();
            response.Header.ContentLength = data.Length;
            response.Header.ContentType = "image/svg+xml";
            // the key and the colour can change, so the browser asks again rather than keeping
            // a stale picture; private, because the answer depends on who may read the workspace
            response.Header.CacheControl = "private, no-cache";
            response.Content = data;

            return response;
        }
    }
}
