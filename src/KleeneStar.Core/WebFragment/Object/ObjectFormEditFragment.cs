using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The structured input mask on the edit routes of the document and blog kinds
    /// (<c>/document/{objectkey}/edit</c>, <c>/blog/{objectkey}/edit</c>) - the writing
    /// surface of the form renderer, standing where the WYSIWYG editor stands for a
    /// prose-rendered object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same mask as the issue edit dialog, built by the same base from the same
    /// <see cref="Model.Entities.FormType.Edit"/> form - see
    /// <see cref="ObjectStructuredEditFormFragmentBase"/>. What differs is where it stands:
    /// a page rather than a dialog, and the primary content of it, because a form-rendered
    /// document <em>is</em> its mask.
    /// </para>
    /// <para>
    /// <see cref="FormRendererCondition"/> gates it against
    /// <see cref="ObjectProseEditorPageFragment"/>, which is scoped to the same two routes:
    /// whichever renderer the object's class names, exactly one of the two draws. There is
    /// no draft here and no publish button - a draft is the prose editor's answer to writing
    /// a long text over several sittings, while a field is saved when it is filled in.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.renderer.form.edit.title")]
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Edit>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Edit>]
    [Condition<FormRendererCondition>]
    [Cache]
    public sealed class ObjectFormEditFragment : ObjectStructuredEditFormFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectFormEditFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
