using KleeneStar.Core.WebManager;
using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Audit
{
    /// <summary>
    /// One audit event in full: what happened and to what, the context it happened in, the
    /// attribute-level changes it produced, and the seal that ties it to the rest of the log.
    /// </summary>
    /// <remarks>
    /// The three sections answer three different questions and all three are needed. The header
    /// says what the event was. "State changes" is the delta - what the action did, and the only
    /// part of the log from which a past state can be reconstructed. "Integrity" shows the
    /// hashes, which are what a reader has to be able to see for the claim of tamper-evidence to
    /// mean anything to them rather than being a promise made in a document.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Settings.Audit.Detail>]
    [Cache]
    public sealed class AuditDetailFragment : FragmentControlPanel
    {
        /// <summary>
        /// The class the modal's selector addresses the dialog content by.
        /// </summary>
        public const string ContentClass = "kleenestar-audit-detail";

        private readonly IAuditManager _auditManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="auditManager">The audit manager the event is read from.</param>
        public AuditDetailFragment(IFragmentContext fragmentContext, IAuditManager auditManager)
            : base(fragmentContext)
        {
            _auditManager = auditManager;
        }

        /// <summary>
        /// Renders the event. Returns <c>null</c> when the fragment's render conditions exclude
        /// it.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var panel = new ControlPanel(ContentClass)
            {
                Padding = _ => new PropertySpacingPadding(PropertySpacing.Space.Two, PropertySpacing.Space.Two, PropertySpacing.Space.One, PropertySpacing.Space.Two)
            };

            panel.Classes = [ContentClass];

            var @event = Resolve(renderContext);

            if (@event is null)
            {
                panel.Add(new ControlText()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:audit.detail.missing"),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return panel.Render(renderContext, visualTree);
            }

            panel.Add(BuildHeader(@event, renderContext));
            panel.Add(BuildContext(@event, renderContext));
            panel.Add(BuildDeltas(@event, renderContext));
            panel.Add(BuildIntegrity(@event, renderContext));

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Reads the event the request addresses, by position in the sequence or by id.
        /// </summary>
        /// <remarks>
        /// Both forms are accepted because both are natural references. The position is what a
        /// reader sees in the list and would type; the id is what a link carries.
        /// </remarks>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The event, or <c>null</c>.</returns>
        private AuditEvent Resolve(IRenderControlContext renderContext)
        {
            var raw = renderContext?.Request?
                .GetParameter(global::KleeneStar.Core.WWW.Settings.Audit.Detail.EventParameter)?
                .Value;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (Guid.TryParse(raw, out var id))
            {
                return _auditManager.GetEvent(id);
            }

            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence)
                ? _auditManager.GetEvent(sequence)
                : null;
        }

        /// <summary>
        /// Builds the head of the detail: the reference and the sentence the typing composes,
        /// then who caused it and when.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The header control.</returns>
        private static IControl BuildHeader(AuditEvent @event, IRenderControlContext renderContext)
        {
            var panel = new ControlPanel("audit-detail-header");

            panel.Add(new ControlText()
            {
                // the sentence is composed from the typed fields rather than stored as one, so
                // it reads in the reader's language and stays filterable in every other reading
                Text = _ => string.Concat
                (
                    @event.Reference,
                    " - ",
                    I18N.Translate(renderContext, @event.Action.Text()),
                    ": ",
                    I18N.Translate(renderContext, @event.TargetType.Text()),
                    string.IsNullOrWhiteSpace(@event.TargetKey) ? string.Empty : string.Concat(" ", @event.TargetKey)
                ),
                Format = _ => TypeFormatText.H4
            });

            panel.Add(new ControlText()
            {
                Text = _ => string.Join
                (
                    " - ",
                    new[]
                    {
                        @event.Actor?.Name ?? @event.ActorName ?? I18N.Translate(renderContext, "kleenestar.core:audit.actor.system"),
                        @event.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                        I18N.Translate(renderContext, @event.Origin.Text()),
                        I18N.Translate(renderContext, @event.Category.Text()),
                        I18N.Translate(renderContext, @event.Outcome.Text()),
                        I18N.Translate(renderContext, @event.Severity.Text())
                    }
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.None)
            });

            return panel;
        }

        /// <summary>
        /// Builds the "Context" section: the durable identifiers that let this event be joined
        /// to the others it belongs with.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The section control.</returns>
        private static IControl BuildContext(AuditEvent @event, IRenderControlContext renderContext)
        {
            var section = new ControlSection("audit-detail-context")
            {
                Header = _ => "kleenestar.core:audit.detail.context.header",
                HeaderIcon = _ => new IconCircleInfo(),
                Layout = _ => TypeLayoutSection.Rule,
                Guide = _ => false
            };

            var table = new ControlTable("audit-detail-context-table")
            {
                Striped = _ => TypeStripedTable.Row,
                SuppressHeaders = _ => true
            }
                .AddColumn(string.Empty)
                .AddColumn(string.Empty);

            Row(table, renderContext, "kleenestar.core:audit.field.sequence", @event.Sequence.ToString(CultureInfo.InvariantCulture));
            Row(table, renderContext, "kleenestar.core:audit.field.timestamp", @event.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            Row(table, renderContext, "kleenestar.core:audit.field.target", @event.TargetId?.ToString("D", CultureInfo.InvariantCulture));
            Row(table, renderContext, "kleenestar.core:audit.field.revision", @event.TargetRevision?.ToString(CultureInfo.InvariantCulture));
            Row(table, renderContext, "kleenestar.core:audit.field.correlation", @event.CorrelationId.ToString("D", CultureInfo.InvariantCulture));
            Row(table, renderContext, "kleenestar.core:audit.field.causation", @event.CausationId?.ToString("D", CultureInfo.InvariantCulture));
            Row(table, renderContext, "kleenestar.core:audit.field.agent", @event.Agent);
            Row(table, renderContext, "kleenestar.core:audit.field.client", @event.ClientAddress);

            section.Add(table);

            return section;
        }

        /// <summary>
        /// Builds the "State changes" section: the deltas, each naming what it did to which
        /// attribute and how the payloads are to be read.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The section control.</returns>
        private static IControl BuildDeltas(AuditEvent @event, IRenderControlContext renderContext)
        {
            var deltas = (@event.Deltas ?? []).OrderBy(x => x.Ordinal).ToList();

            var section = new ControlSection("audit-detail-deltas")
            {
                Header = _ => "kleenestar.core:audit.detail.deltas.header",
                HeaderIcon = _ => new IconPenToSquare(),
                Layout = _ => TypeLayoutSection.Rule,
                Guide = _ => false,
                Badge = deltas.Count > 0 ? _ => deltas.Count.ToString(CultureInfo.InvariantCulture) : null
            };

            if (deltas.Count == 0)
            {
                section.Add(new ControlText()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:audit.detail.deltas.none"),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return section;
            }

            var table = new ControlTable("audit-detail-deltas-table")
            {
                Striped = _ => TypeStripedTable.Row
            }
                // the cell control prints its text verbatim, so the column headers are resolved here
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:audit.delta.column.kind"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:audit.delta.column.attribute"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:audit.delta.column.type"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:audit.delta.column.old"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:audit.delta.column.new"));

            foreach (var delta in deltas)
            {
                table.AddRow
                (
                    new ControlTableCell() { Text = _ => I18N.Translate(renderContext, delta.Kind.Text()) },
                    new ControlTableCell() { Text = _ => delta.Field?.Name ?? delta.Attribute },
                    new ControlTableCell() { Text = _ => I18N.Translate(renderContext, delta.ValueKind.Text()) },
                    // an added attribute has no old value and a removed one has no new value;
                    // the placeholder says "this side carries no meaning here" rather than
                    // letting the reader take a blank for an empty value
                    new ControlTableCell() { Text = _ => Display(delta.Kind == AuditDeltaKind.Added ? null : delta.OldValue, renderContext) },
                    new ControlTableCell() { Text = _ => Display(delta.Kind == AuditDeltaKind.Removed ? null : delta.NewValue, renderContext) }
                );
            }

            section.Add(table);

            return section;
        }

        /// <summary>
        /// Builds the "Integrity" section: the seal of this event and the one it chains onto.
        /// </summary>
        /// <param name="event">The event.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The section control.</returns>
        private static IControl BuildIntegrity(AuditEvent @event, IRenderControlContext renderContext)
        {
            var section = new ControlSection("audit-detail-integrity")
            {
                Header = _ => "kleenestar.core:audit.detail.integrity.header",
                HeaderIcon = _ => new IconShieldHalved(),
                Layout = _ => TypeLayoutSection.Rule,
                Guide = _ => false
            };

            var table = new ControlTable("audit-detail-integrity-table")
            {
                Striped = _ => TypeStripedTable.Row,
                SuppressHeaders = _ => true
            }
                .AddColumn(string.Empty)
                .AddColumn(string.Empty);

            Row(table, renderContext, "kleenestar.core:audit.field.previoushash", @event.PreviousHash);
            Row(table, renderContext, "kleenestar.core:audit.field.hash", @event.Hash);

            section.Add(table);

            return section;
        }

        /// <summary>
        /// Adds a label/value row, skipping the ones the event carries nothing for so the tables
        /// stay a list of facts rather than a list of blanks.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <param name="labelKey">The translation key of the label.</param>
        /// <param name="value">The value, or <c>null</c>.</param>
        private static void Row(IControlTable table, IRenderControlContext renderContext, string labelKey, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            table.AddRow
            (
                new ControlTableCell() { Text = _ => I18N.Translate(renderContext, labelKey) },
                new ControlTableCell() { Text = _ => value }
            );
        }

        /// <summary>
        /// Returns the text a payload is shown as, with an explicit placeholder for one that
        /// carries no value.
        /// </summary>
        /// <param name="value">The payload, or <c>null</c>.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The display text.</returns>
        private static new string Display(string value, IRenderControlContext renderContext)
        {
            return string.IsNullOrEmpty(value)
                ? I18N.Translate(renderContext, "kleenestar.core:audit.value.empty")
                : value;
        }
    }
}
