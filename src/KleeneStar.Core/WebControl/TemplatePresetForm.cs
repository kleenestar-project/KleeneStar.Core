using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

using ClassEntity = KleeneStar.Model.Entities.Class;
using FieldEntity = KleeneStar.Model.Entities.Field;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The presets step of the template wizard: which fields a template can preset, the inputs
    /// that ask for them, and the presets a submitted wizard stores.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A template serves one or several classes of a workspace and stores its presets keyed by
    /// field name, so one preset reaches every served class that has a field of that name. The
    /// step therefore offers the fields of all served classes once per name, laid out after the
    /// create form of the class (else its edit form) that contributes the name first; a field
    /// on neither form follows below. Fields of several classes are grouped in one tab per class.
    /// </para>
    /// <para>
    /// The inputs are the dialog inputs of the object mask
    /// (<see cref="ObjectFieldSheet.CreateDialogInput"/>), none of them required, named
    /// <see cref="Prefix"/> plus the field name: the wizard posts the template's own attributes
    /// (<c>Name</c>, <c>Category</c>, <c>State</c>, ...) in the same payload, and a field of
    /// the same name would otherwise overwrite them. The title of the objects is preset under
    /// <c>Summary</c>; the description of the template is the description preset
    /// (<see cref="WebManager.ITemplateManager.GetPresets"/>), so the step does not ask for it.
    /// Workflow, attachment, picture and password fields take no preset.
    /// </para>
    /// </remarks>
    public static class TemplatePresetForm
    {
        /// <summary>
        /// The prefix of the payload keys of the preset inputs.
        /// </summary>
        public const string Prefix = "Preset_";

        /// <summary>
        /// The payload key of the class selection: the served classes, separated by semicolons.
        /// </summary>
        public const string ClassesField = "Classes";

        /// <summary>
        /// The preset keys that name no field and are kept as stored: the description preset a
        /// template may carry beside its own description, and the security level.
        /// </summary>
        private static readonly HashSet<string> _systemKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ObjectEntity.Description),
            nameof(ObjectEntity.SecurityLevelId)
        };

        /// <summary>
        /// Reads a list of class ids: a string separated by semicolons or commas (the tile
        /// selection), a list (a JSON array) or a single id.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The distinct ids in the given order, without the empty guid.</returns>
        public static IReadOnlyList<Guid> ParseClassIds(object value)
        {
            IEnumerable<string> parts = value switch
            {
                null => [],
                string text => text.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                JsonElement { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText()),
                JsonElement { ValueKind: JsonValueKind.String } element => (element.GetString() ?? string.Empty).Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                IEnumerable list => list.Cast<object>().Select(x => x?.ToString()),
                _ => [value.ToString()]
            };

            return [.. parts
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .Distinct()];
        }

        /// <summary>
        /// Formats class ids the way the tile selection holds them.
        /// </summary>
        /// <param name="classIds">The class ids.</param>
        /// <returns>The ids separated by semicolons.</returns>
        public static string FormatClassIds(IEnumerable<Guid> classIds)
        {
            return string.Join(";", (classIds ?? []).Select(x => x.ToString()));
        }

        /// <summary>
        /// Determines whether a template can preset a field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns><see langword="true"/> when the step offers the field.</returns>
        public static bool Offers(FieldEntity field)
        {
            return field is not null
                && !field.Deprecated
                && field.State == FieldState.Active
                && !ObjectFieldSheet.IsDescriptionAlias(field)
                && !string.Equals(field.Name, nameof(ObjectEntity.Summary), StringComparison.OrdinalIgnoreCase)
                && field.FieldType is not (FieldType.Workflow or FieldType.Attachment or FieldType.Avatar or FieldType.Password);
        }

        /// <summary>
        /// Returns the fields the step offers for the given classes, once per name; the class
        /// named first wins a name several classes share.
        /// </summary>
        /// <param name="classIds">The served classes.</param>
        /// <returns>The fields, keyed by name.</returns>
        public static IReadOnlyDictionary<string, FieldEntity> Fields(IEnumerable<Guid> classIds)
        {
            var fields = new Dictionary<string, FieldEntity>(StringComparer.OrdinalIgnoreCase);

            foreach (var classId in classIds ?? [])
            {
                foreach (var field in CoreHub.FieldManager.GetFields(new ClassIdParameter(classId)).Where(Offers))
                {
                    fields.TryAdd(field.Name, field);
                }
            }

            return fields;
        }

        /// <summary>
        /// Builds the items of the step.
        /// </summary>
        /// <param name="classIds">The served classes, the class the template is bound to first.</param>
        /// <param name="values">The values the inputs start from, keyed by field name.</param>
        /// <param name="inputs">Receives every input with the field it presets; the summary has none.</param>
        /// <returns>The form items.</returns>
        public static IEnumerable<IControlFormItem> BuildItems(IReadOnlyList<Guid> classIds, IReadOnlyDictionary<string, string> values, ICollection<(IControlFormItemInput Input, FieldEntity Field)> inputs)
        {
            values ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var summary = new ControlFormItemInputText()
            {
                Name = _ => Prefix + nameof(ObjectEntity.Summary),
                Label = _ => "kleenestar.core:template.preset.summary.label",
                Placeholder = _ => "kleenestar.core:template.preset.summary.placeholder",
                Help = _ => "kleenestar.core:template.preset.summary.help"
            };

            inputs.Add((summary, null));

            yield return summary;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sections = new List<(ClassEntity Class, List<IControlFormItem> Items)>();

            foreach (var classId in classIds ?? [])
            {
                var @class = CoreHub.ClassManager.GetClass(classId);

                if (@class is null)
                {
                    continue;
                }

                var fields = CoreHub.FieldManager
                    .GetFields(new ClassIdParameter(classId))
                    .Where(Offers)
                    .ToList();
                var byId = fields
                    .Where(f => values.ContainsKey(f.Name))
                    .ToDictionary(f => f.Id, f => values[f.Name]);
                var collected = new List<IControlFormItemInput>();
                var form = ObjectFormLayout.ResolveStandardForm(classId, FormType.Create) is { Tabs.Count: > 0 } create
                    ? create
                    : ObjectFormLayout.ResolveStandardForm(classId, FormType.Edit);
                var items = ObjectFormLayout
                    .BuildItems(form, classId, collected, byId, f => Offers(f) && !seen.Contains(f.Name))
                    .ToList();

                // a field the forms do not place is still a field the objects carry, so its
                // preset is asked for below the form
                var placed = collected.Select(x => x.Name?.Invoke(null)).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var field in fields.Where(f => !seen.Contains(f.Name) && !placed.Contains(f.Name)))
                {
                    if (ObjectFieldSheet.CreateDialogInput(field, byId.TryGetValue(field.Id, out var data) ? data : null) is { } input)
                    {
                        collected.Add(input);
                        items.Add(input);
                    }
                }

                foreach (var input in collected)
                {
                    var name = input.Name?.Invoke(null);
                    var field = fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (field is null || !seen.Add(field.Name))
                    {
                        continue;
                    }

                    Prepare(input, field);
                    inputs.Add((input, field));
                }

                if (items.Count > 0)
                {
                    sections.Add((@class, items));
                }
            }

            if (sections.Count == 1)
            {
                foreach (var item in sections[0].Items)
                {
                    yield return item;
                }

                yield break;
            }

            if (sections.Count == 0)
            {
                yield break;
            }

            var tabs = new ControlFormItemGroupTab()
            {
                Layout = _ => TypeLayoutTab.Underline
            };

            foreach (var (@class, items) in sections)
            {
                var view = new ControlFormItemGroupTabView("preset-" + @class.Id.ToString("N"))
                {
                    Title = _ => @class.Name
                };

                foreach (var item in items)
                {
                    view.Add(item);
                }

                tabs.AddView(view);
            }

            yield return tabs;
        }

        /// <summary>
        /// Writes the starting values into the render context, in the value type each input holds.
        /// </summary>
        /// <param name="renderContext">The context the step is rendered in.</param>
        /// <param name="inputs">The inputs of the step with their fields.</param>
        /// <param name="values">The values, keyed by field name (<c>Summary</c> for the title).</param>
        public static void Seed(IRenderControlFormContext renderContext, IEnumerable<(IControlFormItemInput Input, FieldEntity Field)> inputs, IReadOnlyDictionary<string, string> values)
        {
            foreach (var (input, field) in inputs)
            {
                var key = field?.Name ?? nameof(ObjectEntity.Summary);

                if (!values.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                // an answer of a date input is the short date pattern; the value type reads round-trip
                if (field?.FieldType == FieldType.Date)
                {
                    value = FieldValueForm.StoreDate(value, null, renderContext.Request?.Culture);
                }

                var seed = field is null
                    ? new ControlFormInputValueString(value)
                    : ObjectFieldSheet.CreateInputValue(input, field, value);

                if (seed is not null)
                {
                    renderContext.SetValue(input, seed);
                }
            }
        }

        /// <summary>
        /// Determines whether a payload carries the presets step, which a client that does not
        /// know the step (an API client changing the name) leaves out.
        /// </summary>
        /// <param name="payload">The payload, keyed case-insensitively.</param>
        /// <returns><see langword="true"/> when a key starts with <see cref="Prefix"/>.</returns>
        public static bool Carries(IEnumerable<string> payload)
        {
            return payload?.Any(x => x.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) == true;
        }

        /// <summary>
        /// Computes the presets a submitted step stores.
        /// </summary>
        /// <remarks>
        /// Every offered field takes the submitted answer; a blank answer, an unticked checkbox and
        /// an empty editor document preset nothing. A stored preset the step does not ask for is
        /// kept when it names a field of a served class or is a system key; one that names no
        /// field of the served classes any more is dropped.
        /// </remarks>
        /// <param name="payload">The submitted payload; keys are matched ignoring case.</param>
        /// <param name="classIds">The served classes.</param>
        /// <param name="stored">The presets stored so far, keyed by field name.</param>
        /// <param name="culture">The culture of the submit, which reads a date.</param>
        /// <returns>The presets as JSON, or <see langword="null"/> when there are none.</returns>
        public static string Collect(IReadOnlyDictionary<string, object> payload, IReadOnlyList<Guid> classIds, IReadOnlyDictionary<string, string> stored, CultureInfo culture)
        {
            stored ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var lookup = new Dictionary<string, object>(payload ?? new Dictionary<string, object>(), StringComparer.OrdinalIgnoreCase);
            var offered = Fields(classIds);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var classId in classIds ?? [])
            {
                known.UnionWith(CoreHub.FieldManager.GetFields(new ClassIdParameter(classId)).Select(f => f.Name));
            }

            var presets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in stored)
            {
                if (string.Equals(key, nameof(ObjectEntity.Summary), StringComparison.OrdinalIgnoreCase)
                    || offered.ContainsKey(key)
                    || (!known.Contains(key) && !_systemKeys.Contains(key)))
                {
                    continue;
                }

                presets[key] = value;
            }

            if (lookup.TryGetValue(Prefix + nameof(ObjectEntity.Summary), out var summary)
                && Serialize(summary) is { } title
                && !string.IsNullOrWhiteSpace(title))
            {
                presets[nameof(ObjectEntity.Summary)] = title.Trim();
            }
            else if (!lookup.ContainsKey(Prefix + nameof(ObjectEntity.Summary))
                && stored.TryGetValue(nameof(ObjectEntity.Summary), out var storedSummary))
            {
                presets[nameof(ObjectEntity.Summary)] = storedSummary;
            }

            foreach (var field in offered.Values)
            {
                stored.TryGetValue(field.Name, out var previous);

                if (!lookup.TryGetValue(Prefix + field.Name, out var answer))
                {
                    // an input the step did not render keeps what was stored
                    if (!string.IsNullOrEmpty(previous))
                    {
                        presets[field.Name] = previous;
                    }

                    continue;
                }

                var value = Store(Serialize(answer), field.FieldType, previous, culture);

                // a control that always submits a value submits its starting value untouched;
                // without a stored preset it started there and was not set
                if (string.IsNullOrEmpty(previous) && IsUntouched(value, field.FieldType))
                {
                    continue;
                }

                if (!IsBlank(value, field.FieldType))
                {
                    presets[field.Name] = value;
                }
            }

            return presets.Count == 0 ? null : JsonSerializer.Serialize(presets);
        }

        /// <summary>
        /// Puts an answer into the form a value row stores, the way <c>/api/1/objects</c> does.
        /// </summary>
        /// <param name="raw">The serialized answer.</param>
        /// <param name="fieldType">The type of the field.</param>
        /// <param name="previous">The stored preset, or <see langword="null"/>.</param>
        /// <param name="culture">The culture of the submit.</param>
        /// <returns>The value to store.</returns>
        public static string Store(string raw, FieldType fieldType, string previous, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            return fieldType switch
            {
                FieldType.Tag => string.Join(",", raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                FieldType.Boolean => FieldValueForm.StoreBoolean(raw),
                FieldType.Date => FieldValueForm.StoreDate(raw, previous, culture),
                _ => raw
            };
        }

        /// <summary>
        /// Determines whether an answer is the value a control submits without being set: the
        /// colour input starts black, the rating at zero stars, the two-thumb slider of a
        /// <see cref="FieldType.Slider"/> field over its whole span <c>0;100</c>, the range input
        /// of a <see cref="FieldType.Range"/> field in the middle of <c>0..10</c>.
        /// </summary>
        /// <param name="value">The answer in storage form.</param>
        /// <param name="fieldType">The type of the field.</param>
        /// <returns><see langword="true"/> for the starting value of the control.</returns>
        public static bool IsUntouched(string value, FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.Color => string.Equals(value, "#000000", StringComparison.OrdinalIgnoreCase),
                FieldType.Rating => value == "0",
                FieldType.Slider => value == "0;100",
                FieldType.Range => value == "5",
                _ => false
            };
        }

        /// <summary>
        /// Determines whether a stored answer presets nothing.
        /// </summary>
        /// <param name="value">The answer in storage form.</param>
        /// <param name="fieldType">The type of the field.</param>
        /// <returns><see langword="true"/> for a blank answer.</returns>
        private static bool IsBlank(string value, FieldType fieldType)
        {
            return string.IsNullOrWhiteSpace(value)
                || (fieldType == FieldType.Boolean && !FieldValueForm.IsTrue(value))
                || ProseText.IsEmpty(value);
        }

        /// <summary>
        /// Serializes a payload value: text as it is, a boolean as <c>true</c>/<c>false</c>, a
        /// number invariant, a list separated by semicolons.
        /// </summary>
        /// <param name="value">The payload value.</param>
        /// <returns>The text, or <see langword="null"/>.</returns>
        private static string Serialize(object value)
        {
            return value switch
            {
                null => null,
                string text => text,
                bool flag => flag ? "true" : "false",
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                JsonElement { ValueKind: JsonValueKind.True } => "true",
                JsonElement { ValueKind: JsonValueKind.False } => "false",
                JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
                JsonElement { ValueKind: JsonValueKind.Array } array => string.Join(";", array.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())),
                JsonElement element => element.GetRawText(),
                IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
                IEnumerable list => string.Join(";", list.Cast<object>().Select(Serialize).Where(x => !string.IsNullOrEmpty(x))),
                _ => value.ToString()
            };
        }

        /// <summary>
        /// Makes an input of the object mask an input of the step: optional, and named after
        /// <see cref="Prefix"/> and the field.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="field">The field it presets.</param>
        private static void Prepare(IControlFormItemInput input, FieldEntity field)
        {
            if (input is ControlFormItem item)
            {
                var name = Prefix + field.Name;

                item.Name = _ => name;
            }

            // the required flag lives on the generic input base; a preset is never required
            input.GetType()
                .GetProperty(nameof(ControlFormItemInputText.Required))?
                .SetValue(input, (Func<IRenderControlContext, bool>)(_ => false));
        }
    }
}
