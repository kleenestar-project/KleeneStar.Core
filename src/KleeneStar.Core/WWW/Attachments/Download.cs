using System;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebResource;

namespace KleeneStar.Core.WWW.Attachments
{
    /// <summary>
    /// Streams the binary payload of an object attachment for download. The attachment id is
    /// supplied through the <c>id</c> query parameter (e.g.
    /// <c>/attachments/download?id={guid}</c>); the response carries the attachment's content
    /// type so the browser renders or saves it appropriately.
    /// </summary>
    [Scope<IScopeGeneral>]
    public sealed class Download : ResourceBinary
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="resourceContext">The context of the resource.</param>
        public Download(IResourceContext resourceContext)
            : base(resourceContext)
        {
        }

        /// <summary>
        /// Processing of the resource. Resolves the attachment by its <c>id</c> query
        /// parameter and returns its binary payload, or <see cref="ResponseNotFound"/> when
        /// the id is missing/invalid or the attachment does not exist.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public override IResponse Process(IRequest request)
        {
            if (!Guid.TryParse(request?.GetParameter("id")?.Value, out var id))
            {
                return new ResponseNotFound();
            }

            var attachment = CoreHub.AttachmentManager.GetAttachment(id);

            // a file is as visible as the object it hangs on - its permissions and its security
            // level - and one the caller may not see is answered as not there at all
            if (attachment is null || CoreHub.ObjectManager.GetObject(attachment.ObjectId) is null)
            {
                return new ResponseNotFound();
            }

            var data = attachment.Content;
            if (data is null || data.Length == 0)
            {
                // rows seeded before the Content column existed carry no payload; synthesize a
                // small placeholder so the download still yields a file rather than a 404.
                data = System.Text.Encoding.UTF8.GetBytes(
                    $"KleeneStar attachment\r\n{attachment.FileName}\r\n{attachment.Description}\r\n");
            }

            Data = data;

            var response = base.Process(request);
            response.Header.ContentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;
            response.Header.CacheControl = "private, max-age=0";

            return response;
        }
    }
}
