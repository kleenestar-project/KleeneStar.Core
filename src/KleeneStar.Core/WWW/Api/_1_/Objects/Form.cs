using KleeneStar.Core.WebControl;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

using FormEntity = KleeneStar.Model.Entities.Form;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WWW.Api._1_.Objects
{
    /// <summary>
    /// Renders the last step of the object creation wizard: the create form of the class
    /// the user chose, as an html fragment.
    /// </summary>
    /// <remarks>
    /// The step cannot be rendered with the page, because the class it belongs to is only
    /// decided in an earlier step of the same dialog. The wizard therefore declares the step
    /// as dynamic (<c>data-uri</c>) and posts the answers collected so far; this endpoint
    /// reads the class from them, reproduces the form the form manager holds for it, and
    /// pre-fills the inputs from the presets of the chosen template. A class without an
    /// active create form falls back to the two properties every object carries — its title
    /// and its description.
    /// </remarks>
    [Title("kleenestar.core:object.add.form.api.title")]
    [Cache]
    public sealed class Form : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Form()
        {
        }

        /// <summary>
        /// The value of the <c>part</c> parameter that renders the title and description step.
        /// </summary>
        public const string PartObject = "object";

        /// <summary>
        /// The value of the <c>part</c> parameter that renders the field step (the default).
        /// </summary>
        public const string PartValues = "values";

        /// <summary>
        /// The value of the <c>part</c> parameter that renders the security level step.
        /// </summary>
        public const string PartSecurityLevel = "securitylevel";

        /// <summary>
        /// The name prefix of the hidden input a rendered step carries, followed by the part. Its
        /// value names the class and template the step was rendered for.
        /// </summary>
        public const string ContextPrefix = "WizardContext";

        /// <summary>
        /// The payload keys that are answers of the static steps, not of a rendered one.
        /// </summary>
        private static readonly HashSet<string> _staticKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(ObjectEntity.WorkspaceId), nameof(ObjectEntity.ClassId), "TemplateId",
            "ParentKey", "SubtaskOf", "IdempotencyKey"
        };

        /// <summary>
        /// Handles <c>POST {base}?part=object|values|securitylevel</c>: renders a step of the
        /// create wizard for the class named in the posted wizard payload, pre-filled from the
        /// presets of the chosen template.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item><c>object</c>: the title and below it the description (prose editor).</item>
        ///   <item><c>values</c>: the create form of the class (<see cref="ObjectFormLayout.BuildItems"/>,
        ///   its tabs as a tab group) without the fields named <c>Summary</c> or <c>Description</c>,
        ///   which alias the attributes of the object step.</item>
        ///   <item><c>securitylevel</c>: <see cref="ObjectFormLayout.CreateSecurityLevelNotice"/> and the
        ///   tiles of <see cref="SecurityLevelTiles"/>, preselected with the template preset or the
        ///   class default.</item>
        /// </list>
        /// A step without items answers <c>204 No Content</c>, which the wizard skips. A rendered
        /// step carries the hidden input <c>WizardContext{part}</c> naming class and template; a
        /// reload whose payload carries the same context starts from the answers of the payload,
        /// the template presets fill the rest (<see cref="Answers"/>).
        /// </remarks>
        /// <param name="request">The incoming request carrying the wizard payload as json.</param>
        /// <returns>The rendered fragment as <c>text/html</c>, or 204.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Load(IRequest request)
        {
            var payload = ReadPayload(request);
            var classId = ReadGuid(payload, nameof(ObjectEntity.ClassId));
            var templateId = ReadGuid(payload, "TemplateId");
            var part = request.GetParameter("part")?.Value?.Trim().ToLowerInvariant() ?? PartValues;

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var renderContext = new RenderControlFormContext(null, null, request as Request, null);
            var inputs = new List<IControlFormItemInput>();

            // a step reloaded for the same class and template (the wizard reloads a step whose
            // payload changed, going back as well as forth) starts from what was entered on it;
            // the template presets fill what was not
            var values = new Dictionary<string, string>(WithDefaultSecurityLevel(Presets(templateId), classId, identityId), StringComparer.OrdinalIgnoreCase);

            foreach (var answer in Answers(payload, part, classId, templateId))
            {
                values[answer.Key] = answer.Value;
            }

            var items = part switch
            {
                PartObject => ObjectItems(inputs).ToList(),
                PartSecurityLevel => SecurityLevelItems(renderContext, classId, identityId, values).ToList(),
                _ => ValueItems(classId, inputs, PresetsById(values, classId)).ToList()
            };

            if (items.Count == 0)
            {
                return new ResponseNoContent();
            }

            items.Add(new PresetHiddenInput
            {
                Name = _ => ContextPrefix + part,
                Value = _ => Context(classId, templateId)
            });

            var content = Render(renderContext, items, inputs, values, classId);

            return new ResponseOK { Content = content }
                .AddHeaderContentType("text/html; charset=utf-8");
        }

        /// <summary>
        /// Builds the object step: the title and below it the description.
        /// </summary>
        /// <param name="inputs">Receives the inputs of the step, so they can be pre-filled.</param>
        /// <returns>The form items.</returns>
        private static IEnumerable<IControlFormItem> ObjectItems(ICollection<IControlFormItemInput> inputs)
        {
            var summary = ObjectFormLayout.CreateSummaryInput();
            var description = ObjectFormLayout.CreateDescriptionInput();

            inputs.Add(summary);
            inputs.Add(description);

            yield return summary;
            yield return description;
        }

        /// <summary>
        /// Builds the field step from the create form of the class.
        /// </summary>
        /// <param name="classId">The class the object is created in.</param>
        /// <param name="inputs">Receives the inputs of the step, so they can be pre-filled.</param>
        /// <param name="values">The template presets keyed by field id; they choose the inputs.</param>
        /// <returns>The form items; empty without a create form or fields.</returns>
        private static IEnumerable<IControlFormItem> ValueItems(Guid classId, ICollection<IControlFormItemInput> inputs, IReadOnlyDictionary<Guid, string> values)
        {
            var form = classId != Guid.Empty
                ? ObjectFormLayout.ResolveStandardForm(classId, FormType.Create)
                : null;

            return ObjectFormLayout.BuildItems(form, classId, inputs, values, f =>
                !ObjectFieldSheet.IsDescriptionAlias(f)
                && !string.Equals(f.Name, nameof(ObjectEntity.Summary), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Builds the security level step: who may see the record is decided when it is filed.
        /// </summary>
        /// <param name="renderContext">The context the step is rendered in.</param>
        /// <param name="classId">The class the object is created in.</param>
        /// <param name="identityId">The identity filling the step in.</param>
        /// <param name="presets">The presets; <c>SecurityLevelId</c> preselects a tile.</param>
        /// <returns>The form items; empty when the class offers the caller nothing to decide.</returns>
        private static IEnumerable<IControlFormItem> SecurityLevelItems(IRenderControlContext renderContext, Guid classId, Guid identityId, IReadOnlyDictionary<string, string> presets)
        {
            if (classId == Guid.Empty)
            {
                yield break;
            }

            var notice = ObjectFormLayout.CreateSecurityLevelNotice(classId, identityId, null);

            if (notice is not null)
            {
                yield return notice;
            }

            Guid? preset = presets.TryGetValue(nameof(ObjectEntity.SecurityLevelId), out var value) && Guid.TryParse(value, out var id) && id != Guid.Empty
                ? id
                : null;
            var tiles = SecurityLevelTiles.Create(renderContext, classId, preset, identityId, markCurrent: false);

            if (tiles is not null)
            {
                yield return tiles;
            }
        }

        /// <summary>
        /// Renders the items as an html fragment.
        /// </summary>
        /// <remarks>
        /// The fragment is produced outside the page pipeline, so it is rendered against a
        /// context carrying only the request. That is all a form item reads — none of them
        /// touches the visual tree, and the values they show are supplied here rather than
        /// resolved from a page.
        /// </remarks>
        /// <param name="renderContext">The context the fragment is rendered in.</param>
        /// <param name="classId">The class, whose fields decide the value type of a seed.</param>
        /// <param name="items">The items to render.</param>
        /// <param name="inputs">The inputs the items contain, in creation order.</param>
        /// <param name="presets">The values to pre-fill, keyed by field name.</param>
        /// <returns>The rendered html.</returns>
        private static string Render(RenderControlFormContext renderContext, IEnumerable<IControlFormItem> items, IEnumerable<IControlFormItemInput> inputs, IReadOnlyDictionary<string, string> presets, Guid classId)
        {
            var group = new ControlFormItemGroupVertical();

            foreach (var item in items)
            {
                group.Items.Add(item);
            }

            var fields = classId == Guid.Empty
                ? new Dictionary<string, Model.Entities.Field>(StringComparer.OrdinalIgnoreCase)
                : CoreHub.FieldManager
                    .GetFields(new KleeneStar.Core.WebParameter.ClassIdParameter(classId))
                    .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

            // seed the context before rendering, so every input arrives carrying its answer or
            // the value the chosen template defines for its field, in the value type it holds
            foreach (var input in inputs)
            {
                var name = input.Name?.Invoke(renderContext);

                if (string.IsNullOrEmpty(name)
                    || !presets.TryGetValue(name, out var value)
                    || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                fields.TryGetValue(name, out var field);

                // a date input submits the short date pattern; the value type reads round-trip
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

            return group.Render(renderContext, null)?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Returns the context value of a rendered step: the class and the template it was
        /// rendered for.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <param name="templateId">The template, or <see cref="Guid.Empty"/>.</param>
        /// <returns>The context value.</returns>
        public static string Context(Guid classId, Guid templateId)
        {
            return classId.ToString("N") + ":" + templateId.ToString("N");
        }

        /// <summary>
        /// Returns the answers a wizard payload carries for a rendered step, keyed by input name:
        /// every entry except the static steps' and the context markers, when the payload's
        /// context of the part equals the class and template; else none, because the step was
        /// rendered for another class or template or not yet at all.
        /// </summary>
        /// <param name="payload">The posted wizard payload.</param>
        /// <param name="part">The part being rendered.</param>
        /// <param name="classId">The class the step is rendered for.</param>
        /// <param name="templateId">The template the step is rendered for.</param>
        /// <returns>The answers; a blank answer is kept, so a cleared field stays cleared.</returns>
        public static IReadOnlyDictionary<string, string> Answers(IReadOnlyDictionary<string, JsonElement> payload, string part, Guid classId, Guid templateId)
        {
            var answers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var marker = payload?.FirstOrDefault(x => string.Equals(x.Key, ContextPrefix + part, StringComparison.OrdinalIgnoreCase));

            if (marker?.Key is null
                || marker.Value.Value.ValueKind != JsonValueKind.String
                || !string.Equals(marker.Value.Value.GetString(), Context(classId, templateId), StringComparison.OrdinalIgnoreCase))
            {
                return answers;
            }

            foreach (var (key, value) in payload)
            {
                if (_staticKeys.Contains(key) || key.StartsWith(ContextPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                answers[key] = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                    JsonValueKind.Array => string.Join(";", value.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())),
                    _ => value.GetRawText()
                };
            }

            return answers;
        }

        /// <summary>
        /// Returns the presets that name a field of the class, keyed by field id.
        /// </summary>
        /// <param name="presets">The presets, keyed by field name.</param>
        /// <param name="classId">The class.</param>
        /// <returns>The presets by field id.</returns>
        private static IReadOnlyDictionary<Guid, string> PresetsById(IReadOnlyDictionary<string, string> presets, Guid classId)
        {
            var byName = new Dictionary<string, string>(presets, StringComparer.OrdinalIgnoreCase);

            return CoreHub.FieldManager
                .GetFields(new KleeneStar.Core.WebParameter.ClassIdParameter(classId))
                .Where(f => byName.ContainsKey(f.Name))
                .ToDictionary(f => f.Id, f => byName[f.Name]);
        }

        /// <summary>
        /// Returns the presets of the chosen template, keyed by field name.
        /// </summary>
        /// <param name="templateId">The template, or <see cref="Guid.Empty"/> for none.</param>
        /// <returns>The presets, which may be empty.</returns>
        private static IReadOnlyDictionary<string, string> Presets(Guid templateId)
        {
            if (templateId == Guid.Empty)
            {
                return new Dictionary<string, string>();
            }

            var template = CoreHub.TemplateManager.GetTemplate(templateId);

            return template is null || template.State != TemplateState.Active
                ? new Dictionary<string, string>()
                : CoreHub.TemplateManager.GetPresets(templateId);
        }

        /// <summary>
        /// Adds the class's default security level to the presets, so the selection starts on
        /// the classification a create that names none would get anyway.
        /// </summary>
        /// <param name="presets">The presets of the chosen template.</param>
        /// <param name="classId">The class the object is created in.</param>
        /// <param name="identityId">The identity filling the step in.</param>
        /// <returns>The presets, with the default level where the caller may assign it.</returns>
        private static IReadOnlyDictionary<string, string> WithDefaultSecurityLevel(IReadOnlyDictionary<string, string> presets, Guid classId, Guid identityId)
        {
            var name = nameof(ObjectEntity.SecurityLevelId);

            if (classId == Guid.Empty || presets.ContainsKey(name))
            {
                return presets;
            }

            var level = CoreHub.SecurityLevelManager.GetDefaultSecurityLevel(classId);

            if (level is null || !CoreHub.SecurityLevelManager.IsCleared(identityId, level.Id))
            {
                return presets;
            }

            return new Dictionary<string, string>(presets, StringComparer.OrdinalIgnoreCase)
            {
                [name] = level.Id.ToString()
            };
        }

        /// <summary>
        /// Reads the posted wizard payload.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The payload, which is empty when the body is absent or malformed.</returns>
        private static Dictionary<string, JsonElement> ReadPayload(IRequest request)
        {
            var content = (request as Request)?.Content;

            if (content is null || content.Length == 0)
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Encoding.UTF8.GetString(content))
                    ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>
        /// Reads a guid from the payload, ignoring the case of the key. A value that is not
        /// a guid — the "no template" entry of the template step — reads as absent.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="key">The key to read.</param>
        /// <returns>The guid, or <see cref="Guid.Empty"/>.</returns>
        private static Guid ReadGuid(Dictionary<string, JsonElement> payload, string key)
        {
            var entry = payload.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

            if (entry.Key is null || entry.Value.ValueKind != JsonValueKind.String)
            {
                return Guid.Empty;
            }

            return Guid.TryParse(entry.Value.GetString(), out var id) ? id : Guid.Empty;
        }
    }
}
