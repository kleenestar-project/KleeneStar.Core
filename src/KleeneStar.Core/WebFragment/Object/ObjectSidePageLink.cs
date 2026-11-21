using KleeneStar.Core.WebParameter;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Resolves the two pages a prose object keeps beside its text - its files and its relations
    /// - and the counts their entries are labelled with.
    /// </summary>
    /// <remarks>
    /// Both pages are addressed by object key alone (see
    /// <see cref="WWW.Issue._objectkey_.Attachments"/> and
    /// <see cref="WWW.Issue._objectkey_.Relations"/>), so they serve every object kind. The
    /// resolution lives here rather than in each fragment because the toolbar button and the
    /// actions-menu entry of one page have to lead to the same place and count the same things
    /// - and because a fragment that answers no object must render nothing rather than a link
    /// that cannot resolve.
    /// </remarks>
    internal static class ObjectSidePageLink
    {
        /// <summary>
        /// Resolves the object the request addresses.
        /// </summary>
        /// <param name="renderContext">The render context carrying the object key.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        public static Model.Entities.Object ResolveObject(IRenderControlContext renderContext)
        {
            var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();

            return CoreHub.ObjectManager.GetObjectByKey(objectKey);
        }

        /// <summary>
        /// Resolves the attachment page of the addressed object.
        /// </summary>
        /// <param name="renderContext">The render context carrying the object key.</param>
        /// <returns>The bound route, or <see langword="null"/> when no object is addressed.</returns>
        public static IUri ResolveAttachmentsUri(IRenderControlContext renderContext)
        {
            return ResolveObject(renderContext) is null
                ? null
                : CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Attachments>()?
                    .BindParameters(renderContext.Request);
        }

        /// <summary>
        /// Resolves the relation page of the addressed object.
        /// </summary>
        /// <param name="renderContext">The render context carrying the object key.</param>
        /// <returns>The bound route, or <see langword="null"/> when no object is addressed.</returns>
        public static IUri ResolveRelationsUri(IRenderControlContext renderContext)
        {
            return ResolveObject(renderContext) is null
                ? null
                : CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Relations>()?
                    .BindParameters(renderContext.Request);
        }

        /// <summary>
        /// Resolves the impact page of the addressed object - what changing it touches.
        /// </summary>
        /// <remarks>
        /// Resolved here beside the relation page because the two belong together: the relations
        /// are what an object holds, the impact is what those relations do when something
        /// changes, and every surface that offers the first should be able to offer the second.
        /// </remarks>
        /// <param name="renderContext">The render context carrying the object key.</param>
        /// <returns>The bound route, or <see langword="null"/> when no object is addressed.</returns>
        public static IUri ResolveImpactUri(IRenderControlContext renderContext)
        {
            return ResolveObject(renderContext) is null
                ? null
                : CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.Impact>()?
                    .BindParameters(renderContext.Request);
        }

        /// <summary>
        /// Builds the label of an entry: the caption, followed by the number of entries the page
        /// holds when it holds any.
        /// </summary>
        /// <remarks>
        /// The count is what makes the entry worth reading before clicking it - "Files" says
        /// nothing, "Files 3" says the document has some. A zero is left off rather than shown,
        /// because an empty page is what the plain caption already promises.
        /// </remarks>
        /// <param name="caption">The already translated caption.</param>
        /// <param name="count">The number of entries.</param>
        /// <returns>The label.</returns>
        public static string Label(string caption, int count)
        {
            return count <= 0
                ? caption
                : caption + " " + count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Counts the files attached to the object.
        /// </summary>
        /// <param name="object">The object, or <see langword="null"/>.</param>
        /// <returns>The number of attachments.</returns>
        public static int CountAttachments(Model.Entities.Object @object)
        {
            return @object is null ? 0 : CoreHub.AttachmentManager.GetAttachments(@object.Id).Count();
        }

        /// <summary>
        /// Counts the relations the object holds.
        /// </summary>
        /// <param name="object">The object, or <see langword="null"/>.</param>
        /// <returns>The number of relations.</returns>
        public static int CountRelations(Model.Entities.Object @object)
        {
            return @object is null ? 0 : CoreHub.ObjectRelationManager.GetRelations(@object.Id).Count();
        }
    }
}
