using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The workbook editor on the edit route of a spreadsheet document
    /// (<c>/document/{objectkey}/edit</c>), where it opens with the page - the counterpart of
    /// <see cref="ObjectProseEditorPageFragment"/>, and the same draft either way.
    /// </summary>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Edit>]
    [Condition<SpreadsheetRendererCondition>]
    [Cache]
    public sealed class ObjectSpreadsheetEditorPageFragment : ObjectSpreadsheetEditorFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectSpreadsheetEditorPageFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Show = _ => true;
        }
    }
}
