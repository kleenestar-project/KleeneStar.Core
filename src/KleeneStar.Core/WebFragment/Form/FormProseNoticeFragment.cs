using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Form
{
    /// <summary>
    /// Stands in for the form editor on a form whose class renders its objects as prose.
    /// </summary>
    /// <remarks>
    /// Prose has no fields: an object of such a class is one text in the WYSIWYG editor, and
    /// neither the reading view nor the editor reads the form's structure. Offering the editor
    /// anyway would let an administrator lay out fields that are never shown and never asked
    /// for, and then wonder where they went. So the editor is withheld
    /// (<see cref="FormDetailViewFragment"/> is gated on the opposite condition) and this
    /// notice says why, rather than the page standing empty.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Index>]
    [Condition<FormProseRendererCondition>]
    [Cache]
    public sealed class FormProseNoticeFragment : FragmentControlEmptyState
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public FormProseNoticeFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            // the glyph the prose renderer is listed under, so the notice reads as the renderer
            // speaking rather than as a fault
            Icon = _ => new IconAlignLeft();
            Title = _ => "kleenestar.core:form.editor.prose.title";
            Message = _ => "kleenestar.core:form.editor.prose.message";
        }
    }
}
