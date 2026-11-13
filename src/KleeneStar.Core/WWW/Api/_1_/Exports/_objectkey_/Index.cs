using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebReport;
using KleeneStar.Core.WebRestApi;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WWW.Api._1_.Exports._objectkey_
{
    /// <summary>
    /// Exports the object of the route in a format of <see cref="WebManager.IObjectExportManager"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>GET ?format={key}</c> collects the object (<see cref="ObjectExport.Collect"/>) and
    /// answers what the format writes (<see cref="IObjectExportFormat.Write"/>), an attachment
    /// named after the object key. Without <c>format</c> it answers <c>{ format }</c> with the key
    /// of the first format, the value the dialog form loads. <c>POST</c> is the submit of the
    /// export dialog (<c>Format</c> as form data or JSON) and answers <c>{ uri }</c> with the
    /// address of that <c>GET</c>, extended by <see cref="IObjectExportFormat.Parameters"/>; the
    /// dialog follows it (<c>TypeRestFormSuccess.FollowUri</c>).
    /// </para>
    /// <para>
    /// The object is read through <see cref="WebManager.IObjectManager.GetObjectByKey(string)"/>,
    /// so content visibility and security levels apply: an object the caller may not read
    /// answers 404. A key no format carries answers 400.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.export.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// The payload of the export dialog.
        /// </summary>
        private sealed class ExportPayload
        {
            /// <summary>
            /// Gets or sets the requested format key.
            /// </summary>
            public string Format { get; set; }
        }

        /// <summary>
        /// Answers the export file.
        /// </summary>
        /// <param name="request">The request naming the object and the format.</param>
        /// <returns>The file; without <c>format</c> the starting values of the dialog; 400 for an
        /// unknown format, 404 for an unreadable object.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            var requested = request.GetParameter("format")?.Value;

            // the export dialog is a REST form and loads its starting values from this address
            if (string.IsNullOrWhiteSpace(requested))
            {
                return JsonApi.Json(new { format = CoreHub.ObjectExportManager.Formats.FirstOrDefault()?.Key });
            }

            var format = CoreHub.ObjectExportManager.Get(requested);

            if (format is null)
            {
                return JsonApi.Refuse(I18N.Translate(request, "kleenestar.core:object.export.format.refused"));
            }

            return format.Write(ObjectExport.Collect(@object, request), request);
        }

        /// <summary>
        /// Answers the address of the export file for the format chosen in the dialog.
        /// </summary>
        /// <param name="request">The request carrying the dialog payload.</param>
        /// <returns><c>{ uri }</c>, 400 for an unknown format, 404 for an unreadable object.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Prepare(IRequest request)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null)
            {
                return new ResponseNotFound();
            }

            var format = CoreHub.ObjectExportManager.Get(JsonApi.Read<ExportPayload>(request)?.Format);

            if (format is null)
            {
                return JsonApi.Refuse(I18N.Translate(request, "kleenestar.core:object.export.format.refused"));
            }

            var uri = CoreHub.GetUri<Index>()?
                .BindParameters(new ObjectKeyParameter(@object.Key))
                .Add([new UriQuery("format", format.Key), .. format.Parameters(request) ?? []]);

            return JsonApi.Json(new { uri = uri?.ToString() });
        }
    }
}
