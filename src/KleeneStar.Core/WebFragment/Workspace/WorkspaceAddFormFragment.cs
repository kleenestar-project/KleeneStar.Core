using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebWorkspaceTemplate;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The wizard that creates a workspace: first what kind of workspace it is to be, then what
    /// it is called.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two steps are in that order because the first one is the decision and the second is
    /// paperwork. A workspace is not a name and a key - it is a set of classes, and choosing them
    /// one at a time afterwards is the afternoon the templates exist to save. Asking for the
    /// shape first also lets the chosen template propose the key, which is the one field nobody
    /// has an opinion about until they have had to invent one.
    /// </para>
    /// <para>
    /// The templates come from <see cref="WebManager.IWorkspaceTemplateManager"/>, which reads
    /// them out of the installed plugins. The step therefore has nothing of its own to offer when
    /// no plugin ships any - which is why it always carries the empty-workspace card as well:
    /// a workspace set up by hand has to stay one click away, and on a bare installation it is
    /// the only way through.
    /// </para>
    /// <para>
    /// This mirrors the object wizard (<see cref="Object.ObjectAddFormFragment"/>), which asks
    /// for the workspace, the class and the template before it asks for values, for the same
    /// reason.
    /// </para>
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Workspaces.Add>]
    [Cache]
    public sealed class WorkspaceAddFormFragment : FragmentControlDataWizard
    {
        private const string StepTemplate = "step-template";
        private const string StepValues = "step-values";

        /// <summary>
        /// The value the "empty workspace" card carries. It is not a template key, so the create
        /// endpoint reads it as "no template was chosen" and creates no classes.
        /// </summary>
        internal const string NoTemplate = "none";

        /// <summary>
        /// The name of the field the chosen template travels to the create endpoint in.
        /// </summary>
        internal const string TemplateField = "TemplateKey";

        /// <summary>
        /// Gets the tile control for choosing the shape of the workspace.
        /// </summary>
        public ControlFormItemInputTile TemplateSelection { get; } = new()
        {
            Name = _ => TemplateField,
            Help = _ => "kleenestar.core:workspace.add.template.help",
            Searchable = _ => true,
            SearchPlaceholder = _ => "kleenestar.core:workspace.add.template.search",
            EmptyText = _ => "kleenestar.core:workspace.add.template.empty",
            Columns = _ => 2,
            Required = _ => true
        };

        /// <summary>
        /// Gets the input text control for specifying the key of the workspace.
        /// </summary>
        public ControlDataFormItemInputUnique Key { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Key),
            Label = _ => "kleenestar.core:workspace.key.label",
            Placeholder = _ => "kleenestar.core:workspace.key.placeholder",
            Help = _ => "kleenestar.core:workspace.key.help",
            Required = _ => true,
            MaxLength = _ => 10,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueKey>().ToString())
        };

        /// <summary>
        /// Gets the input text control for specifying the name of the workspace.
        /// </summary>
        public ControlDataFormItemInputUnique WorkspaceName { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Name),
            Label = _ => "kleenestar.core:workspace.name.label",
            Placeholder = _ => "kleenestar.core:workspace.name.placeholder",
            Help = _ => "kleenestar.core:workspace.name.help",
            Required = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.UniqueName>().ToString())
        };

        /// <summary>
        /// Gets the input tag definition for the category field.
        /// </summary>
        public ControlFormItemInputTag Category { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Categories),
            Label = _ => "kleenestar.core:workspace.category.label",
            Placeholder = _ => "kleenestar.core:workspace.category.placeholder",
            Help = _ => "kleenestar.core:workspace.category.help"
        };

        /// <summary>
        /// Gets the input text control for specifying the description of the workspace.
        /// </summary>
        public ControlFormItemInputText Description { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.Description),
            Label = _ => "kleenestar.core:workspace.description.label",
            Placeholder = _ => "kleenestar.core:workspace.description.placeholder",
            Format = _ => TypeEditTextFormat.Wysiwyg,
            LinkLibraryUri = CoreHub.EditorLinkLibrary,
            Required = _ => false
        };

        /// <summary>
        /// Gets the input selection control for the inherited workspace.
        /// </summary>
        public ControlDataFormItemInputSelection InheritedSelection { get; } = new()
        {
            Name = _ => "InheritedId",
            Label = _ => "kleenestar.core:workspace.inherited.label",
            Placeholder = _ => "kleenestar.core:workspace.inherited.placeholder",
            Help = _ => "kleenestar.core:workspace.inherited.help",
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Inherited>().ToString())
        };

        /// <summary>
        /// Gets the input selection control for the access modifier.
        /// </summary>
        public ControlDataFormItemInputSelection AccessModifierSelection { get; } = new()
        {
            Name = _ => "AccessModifier",
            Label = _ => "kleenestar.core:workspace.accessmodifier.label",
            Placeholder = _ => "kleenestar.core:workspace.accessmodifier.placeholder",
            Help = _ => "kleenestar.core:workspace.accessmodifier.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.AccessModifier>().ToString())
        };

        /// <summary>
        /// Gets the checkbox control for the sealed flag.
        /// </summary>
        public ControlFormItemInputCheck WorkspaceSealed { get; } = new()
        {
            Name = _ => "Sealed",
            Label = _ => "kleenestar.core:workspace.sealed.label",
            Help = _ => "kleenestar.core:workspace.sealed.help",
            Layout = _ => TypeLayoutCheck.Switch
        };

        /// <summary>
        /// Gets the tenant management input.
        /// </summary>
        public ControlFormItemInputTag Tenant { get; } = new()
        {
            Name = _ => "Tenant",
            Label = _ => "kleenestar.core:workspace.tenant.label",
            Placeholder = _ => "kleenestar.core:workspace.tenant.placeholder",
            Help = _ => "kleenestar.core:workspace.tenant.help"
        };

        /// <summary>
        /// Gets the input selection control for the state.
        /// </summary>
        public ControlDataFormItemInputSelection WorkspaceState { get; } = new()
        {
            Name = _ => nameof(Model.Entities.Workspace.State),
            Label = _ => "kleenestar.core:workspace.state.label",
            Placeholder = _ => "kleenestar.core:workspace.state.placeholder",
            Help = _ => "kleenestar.core:workspace.state.help",
            StickySelection = _ => true,
            ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Workspaces.State>().ToString())
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var step1 = new ControlDataWizardPage(StepTemplate)
            {
                Title = _ => "kleenestar.core:workspace.add.step.template.title",
                Subtitle = _ => "kleenestar.core:workspace.add.step.template.subtitle",
                SummarySource = _ => TemplateField
            };
            step1.Add(TemplateSelection);

            var step2 = new ControlDataWizardPage(StepValues)
            {
                Title = _ => "kleenestar.core:workspace.add.step.values.title",
                Subtitle = _ => "kleenestar.core:workspace.add.step.values.subtitle"
            };
            step2.Add(Key);
            step2.Add(WorkspaceName);
            step2.Add(Category);
            step2.Add(Description);
            step2.Add(InheritedSelection);
            step2.Add(AccessModifierSelection);
            step2.Add(WorkspaceSealed);
            step2.Add(Tenant);
            step2.Add(WorkspaceState);

            Add(step1, step2);

            Mode = _ => TypeRestFormMode.Add;
            FinishLabel = _ => "kleenestar.core:workspace.add.submit.label";
            FinishIcon = _ => new IconPlus();

            // the wizard shapes its own load and submit requests and picks the method per
            // request, so it takes the endpoint and nothing else - a pinned method would leave
            // the last step submitting a read
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Workspaces.Index>();
        }

        /// <summary>
        /// Renders the wizard, rebuilding the template cards from what the installed plugins
        /// currently offer.
        /// </summary>
        /// <remarks>
        /// The cards are built per request rather than once in the constructor, because a plugin
        /// may be installed or removed while the host runs and the catalogue has to follow it.
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            PopulateTemplateSelection(renderContext);

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Rebuilds the template cards.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        private void PopulateTemplateSelection(IRenderControlContext renderContext)
        {
            foreach (var existing in TemplateSelection.Items.ToList())
            {
                TemplateSelection.Remove(existing);
            }

            foreach (var context in CoreHub.WorkspaceTemplateManager.WorkspaceTemplates)
            {
                TemplateSelection.Add(BuildCard(renderContext, context.Template));
            }

            TemplateSelection.Add(BuildEmptyCard());
        }

        /// <summary>
        /// Builds the card of one template.
        /// </summary>
        /// <remarks>
        /// The card states the classes the template creates rather than only naming the template,
        /// because that is what is actually being chosen; and it projects the suggested key into
        /// the key field of the next step, which is the one field nobody has an opinion about
        /// until they have had to invent one.
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="template">The template the card stands for.</param>
        /// <returns>The card.</returns>
        private static IControlTileCard BuildCard(IRenderControlContext renderContext, IWorkspaceTemplate template)
        {
            var classes = (template.Classes ?? []).ToList();
            var names = string.Join(", ", classes.Select(x => x.Name));

            var card = new ControlTileCard(template.Key)
            {
                Header = _ => template.Name,
                Icon = _ => template.Icon,
                Badge = _ => template.SuggestedKey,
                BadgeColor = _ => new PropertyColorTile(TypeColorTile.Primary),
                Bindings = _ => new Dictionary<string, string>
                {
                    [nameof(Model.Entities.Workspace.Key)] = template.SuggestedKey
                }
            };

            card.Add(new ControlText { Text = _ => template.Description });

            if (classes.Count > 0)
            {
                card.AddFooter(new ControlText
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:workspace.add.template.classes", classes.Count),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                    Format = _ => TypeFormatText.Small
                });

                card.AddFooter(new ControlText
                {
                    Text = _ => names,
                    TextColor = _ => new PropertyColorText(TypeColorText.Muted),
                    Format = _ => TypeFormatText.Small
                });
            }

            return card;
        }

        /// <summary>
        /// Builds the card that stands for creating a workspace without a template.
        /// </summary>
        /// <returns>The card.</returns>
        private static IControlTileCard BuildEmptyCard()
        {
            var card = new ControlTileCard(NoTemplate)
            {
                Header = _ => "kleenestar.core:workspace.add.template.none.label",
                Icon = _ => new IconFile(),

                // it is offered whatever the search says: a workspace set up by hand must stay
                // reachable, and typing a word no card matches must not leave the step with no
                // way on
                AlwaysVisible = _ => true
            };

            card.Add(new ControlText { Text = _ => "kleenestar.core:workspace.add.template.none.description" });

            return card;
        }
    }
}
