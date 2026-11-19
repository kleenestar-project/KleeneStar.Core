using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Transfers
{
    /// <summary>
    /// Renders the steps of the transfer wizard that depend on the chosen target workspace, as
    /// html fragments.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><c>classes</c>: one tile selection per class of the subtree. The first tile keeps the
    ///   class (the value is the source class itself), the others are the active, concrete classes
    ///   of the target workspace. Preselected with the earlier answer for the same workspace, else
    ///   the class of the same name, else the kept class.</item>
    ///   <item><c>review</c>: what the move does - the number of objects or the refusal, the
    ///   security levels without counterpart (the objects lose them), the class mapping, the
    ///   workflow states and the dropped fields.</item>
    /// </list>
    /// The object is read narrowed and needs write access; otherwise <c>404</c>.
    /// </remarks>
    [Title("kleenestar.core:transfer.form.api.title")]
    [Cache]
    public sealed class Form : IRestApi
    {
        /// <summary>
        /// The part rendering the class mapping.
        /// </summary>
        public const string PartClasses = "classes";

        /// <summary>
        /// The part rendering the review.
        /// </summary>
        public const string PartReview = "review";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Form()
        {
        }

        /// <summary>
        /// Handles <c>POST {base}?part=classes|review</c> with the wizard payload as JSON.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The rendered step as <c>text/html</c>, <c>204</c> or <c>404</c>.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Load(IRequest request)
        {
            var answers = ObjectTransferForm.Read(request);
            var @object = ObjectTransferForm.ResolveObject(answers);

            if (@object is null || !ContentAuthorization.MayWrite(@object, request))
            {
                return new ResponseNotFound();
            }

            var target = ObjectTransferForm.ResolveTarget(answers, @object);

            if (target is null)
            {
                return new ResponseNoContent();
            }

            var part = request.GetParameter("part")?.Value?.Trim().ToLowerInvariant();
            var renderContext = new RenderControlFormContext(null, null, request as Request, null);
            var items = part == PartClasses
                ? ClassItems(renderContext, @object, target, answers)
                : ReviewItems(renderContext, @object, target, answers);

            if (items.Count == 0)
            {
                return new ResponseNoContent();
            }

            var group = new ControlFormItemGroupVertical();

            foreach (var item in items)
            {
                group.Items.Add(item);
            }

            return new ResponseOK { Content = group.Render(renderContext, null)?.ToString() ?? string.Empty }
                .AddHeaderContentType("text/html; charset=utf-8");
        }

        /// <summary>
        /// Builds the class step.
        /// </summary>
        private static List<IControlFormItem> ClassItems(IRenderControlFormContext renderContext, ObjectEntity @object, Workspace target, IReadOnlyDictionary<string, string> answers)
        {
            var plan = ObjectTransferForm.Plan(@object, ObjectTransferForm.Options(answers, target), renderContext.Request);
            var items = new List<IControlFormItem>();

            foreach (var source in plan.Transfer?.Classes ?? [])
            {
                var candidates = source.Candidates ?? [];

                // without a class of the same name the objects keep theirs
                var selection = new PresetInputTile((source.TargetId ?? source.SourceId).ToString())
                {
                    Name = _ => ObjectTransferForm.ClassMapPrefix + source.SourceId,
                    Label = _ => I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.class.label", source.SourceName),
                    Help = _ => "kleenestar.core:transfer.dialog.class.help",
                    Searchable = _ => candidates.Count > 6,
                    SearchPlaceholder = _ => "kleenestar.core:transfer.dialog.class.search",
                    EmptyText = _ => "kleenestar.core:transfer.dialog.class.empty",
                    Columns = _ => 2,
                    Required = _ => true
                };

                selection.Add(KeepCard(renderContext, source));

                foreach (var candidate in candidates)
                {
                    selection.Add(ClassCard(renderContext, candidate, string.Equals(candidate.Name, source.SourceName, StringComparison.CurrentCultureIgnoreCase)));
                }

                items.Add(selection);
            }

            items.Add(new PresetHiddenInput
            {
                Name = _ => ObjectTransferForm.ContextField,
                Value = _ => ObjectTransferForm.Context(target)
            });

            return items;
        }

        /// <summary>
        /// Builds the tile that keeps the class of the objects; its value is the source class.
        /// </summary>
        private static ControlTileCard KeepCard(IRenderControlContext renderContext, TransferClass source)
        {
            var @class = CoreHub.ClassManager.GetClass(source.SourceId);

            var card = new ControlTileCard(source.SourceId.ToString())
            {
                Header = _ => I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.keep.label", source.SourceName),
                Icon = _ => (IIcon)@class?.Icon ?? new IconObject(),
                Badge = _ => I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.keep.badge"),
                BadgeColor = _ => new PropertyColorTile(TypeColorTile.Success)
            };

            card.Add(new ControlText
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.keep.description")
            });

            return card;
        }

        /// <summary>
        /// Builds the tile of a class of the target workspace.
        /// </summary>
        private static ControlTileCard ClassCard(IRenderControlContext renderContext, TransferCandidate candidate, bool sameName)
        {
            var @class = CoreHub.ClassManager.GetClass(candidate.Id);
            var kind = CoreHub.ObjectKindManager.GetKind(@class?.Kind)?.Label;

            var card = new ControlTileCard(candidate.Id.ToString())
            {
                Header = _ => candidate.Name,
                Icon = _ => (IIcon)@class?.Icon ?? new IconObject(),
                Badge = _ => kind,
                BadgeColor = _ => new PropertyColorTile(CoreHub.AccentColor(candidate.Id)),
                Chip = _ => sameName ? I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.class.samename") : null
            };

            if (@class is not null && !ProseText.IsEmpty(@class.Description))
            {
                card.Add(new ControlText { Text = _ => ProseText.ToPlainText(@class.Description) });
            }

            return card;
        }

        /// <summary>
        /// Builds the review step.
        /// </summary>
        private static List<IControlFormItem> ReviewItems(IRenderControlFormContext renderContext, ObjectEntity @object, Workspace target, IReadOnlyDictionary<string, string> answers)
        {
            var plan = ObjectTransferForm.Plan(@object, ObjectTransferForm.Options(answers, target), renderContext.Request);
            var transfer = plan.Transfer;
            var items = new List<IControlFormItem>
            {
                plan.Refused
                    ? Alert(Operations._workspacekey_.Index.Explain(plan, renderContext.Request), TypeColorBackgroundAlert.Danger)
                    : Alert(I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.review.total", plan.Items.Count, target.Name), TypeColorBackgroundAlert.Info)
            };

            if (transfer is null)
            {
                return items;
            }

            var declassified = transfer.Levels.Where(x => x.TargetId is null).Select(x => x.Name).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();

            if (declassified.Count > 0)
            {
                items.Add(Alert(I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.review.declassified", string.Join(", ", declassified)), TypeColorBackgroundAlert.Warning));
            }

            if (transfer.Classes.Count > 0)
            {
                items.Add(Listing("classes", "kleenestar.core:transfer.dialog.review.classes", new IconObject(), transfer.Classes
                    .Select(x => x.TargetId == x.SourceId
                        ? I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.review.kept", x.SourceName)
                        : x.SourceName + " → " + (x.TargetName ?? I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.review.unmapped")))));
            }

            // a kept class keeps its states and fields; only a change of class is worth listing
            var changed = transfer.States.Where(x => !string.Equals(x.From, x.To, StringComparison.OrdinalIgnoreCase)).ToList();

            if (changed.Count > 0)
            {
                items.Add(Listing("states", "kleenestar.core:transfer.dialog.review.states", new IconWorkflow(), changed
                    .Select(x => x.ClassName + ": " + x.From + " → " + (x.To ?? I18N.Translate(renderContext, "kleenestar.core:transfer.dialog.review.nostate")))));
            }

            if (transfer.DroppedFields.Count > 0)
            {
                items.Add(Listing("dropped", "kleenestar.core:transfer.dialog.review.dropped", new IconTrash(), transfer.DroppedFields
                    .Select(x => x.ClassName + ": " + x.FieldName)));
            }

            return items;
        }

        /// <summary>
        /// Creates a labelled list of lines.
        /// </summary>
        private static IControlFormItem Listing(string id, string label, IIcon icon, IEnumerable<string> lines)
        {
            var list = new ControlList("transfer-review-" + id + "-list");

            foreach (var (line, index) in lines.Select((x, i) => (x, i)))
            {
                list.Add(new ControlListItem("transfer-review-" + id + "-" + index)
                {
                    Text = _ => line,
                    Icon = _ => icon
                });
            }

            return new ControlFormItemPanel
            (
                "transfer-review-" + id,
                new ControlFormItemLabel("transfer-review-" + id + "-label") { Text = _ => label },
                list
            );
        }

        /// <summary>
        /// Creates an alert as a form item.
        /// </summary>
        private static IControlFormItem Alert(string text, TypeColorBackgroundAlert color)
        {
            return new ControlFormItemPanel
            (
                null,
                new ControlAlert()
                {
                    Text = _ => text,
                    BackgroundColor = _ => new PropertyColorBackgroundAlert(color)
                }
            );
        }
    }
}
