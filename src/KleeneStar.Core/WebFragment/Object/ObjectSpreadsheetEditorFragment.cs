using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The workbook editor on the reading view of a spreadsheet document. It renders closed
    /// and is opened by <see cref="ObjectSpreadsheetEditButtonFragment"/>, which addresses it by
    /// <see cref="ObjectSpreadsheetEditorFragmentBase.ModalId"/>.
    /// </summary>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Condition<SpreadsheetRendererCondition>]
    [Cache]
    public sealed class ObjectSpreadsheetEditorFragment : ObjectSpreadsheetEditorFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectSpreadsheetEditorFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
