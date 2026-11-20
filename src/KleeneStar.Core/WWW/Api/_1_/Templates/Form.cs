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
    /// Renders the presets step of the template wizard as an html fragment.
    /// </summary>
    /// <remarks>
    /// The step depends on the classes chosen in the first step of the same dialog, so the
    /// wizard declares it as dynamic and posts the answers collected so far. The endpoint reads
    /// the classes (<see cref="TemplatePresetForm.ClassesField"/>) from them, else the classes
    /// of the edited template (<c>?id=</c>), and builds the inputs through
    /// <see cref="TemplatePresetForm"/>. An input starts from the answer the payload carries for
    /// it, else from the stored preset of the edited template.
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
