using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using KleeneStar.Model.Forms;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Forms
{
    /// <summary>
    /// Provides editing capabilities for form structures via a REST API, enabling retrieval and update operations for
    /// form elements.
    /// </summary>
    /// <remarks>
    /// The editor identifies a field node by whatever it has: the id of the stored element it was
    /// loaded as, the id of the class field it was picked from, or - for a node the editor built
    /// itself, which carries a generated id - by its label, which is the field's name. The stored
    /// structure holds field <em>references</em> only, so the type, the required mark and the
    /// help a node travels with are read off the field on the way out and ignored on the way in.
    /// </remarks>
    [Title("Form structure")]
    public sealed class FormEditor : RestApiFormEditor<Model.Entities.Form>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public FormEditor()
        {
        }

        /// <summary>
        /// Creates a query context backed by the application's database.
        /// </summary>
        /// <returns>The shared <see cref="KleeneStarDbContext"/>.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves a catalog of form editor field items based on the specified query context and request parameters.
        /// </summary>
        /// <param name="context">
        /// The query context that provides information about the current data retrieval operation. Cannot be null.
        /// </param>
        /// <param name="request">
        /// The request containing parameters that influence which catalog items are retrieved. Cannot be null.
        /// </param>
        /// <returns>
        /// An enumerable collection of catalog field items that match the specified context and request. The
        /// collection may be empty if no items are found.
        /// </returns>
        protected override IEnumerable<RestApiFormEditorFieldItem> RetrieveCatalog(string formId, IQueryContext context, IRequest request)
        {
            var guid = Guid.TryParse(formId, out var g) ? g : Guid.Empty;
            var form = CoreHub.FormManager.GetForm(guid);

            if (form is null)
            {
                return [];
            }

            return CoreHub.FieldManager
                .GetFields(new ClassIdParameter(form.ClassId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .OrderBy(f => f.Name)
                .Select(f => new RestApiFormEditorFieldItem()
                {
                    Id = f.Id.ToString(),
                    Label = f.Name,
                    Type = MapFieldType(f.FieldType),
                    Required = f.Required,
                    Help = f.HelpText
                });
        }

        /// <summary>
        /// Retrieves the full structural tree of the form addressed by <paramref name="formId"/>.
        /// </summary>
        /// <param name="formId">The unique identifier of the form to load.</param>
        /// <param name="context">The query context (a <see cref="KleeneStarDbContext"/>).</param>
        /// <param name="request">The current API request.</param>
        /// <returns>
        /// The form editor item, or <c>null</c> when the form does not exist (the base
        /// class converts this into a 404 response).
        /// </returns>
        protected override RestApiFormEditorItem RetrieveItem(string formId, IQueryContext context, IRequest request)
        {
            if (!Guid.TryParse(formId, out var guid))
            {
                return null;
            }

            var form = CoreHub.FormManager.GetFormWithStructure(guid);

            if (form is null)
            {
                return null;
            }

            return Project(form);
        }

        /// <summary>
        /// Persists the structural tree contained in <paramref name="item"/> for the form
        /// addressed by <paramref name="formId"/>.
        /// </summary>
        /// <param name="formId">The unique identifier of the form to update.</param>
        /// <param name="item">The form structure sent by the editor.</param>
        /// <param name="context">The query context (a <see cref="KleeneStarDbContext"/>).</param>
        /// <param name="request">The current API request.</param>
        /// <returns>
        /// The freshly reloaded form structure with the new version number embedded.
        /// </returns>
        /// <remarks>
        /// The version the editor sends is the one it loaded, and the save is refused with a
        /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> when the
        /// stored form has moved on; a field the class does not define is refused as well. The
        /// base class converts both into a 400 response carrying the message, which the editor
        /// reports as a failed save and keeps its state for.
        /// </remarks>
        protected override RestApiFormEditorItem UpdateItem(string formId, RestApiFormEditorItem item, IQueryContext context, IRequest request)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (!Guid.TryParse(formId, out var guid))
            {
                throw new ArgumentException("Form id is not a valid GUID.", nameof(formId));
            }

            var form = CoreHub.FormManager.GetFormWithStructure(guid)
                ?? throw new InvalidOperationException($"Form '{guid}' not found.");

            var snapshot = ToSnapshot(item, form);

            CoreHub.FormManager.SaveFormStructure(guid, snapshot, item.Version);

            // the stored structure carries fresh element ids, and the answer is what the editor
            // could resynchronize from; the version it does read is the stored one
            return Project(CoreHub.FormManager.GetFormWithStructure(guid));
        }

        /// <summary>
        /// Translates the editor's payload into the provider-agnostic snapshot the model stores.
        /// </summary>
        /// <param name="item">The structure sent by the editor.</param>
        /// <param name="form">The stored form with its current structure, used to resolve the
        /// nodes the editor loaded from it.</param>
        /// <returns>The snapshot.</returns>
        /// <exception cref="InvalidOperationException">A field node names a field the class does
        /// not define.</exception>
        internal static FormStructureSnapshot ToSnapshot(RestApiFormEditorItem item, Model.Entities.Form form)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(form);

            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(form.ClassId))
                .ToList();

            var resolver = new FieldResolver(form, fields);

            // the name and description are edited inline in the editor's header and travel with
            // every save; a blank name is not a rename, because the form is addressed by it
            var name = string.IsNullOrWhiteSpace(item.FormName) ? form.Name : item.FormName.Trim();

            return new FormStructureSnapshot
            {
                FormName = name,
                FormDescription = item.FormDescription?.Trim(),
                Tabs = [.. (item.Tabs ?? []).Where(t => t is not null).Select((t, index) => new TabSnapshot
                {
                    Name = string.IsNullOrWhiteSpace(t.Name) ? $"Tab {index + 1}" : t.Name.Trim(),
                    Children = ToSnapshot(t.Children, resolver)
                })]
            };
        }

        /// <summary>
        /// Translates a list of editor nodes into snapshot nodes, recursively.
        /// </summary>
        /// <param name="nodes">The editor nodes.</param>
        /// <param name="resolver">The field resolver of the form.</param>
        /// <returns>The snapshot nodes, in order.</returns>
        private static List<NodeSnapshot> ToSnapshot(IEnumerable<RestApiFormEditorNodeItem> nodes, FieldResolver resolver)
        {
            var result = new List<NodeSnapshot>();

            foreach (var node in nodes ?? [])
            {
                switch (node)
                {
                    case RestApiFormEditorGroupItem group:
                        result.Add(new GroupSnapshot
                        {
                            Label = group.Label?.Trim(),
                            Layout = FormGroupLayoutExtensions.FromEditorString(group.Layout?.Trim().ToLowerInvariant()),
                            Children = ToSnapshot(group.Children, resolver)
                        });
                        break;

                    case RestApiFormEditorFieldItem field:
                        result.Add(new FieldRefSnapshot { FieldId = resolver.Resolve(field) });
                        break;

                    case null:
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown form node '{node.GetType().Name}'.");
                }
            }

            return result;
        }

        /// <summary>
        /// Projects a loaded form onto the editor's structure.
        /// </summary>
        /// <param name="form">The form with its structure.</param>
        /// <returns>The editor item.</returns>
        private static RestApiFormEditorItem Project(Model.Entities.Form form)
        {
            // The persisted structure only stores field references (FieldId); the field
            // metadata (name, type, required, help) lives on the Field entity, which the
            // structure loader does not hydrate. Resolve the class fields once and index them
            // by id so each reference can surface the real name and type instead of the
            // "unknown" / "string" fallbacks.
            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(form.ClassId))
                .ToDictionary(f => f.Id);

            return new RestApiFormEditorItem()
            {
                ClassName = form.Class?.Name,
                FormId = form.Id.ToString(),
                FormName = form.Name,
                FormDescription = form.Description,
                Version = form.Version,
                Tabs = (form.Tabs ?? []).OrderBy(t => t.Position).Select(t => new RestApiFormEditorTabItem()
                {
                    Id = t.Id.ToString(),
                    Name = t.Name,
                    Children = (t.Elements ?? [])
                        .OrderBy(e => e.Position)
                        .Select(e => GetChildren(e, fields))
                        .Where(e => e is not null)
                        .ToList()
                }).ToList()
            };
        }

        /// <summary>
        /// Creates a node item representing the specified form element for use in the REST API form editor.
        /// </summary>
        /// <param name="element">
        /// The form element to convert to a node item. Must be a field or group element.
        /// </param>
        /// <returns>
        /// A node item representing the form element, or null if the element type is not supported.
        /// </returns>
        private static RestApiFormEditorNodeItem GetChildren(FormElement element, IDictionary<Guid, Model.Entities.Field> fields)
        {
            if (element is FormFieldRefElement fieldRef)
            {
                fields.TryGetValue(fieldRef.FieldId, out var field);

                return new RestApiFormEditorFieldItem()
                {
                    Id = fieldRef.Id.ToString(),
                    Label = field?.Name ?? fieldRef.Field?.Name ?? "unknown",
                    Type = MapFieldType(field?.FieldType),
                    Required = field?.Required ?? false,
                    Help = field?.HelpText
                };
            }
            else if (element is FormGroupElement group)
            {
                return new RestApiFormEditorGroupItem()
                {
                    Id = group.Id.ToString(),
                    Label = group.Label,
                    Layout = group.Layout.ToEditorString(),
                    Children = (group.Children ?? [])
                        .OrderBy(c => c.Position)
                        .Select(c => GetChildren(c, fields))
                        .Where(c => c is not null)
                        .ToList()
                };
            }

            return null;
        }

        /// <summary>
        /// Maps a KleeneStar <see cref="FieldType"/> onto the logical field-type string the
        /// form editor understands (<c>string</c>, <c>text</c>, <c>number</c>,
        /// <c>timestamp</c>, <c>ref</c>, <c>enum</c>, <c>tags</c>, <c>file</c>). A missing or
        /// unrecognized type falls back to <c>string</c>.
        /// </summary>
        /// <param name="type">The field type, or <c>null</c> when the field could not be
        /// resolved from the catalog.</param>
        /// <returns>The editor field-type discriminator.</returns>
        private static string MapFieldType(FieldType? type)
        {
            return type?.Editor() ?? "string";
        }

        /// <summary>
        /// Resolves the field a node of the editor stands for.
        /// </summary>
        /// <remarks>
        /// Three things can identify a node, tried in this order: the id of the stored element
        /// the editor loaded it as (a node that was on the form already), the id of the class
        /// field (a node whose id the editor preserved from the catalog), and the label (a node
        /// the editor built with a generated id). The label is the field's name, so a node
        /// renamed in the editor names a field that does not exist and is refused - a form
        /// references its fields, it does not relabel them.
        /// </remarks>
        private sealed class FieldResolver
        {
            private readonly string _className;
            private readonly Dictionary<Guid, Guid> _fieldIdByElementId;
            private readonly Dictionary<Guid, Model.Entities.Field> _fieldsById;
            private readonly Dictionary<string, Model.Entities.Field> _fieldsByName;

            /// <summary>
            /// Initializes a new instance of the class.
            /// </summary>
            /// <param name="form">The stored form with its current structure.</param>
            /// <param name="fields">The fields of the form's class.</param>
            public FieldResolver(Model.Entities.Form form, IEnumerable<Model.Entities.Field> fields)
            {
                _className = form.Class?.Name ?? form.ClassId.ToString();
                _fieldsById = fields.ToDictionary(f => f.Id);

                // a class with two fields of one name is a mistake elsewhere; here the first
                // one answers rather than the whole save failing over it
                _fieldsByName = new Dictionary<string, Model.Entities.Field>(StringComparer.OrdinalIgnoreCase);

                foreach (var field in fields.Where(f => !string.IsNullOrWhiteSpace(f.Name)))
                {
                    _fieldsByName.TryAdd(field.Name.Trim(), field);
                }

                _fieldIdByElementId = (form.Tabs ?? [])
                    .SelectMany(t => Flatten(t.Elements))
                    .OfType<FormFieldRefElement>()
                    .ToDictionary(e => e.Id, e => e.FieldId);
            }

            /// <summary>
            /// Resolves the id of the field a node stands for.
            /// </summary>
            /// <param name="node">The field node.</param>
            /// <returns>The field id.</returns>
            /// <exception cref="InvalidOperationException">The node names no field of the class.</exception>
            public Guid Resolve(RestApiFormEditorFieldItem node)
            {
                if (Guid.TryParse(node.Id, out var id))
                {
                    if (_fieldIdByElementId.TryGetValue(id, out var referenced) && _fieldsById.ContainsKey(referenced))
                    {
                        return referenced;
                    }

                    if (_fieldsById.ContainsKey(id))
                    {
                        return id;
                    }
                }

                var label = node.Label?.Trim();

                if (!string.IsNullOrEmpty(label) && _fieldsByName.TryGetValue(label, out var field))
                {
                    return field.Id;
                }

                throw new InvalidOperationException
                (
                    $"Field '{label ?? node.Id}' is not defined on class '{_className}'."
                );
            }

            /// <summary>
            /// Enumerates a subtree of elements, parents before children.
            /// </summary>
            /// <param name="elements">The elements to walk.</param>
            /// <returns>Every element of the subtree.</returns>
            private static IEnumerable<FormElement> Flatten(IEnumerable<FormElement> elements)
            {
                foreach (var element in elements ?? [])
                {
                    yield return element;

                    if (element is FormGroupElement group)
                    {
                        foreach (var child in Flatten(group.Children))
                        {
                            yield return child;
                        }
                    }
                }
            }
        }
    }
}
