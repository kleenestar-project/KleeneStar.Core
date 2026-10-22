using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Object.Renderers
{
    /// <summary>
    /// The built-in spreadsheet renderer: the object is a workbook, worked on in the
    /// framework's workbook dialog and read as a spreadsheet without toolbar, formula bar and
    /// editing.
    /// </summary>
    /// <remarks>
    /// The workbook is stored as its JSON in <see cref="Model.Entities.Object.Description"/> -
    /// the column prose keeps its document in - so it drafts and publishes through the same
    /// two endpoints the prose editor does, and the commit history, the audit and the draft
    /// row need nothing new. <see cref="WebControl.WorkbookText"/> is the reader that tells a
    /// workbook apart from a document and answers its words to every list.
    /// <para>
    /// It serves the <b>document</b> kind only: a sheet stands in the page tree beside the
    /// pages, a post on a timeline is an article and an issue has a mask. The surfaces are
    /// <see cref="ObjectSpreadsheetReadFragment"/> and the two subclasses of
    /// <see cref="ObjectSpreadsheetEditorFragmentBase"/>, each gated on
    /// <see cref="SpreadsheetRendererCondition"/>.
    /// </para>
    /// </remarks>
    public sealed class SpreadsheetRenderer : IObjectRenderer
    {
        /// <summary>
        /// Gets the persisted renderer key.
        /// </summary>
        public string Key => Model.Entities.ObjectRenderer.Spreadsheet;

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        public string Label => "kleenestar.core:object.renderer.spreadsheet.label";

        /// <summary>
        /// Gets the internationalization key of the one-line description.
        /// </summary>
        public string Description => "kleenestar.core:object.renderer.spreadsheet.description";

        /// <summary>
        /// Gets the icon representing the spreadsheet renderer.
        /// </summary>
        public IIcon Icon => new IconTableCells();

        /// <summary>
        /// Gets the display order; the spreadsheet follows prose and form.
        /// </summary>
        public int Order => 3;

        /// <summary>
        /// Gets the kinds the spreadsheet serves: documents alone.
        /// </summary>
        public IEnumerable<string> Kinds =>
        [
            Model.Entities.ObjectKind.Document
        ];

        /// <summary>
        /// Gets <see langword="false"/>: a workbook is one body, it has no fields.
        /// </summary>
        public bool Structured => false;
    }
}
