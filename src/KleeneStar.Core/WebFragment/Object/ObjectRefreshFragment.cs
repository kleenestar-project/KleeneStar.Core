using KleeneStar.Core.WebParameter;
using WebExpress.WebApp.WebMessageQueue;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Names the object an object detail page or preview shows, in meta tags of the page head,
    /// so the refresh script (<see cref="WebInclude.IncludeObjectRefreshScript"/>) can tell a
    /// dialog that changed <em>this</em> record from one that changed something else, run the
    /// links to the object's transition endpoint in the background and follow the announcements
    /// of the data change channel for this object.
    /// </summary>
    /// <remarks>
    /// The route carries the object's key, the dialogs and the change channel address the
    /// record by its id; the two meet here. The fragment renders nothing into its section - the
    /// head is written through the visual tree, which is why it is a fragment and not a page
    /// concern: every kind's detail page gets it by scope, and none of them has to know the
    /// script exists. The preview is embedded into a master-detail pane without its head; the
    /// script reads the tags from the page the pane loaded.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WebScope.IScopeObjectKindDetail>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Preview>]
    [Cache]
    public sealed class ObjectRefreshFragment : FragmentControlPanel
    {
        /// <summary>
        /// The name of the meta tag carrying the id of the object the page shows. It has to
        /// match the name the script reads.
        /// </summary>
        public const string MetaName = "kleenestar.object";

        /// <summary>
        /// The name of the meta tag carrying the address of the transition endpoint of the object
        /// the page shows; the script runs the links of the status menu that point there in the
        /// background instead of navigating to them. It has to match the name the script reads.
        /// </summary>
        public const string TransitionMetaName = "kleenestar.object.transition";

        /// <summary>
        /// The name of the meta tag carrying the domain objects are announced under on the data
        /// change channel. It has to match the name the script reads.
        /// </summary>
        public const string DomainMetaName = "kleenestar.object.domain";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectRefreshFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Writes the meta tags and renders nothing. Returns <c>null</c> always, and writes
        /// nothing when the route addresses no object the caller may see - a page about
        /// nothing has nothing to keep up to date.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns><c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var objectKey = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = objectKey is not null ? CoreHub.ObjectManager.GetObjectByKey(objectKey) : null;

            if (@object is null)
            {
                return null;
            }

            visualTree?.AddMeta(MetaName, @object.Id.ToString());
            visualTree?.AddMeta(DomainMetaName, DataChangedNotifier.DomainName(typeof(Model.Entities.Object)));

            // the endpoint the status menu links to (ObjectWorkflowTargets.TransitionUri),
            // without the field and the state each entry adds
            var transition = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Transitions._objectkey_.Index>()?
                .BindParameters(new ObjectKeyParameter(@object.Key));

            if (transition is not null)
            {
                visualTree?.AddMeta(TransitionMetaName, transition.ToString());
            }

            return null;
        }
    }
}
