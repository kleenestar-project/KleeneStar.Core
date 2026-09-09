using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The prose editor on the reading views of the document and blog kinds. It renders closed
    /// and is opened by <see cref="ObjectProseEditButtonFragment"/>, which addresses it by
    /// <see cref="ObjectProseEditorFragmentBase.ModalId"/>.
    /// </summary>
    /// <remarks>
    /// It sits at body level rather than in the content column, like the other dialogs of the
    /// object views: an overlay nested inside a column can be clipped by it.
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<ProseRendererCondition>]
    [Cache]
    public sealed class ObjectProseEditorFragment : ObjectProseEditorFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectProseEditorFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
