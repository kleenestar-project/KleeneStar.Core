using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System.Linq;
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
    /// Object-scoped property card that carries every date of the current object on the issue
    /// and asset pages: the values of its date fields (<see cref="ObjectFieldSheet.IsDateField"/>).
    /// </summary>
    /// <remarks>
    /// The rows are an <see cref="ObjectFieldSheet"/>, the control of the details card, so a date
    /// reads and edits the same way as every other value. Only filled dates are listed; an unfilled
    /// one is set through the edit dialog. Without a filled date the card says so
    /// (<c>object.property.date.empty</c>) instead of showing empty rows. The details card leaves the date fields out. Fields aliasing the
    /// creation or update time are left out too: the lifecycle card shows them.
    /// </remarks>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Order(3)]
    [Cache]
    public sealed class ObjectPropertyDateCardFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IFieldManager _fieldManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        /// <param name="fieldManager">The field manager used to enumerate the class fields.</param>
        public ObjectPropertyDateCardFragment(IFragmentContext fragmentContext, IObjectManager objectManager, IFieldManager fieldManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _fieldManager = fieldManager;
        }

        /// <summary>
        /// Renders the date card for the current object. Returns <c>null</c> when the
        /// fragment's render conditions exclude it, when no object can be resolved from the
        /// request, or when the object's class has no date fields.
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

            var dateFields = _fieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active && ObjectFieldSheet.IsDateField(f)
                    && !IsSystemTimestampAlias(f.Name))
                .ToList();

            if (dateFields.Count == 0)
            {
                return null;
            }

            var values = ObjectFieldSheet.LoadValues(@object.Id);
            var editable = ContentAuthorization.MayWrite(@object, renderContext?.Request);
            var filled = dateFields
                .Where(f => !ObjectFieldSheet.IsEmpty(f, values.TryGetValue(f.Id, out var value) ? value?.Data : null))
                .ToList();

            var section = new ControlSection("object-property-date-section")
            {
                Header = _ => "kleenestar.core:object.property.date.header",
                HeaderIcon = _ => new IconCalendarDays(),
                Layout = _ => TypeLayoutSection.Rule
            };

            if (filled.Count == 0)
            {
                section.Add(new ControlText("object-property-date-empty")
                {
                    Text = _ => "kleenestar.core:object.property.date.empty",
                    Format = _ => TypeFormatText.Small,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary)
                });
            }
            else
            {
                // only the filled dates; an unfilled one is set through the edit dialog
                section.Add(new ObjectFieldSheet("object-property-date-sheet", @object, filled, values)
                {
                    Editable = editable,
                    Uri = ObjectItemDetailFragment.ResolveObjectRestUri(@object, renderContext),
                    Classes = ["ks-field-sheet-compact"]
                });
            }

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Returns <c>true</c> when the supplied date-field name aliases one of the object's
        /// system lifecycle timestamps (creation / last update). Those are already surfaced by
        /// <see cref="ObjectPropertyLifecycleCardFragment"/>, so such fields are omitted here to
        /// avoid showing the creation and update dates twice.
        /// </summary>
        /// <param name="fieldName">The field name as configured on the class.</param>
        /// <returns><c>true</c> when the field duplicates a system lifecycle timestamp.</returns>
        private static bool IsSystemTimestampAlias(string fieldName)
        {
            var normalized = new string((fieldName ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

            return normalized is "created" or "createdat" or "createddate" or "updated" or "updatedat" or "updateddate";
        }
    }
}
