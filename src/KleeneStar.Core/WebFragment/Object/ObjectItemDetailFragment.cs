using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The *Details* of the object page: the field values of the object as the view form of its
    /// class arranges them, as an <see cref="ObjectFieldSheet"/>.
    /// </summary>
    /// <remarks>
    /// The fragment resolves the object addressed by the request and the
    /// <see cref="FormType.View"/> form of its class, and shows one sheet per tab of that form
    /// (a tab strip when the form has several). Every value is shown the way its type reads,
    /// and a caller who may change the object edits it in place - each value writes through the
    /// object endpoint on its own, which is why the sheet carries the presence and carets of
    /// whoever else has the object open. The description, the comments and the property column
    /// are other fragments' (<see cref="ObjectDescriptionCardFragment"/>,
    /// <see cref="ObjectCommentDockFragment"/>).
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Cache]
    public sealed class ObjectItemDetailFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its operation.
        /// Cannot be null.
        /// </param>
        public ObjectItemDetailFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation. When no view form is configured, or
        /// the form places no field the sheet would show, nothing is rendered: a captioned
        /// section with nothing under it reads as a fault.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered section, or <c>null</c> when no
        /// object can be resolved from the request or the section would be empty.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var keyParam = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(keyParam);

            if (@object is null)
            {
                return null;
            }

            var body = new HtmlElementTextContentDiv()
            {
                Id = Id,
                Class = Css.Concatenate("wx-kleenestar-object-detail", GetClasses(renderContext)),
                Style = GetStyles(renderContext),
                Role = Role?.Invoke(renderContext)
            };

            body.AddUserAttribute("data-object-id", @object.Id.ToString());
            body.AddUserAttribute("data-object-key", @object.Key);

            // the property column carries the state with its transitions and the dates, the
            // description has its own card
            var details = ObjectFieldSheet.BuildForm
            (
                "object-detail",
                @object,
                x => !ObjectFieldSheet.IsDescriptionAlias(x) && x.FieldType is not (FieldType.Workflow or FieldType.Date),
                ContentAuthorization.MayWrite(@object, renderContext?.Request),
                ResolveObjectRestUri(@object, renderContext),
                collapseEmpty: false
            );

            if (details is null)
            {
                return null;
            }

            body.Add(details.Render(renderContext, visualTree));

            var section = new ControlSection("object-detail-section")
            {
                Header = _ => "kleenestar.core:object.detail.card.header",
                HeaderIcon = _ => new IconTableList(),
                Layout = _ => TypeLayoutSection.Rule
            };

            // the sheet is where two people edit the same object at the same time - every value
            // writes through on its own - so it is the surface that carries the presence, the
            // pointers and the carets of whoever else has the object open
            section.Add(ObjectCollaborativeHost.Create
            (
                @object,
                "detail",
                renderContext,
                new ControlHtml("object-detail-body-" + @object.Id.ToString("N"))
                {
                    Html = _ => body.ToString()
                }
            ));

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns the object endpoint bound to the object's id, which the inline edits write to.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The address, or <c>null</c> without a sitemap.</returns>
        internal static IUri ResolveObjectRestUri(Model.Entities.Object @object, IRenderControlContext renderContext)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();

            if (uri is null)
            {
                return null;
            }

            var withQuery = uri.Add(new UriQuery("id", @object.Id.ToString()));

            return renderContext?.Request is null
                ? withQuery
                : withQuery.BindParameters(renderContext.Request);
        }
    }
}
