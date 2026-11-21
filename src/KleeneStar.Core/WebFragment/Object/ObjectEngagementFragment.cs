using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Closes a document or a post off with its labels and its likes, and lets the reader add
    /// to both.
    /// </summary>
    /// <remarks>
    /// The line stands under the text whichever renderer draws it - prose or the form sheet -
    /// so it is a fragment of its own rather than a part of either reading view.
    /// <list type="bullet">
    /// <item>The labels are the framework's tag control over <c>/api/1/tags/{objectkey}</c>:
    /// the chips and, for a caller who may change the object, the <c>+</c> that adds and removes
    /// them. A reader who may not change it sees the chips read-only, and nothing at all when
    /// there are none.</item>
    /// <item>The like is the one of the document overview: the figure, and for a signed-in
    /// caller the click that joins it (<c>/api/1/objects/like</c>).</item>
    /// </list>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Order(100)]
    [Cache]
    public sealed class ObjectEngagementFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectEngagementFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the labels and the likes of the object the page shows.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The line, or <see langword="null"/> when the page shows no object.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var request = renderContext?.Request;
            var @object = CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null)
            {
                return null;
            }

            var id = @object.Id.ToString("N");
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var mayWrite = identityId != Guid.Empty && ContentAuthorization.MayWrite(@object, request);
            var hasTags = CoreHub.ObjectTagManager.GetTags(@object.Id).Any();

            var line = new ControlFlex("object-engagement-" + id)
            {
                Classes = ["ks-object-engagement"]
            };

            if (mayWrite || hasTags)
            {
                line.Add(new ControlIcon("object-tags-icon-" + id)
                {
                    Icon = _ => new IconTags(),
                    Classes = ["ks-object-tags-icon"]
                });

                line.Add(new ControlDataTag("object-tags-" + id)
                {
                    Readonly = _ => !mayWrite,
                    Placeholder = _ => "kleenestar.core:object.tags.placeholder",
                    Classes = ["ks-object-tags"],
                    ServiceFactory = ctx => DataServiceDescriptor.QueryData
                    (
                        CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Tags._objectkey_.Index>()
                            .BindParameters(ctx.Request)
                            .ToString()
                    )
                });
            }

            line.Add(new ControlLike("object-likes-" + id)
            {
                Value = _ => CoreHub.ObjectManager.GetLikeCount(@object.Id),
                Active = _ => CoreHub.ObjectManager.IsLiked(identityId, @object.Id),
                Label = _ => "kleenestar.core:object.kind.documents.metric.likes",

                // no address for a reader who is not signed in: a like belongs to somebody, and
                // offering the click only to answer 401 is worse than not offering it
                Uri = _ => identityId == Guid.Empty
                    ? null
                    : CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Like>(),
                Payload = _ => System.Text.Json.JsonSerializer.Serialize(new { @object = @object.Key })
            });

            return line.Render(renderContext, visualTree);
        }
    }
}
