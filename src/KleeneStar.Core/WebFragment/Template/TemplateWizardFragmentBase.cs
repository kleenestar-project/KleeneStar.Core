using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

using ClassEntity = KleeneStar.Model.Entities.Class;
using FieldEntity = KleeneStar.Model.Entities.Field;
using TemplateEntity = KleeneStar.Model.Entities.Template;
using WorkspaceEntity = KleeneStar.Model.Entities.Workspace;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// The wizard a template is created and edited with, laid out like the object creation
    /// wizard.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    ///   <item><c>classes</c>: the classes of the workspace the caller administers, as tiles with
    ///   several selectable (<see cref="TemplatePresetForm.ClassesField"/>). The first selected
    ///   class owns the template; on an edit the owning class stays first while it is selected.</item>
    ///   <item><c>template</c>: name, category, description and state. The description is the
    ///   description the objects start with.</item>
    ///   <item><c>presets</c>: the values the objects start with, rendered by
    ///   <c>/api/1/templates/form?part=presets</c> from the chosen classes
    ///   (<see cref="TemplatePresetForm"/>).</item>
    ///   <item><c>securitylevel</c>: the security level per served class, rendered by
    ///   <c>/api/1/templates/form?part=securitylevel</c> (<see cref="TemplateSecurityLevels"/>);
    ///   skipped where no class has a level the caller may assign.</item>
    /// </list>
    /// The steps are built per request, because the class tiles and the edited record are.
    /// </remarks>
    public abstract class TemplateWizardFragmentBase : FragmentControlDataWizard
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected TemplateWizardFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Templates.Index>();
        }

        /// <summary>
        /// Returns the edited template, or null while one is created.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The template.</returns>
        protected abstract TemplateEntity ResolveTemplate(IRenderControlContext renderContext);

        /// <summary>
        /// Returns the workspace whose classes the template may serve.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="template">The edited template, or null.</param>
        /// <returns>The workspace, or null.</returns>
        protected abstract WorkspaceEntity ResolveWorkspace(IRenderControlContext renderContext, TemplateEntity template);

        /// <summary>
        /// Renders the wizard with the steps built for the request.
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

            var template = ResolveTemplate(renderContext);
            var workspace = ResolveWorkspace(renderContext, template);

            if (workspace is null)
            {
                return null;
            }

            return Render(renderContext, visualTree, BuildPages(renderContext, workspace, template));
        }

        /// <summary>
        /// Builds the steps of the wizard.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="workspace">The workspace of the template.</param>
        /// <param name="template">The edited template, or null.</param>
        /// <returns>The steps.</returns>
        private IEnumerable<IControlDataWizardPage> BuildPages(IRenderControlContext renderContext, WorkspaceEntity workspace, TemplateEntity template)
        {
            // no summary source: the wizard reads a multiple tile selection back as its raw ids,
            // so the step keeps showing its subtitle
            var classes = new ControlDataWizardPage("classes")
            {
                Title = _ => "kleenestar.core:template.wizard.step.classes.title",
                Subtitle = _ => "kleenestar.core:template.wizard.step.classes.subtitle"
            };
            classes.Add(CreateClassSelection(renderContext, workspace, template));

            yield return classes;

            var general = new ControlDataWizardPage("template")
            {
                Title = _ => "kleenestar.core:template.wizard.step.template.title",
                Subtitle = _ => "kleenestar.core:template.wizard.step.template.subtitle",
                SummarySource = _ => nameof(TemplateEntity.Name)
            };
            general.Add(CreateName(template), CreateCategory(), CreateDescription(), CreateState());

            yield return general;

            yield return new ControlDataWizardPage("presets")
            {
                Title = _ => "kleenestar.core:template.wizard.step.presets.title",
                Subtitle = _ => "kleenestar.core:template.wizard.step.presets.subtitle",
                Uri = _ =>
                {
                    var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates.Form>()?
                        .Add(new UriQuery("part", global::KleeneStar.Core.WWW.Api._1_.Templates.Form.PartPresets));

                    return template is null ? uri : uri?.Add(new UriQuery("id", template.Id.ToString()));
                }
            };

            yield return new ControlDataWizardPage("securitylevel")
            {
                Title = _ => "kleenestar.core:template.wizard.step.securitylevel.title",
                Subtitle = _ => "kleenestar.core:template.wizard.step.securitylevel.subtitle",
                Uri = _ =>
                {
                    var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates.Form>()?
                        .Add(new UriQuery("part", global::KleeneStar.Core.WWW.Api._1_.Templates.Form.PartSecurityLevel));

                    return template is null ? uri : uri?.Add(new UriQuery("id", template.Id.ToString()));
                }
            };
        }

        /// <summary>
        /// Creates the tile selection of the classes: the active concrete classes of the
        /// workspace the caller administers, plus the classes the edited template already serves.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="workspace">The workspace of the template.</param>
        /// <param name="template">The edited template, or null.</param>
        /// <returns>The tile selection.</returns>
        private static ControlFormItemInputTile CreateClassSelection(IRenderControlContext renderContext, WorkspaceEntity workspace, TemplateEntity template)
        {
            var current = template is null ? [] : CoreHub.TemplateManager.GetClassIds(template.Id);
            var classes = CoreHub.ClassManager
                .GetClasses(new Query<ClassEntity>().Where(x => x.WorkspaceId == workspace.Id))
                .Where(x => current.Contains(x.Id)
                    || (!x.IsAbstract && x.State == ClassState.Active && ContentAuthorization.MayAdminister(x.Id, renderContext.Request)))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var known = classes.Select(x => x.Id).ToHashSet();
            var fields = CoreHub.FieldManager
                .GetFields(new Query<FieldEntity>())
                .Where(x => known.Contains(x.ClassId) && TemplatePresetForm.Offers(x))
                .GroupBy(x => x.ClassId)
                .ToDictionary(g => g.Key, g => g.Count());
            var templates = CoreHub.TemplateManager
                .GetClassIds(CoreHub.TemplateManager.GetTemplates(new Query<TemplateEntity>().Where(x => x.Class.WorkspaceId == workspace.Id)))
                .SelectMany(x => x.Value)
                .GroupBy(x => x)
                .ToDictionary(g => g.Key, g => g.Count());

            var selection = new PresetInputTile(TemplatePresetForm.FormatClassIds(current))
            {
                Name = _ => TemplatePresetForm.ClassesField,
                Help = _ => "kleenestar.core:template.classes.help",
                MultiSelect = _ => true,
                Searchable = _ => true,
                SearchPlaceholder = _ => "kleenestar.core:template.classes.search",
                EmptyText = _ => "kleenestar.core:template.classes.empty",
                Columns = _ => 2,
                Required = _ => true
            };

            foreach (var @class in classes)
            {
                var kind = CoreHub.ObjectKindManager.GetKind(@class.Kind)?.Label;
                var fieldCount = fields.TryGetValue(@class.Id, out var f) ? f : 0;
                var templateCount = templates.TryGetValue(@class.Id, out var t) ? t : 0;

                var card = new ControlTileCard(@class.Id.ToString())
                {
                    Header = _ => @class.Name,
                    Icon = _ => @class.Icon,
                    Badge = _ => kind,
                    BadgeColor = _ => new PropertyColorTile(CoreHub.AccentColor(@class.Id)),
                    Chip = _ => template?.ClassId == @class.Id ? "kleenestar.core:template.classes.owner" : null
                };

                if (!ProseText.IsEmpty(@class.Description))
                {
                    card.Add(new ControlText { Text = _ => ProseText.ToPlainText(@class.Description) });
                }

                card.AddFooter(new ControlText
                {
                    Text = _ => Count(renderContext, "kleenestar.core:template.classes.fields", fieldCount)
                });
                card.AddFooter(new ControlText
                {
                    Text = _ => Count(renderContext, "kleenestar.core:object.add.class.templates", templateCount)
                });

                selection.Add(card);
            }

            return selection;
        }

        /// <summary>
        /// Creates the name input, checked for uniqueness against every other template.
        /// </summary>
        /// <param name="template">The edited template, or null.</param>
        /// <returns>The input.</returns>
        private static ControlDataFormItemInputUnique CreateName(TemplateEntity template)
        {
            return new ControlDataFormItemInputUnique()
            {
                Name = _ => nameof(TemplateEntity.Name),
                Label = _ => "kleenestar.core:template.name.label",
                Placeholder = _ => "kleenestar.core:template.name.placeholder",
                Help = _ => "kleenestar.core:template.name.help",
                Required = _ => true,
                ServiceFactory = _ =>
                {
                    var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates.UniqueName>();

                    // the edited template does not compete with its own name
                    uri = template is null ? uri : uri?.Add(new UriQuery("exclude", template.Id.ToString()));

                    return DataServiceDescriptor.QueryData(uri?.ToString());
                }
            };
        }

        /// <summary>
        /// Creates the category input: a tag input, a template may stand in several categories
        /// (<see cref="TemplateCategories"/>).
        /// </summary>
        /// <returns>The input.</returns>
        private static ControlFormItemInputTag CreateCategory()
        {
            return new ControlFormItemInputTag()
            {
                Name = _ => nameof(TemplateEntity.Category),
                Label = _ => "kleenestar.core:template.category.label",
                Placeholder = _ => "kleenestar.core:template.category.placeholder",
                Help = _ => "kleenestar.core:template.category.help"
            };
        }

        /// <summary>
        /// Creates the description input, which the objects of the template start with.
        /// </summary>
        /// <returns>The input.</returns>
        private static ControlFormItemInputText CreateDescription()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(TemplateEntity.Description),
                Label = _ => "kleenestar.core:template.description.label",
                Placeholder = _ => "kleenestar.core:template.description.placeholder",
                Help = _ => "kleenestar.core:template.description.help",
                Format = _ => TypeEditTextFormat.Wysiwyg,
                LinkLibraryUri = CoreHub.EditorLinkLibrary,
                Required = _ => false
            };
        }

        /// <summary>
        /// Creates the state input, which decides whether the template is offered.
        /// </summary>
        /// <returns>The input.</returns>
        private static ControlDataFormItemInputSelection CreateState()
        {
            return new StateInputSelection()
            {
                Name = _ => nameof(TemplateEntity.State),
                DefaultValue = _ => global::KleeneStar.Model.Entities.TemplateStateExtensions.Id(global::KleeneStar.Model.Entities.TemplateState.Active).ToString(),
                Label = _ => "kleenestar.core:template.state.label",
                Placeholder = _ => "kleenestar.core:template.state.placeholder",
                Help = _ => "kleenestar.core:template.state.help",
                ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates.State>()?.ToString())
            };
        }

        /// <summary>
        /// Translates a message about a number of things, picking the singular or the plural
        /// wording of the key.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="key">The base key; ".one" or ".other" is appended to it.</param>
        /// <param name="count">The number.</param>
        /// <returns>The translated message.</returns>
        private static string Count(IRenderControlContext renderContext, string key, int count)
        {
            return I18N.Translate(renderContext, $"{key}.{(count == 1 ? "one" : "other")}", count);
        }
    }
}
