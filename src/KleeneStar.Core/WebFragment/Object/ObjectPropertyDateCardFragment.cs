using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Object-scoped property card that surfaces every <see cref="FieldType.Date"/> field of
    /// the current object's class as an inline-editable "field name: value" row on
    /// <see cref="WWW.Issue._objectkey_.Index"/>.
    /// </summary>
    /// <remarks>
    /// The date fields are intentionally omitted from the form-driven detail view rendered by
    /// <see cref="ObjectItemDetailFragment"/> and grouped here instead. Each value is wrapped
    /// in a <see cref="ControlSmartEdit"/> hosting a <see cref="ControlFormItemInputDate"/>,
    /// so edits persist inline via the object REST API exactly as they did in the detail list.
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
        private readonly IValueManager _valueManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the current object
        /// from the URL-bound object key.</param>
        /// <param name="fieldManager">The field manager used to enumerate the class fields.</param>
        /// <param name="valueManager">The value manager used to read the object's current
        /// field values.</param>
        public ObjectPropertyDateCardFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IFieldManager fieldManager,
            IValueManager valueManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _fieldManager = fieldManager;
            _valueManager = valueManager;
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
                .Where(f => !f.Deprecated && f.State == FieldState.Active && f.FieldType == FieldType.Date
                    && !IsSystemTimestampAlias(f.Name))
                .ToList();

            if (dateFields.Count == 0)
            {
                return null;
            }

            var section = new ControlSection("object-property-date-section")
            {
                Header = _ => "kleenestar.core:object.property.date.header",
                HeaderIcon = _ => new IconCalendarDays(),
                Layout = _ => TypeLayoutSection.Rule
            };

            var objectUri = ResolveObjectRestUri(@object, renderContext);

            foreach (var field in dateFields)
            {
                var value = _valueManager.GetValue(@object.Id, field.Id);
                section.Add(BuildDateBlock(@object, objectUri, field, value));
            }

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds a single "field name: value" row for a date field: the field name (with the
        /// field description as a native HTML <c>title</c> tooltip and a trailing asterisk for
        /// required fields) followed by an inline-editable date smart-edit. The row carries no
        /// layout of its own - the reference zone lays out its key/value rows as one rule.
        /// </summary>
        /// <param name="object">The object whose value is displayed.</param>
        /// <param name="objectUri">The REST URI bound to the object's id; the smart-edit PUTs
        /// against it when the value changes.</param>
        /// <param name="field">The date field being rendered.</param>
        /// <param name="value">The persisted value for the field, or <c>null</c> when unset.</param>
        /// <returns>The control hosting the field label and the inline date editor.</returns>
        private static IControl BuildDateBlock(Model.Entities.Object @object, IUri objectUri, Model.Entities.Field field, Value value)
        {
            var input = new ControlFormItemInputDate()
            {
                Name = _ => field.Name,
                Label = _ => field.Name,
                // Pin an explicit, culture-neutral ISO format. ControlFormItemInputDate renders the
                // value with the thread's CurrentCulture but emits data-format from the request culture;
                // when the two differ (e.g. a German host serving an en-US app) the '/' in a slash
                // pattern is rewritten to the CurrentCulture date separator ('.'), so data-value
                // ("6.24.2026") no longer matches data-format ("M/d/yyyy") and the client date control
                // (webexpress.webui.input.date) fails to parse it, leaving the field blank. "yyyy-MM-dd"
                // uses a literal '-' that .NET never substitutes and is one of the formats the client
                // _parseDate understands, so value and format stay in sync across cultures.
                Format = _ => "yyyy-MM-dd",
                Placeholder = _ => field.Placeholder,
                Description = _ => field.HelpText,
                Help = _ => ProseText.ToPlainText(field.Description),
                Required = _ => field.Required
            };

            var smartEdit = new ControlSmartEdit("date-field-" + field.Id.ToString("N"))
            {
                ObjectId = _ => @object.Id.ToString(),
                ObjectName = _ => field.Name,
                Uri = _ => objectUri,
                Method = _ => RequestMethod.PUT
            };

            smartEdit.Add(input);
            smartEdit.Initialize(args => args.SetValue(input, ParseDateValue(value?.Data)));

            var label = new ControlHtml("date-field-label-" + field.Id.ToString("N"))
            {
                Html = _ =>
                {
                    var span = new HtmlElementTextSemanticsSpan
                    {
                        Class = "wx-kleenestar-field-label"
                    };

                    if (!ProseText.IsEmpty(field.Description))
                    {
                        span.AddUserAttribute("title", ProseText.ToPlainText(field.Description));
                    }

                    span.Add(new HtmlText(field.Name + (field.Required ? " *" : "") + ":"));
                    return span.ToString();
                }
            };

            // the row takes the layout every key/value row of the reference zone takes, so a
            // date lines up with the people and the tags beside it rather than with a rule of
            // its own
            return new ControlPanel("date-field-row-" + field.Id.ToString("N"), label, smartEdit)
            {
                Classes = ["wx-kleenestar-field"]
            };
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

        /// <summary>
        /// Parses the persisted <see cref="Value.Data"/> payload of a date field into the
        /// strongly-typed input value consumed by the smart-edit. Empty or unparsable payloads
        /// yield an empty date so the input renders blank rather than throwing.
        /// </summary>
        /// <param name="data">The persisted value payload; <c>null</c> or empty when unset.</param>
        /// <returns>The date input-value wrapper.</returns>
        private static ControlFormInputValueDate ParseDateValue(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return new ControlFormInputValueDate((DateTime?)null);
            }

            return DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                ? new ControlFormInputValueDate(dt)
                : new ControlFormInputValueDate((DateTime?)null);
        }

        /// <summary>
        /// Returns the REST endpoint that owns the object's persistence, augmented with the
        /// object's id so smart-edit PUTs target the right record.
        /// </summary>
        /// <param name="object">The object whose REST endpoint is resolved.</param>
        /// <param name="renderContext">The current render context; used to bind the URI to the
        /// active request's route parameters.</param>
        /// <returns>The bound REST URI, or <c>null</c> when no endpoint is registered.</returns>
        private static IUri ResolveObjectRestUri(Model.Entities.Object @object, IRenderControlContext renderContext)
        {
            var uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();
            if (uri is null)
            {
                return null;
            }

            var withQuery = uri.Add(new UriQuery("id", @object.Id.ToString()));

            return renderContext?.Request is null
                ? withQuery
                : withQuery.BindParameters(renderContext.Request);
        }
    }
}
