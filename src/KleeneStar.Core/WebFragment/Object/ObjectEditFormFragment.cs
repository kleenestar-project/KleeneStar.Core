using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The structured input mask on the issue edit dialog. The visible structure is derived
    /// dynamically from the <see cref="Model.Entities.FormType.Edit"/> form configured for
    /// the object's class as exposed via <see cref="WWW.Api._1_.Forms.FormEditor"/>; tabs,
    /// layout groups, and field references defined there are reproduced one-to-one by
    /// <see cref="ObjectStructuredEditFormFragmentBase"/>.
    /// </summary>
    /// <remarks>
    /// It carries no condition: the issue kind offers exactly one renderer, so the mask is
    /// what an issue is edited through and there is nothing for a condition to decide
    /// between. The same mask on the document and blog kinds - where prose is the
    /// alternative - is <see cref="ObjectFormEditFragment"/>.
    /// </remarks>
    [Title("kleenestar.core:object.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Edit>]
    [Cache]
    public sealed class ObjectEditFormFragment : ObjectStructuredEditFormFragmentBase
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }
    }
}
