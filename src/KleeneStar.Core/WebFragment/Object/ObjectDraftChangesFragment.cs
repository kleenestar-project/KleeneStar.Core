using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using System;
using System.Globalization;
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
    /// The content of the draft-changes dialog (<see cref="WWW.Issue._objectkey_.Draft"/>): what
    /// the unpublished draft of an object would change about its published text, attribute by
    /// attribute.
    /// </summary>
    /// <remarks>
    /// The editor shows what the text will say and the reading view shows what it says now;
    /// neither shows the difference, and the difference is what publishing decides about. The
    /// two states are therefore put side by side per attribute, and an attribute the draft
    /// leaves alone is named as unchanged rather than repeated - a comparison whose rows are
    /// mostly identical hides the one row that is not.
    /// <para>
    /// The bodies are handed to <see cref="ControlContent"/> for the same reason the reading
    /// view uses it: what the editor stores is its working surface, and printing that verbatim
    /// would compare scaffolding rather than documents. This is a comparison of the rendered
    /// texts, not a character-level diff - the question it answers is "what am I about to
    /// publish", and for prose that is read rather than counted.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Draft>]
    [Cache]
    public sealed class ObjectDraftChangesFragment : FragmentControlPanel
    {
        /// <summary>
        /// The class the rendered comparison carries. The page-modal that hosts the dialog
        /// copies the element matching it out of the fetched page, and a fragment's element id
        /// is derived from its fragment id and cannot be chosen - so the two agree on a class.
        /// </summary>
        public const string ContentClass = "ks-draft-changes";

        private readonly IObjectManager _objectManager;
        private readonly IObjectDraftManager _draftManager;
        private readonly IIdentityManager _identityManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the object from the
        /// URL-bound object key.</param>
        /// <param name="draftManager">The draft manager the unpublished state is read from.</param>
        /// <param name="identityManager">The identity manager used to name the last writer.</param>
        public ObjectDraftChangesFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IObjectDraftManager draftManager,
            IIdentityManager identityManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _draftManager = draftManager;
            _identityManager = identityManager;
        }

        /// <summary>
        /// Renders the comparison. Returns <c>null</c> when the fragment's render conditions
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
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var id = @object.Id.ToString("N");
            var draft = _draftManager.GetDraft(@object.Id);
            var panel = new ControlPanel("object-draft-changes-" + id)
            {
                Classes = [ContentClass]
            };

            if (draft is null)
            {
                panel.Add(new ControlText("object-draft-changes-none-" + id)
                {
                    Text = _ => "kleenestar.core:object.draft.changes.none",
                    Format = _ => TypeFormatText.Paragraph
                });

                return panel.Render(renderContext, visualTree);
            }

            panel.Add(new ControlText("object-draft-changes-meta-" + id)
            {
                Text = _ => BuildMeta(draft),
                Format = _ => TypeFormatText.Small
            });

            panel.Add(BuildTitleSection(id, @object, draft));
            panel.Add(BuildBodySection(id, @object, draft));

            return panel.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the section comparing the titles.
        /// </summary>
        /// <param name="id">The object id, formatted for use in element ids.</param>
        /// <param name="object">The published object.</param>
        /// <param name="draft">The unpublished draft.</param>
        /// <returns>The section.</returns>
        private static IControl BuildTitleSection(string id, Model.Entities.Object @object, Model.Entities.ObjectDraft draft)
        {
            var section = new ControlSection("object-draft-title-" + id)
            {
                Header = _ => "kleenestar.core:object.summary.label",
                HeaderIcon = _ => new IconHeading(),
                Layout = _ => TypeLayoutSection.Rule
            };

            if (draft.Summary is null || string.Equals(draft.Summary, @object.Summary, StringComparison.Ordinal))
            {
                section.Add(Unchanged("object-draft-title-same-" + id));

                return section;
            }

            section.Add(Before("object-draft-title-before-" + id, @object.Summary));
            section.Add(After("object-draft-title-after-" + id, draft.Summary));

            return section;
        }

        /// <summary>
        /// Builds the section comparing the rich-text bodies.
        /// </summary>
        /// <param name="id">The object id, formatted for use in element ids.</param>
        /// <param name="object">The published object.</param>
        /// <param name="draft">The unpublished draft.</param>
        /// <returns>The section.</returns>
        private static IControl BuildBodySection(string id, Model.Entities.Object @object, Model.Entities.ObjectDraft draft)
        {
            var section = new ControlSection("object-draft-body-" + id)
            {
                Header = _ => "kleenestar.core:object.description.label",
                HeaderIcon = _ => new IconAlignLeft(),
                Layout = _ => TypeLayoutSection.Rule
            };

            if (draft.Description is null || string.Equals(draft.Description, @object.Description, StringComparison.Ordinal))
            {
                section.Add(Unchanged("object-draft-body-same-" + id));

                return section;
            }

            section.Add(Label("object-draft-body-before-label-" + id, "kleenestar.core:object.draft.changes.published"));

            // a workbook is compared as the two grids; the published body of a document switched
            // to the spreadsheet renderer may still be prose and is shown as such
            if (ObjectSpreadsheetView.Create("object-draft-body-after-" + id, draft.Description, ObjectSpreadsheetView.SectionHeight, "kleenestar.core:object.draft.changes.empty") is { } after)
            {
                section.Add(ObjectSpreadsheetView.Create("object-draft-body-before-" + id, @object.Description, ObjectSpreadsheetView.SectionHeight, "kleenestar.core:object.draft.changes.empty")
                    ?? new ControlContent("object-draft-body-before-" + id)
                    {
                        Content = _ => @object.Description,
                        Format = _ => TypeFormatContent.RichText,
                        Placeholder = _ => "kleenestar.core:object.draft.changes.empty",
                        Classes = ["ks-draft-changes-before"]
                    });
                section.Add(Label("object-draft-body-after-label-" + id, "kleenestar.core:object.draft.changes.draft"));
                section.Add(after);

                return section;
            }

            section.Add(new ControlContent("object-draft-body-before-" + id)
            {
                Content = _ => @object.Description,
                Format = _ => TypeFormatContent.RichText,
                Placeholder = _ => "kleenestar.core:object.draft.changes.empty",
                Classes = ["ks-draft-changes-before"]
            });

            section.Add(Label("object-draft-body-after-label-" + id, "kleenestar.core:object.draft.changes.draft"));
            section.Add(new ControlContent("object-draft-body-after-" + id)
            {
                Content = _ => draft.Description,
                Format = _ => TypeFormatContent.RichText,
                Placeholder = _ => "kleenestar.core:object.draft.changes.empty",
                Classes = ["ks-draft-changes-after"]
            });

            return section;
        }

        /// <summary>
        /// Builds the note that an attribute is not touched by the draft.
        /// </summary>
        /// <param name="id">The element id.</param>
        /// <returns>The control.</returns>
        private static IControl Unchanged(string id)
        {
            return new ControlText(id)
            {
                Text = _ => "kleenestar.core:object.draft.changes.unchanged",
                Format = _ => TypeFormatText.Italic,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            };
        }

        /// <summary>
        /// Builds the caption above one side of a comparison.
        /// </summary>
        /// <param name="id">The element id.</param>
        /// <param name="key">The i18n key of the caption.</param>
        /// <returns>The control.</returns>
        private static IControl Label(string id, string key)
        {
            return new ControlText(id)
            {
                Text = _ => key,
                Format = _ => TypeFormatText.Small,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
            };
        }

        /// <summary>
        /// Builds the published value of a single-line attribute.
        /// </summary>
        /// <param name="id">The element id.</param>
        /// <param name="value">The published value.</param>
        /// <returns>The control.</returns>
        private static IControl Before(string id, string value)
        {
            return new ControlText(id)
            {
                Text = _ => value,
                Format = _ => TypeFormatText.Paragraph,
                Classes = ["ks-draft-changes-before"]
            };
        }

        /// <summary>
        /// Builds the unpublished value of a single-line attribute.
        /// </summary>
        /// <param name="id">The element id.</param>
        /// <param name="value">The unpublished value.</param>
        /// <returns>The control.</returns>
        private static IControl After(string id, string value)
        {
            return new ControlText(id)
            {
                Text = _ => value,
                Format = _ => TypeFormatText.Paragraph,
                Classes = ["ks-draft-changes-after"]
            };
        }

        /// <summary>
        /// Builds the line naming who wrote the draft last and when.
        /// </summary>
        /// <param name="draft">The draft.</param>
        /// <returns>The meta text.</returns>
        private string BuildMeta(Model.Entities.ObjectDraft draft)
        {
            var stamp = draft.Updated.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            var author = draft.UpdaterId.HasValue
                ? _identityManager.GetIdentity(draft.UpdaterId.Value)?.Name
                : null;

            return string.IsNullOrWhiteSpace(author) ? stamp : author + " · " + stamp;
        }
    }
}
