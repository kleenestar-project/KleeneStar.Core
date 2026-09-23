using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using System;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using CommitEntity = KleeneStar.Model.Entities.Commit;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The detail side of the history dialog: one revision of an object with its metadata, the
    /// fields it changed, the complete field set it left behind, and the button that reapplies it.
    /// </summary>
    /// <remarks>
    /// The two tables answer two different questions and both are needed. "Changed fields" is
    /// what the commit stores — the delta, and therefore what the action actually did. "All
    /// fields at this commit" is what the object looked like afterwards, which no single commit
    /// holds: it is replayed from the chain. Showing only the delta would leave a reader unable
    /// to tell what the object was; storing the full set per commit would make the history grow
    /// with the square of its length.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryDetail>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<ObjectViewPolicy>>]
    [Cache]
    public sealed class ObjectHistoryDetailFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly ICommitManager _commitManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the object from the request.</param>
        /// <param name="commitManager">The commit manager the revision is read from.</param>
        public ObjectHistoryDetailFragment(IFragmentContext fragmentContext, IObjectManager objectManager, ICommitManager commitManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _commitManager = commitManager;
        }

        /// <summary>
        /// Renders the revision. Returns <c>null</c> when the fragment's render conditions
        /// exclude it or when no object can be resolved from the request.
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

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter);

            if (@object is null)
            {
                return null;
            }

            var commit = Resolve(@object, renderContext);

            if (commit is null)
            {
                return Missing(renderContext, visualTree);
            }

            var state = _commitManager.GetStateAt(@object.Id, commit.Number);
            var panel = new ControlPanel("object-history-detail")
            {
                Padding = _ => new PropertySpacingPadding(PropertySpacing.Space.Two, PropertySpacing.Space.Two, PropertySpacing.Space.One, PropertySpacing.Space.Two)
            };

            panel.Add(BuildHeader(commit, renderContext));
            panel.Add(BuildChanges(commit, renderContext));

            if (state is not null)
            {
                panel.Add(BuildState(state, renderContext));
            }

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the head of the detail: the revision reference and what happened, then the
        /// author, the timestamp and the commit message.
        /// </summary>
        /// <param name="commit">The revision.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The header control.</returns>
        private static IControl BuildHeader(CommitEntity commit, IRenderControlContext renderContext)
        {
            var panel = new ControlPanel("object-history-detail-header");

            panel.Add(new ControlText()
            {
                Text = _ => string.Concat
                (
                    commit.Reference,
                    " — ",
                    I18N.Translate(renderContext, Model.Entities.CommitTypeExtensions.Text(commit.Type))
                ),
                Format = _ => TypeFormatText.H4
            });

            panel.Add(new ControlText()
            {
                Text = _ => string.Join
                (
                    " · ",
                    new[]
                    {
                        commit.CreatedBy?.Name ?? commit.CreatedByName ?? I18N.Translate(renderContext, "kleenestar.core:object.history.author.system"),
                        commit.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    }
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small
            });

            if (!string.IsNullOrWhiteSpace(commit.Message))
            {
                panel.Add(new ControlText()
                {
                    Text = _ => commit.Message,
                    Format = _ => TypeFormatText.Italic,
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.None)
                });
            }

            return panel;
        }

        /// <summary>
        /// Builds the "Changed fields" section: only the attributes this revision touched, as
        /// before/after pairs.
        /// </summary>
        /// <param name="commit">The revision.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The section control.</returns>
        private static IControl BuildChanges(CommitEntity commit, IRenderControlContext renderContext)
        {
            var changes = (commit.Changes ?? []).ToList();

            // the two sections stand side by side, so their counts are read against each other -
            // how much of the object this revision touched. the table draws its own structure,
            // so the section adds no guide line beside it.
            var section = new ControlSection("object-history-changes")
            {
                Header = _ => "kleenestar.core:object.history.changes.header",
                HeaderIcon = _ => new IconPenToSquare(),
                Layout = _ => TypeLayoutSection.Rule,
                Guide = _ => false,
                Badge = changes.Count > 0 ? _ => changes.Count.ToString(CultureInfo.InvariantCulture) : null
            };

            if (changes.Count == 0)
            {
                section.Add(new ControlText()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:object.history.changes.none"),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return section;
            }

            var table = new ControlTable("object-history-changes-table")
            {
                Striped = _ => TypeStripedTable.Row
            }
                // the cell control prints its text verbatim, so the column headers are resolved here
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:object.history.changes.column.field"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:object.history.changes.column.old"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:object.history.changes.column.new"));

            foreach (var change in changes)
            {
                table.AddRow
                (
                    new ControlTableCell() { Text = _ => Label(change.Name, change.Field?.Name, renderContext) },
                    new ControlTableCell() { Text = _ => Display(change.Name, change.FieldId, change.OldValue, renderContext) },
                    new ControlTableCell() { Text = _ => Display(change.Name, change.FieldId, change.NewValue, renderContext) }
                );
            }

            section.Add(table);

            return section;
        }

        /// <summary>
        /// Builds the "All fields at this commit" section: the complete field set of the object
        /// at this revision, replayed from the chain.
        /// </summary>
        /// <param name="state">The replayed state.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The section control.</returns>
        private static IControl BuildState(ObjectState state, IRenderControlContext renderContext)
        {
            var fields = state.Fields.Where(x => !string.IsNullOrEmpty(x.Value)).ToList();

            var section = new ControlSection("object-history-state")
            {
                Header = _ => "kleenestar.core:object.history.state.header",
                HeaderIcon = _ => new IconTableList(),
                Layout = _ => TypeLayoutSection.Rule,
                Guide = _ => false,
                Badge = fields.Count > 0 ? _ => fields.Count.ToString(CultureInfo.InvariantCulture) : null
            };

            if (fields.Count == 0)
            {
                section.Add(new ControlText()
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:object.history.state.none"),
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });

                return section;
            }

            var table = new ControlTable("object-history-state-table")
            {
                Striped = _ => TypeStripedTable.Row,
                SuppressHeaders = _ => true
            }
                .AddColumn("")
                .AddColumn("");

            foreach (var field in fields)
            {
                table.AddRow
                (
                    new ControlTableCell() { Text = _ => Label(field.Name, field.Label, renderContext) },
                    new ControlTableCell() { Text = _ => Display(field.Name, field.FieldId, field.Value, renderContext) }
                );
            }

            section.Add(table);

            return section;
        }

        /// <summary>
        /// Renders the note shown when the addressed revision does not exist — a stale deep link,
        /// or a commit of another object.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node.</returns>
        private static IHtmlNode Missing(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var panel = new ControlPanel("object-history-detail-missing");

            panel.Add(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:object.history.commit.missing"),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Paragraph
            });

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Resolves the revision addressed by the <c>?commit=</c> query, which carries either the
        /// revision number or the commit id. Falls back to the head so that opening the detail
        /// page without a query still shows something.
        /// </summary>
        /// <param name="object">The object.</param>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The commit, or <c>null</c>.</returns>
        private CommitEntity Resolve(ObjectEntity @object, IRenderControlContext renderContext)
        {
            var raw = renderContext?.Request?.GetParameter(global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryDetail.CommitParameter)?.Value;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return _commitManager.GetHead(@object.Id);
            }

            if (Guid.TryParse(raw, out var commitId))
            {
                var byId = _commitManager.GetCommit(commitId);

                return byId?.ObjectId == @object.Id ? byId : null;
            }

            return int.TryParse(raw.TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? _commitManager.GetCommit(@object.Id, number)
                : null;
        }

        /// <summary>
        /// Returns the label an attribute is shown under: the localized name of a system
        /// property, or the name of the class field.
        /// </summary>
        /// <param name="name">The recorded attribute name.</param>
        /// <param name="fieldName">The resolved field name, when the attribute is a class field.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The label.</returns>
        private static string Label(string name, string fieldName, IRenderControlContext renderContext)
        {
            var key = ObjectProperty.Text(name);

            return key is null
                ? fieldName ?? name
                : I18N.Translate(renderContext, key);
        }

        /// <summary>
        /// Returns the text a recorded value is shown as. A system property's stored reference id
        /// reads as the name it points at; an absent value reads as an em dash so an emptied
        /// field is visibly empty rather than blank.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="fieldId">The field id, or <c>null</c> for a system property.</param>
        /// <param name="value">The recorded value.</param>
        /// <param name="renderContext">The render context used for translating.</param>
        /// <returns>The display text.</returns>
        private static string Display(string name, Guid? fieldId, string value, IRenderControlContext renderContext)
        {
            var resolved = fieldId.HasValue ? value : ObjectProperty.Describe(name, value);

            return string.IsNullOrEmpty(resolved)
                ? I18N.Translate(renderContext, "kleenestar.core:object.history.value.empty")
                : resolved;
        }
    }
}
