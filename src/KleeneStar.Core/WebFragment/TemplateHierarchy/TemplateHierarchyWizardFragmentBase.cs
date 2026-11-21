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
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

using ClassEntity = KleeneStar.Model.Entities.Class;
using TemplateEntity = KleeneStar.Model.Entities.Template;
using TemplateHierarchyEntity = KleeneStar.Model.Entities.TemplateHierarchy;
using WorkspaceEntity = KleeneStar.Model.Entities.Workspace;

namespace KleeneStar.Core.WebFragment.TemplateHierarchy
{
    /// <summary>
    /// The wizard a template hierarchy is created and edited with, laid out like the template
    /// wizard.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    ///   <item><c>hierarchy</c>: name (unique within the workspace), category, description and
    ///   state.</item>
    ///   <item><c>root</c>: the root template, a tile selection named
    ///   <see cref="global::KleeneStar.Core.WWW.Api._1_.TemplateHierarchies.Index.RootField"/>
    ///   offering every active template of the workspace once per class it serves
    ///   (<see cref="TemplateChoice"/>), so one click chooses the template and the class of the
    ///   root object.</item>
    ///   <item><c>nodes</c>: the tree of nodes (<see cref="TemplateHierarchyInput"/>).</item>
    /// </list>
    /// The steps are built per request, because the tiles and the edited record are.
    /// </remarks>
    public abstract class TemplateHierarchyWizardFragmentBase : FragmentControlDataWizard
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        protected TemplateHierarchyWizardFragmentBase(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.TemplateHierarchies.Index>();
        }

        /// <summary>
        /// Returns the edited hierarchy, or null while one is created.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The hierarchy.</returns>
        protected abstract TemplateHierarchyEntity ResolveHierarchy(IRenderControlContext renderContext);

        /// <summary>
        /// Returns the workspace the hierarchy belongs to.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="hierarchy">The edited hierarchy, or null.</param>
        /// <returns>The workspace, or null.</returns>
        protected abstract WorkspaceEntity ResolveWorkspace(IRenderControlContext renderContext, TemplateHierarchyEntity hierarchy);

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

            var hierarchy = ResolveHierarchy(renderContext);
            var workspace = ResolveWorkspace(renderContext, hierarchy);

            if (workspace is null)
            {
                return null;
            }

            return Render(renderContext, visualTree, BuildPages(renderContext, workspace, hierarchy));
        }

        /// <summary>
        /// Builds the steps of the wizard.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="workspace">The workspace of the hierarchy.</param>
        /// <param name="hierarchy">The edited hierarchy, or null.</param>
        /// <returns>The steps.</returns>
        private static IEnumerable<IControlDataWizardPage> BuildPages(IRenderControlContext renderContext, WorkspaceEntity workspace, TemplateHierarchyEntity hierarchy)
        {
            var general = new ControlDataWizardPage("hierarchy")
            {
                Title = _ => "kleenestar.core:template.hierarchy.wizard.step.hierarchy.title",
                Subtitle = _ => "kleenestar.core:template.hierarchy.wizard.step.hierarchy.subtitle",
                SummarySource = _ => nameof(TemplateHierarchyEntity.Name)
            };
            general.Add(CreateName(), CreateCategory(), CreateDescription(), CreateState());

            yield return general;

            var root = new ControlDataWizardPage("root")
            {
                Title = _ => "kleenestar.core:template.hierarchy.wizard.step.root.title",
                Subtitle = _ => "kleenestar.core:template.hierarchy.wizard.step.root.subtitle",
                SummarySource = _ => global::KleeneStar.Core.WWW.Api._1_.TemplateHierarchies.Index.RootField
            };
            root.Add(CreateRootSelection(renderContext, workspace, hierarchy));

            yield return root;

            // no summary source: the nodes travel as JSON, which the step indicator would print
            var nodes = new ControlDataWizardPage("nodes")
            {
                Title = _ => "kleenestar.core:template.hierarchy.wizard.step.nodes.title",
                Subtitle = _ => "kleenestar.core:template.hierarchy.wizard.step.nodes.subtitle"
            };
            nodes.Add(new TemplateHierarchyInput()
            {
                Name = _ => TemplateHierarchyNodeList.Field,
                Label = _ => "kleenestar.core:template.hierarchy.nodes.label",
                Help = _ => "kleenestar.core:template.hierarchy.nodes.help",
                Hierarchy = _ => hierarchy,
                Workspace = _ => workspace
            });

            yield return nodes;
        }

        /// <summary>
        /// Creates the tile selection of the root template: every active template of the workspace
        /// the caller administers, once per class it serves, plus the root of the edited hierarchy.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="workspace">The workspace.</param>
        /// <param name="hierarchy">The edited hierarchy, or null.</param>
        /// <returns>The tile selection.</returns>
        private static ControlFormItemInputTile CreateRootSelection(IRenderControlContext renderContext, WorkspaceEntity workspace, TemplateHierarchyEntity hierarchy)
        {
            var templates = CoreHub.TemplateManager
                .GetTemplates(new Query<TemplateEntity>().Where(x => x.Class.WorkspaceId == workspace.Id))
                .Where(x => x.Id == hierarchy?.RootTemplateId
                    || (x.State == TemplateState.Active && ContentAuthorization.MayAdminister(x.ClassId, renderContext.Request)))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var classIds = CoreHub.TemplateManager.GetClassIds(templates);
            var classes = CoreHub.ClassManager
                .GetClasses(new Query<ClassEntity>().Where(x => x.WorkspaceId == workspace.Id))
                .ToDictionary(x => x.Id);
            var current = hierarchy is null
                ? null
                : TemplateChoice.Value(hierarchy.RootTemplateId, hierarchy.RootClassId ?? hierarchy.RootTemplate?.ClassId ?? Guid.Empty, hierarchy.RootTemplate?.ClassId ?? Guid.Empty);

            var selection = new PresetInputTile(current)
            {
                Name = _ => global::KleeneStar.Core.WWW.Api._1_.TemplateHierarchies.Index.RootField,
                Help = _ => "kleenestar.core:template.hierarchy.root.help",
                Searchable = _ => true,
                SearchPlaceholder = _ => "kleenestar.core:template.hierarchy.root.search",
                EmptyText = _ => "kleenestar.core:template.hierarchy.root.empty",
                Columns = _ => 2,
                Required = _ => true
            };

            foreach (var template in templates)
            {
                foreach (var classId in classIds[template.Id])
                {
                    if (!classes.TryGetValue(classId, out var @class))
                    {
                        continue;
                    }

                    var card = new ControlTileCard(TemplateChoice.Value(template.Id, classId, template.ClassId))
                    {
                        Header = _ => template.Name,
                        Icon = _ => template.Icon ?? @class.Icon,
                        Badge = _ => @class.Name,
                        BadgeColor = _ => new PropertyColorTile(CoreHub.AccentColor(@class.Id))
                    };

                    if (!ProseText.IsEmpty(template.Description))
                    {
                        card.Add(new ControlText { Text = _ => ProseText.ToPlainText(template.Description) });
                    }

                    var categories = TemplateCategories.Parse(template.Category).ToList();

                    if (categories.Count > 0)
                    {
                        card.AddFooter(new ControlText { Text = _ => string.Join(" · ", categories) });
                    }

                    selection.Add(card);
                }
            }

            return selection;
        }

        /// <summary>
        /// Creates the name input.
        /// </summary>
        /// <returns>The input.</returns>
        private static ControlFormItemInputText CreateName()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(TemplateHierarchyEntity.Name),
                Label = _ => "kleenestar.core:template.hierarchy.name.label",
                Placeholder = _ => "kleenestar.core:template.hierarchy.name.placeholder",
                Help = _ => "kleenestar.core:template.hierarchy.name.help",
                MaxLength = _ => 64,
                Required = _ => true
            };
        }

        /// <summary>
        /// Creates the category input: a tag input, a hierarchy may stand in several categories.
        /// </summary>
        /// <returns>The input.</returns>
        private static ControlFormItemInputTag CreateCategory()
        {
            return new ControlFormItemInputTag()
            {
                Name = _ => nameof(TemplateHierarchyEntity.Category),
                Label = _ => "kleenestar.core:template.category.label",
                Placeholder = _ => "kleenestar.core:template.category.placeholder",
                Help = _ => "kleenestar.core:template.hierarchy.category.help"
            };
        }

        /// <summary>
        /// Creates the description input.
        /// </summary>
        /// <returns>The input.</returns>
        private static ControlFormItemInputText CreateDescription()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(TemplateHierarchyEntity.Description),
                Label = _ => "kleenestar.core:template.hierarchy.description.label",
                Placeholder = _ => "kleenestar.core:template.hierarchy.description.placeholder",
                Help = _ => "kleenestar.core:template.hierarchy.description.help",
                Format = _ => TypeEditTextFormat.Wysiwyg,
                LinkLibraryUri = CoreHub.EditorLinkLibrary,
                Required = _ => false
            };
        }

        /// <summary>
        /// Creates the state input, which decides whether the hierarchy is offered.
        /// </summary>
        /// <returns>The input.</returns>
        private static ControlDataFormItemInputSelection CreateState()
        {
            return new StateInputSelection()
            {
                Name = _ => nameof(TemplateHierarchyEntity.State),
                DefaultValue = _ => global::KleeneStar.Model.Entities.TemplateStateExtensions.Id(global::KleeneStar.Model.Entities.TemplateState.Active).ToString(),
                Label = _ => "kleenestar.core:template.state.label",
                Placeholder = _ => "kleenestar.core:template.state.placeholder",
                Help = _ => "kleenestar.core:template.hierarchy.state.help",
                ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Templates.State>()?.ToString())
            };
        }
    }
}
