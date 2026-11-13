using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebReport
{
    /// <summary>
    /// A file format a single object is exported in, offered as a tile of the export dialog and
    /// addressed as <c>/api/1/exports/{objectkey}?format={Key}</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="WebManager.IObjectExportManager"/> collects every <b>public sealed</b> class
    /// implementing this interface with a public parameterless constructor from the core and the
    /// plugins (WebExpress <c>PluginTypeCatalog</c>). A plugin ships a format by declaring such a
    /// class; no registration call is needed. The latest offer of a key is in use.
    /// </remarks>
    public interface IObjectExportFormat
    {
        /// <summary>
        /// Gets the key: the value of the <c>format</c> parameter, lower case, unique.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Gets the file extension without dot.
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// Gets the i18n key of the tile header.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// Gets the i18n key of the tile text.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the i18n key of the tile footer naming the content of the file.
        /// </summary>
        string Content { get; }

        /// <summary>
        /// Gets the badge text of the tile, the short name of the format.
        /// </summary>
        string Badge { get; }

        /// <summary>
        /// Gets the colour of the badge marker.
        /// </summary>
        TypeColorTile Color { get; }

        /// <summary>
        /// Gets the icon of the tile.
        /// </summary>
        IIcon Icon { get; }

        /// <summary>
        /// Gets the position of the tile; lower values come first.
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Returns the query parameters the download address carries beside <c>format</c>, derived
        /// from the request of the dialog submit (for example the CSV delimiter of the culture).
        /// </summary>
        /// <param name="request">The request of the dialog submit.</param>
        /// <returns>The parameters.</returns>
        IEnumerable<IUriQuery> Parameters(IRequest request) => [];

        /// <summary>
        /// Writes the record as a file download.
        /// </summary>
        /// <param name="record">The collected content (<see cref="ObjectExport.Collect"/>).</param>
        /// <param name="request">The download request, carrying culture and parameters.</param>
        /// <returns>The response; <see cref="ObjectExport.Attachment"/> builds one.</returns>
        IResponse Write(ObjectExportRecord record, IRequest request);
    }
}
