using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The prose editor on the edit routes of the document and blog kinds
    /// (<c>/document/{objectkey}/edit</c>, <c>/blog/{objectkey}/edit</c>), where it opens with
    /// the page.
    /// </summary>
    /// <remarks>
    /// The route exists so the editor can be linked to directly - from a notification, a mail, a
    /// bookmark - and a page that <i>is</i> the editor has no trigger to wait for. Everything
    /// else about it is identical to the dialog on the reading view, including the draft: the
    /// same object opened either way resumes the same unpublished text.
    /// </remarks>
    [Section<SectionBodySecondary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Edit>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Edit>]
    [Condition<ProseRendererCondition>]
    [Cache]
    public sealed class ObjectProseEditorPageFragment : ObjectProseEditorFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context in which the fragment is used.</param>
        public ObjectProseEditorPageFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Show = _ => true;
        }
    }
}
