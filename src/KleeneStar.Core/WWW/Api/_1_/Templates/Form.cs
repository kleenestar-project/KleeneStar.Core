using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebRestApi;
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

namespace KleeneStar.Core.WWW.Api._1_.Templates
{
    /// <summary>
    /// Renders a dynamic step of the template wizard as an html fragment: the presets
    /// (<c>?part=presets</c>) or the security levels (<c>?part=securitylevel</c>).
    /// </summary>
    /// <remarks>
    /// The step depends on the classes chosen in the first step of the same dialog, so the
    /// wizard declares it as dynamic and posts the answers collected so far. The endpoint reads
    /// the classes (<see cref="TemplatePresetForm.ClassesField"/>) from them, else the classes
    /// of the edited template (<c>?id=</c>), and builds the inputs through
    /// <see cref="TemplatePresetForm"/>. An input starts from the answer the payload carries for
    /// it, else from the stored preset of the edited template.
    /// <para>
    /// The security level step shows one tile selection per served class
    /// (<see cref="SecurityLevelTiles.CreateForTemplate"/>), preselected with the answer of the
    /// payload, else with the stored choice (<see cref="TemplateSecurityLevels"/>), else with the
    /// class default. A step without any class that has assignable levels answers 204, which the
    /// wizard skips.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:template.preset.api.title")]
    [Cache]
    public sealed class Form : IRestApi
    {
        /// <summary>
        /// The name of the presets part.
        /// </summary>
        public const string PartPresets = "presets";

        /// <summary>
        /// The name of the security level part.
        /// </summary>
        public const string PartSecurityLevel = "securitylevel";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Form()
        {
        }

        /// <summary>
        /// Renders the presets step from the posted wizard payload.
        /// </summary>
        /// <param name="request">The request carrying the wizard payload as json.</param>
        /// <returns>The rendered step as <c>text/html</c>, 403 or 204.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Load(IRequest request)
        {
            var payload = ReadPayload(request);
            var template = Guid.TryParse(request.GetParameter("id")?.Value, out var templateId)
                ? CoreHub.TemplateManager.GetTemplate(templateId)
                : null;

            if (template is not null && !ContentAuthorization.MayAdminister(template.ClassId, request))
            {
                return new ResponseForbidden();
            }

            var classIds = payload.TryGetValue(TemplatePresetForm.ClassesField, out var classes)
                ? TemplatePresetForm.ParseClassIds(classes)
                : [];

            if (classIds.Count == 0 && template is not null)
            {
                classIds = CoreHub.TemplateManager.GetClassIds(template.Id);
            }

            // the owning class of an edited template leads, so its form lays out a shared field
            if (template is not null && classIds.Contains(template.ClassId))
            {
                classIds = [template.ClassId, .. classIds.Where(x => x != template.ClassId)];
            }

            classIds = [.. classIds.Where(x => CoreHub.ClassManager.GetClass(x) is not null && ContentAuthorization.MayAdminister(x, request))];

            if (classIds.Count == 0)
            {
                return new ResponseNoContent();
            }

            if (string.Equals(request.GetParameter("part")?.Value, PartSecurityLevel, StringComparison.OrdinalIgnoreCase))
            {
                return SecurityLevels(request, payload, template, classIds);
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (template is not null)
            {
                foreach (var (key, value) in CoreHub.TemplateManager.GetStoredPresets(template.Id))
                {
                    values[key] = value;
                }
            }

            // a step rendered before keeps what was entered on it, the cleared answers included
            foreach (var (key, value) in payload.Where(x => x.Key.StartsWith(TemplatePresetForm.Prefix, StringComparison.OrdinalIgnoreCase)))
            {
                values[key[TemplatePresetForm.Prefix.Length..]] = Text(value);
            }

            var inputs = new List<(IControlFormItemInput Input, Model.Entities.Field Field)>();
            var items = TemplatePresetForm.BuildItems(classIds, values, inputs).ToList();
            var renderContext = new RenderControlFormContext(null, null, request as Request, null);
            var group = new ControlFormItemGroupVertical();

            if (classIds.Count > 1)
            {
                group.Items.Add(new ControlFormItemStaticText()
                {
                    Text = _ => "kleenestar.core:template.preset.shared"
                });
            }

            foreach (var item in items)
            {
                group.Items.Add(item);
            }

            TemplatePresetForm.Seed(renderContext, inputs, values);

            return new ResponseOK { Content = group.Render(renderContext, null)?.ToString() ?? string.Empty }
                .AddHeaderContentType("text/html; charset=utf-8");
        }

        /// <summary>
        /// Renders the security level step: one tile selection per served class that has levels
        /// the caller may assign.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="payload">The wizard payload.</param>
        /// <param name="template">The edited template, or null.</param>
        /// <param name="classIds">The served classes the caller administers.</param>
        /// <returns>The rendered step, or 204 when no class offers a level.</returns>
        private static IResponse SecurityLevels(IRequest request, IReadOnlyDictionary<string, JsonElement> payload, Model.Entities.Template template, IReadOnlyList<Guid> classIds)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var stored = template is not null && CoreHub.TemplateManager.GetStoredPresets(template.Id).TryGetValue(TemplateSecurityLevels.PresetKey, out var raw)
                ? TemplateSecurityLevels.Parse(raw)
                : new Dictionary<Guid, Guid?>();
            var answers = payload.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase);
            var renderContext = new RenderControlFormContext(null, null, request as Request, null);
            var group = new ControlFormItemGroupVertical();

            foreach (var classId in classIds)
            {
                var @class = CoreHub.ClassManager.GetClass(classId);
                var value = TemplateSecurityLevels.TryReadAnswer(answers, classId, out var answer) && !string.IsNullOrWhiteSpace(answer)
                    ? answer
                    : stored.TryGetValue(classId, out var level) || stored.TryGetValue(Guid.Empty, out level)
                        ? (level ?? Guid.Empty).ToString()
                        : TemplateSecurityLevels.DefaultValue;
                var tiles = SecurityLevelTiles.CreateForTemplate(renderContext, @class, value, identityId);

                if (tiles is not null)
                {
                    group.Items.Add(tiles);
                }
            }

            if (group.Items.Count == 0)
            {
                return new ResponseNoContent();
            }

            return new ResponseOK { Content = group.Render(renderContext, null)?.ToString() ?? string.Empty }
                .AddHeaderContentType("text/html; charset=utf-8");
        }

        /// <summary>
        /// Reads the posted wizard payload.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The payload keyed ignoring case; empty when the body is absent or malformed.</returns>
        private static Dictionary<string, JsonElement> ReadPayload(IRequest request)
        {
            var content = (request as Request)?.Content;
            var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            if (content is null || content.Length == 0)
            {
                return result;
            }

            try
            {
                foreach (var (key, value) in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Encoding.UTF8.GetString(content)) ?? [])
                {
                    result[key] = value;
                }
            }
            catch (JsonException)
            {
                // a malformed payload renders the step from the stored presets
            }

            return result;
        }

        /// <summary>
        /// Reads a payload value as text; a list is joined by semicolons, as a multiple choice
        /// submits it.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The text; empty for null.</returns>
        private static string Text(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.Array => string.Join(";", value.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())),
                _ => value.GetRawText()
            };
        }
    }
}
