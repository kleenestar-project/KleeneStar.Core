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
        /// Handles <c>POST {base}</c>: renders the create form of the class named in the
        /// posted wizard payload.
        /// </summary>
        /// <param name="request">The incoming request carrying the wizard payload as json.</param>
        /// <returns>The rendered fragment as <c>text/html</c>.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Load(IRequest request)
        {
            var payload = ReadPayload(request);
            var classId = ReadGuid(payload, nameof(ObjectEntity.ClassId));
            var templateId = ReadGuid(payload, "TemplateId");

            var form = classId != Guid.Empty
                ? ObjectFormLayout.ResolveStandardForm(classId, FormType.Create)
                : null;

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var inputs = new List<IControlFormItemInput>();
            var items = BuildItems(form, classId, identityId, inputs).ToList();
            var content = Render(request, items, inputs, Presets(templateId));

            return new ResponseOK { Content = content }
                .AddHeaderContentType("text/html; charset=utf-8");
        }

        /// <summary>
        /// Builds the items of the step: the title every object carries, followed by the
        /// structure of the create form. When the class has no create form, the description
        /// is offered alongside the title so the object can still be described.
        /// </summary>
        /// <param name="form">The create form of the class, or null.</param>
        /// <param name="classId">The class the object is created in.</param>
        /// <param name="identityId">The identity filling the step in.</param>
        /// <param name="inputs">Receives the inputs of the step, so they can be pre-filled.</param>
        /// <returns>The form items.</returns>
        private static IEnumerable<IControlFormItem> BuildItems(FormEntity form, Guid classId, Guid identityId, ICollection<IControlFormItemInput> inputs)
        {
            var summary = ObjectFormLayout.CreateSummaryInput();
            inputs.Add(summary);

            yield return summary;

            // the wizard collects the content of the record, not who may see it: a new object
            // starts on the default level of its class and is reclassified afterwards through
            // the dialog of the overflow menu. What the step does say is when the caller is
            // cleared for none of the class's levels - the record they are about to file may
            // then leave their own lists the moment it exists
            var notice = ObjectFormLayout.CreateSecurityLevelNotice(classId, identityId, null);

            if (notice is not null)
            {
                yield return notice;
            }

            var structure = ObjectFormLayout.BuildItems(form, classId, inputs).ToList();

            if (structure.Count == 0)
            {
                var description = ObjectFormLayout.CreateDescriptionInput();
                inputs.Add(description);

                yield return description;
                yield break;
            }

            foreach (var item in structure)
            {
                yield return item;
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
        /// <param name="request">The request the fragment is rendered for.</param>
        /// <param name="items">The items to render.</param>
        /// <param name="inputs">The inputs the items contain, in creation order.</param>
        /// <param name="presets">The values to pre-fill, keyed by field name.</param>
        /// <returns>The rendered html.</returns>
        private static string Render(IRequest request, IEnumerable<IControlFormItem> items, IEnumerable<IControlFormItemInput> inputs, IReadOnlyDictionary<string, string> presets)
        {
            var renderContext = new RenderControlFormContext(null, null, request as Request, null);
            var group = new ControlFormItemGroupVertical();

            foreach (var item in items)
            {
                group.Items.Add(item);
            }

            // seed the context before rendering, so every input arrives carrying the value
            // the chosen template defines for its field
            foreach (var input in inputs)
            {
                var name = input.Name?.Invoke(renderContext);

                if (!string.IsNullOrEmpty(name)
                    && presets.TryGetValue(name, out var value)
                    && !string.IsNullOrEmpty(value))
                {
                    renderContext.SetValue(input, new ControlFormInputValueString(value));
                }
            }

            return group.Render(renderContext, null)?.ToString() ?? string.Empty;
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
