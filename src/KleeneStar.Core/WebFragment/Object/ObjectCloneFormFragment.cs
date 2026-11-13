using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The clone wizard of an object (<see cref="ControlDataWizard"/> in mode
    /// <see cref="TypeRestFormMode.Clone"/> over <c>/api/1/objects</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steps, built per request from the object's class:
    /// <list type="number">
    ///   <item><c>object</c>: the summary of the copy (required) and the description in the prose
    ///   editor (<see cref="TypeEditTextFormat.Wysiwyg"/>).</item>
    ///   <item><c>details</c>: the create form of the class (<see cref="FormType.Create"/>, else the
    ///   edit form) through <see cref="ObjectFormLayout.BuildItems"/>, its tabs as a tab group like
    ///   the view form, the dialog inputs preset from the original. A workflow field has no input
    ///   (the copy starts in the entry state); fields named <c>Summary</c> or <c>Description</c>
    ///   alias the attributes of the first step and attachment fields belong to the original,
    ///   both are left out. Without inputs there is no such step.</item>
    ///   <item><c>securitylevel</c>: <see cref="ObjectFormLayout.CreateSecurityLevelNotice"/> and the
    ///   tiles of <see cref="SecurityLevelTiles"/>, preset with the classification of the original;
    ///   present when the class has a level the caller may assign or the notice applies.</item>
    /// </list>
    /// </para>
    /// <para>
    /// The wizard loads the clone values (<c>mode=clone</c>: summary with the copy suffix,
    /// description, field values, security level) and posts all steps at once with the id of the
    /// original. The endpoint answers <c>{ created, id, key, uri }</c>, which
    /// <c>objectcreated.js</c> follows to the copy.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.clone.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Clone>]
    [Cache]
    public sealed class ObjectCloneFormFragment : FragmentControlDataWizard
    {
        /// <summary>
        /// Gets the summary input of the copy.
        /// </summary>
        public ControlFormItemInputText Summary { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Object.Summary),
            Label = _ => "kleenestar.core:object.summary.label",
            Placeholder = _ => "kleenestar.core:object.summary.placeholder",
            Help = _ => "kleenestar.core:object.summary.help",
            Required = _ => true
        };

        /// <summary>
        /// Gets the description input of the first step.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new ControlFormItemInputText()
        {
            Name = _ => nameof(Model.Entities.Object.Description),
            Label = _ => "kleenestar.core:object.description.label",
            Placeholder = _ => "kleenestar.core:object.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectCloneFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();

            Mode = _ => TypeRestFormMode.Clone;
            FinishLabel = _ => "webexpress.webapp:clone.label";
            FinishIcon = _ => new IconClone();
            ItemId = renderContext => CoreHub.ObjectManager
                .GetObjectByKey(renderContext.Request.GetParameter<ObjectKeyParameter>())?
                .Id.ToString();
        }

        /// <summary>
        /// Renders the wizard with the steps of the object's class.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Check(renderContext?.Request))
            {
                return null;
            }

            var @object = CoreHub.ObjectManager.GetObjectByKey(renderContext.Request.GetParameter<ObjectKeyParameter>());

            if (@object is null)
            {
                return null;
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

            return Render(renderContext, visualTree, BuildPages(renderContext, @object, identityId));
        }

        /// <summary>
        /// Builds the steps of the wizard.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="object">The original.</param>
        /// <param name="identityId">The identity the wizard is rendered for.</param>
        /// <returns>The steps.</returns>
        private IEnumerable<IControlDataWizardPage> BuildPages(IRenderControlContext renderContext, Model.Entities.Object @object, Guid identityId)
        {
            var first = new ControlDataWizardPage("object")
            {
                Title = _ => "kleenestar.core:object.clone.step.object.title",
                Subtitle = _ => "kleenestar.core:object.clone.step.object.subtitle",
                SummarySource = _ => nameof(Model.Entities.Object.Summary)
            };
            first.Add(Summary, Description);

            yield return first;

            // the copy is a new record and is filed through the create form; a class without one
            // uses its edit form. A field named after a system attribute (Summary, Description)
            // holds no value row and is edited on the first step; an attachment belongs to the
            // original
            var form = ObjectFormLayout.ResolveStandardForm(@object.ClassId, FormType.Create) is { Tabs.Count: > 0 } create
                ? create
                : ObjectFormLayout.ResolveStandardForm(@object.ClassId, FormType.Edit);
            var values = ObjectFieldSheet.LoadValues(@object.Id).ToDictionary(x => x.Key, x => x.Value.Data);
            var details = ObjectFormLayout.BuildItems(form, @object.ClassId, values: values, include: f =>
                    !ObjectFieldSheet.IsDescriptionAlias(f)
                    && !string.Equals(f.Name, nameof(Model.Entities.Object.Summary), StringComparison.OrdinalIgnoreCase)
                    && f.FieldType != FieldType.Attachment)
                .ToList();

            if (details.Count > 0)
            {
                var page = new ControlDataWizardPage("details")
                {
                    Title = _ => "kleenestar.core:object.clone.step.details.title",
                    Subtitle = _ => "kleenestar.core:object.clone.step.fields.subtitle"
                };

                page.Add(details);

                yield return page;
            }

            var notice = ObjectFormLayout.CreateSecurityLevelNotice(@object.ClassId, identityId, @object.SecurityLevelId);
            var tiles = SecurityLevelTiles.Create(renderContext, @object, identityId);

            if (notice is null && tiles is null)
            {
                yield break;
            }

            var security = new ControlDataWizardPage("securitylevel")
            {
                Title = _ => "kleenestar.core:object.clone.step.securitylevel.title",
                Subtitle = _ => "kleenestar.core:object.clone.step.securitylevel.subtitle"
            };

            if (notice is not null)
            {
                security.Add(notice);
            }

            if (tiles is not null)
            {
                security.Add(tiles);
            }

            yield return security;
        }
    }
}
