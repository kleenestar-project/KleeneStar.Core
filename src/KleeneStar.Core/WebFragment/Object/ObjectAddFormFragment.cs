using KleeneStar.Core.WebControl;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

// the fragment lives in KleeneStar.Core.WebFragment.Object, next to sibling namespaces
// named after the same concepts as the entities (Class, Field, Template, ...), so the
// entity types are aliased rather than referenced by their bare names
using ClassEntity = KleeneStar.Model.Entities.Class;
using FieldEntity = KleeneStar.Model.Entities.Field;
using IdentityEntity = KleeneStar.Model.Entities.Identity;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using PriorityEntity = KleeneStar.Model.Entities.Priority;
using TemplateEntity = KleeneStar.Model.Entities.Template;
using WorkspaceEntity = KleeneStar.Model.Entities.Workspace;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Represents a multi-step wizard fragment for creating a new object.
    /// </summary>
    /// <remarks>
    /// The wizard guides the user through three steps:
    /// <list type="number">
    ///   <item>Workspace — the workspaces as cards, grouped by the category they belong to and
    ///   searchable by name, key and description.</item>
    ///   <item>Template — the templates of every class as cards, each labelled with the class it
    ///   creates and the effort it implies (field count, priority, SLA). The cards carry the
    ///   workspace of their class, so the list narrows to the workspace chosen in step 1.</item>
    ///   <item>Values — the properties every object carries. The class and template chosen in
    ///   step 2 are projected into hidden inputs by the card itself, and the remaining fields of
    ///   the template are named in a note rather than asked for here: the wizard opens an object,
    ///   the object's own form completes it.</item>
    /// </list>
    /// </remarks>
    [Title("kleenestar.core:object.add.title")]
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Objects.Add>]
    [Cache]
    public sealed class ObjectAddFormFragment : FragmentControlDataWizard
    {
        private const string StepWorkspace = "step-workspace";
        private const string StepClass = "step-class";
        private const string StepTemplate = "step-template";
        private const string StepValues = "step-values";

        /// <summary>
        /// The value the "no template" card of the template step carries. It is not a
        /// template id, so the create endpoint reads it as "no template was chosen" and
        /// applies neither presets nor child templates.
        /// </summary>
        private const string NoTemplate = "none";

        /// <summary>
        /// The query parameter the create button names the open document with, so a document
        /// created from it lands below it in the page tree.
        /// </summary>
        public const string ParentParameter = "parent";

        /// <summary>
        /// The payload field that carries the key of that document to the create endpoint.
        /// </summary>
        /// <remarks>
        /// A key rather than <c>ParentId</c>: the wizard fills its fields from the data it loads,
        /// and a field named after a property of the object would be emptied by the
        /// <c>parentId: null</c> of a new one.
        /// </remarks>
        public const string ParentKeyField = "ParentKey";

        /// <summary>
        /// Gets the hidden input carrying the document the wizard was opened from - only a
        /// document the caller may read; the endpoint decides whether the new object goes
        /// below it.
        /// </summary>
        public PresetHiddenInput ParentKey { get; } = new()
        {
            Name = _ => ParentKeyField,
            Value = ctx =>
            {
                var key = ctx?.Request?.GetParameter(ParentParameter)?.Value;
                var parent = string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);

                return string.Equals(parent?.Kind, Model.Entities.ObjectKind.Document, StringComparison.OrdinalIgnoreCase)
                    ? parent.Key
                    : null;
            }
        };

        /// <summary>
        /// Gets the tile control for selecting the workspace the object is created in.
        /// </summary>
        public ControlFormItemInputTile WorkspaceSelection { get; } = new()
        {
            Name = _ => nameof(ObjectEntity.WorkspaceId),
            Help = _ => "kleenestar.core:object.add.workspace.help",
            Searchable = _ => true,
            SearchPlaceholder = _ => "kleenestar.core:object.add.workspace.search",
            EmptyText = _ => "kleenestar.core:object.add.workspace.empty",
            Columns = _ => 2,
            Required = _ => true
        };

        /// <summary>
        /// Gets the tile control for selecting the class the object is an instance of.
        /// The classes are narrowed to the workspace chosen in the first step.
        /// </summary>
        public ControlFormItemInputTile ClassSelection { get; } = new()
        {
            Name = _ => nameof(ObjectEntity.ClassId),
            Help = _ => "kleenestar.core:object.add.class.help",
            Searchable = _ => true,
            SearchPlaceholder = _ => "kleenestar.core:object.add.class.search",
            EmptyText = _ => "kleenestar.core:object.add.class.empty",
            FilterSource = _ => nameof(ObjectEntity.WorkspaceId),
            Columns = _ => 2,
            Required = _ => true
        };

        /// <summary>
        /// Gets the tile control for selecting the template the object is created from,
        /// narrowed to the class chosen in the second step.
        /// </summary>
        /// <remarks>
        /// The step always offers a way on: besides the templates of the class it carries
        /// a "no template" card, which is what a class without templates is left with.
        /// The card also projects the priority the template presets and the note stating
        /// what it will add to the object.
        /// </remarks>
        public ControlFormItemInputTile TemplateSelection { get; } = new()
        {
            Name = _ => "TemplateId",
            Help = _ => "kleenestar.core:object.add.template.help",
            Searchable = _ => true,
            SearchPlaceholder = _ => "kleenestar.core:object.add.template.search",
            FilterSource = _ => nameof(ObjectEntity.ClassId),
            Columns = _ => 2,
            Required = _ => true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var step1 = new ControlDataWizardPage(StepWorkspace)
            {
                Title = _ => "kleenestar.core:object.add.step.workspace.title",
                Subtitle = _ => "kleenestar.core:object.add.step.workspace.subtitle",
                SummarySource = _ => nameof(ObjectEntity.WorkspaceId)
            };
            step1.Add(WorkspaceSelection, ParentKey);

            var step2 = new ControlDataWizardPage(StepClass)
            {
                Title = _ => "kleenestar.core:object.add.step.class.title",
                Subtitle = _ => "kleenestar.core:object.add.step.class.subtitle",
                SummarySource = _ => nameof(ObjectEntity.ClassId)
            };
            step2.Add(ClassSelection);

            var step3 = new ControlDataWizardPage(StepTemplate)
            {
                Title = _ => "kleenestar.core:object.add.step.template.title",
                Subtitle = _ => "kleenestar.core:object.add.step.template.subtitle",
                SummarySource = _ => "TemplateId"
            };
            step3.Add(TemplateSelection);

            // the last step is rendered by the server rather than upfront: it shows the
            // create form of the class, which is only known once the second step has been
            // answered. The endpoint receives the answers collected so far and replies with
            // the form of that class, pre-filled from the presets of the chosen template.
            var step4 = new ControlDataWizardPage(StepValues)
            {
                Title = _ => "kleenestar.core:object.add.step.values.title",
                Subtitle = _ => "kleenestar.core:object.add.step.values.subtitle",
                Uri = _ => CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Form>()
            };

            Add(step1, step2, step3, step4);

            Mode = _ => TypeRestFormMode.Add;
            FinishLabel = _ => "kleenestar.core:object.add.submit.label";
            FinishIcon = _ => new IconPlus();

            // the wizard shapes its own load and submit requests and picks the method per
            // request, so it needs the endpoint and nothing else. Pinning a method here —
            // QueryData pins GET — leaves the final step submitting a read, which is why the
            // wizard used to walk through all four steps and create nothing. The extension
            // also declares the domain, so the object lists refresh once the object exists.
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">
        /// The context in which the control is rendered.
        /// </param>
        /// <param name="visualTree">
        /// The visual tree representing the control's structure.
        /// </param>
        /// <returns>
        /// An HTML node representing the rendered control.
        /// </returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var catalog = Catalog.Load();

            PopulateWorkspaceSelection(renderContext, catalog);
            PopulateClassSelection(renderContext, catalog);
            PopulateTemplateSelection(renderContext, catalog);

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Rebuilds the workspace cards from the workspaces currently registered.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="catalog">The data the wizard is built from.</param>
        private void PopulateWorkspaceSelection(IRenderControlContext renderContext, Catalog catalog)
        {
            foreach (var existing in WorkspaceSelection.Items.ToList())
            {
                WorkspaceSelection.Remove(existing);
            }

            foreach (var workspace in catalog.Workspaces)
            {
                var classes = catalog.Classes.Count(x => x.WorkspaceId == workspace.Id);
                var category = workspace.Categories?.OrderBy(x => x.Name).FirstOrDefault()?.Name;

                var card = new ControlTileCard(workspace.Id.ToString())
                {
                    Header = _ => workspace.Name,
                    Icon = _ => workspace.Icon,
                    Badge = _ => category ?? workspace.Key,
                    BadgeColor = _ => new PropertyColorTile(CoreHub.AccentColor(workspace.Id))
                };

                if (!ProseText.IsEmpty(workspace.Description))
                {
                    card.Add(new ControlText { Text = _ => ProseText.ToPlainText(workspace.Description) });
                }

                card.AddFooter(new ControlText { Text = _ => workspace.Key });
                card.AddFooter(new ControlText
                {
                    Text = _ => Count(renderContext, "kleenestar.core:object.add.workspace.classes", classes)
                });

                WorkspaceSelection.Add(card);
            }
        }

        /// <summary>
        /// Rebuilds the class cards from the concrete classes. Each card carries the workspace
        /// of its class, so the list narrows to the workspace chosen in the first step.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="catalog">The data the wizard is built from.</param>
        private void PopulateClassSelection(IRenderControlContext renderContext, Catalog catalog)
        {
            foreach (var existing in ClassSelection.Items.ToList())
            {
                ClassSelection.Remove(existing);
            }

            foreach (var @class in catalog.Classes)
            {
                var fields = catalog.FieldCount(@class.Id);
                var templates = catalog.Templates.Count(x => x.ClassId == @class.Id);
                var sla = catalog.Sla(renderContext, @class.Id, null);
                var kind = ObjectKindCatalog.GetKind(@class.Kind)?.Label;

                var card = new ControlTileCard(@class.Id.ToString())
                {
                    Header = _ => @class.Name,
                    Icon = _ => @class.Icon,
                    Badge = _ => kind,
                    BadgeColor = _ => new PropertyColorTile(CoreHub.AccentColor(@class.Id)),
                    FilterValue = _ => @class.WorkspaceId.ToString()
                };

                if (!ProseText.IsEmpty(@class.Description))
                {
                    card.Add(new ControlText { Text = _ => ProseText.ToPlainText(@class.Description) });
                }

                card.AddFooter(new ControlText
                {
                    Text = _ => Count(renderContext, "kleenestar.core:object.add.class.fields", fields)
                });
                card.AddFooter(new ControlText
                {
                    Text = _ => Count(renderContext, "kleenestar.core:object.add.class.templates", templates)
                });

                if (!string.IsNullOrWhiteSpace(sla))
                {
                    card.AddFooter(new ControlText { Text = _ => sla });
                }

                ClassSelection.Add(card);
            }
        }

        /// <summary>
        /// Rebuilds the template cards: the active templates of every class, plus the single
        /// "no template" card that carries no class of its own and therefore stays offered
        /// whichever class was chosen — including a class nobody has written a template for.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="catalog">The data the wizard is built from.</param>
        private void PopulateTemplateSelection(IRenderControlContext renderContext, Catalog catalog)
        {
            foreach (var existing in TemplateSelection.Items.ToList())
            {
                TemplateSelection.Remove(existing);
            }

            TemplateSelection.Add(CreateNoTemplateCard(renderContext));

            foreach (var @class in catalog.Classes)
            {
                var templates = catalog.Templates
                    .Where(x => x.ClassId == @class.Id)
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.Name)
                    .ToList();

                // the first template of a class is its default starting point and is marked
                // as such, so the common case stands out among the alternatives
                var recommended = templates.FirstOrDefault();

                foreach (var template in templates)
                {
                    TemplateSelection.Add(CreateTemplateCard(renderContext, catalog, @class, template, template == recommended));
                }
            }
        }

        /// <summary>
        /// Creates the card of a template.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="catalog">The data the wizard is built from.</param>
        /// <param name="class">The class the template instantiates.</param>
        /// <param name="template">The template the card stands for.</param>
        /// <param name="recommended">Whether the template is the default of its class.</param>
        /// <returns>The card.</returns>
        private static ControlTileCard CreateTemplateCard(IRenderControlContext renderContext, Catalog catalog, ClassEntity @class, TemplateEntity template, bool recommended)
        {
            var priority = catalog.PresetPriority(template);
            var sla = catalog.Sla(renderContext, @class.Id, priority);
            var presets = catalog.PresetCount(template);

            // the presets themselves are not projected from here: the last step is rendered
            // by the server, which reads the chosen template from the payload and fills the
            // form with them directly
            var card = new ControlTileCard(template.Id.ToString())
            {
                Header = _ => template.Name,
                Icon = _ => template.Icon ?? @class.Icon,
                Badge = _ => template.Category,
                BadgeColor = _ => new PropertyColorTile(CoreHub.AccentColor(template.Id)),
                Chip = _ => recommended ? "kleenestar.core:object.add.template.recommended" : null,
                FilterValue = _ => @class.Id.ToString()
            };

            if (!ProseText.IsEmpty(template.Description))
            {
                card.Add(new ControlText { Text = _ => ProseText.ToPlainText(template.Description) });
            }

            card.AddFooter(new ControlText
            {
                Text = _ => Count(renderContext, "kleenestar.core:object.add.template.presets", presets)
            });

            if (!string.IsNullOrWhiteSpace(sla))
            {
                card.AddFooter(new ControlText { Text = _ => sla });
            }

            return card;
        }

        /// <summary>
        /// Creates the card that starts the object without a template.
        /// </summary>
        /// <remarks>
        /// The card is marked as always visible, so neither the class filter nor the search
        /// box can take it away — it is the way on for a class nobody has written a template
        /// for. Its value is not a template id, which is how the create endpoint and the
        /// form endpoint both read "no template".
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The card.</returns>
        private static ControlTileCard CreateNoTemplateCard(IRenderControlContext renderContext)
        {
            var card = new ControlTileCard(NoTemplate)
            {
                Header = _ => "kleenestar.core:object.add.template.none",
                AlwaysVisible = _ => true
            };

            card.Add(new ControlText
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:object.add.template.none.description")
            });

            return card;
        }

        /// <summary>
        /// Translates a message that talks about a number of things, picking the singular
        /// or the plural wording of the key from the count it is given.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="key">The base key; ".one" or ".other" is appended to it.</param>
        /// <param name="count">The number the message talks about.</param>
        /// <returns>The translated message.</returns>
        private static string Count(IRenderControlContext renderContext, string key, int count)
        {
            return I18N.Translate(renderContext, $"{key}.{(count == 1 ? "one" : "other")}", count);
        }

        /// <summary>
        /// Shortens a priority name to the token it is commonly addressed by, so a segmented
        /// control stays readable: "P2 - High" becomes "P2", a single-word name stays as it is.
        /// </summary>
        /// <param name="name">The priority name.</param>
        /// <returns>The short label.</returns>
        private static string ShortLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var separator = name.IndexOf(" - ", StringComparison.Ordinal);

            return separator > 0 ? name[..separator] : name;
        }

        /// <summary>
        /// The data the wizard is built from, read once per render.
        /// </summary>
        /// <remarks>
        /// The cards of both steps need the classes, their templates, their fields, their
        /// priorities and their SLA policies at once. Reading each of them per class would run
        /// one query per class and per card; they are therefore loaded in one query each and
        /// indexed in memory.
        /// </remarks>
        private sealed class Catalog
        {
            /// <summary>
            /// Gets the workspaces, ordered by name.
            /// </summary>
            public IReadOnlyList<WorkspaceEntity> Workspaces { get; private init; }

            /// <summary>
            /// Gets the concrete classes, ordered by workspace and name.
            /// </summary>
            public IReadOnlyList<ClassEntity> Classes { get; private init; }

            /// <summary>
            /// Gets the active templates.
            /// </summary>
            public IReadOnlyList<TemplateEntity> Templates { get; private init; }

            private IReadOnlyDictionary<Guid, int> Fields { get; init; }

            private IReadOnlyDictionary<Guid, List<PriorityEntity>> PrioritiesByClass { get; init; }

            private IReadOnlyDictionary<Guid, List<SlaPolicy>> SlasByClass { get; init; }

            /// <summary>
            /// Reads the data the wizard is built from.
            /// </summary>
            /// <returns>The catalog.</returns>
            public static Catalog Load()
            {
                // the wizard offers only what the caller may create: a class whose chain refuses
                // them object writes is left out, and so is a workspace they may not read
                var refused = CoreHub.SessionManager.HasCaller
                    ? CoreHub.PermissionManager.GetRefusedClassIds(
                        CoreHub.SessionManager.GetCurrentIdentityId(null),
                        typeof(global::KleeneStar.Core.WebPermissions.ObjectUpdatePermission))
                    : new HashSet<Guid>();

                var classes = CoreHub.ClassManager
                    .GetClasses(new Query<ClassEntity>())
                    .Where(x => !x.IsAbstract && x.State == ClassState.Active && !refused.Contains(x.Id))
                    .ToList();

                var known = classes.Select(x => x.Id).ToHashSet();

                return new Catalog
                {
                    Workspaces = [.. CoreHub.WorkspaceManager
                        .GetWorkspaces(global::KleeneStar.Core.WebPermission.ContentVisibility.Restrict(new Query<WorkspaceEntity>()))
                        .Where(x => x.State == WorkspaceState.Active)
                        .OrderBy(x => x.Name)],
                    Classes = [.. classes.OrderBy(x => x.WorkspaceId).ThenBy(x => x.Name)],
                    Templates = [.. CoreHub.TemplateManager
                        .GetTemplates(new Query<TemplateEntity>())
                        .Where(x => x.State == TemplateState.Active && known.Contains(x.ClassId))],
                    Fields = CoreHub.FieldManager
                        .GetFields(new Query<FieldEntity>())
                        .Where(x => !x.Deprecated && x.State == FieldState.Active)
                        .GroupBy(x => x.ClassId)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    PrioritiesByClass = CoreHub.PriorityManager
                        .GetPriorities(new Query<PriorityEntity>())
                        .Where(x => x.State == PriorityState.Active)
                        .GroupBy(x => x.ClassId)
                        .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Order).ToList()),
                    SlasByClass = CoreHub.SlaManager
                        .GetSlas(new Query<SlaPolicy>())
                        .Where(x => x.State == SlaPolicyState.Active)
                        .GroupBy(x => x.ClassId)
                        .ToDictionary(g => g.Key, g => g.ToList())
                };
            }

            /// <summary>
            /// Returns the number of fields configured on a class.
            /// </summary>
            /// <param name="classId">The class.</param>
            /// <returns>The number of active fields.</returns>
            public int FieldCount(Guid classId)
            {
                return Fields.TryGetValue(classId, out var count) ? count : 0;
            }

            /// <summary>
            /// Returns the priorities of a class, in display order.
            /// </summary>
            /// <param name="classId">The class.</param>
            /// <returns>The priorities, which may be empty.</returns>
            public IReadOnlyList<PriorityEntity> Priorities(Guid classId)
            {
                return PrioritiesByClass.TryGetValue(classId, out var priorities) ? priorities : [];
            }

            /// <summary>
            /// Returns the number of fields a template presets.
            /// </summary>
            /// <param name="template">The template.</param>
            /// <returns>The number of presets.</returns>
            public int PresetCount(TemplateEntity template)
            {
                return CoreHub.TemplateManager.GetPresets(template.Id).Count;
            }

            /// <summary>
            /// Returns the priority a template presets, or null when it presets none.
            /// </summary>
            /// <param name="template">The template.</param>
            /// <returns>The preset priority.</returns>
            public string PresetPriority(TemplateEntity template)
            {
                return CoreHub.TemplateManager.GetPresets(template.Id)
                    .FirstOrDefault(x => string.Equals(x.Key, "Priority", StringComparison.OrdinalIgnoreCase))
                    .Value;
            }

            /// <summary>
            /// Describes what a class promises for the given priority: the priority itself and
            /// the tightest target the matching SLA policy states.
            /// </summary>
            /// <remarks>
            /// A policy applies to a severity bucket rather than to a named priority, so the
            /// preset is matched against the bucket names and falls back to the only policy of
            /// the class when it names none of them. A class without SLA policies promises
            /// nothing, and the card then shows its field count alone.
            /// </remarks>
            /// <param name="renderContext">The context in which the control is rendered.</param>
            /// <param name="classId">The class.</param>
            /// <param name="priority">The preset priority, or null.</param>
            /// <returns>The line, or null when the class has no policy to report.</returns>
            public string Sla(IRenderControlContext renderContext, Guid classId, string priority)
            {
                if (!SlasByClass.TryGetValue(classId, out var policies) || policies.Count == 0)
                {
                    return null;
                }

                var policy = policies.FirstOrDefault(x => Matches(x.Priority, priority)) ?? policies[0];
                var target = policy.Targets?.OrderBy(x => Rank(x.Kind)).FirstOrDefault();

                if (target is null)
                {
                    return null;
                }

                var unit = Count(renderContext, $"kleenestar.core:object.add.sla.unit.{target.Unit.ToString().ToLowerInvariant()}", target.TargetValue);
                var kind = I18N.Translate(renderContext, $"kleenestar.core:object.add.sla.kind.{target.Kind.ToString().ToLowerInvariant()}");
                var duration = $"{target.TargetValue.ToString(CultureInfo.InvariantCulture)} {unit} {kind}";

                return string.IsNullOrWhiteSpace(priority)
                    ? duration
                    : $"{ShortLabel(priority)} · {duration}";
            }

            /// <summary>
            /// Checks whether a severity bucket corresponds to a preset priority.
            /// </summary>
            /// <param name="bucket">The bucket of the policy.</param>
            /// <param name="priority">The preset priority, or null.</param>
            /// <returns>True when the bucket is the one the priority falls into.</returns>
            private static bool Matches(SlaPriority bucket, string priority)
            {
                return !string.IsNullOrWhiteSpace(priority)
                    && priority.Contains(bucket.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            /// <summary>
            /// Ranks the milestones of an SLA target, so the one a card reports is the earliest
            /// promise the policy makes rather than an arbitrary one.
            /// </summary>
            /// <param name="kind">The milestone.</param>
            /// <returns>The rank, lower being earlier.</returns>
            private static int Rank(SlaTargetKind kind)
            {
                return kind switch
                {
                    SlaTargetKind.Response => 0,
                    SlaTargetKind.Approval => 1,
                    SlaTargetKind.Update => 2,
                    SlaTargetKind.Fulfillment => 3,
                    SlaTargetKind.Implementation => 4,
                    SlaTargetKind.Resolution => 5,
                    _ => 6
                };
            }
        }
    }
}
