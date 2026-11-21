using KleeneStar.Core.WebParameter;
using System;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// The relation administration of a class: the <see cref="ControlDataRelationEditor"/> on
    /// <see cref="WWW.Relations._classid_.Index"/>, listing the relations objects of the class
    /// may hold and opening the editor that defines and changes them.
    /// </summary>
    /// <remarks>
    /// The table, its editor dialog, the reordering and the activation toggle are built on the
    /// client from the data service alone, so this fragment only supplies the endpoint and the
    /// two facts the surface cannot derive: which class is being administered, and an example
    /// key to write the preview sentence with.
    /// <para>
    /// The class travels as its <b>name</b> rather than as its id, because that is what a
    /// relation stores in its accepted-class list and what the target of a stored relation is
    /// validated against. The route still carries the id - it addresses the class - but what
    /// the surface reasons about is the name.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Relations._classid_.Index>]
    [Cache]
    public sealed class ClassRelationEditorFragment : FragmentControlPanel
    {
        /// <summary>
        /// Gets the REST-backed relation type administration control.
        /// </summary>
        public ControlDataRelationEditor Relations { get; } = new("class-relation-types")
        {
            Class = renderContext => ResolveClassName(renderContext),
            Sample = renderContext => ResolveSampleKey(renderContext)
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        public ClassRelationEditorFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Relations.DataService<global::KleeneStar.Core.WWW.Api._1_.RelationTypes._classid_.Index>();
        }

        /// <summary>
        /// Renders the relation administration. Returns <c>null</c> when the fragment's render
        /// conditions exclude it or when the route addresses no class, because an editor that
        /// cannot name the class it administers would write its rules against nothing.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            return string.IsNullOrEmpty(ResolveClassName(renderContext))
                ? null
                : Relations.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the name of the class the route addresses.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The class name, or an empty string when the route addresses none.</returns>
        private static string ResolveClassName(IRenderControlContext renderContext)
        {
            return ResolveClassId(renderContext) is var id && id != Guid.Empty
                ? CoreHub.ClassManager.GetClass(id)?.Name ?? string.Empty
                : string.Empty;
        }

        /// <summary>
        /// Resolves the key the editor writes its preview sentence with: the key of an object
        /// that actually has the class, so the preview reads like the relations the user will
        /// go on to establish. With no object of the class yet, the client falls back to the
        /// class name on its own.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The example key, or <c>null</c>.</returns>
        private static string ResolveSampleKey(IRenderControlContext renderContext)
        {
            var classId = ResolveClassId(renderContext);

            if (classId == Guid.Empty)
            {
                return null;
            }

            var query = new Query<ObjectEntity>()
                .Where(x => x.ClassId == classId)
                .WithPaging(0, 1);

            return CoreHub.ObjectManager.GetObjects(query).FirstOrDefault()?.Key;
        }

        /// <summary>
        /// Reads the class id from the route.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The class id, or <see cref="Guid.Empty"/>.</returns>
        private static Guid ResolveClassId(IRenderControlContext renderContext)
        {
            var parameter = renderContext?.Request?.GetParameter<ClassIdParameter>();

            return Guid.TryParse(parameter?.Value, out var id) ? id : Guid.Empty;
        }
    }
}
