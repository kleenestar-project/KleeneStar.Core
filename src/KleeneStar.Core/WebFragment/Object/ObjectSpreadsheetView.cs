using KleeneStar.Core.WebControl;
using WebExpress.WebUI.WebControl;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Builds the reading view of a workbook body: the framework's spreadsheet, read-only and
    /// without toolbar, formula bar and status bar - the sheet tabs stay, a workbook may have
    /// several. The one shape every surface that shows the body of a spreadsheet document
    /// uses: the reading view, the document home, the preview pane and the draft comparison.
    /// </summary>
    public static class ObjectSpreadsheetView
    {
        /// <summary>
        /// The grid height of the reading view of a document, which is the page.
        /// </summary>
        public const string PageHeight = "70vh";

        /// <summary>
        /// The grid height where the workbook is one section among others.
        /// </summary>
        public const string SectionHeight = "18rem";

        /// <summary>
        /// Builds the view of a stored body when it is a workbook.
        /// </summary>
        /// <param name="id">The element id.</param>
        /// <param name="value">The stored description.</param>
        /// <param name="height">The grid height.</param>
        /// <param name="placeholder">The i18n key shown in place of an empty workbook, or
        /// <see langword="null"/> to show the empty grid.</param>
        /// <returns>The control, or <see langword="null"/> when the value is no workbook.</returns>
        public static IControl Create(string id, string value, string height, string placeholder = null)
        {
            var workbook = WorkbookText.Read(value);

            if (workbook is null)
            {
                return null;
            }

            if (workbook.IsEmpty && placeholder is not null)
            {
                return new ControlText(id)
                {
                    Text = _ => placeholder,
                    Format = _ => TypeFormatText.Paragraph,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                };
            }

            return new ControlSpreadsheet(id)
            {
                Workbook = _ => workbook,
                ReadOnly = _ => true,
                Toolbar = _ => false,
                FormulaBar = _ => false,
                StatusBar = _ => false,
                GridHeight = _ => height,
                Classes = ["ks-spreadsheet-content"]
            };
        }
    }
}
