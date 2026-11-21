using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebIndex.Queries;

using TemplateHierarchyEntity = KleeneStar.Model.Entities.TemplateHierarchy;

namespace KleeneStar.Core.WWW.Api._1_.TemplateHierarchies
{
    /// <summary>
    /// Provides CRUD operations for template hierarchies via a REST API.
    /// </summary>
    /// <remarks>
    /// A hierarchy is administered with the class of its root template (<see cref="ClassOf"/>),
    /// like the template itself. The payload names the root as <see cref="RootField"/> (a
    /// template id, followed by <c>@{class id}</c> for a class the template serves beside its
    /// own) and the nodes as <see cref="TemplateHierarchyNodeList.Field"/>; the attributes
    /// <c>Name</c>, <c>Description</c>, <c>Category</c> and <c>State</c> are bound by name. The
    /// hierarchy belongs to the workspace of its root template.
    /// </remarks>
    [Cache]
    public sealed class Index : RestApiCrudClassStructure<TemplateHierarchyEntity>
    {
        /// <summary>
        /// The name of the payload field naming the root template.
        /// </summary>
        public const string RootField = "Root";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Returns the class a hierarchy belongs to, which decides who may change it: the class
        /// of its root template.
        /// </summary>
        /// <param name="item">The hierarchy.</param>
        /// <returns>The class id.</returns>
        protected override Guid? ClassOf(TemplateHierarchyEntity item)
        {
            return item is null
                ? null
                : item.RootTemplate?.ClassId ?? CoreHub.TemplateManager.GetTemplate(item.RootTemplateId)?.ClassId;
        }

        /// <summary>
        /// Determines whether the caller may create a hierarchy: they must administer the class
        /// of the root template the payload names. The load of an empty dialog names no root
        /// yet and is judged like a template's; the create without a root is refused by the
        /// validation.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>True when the hierarchy may be created.</returns>
        protected override bool MayCreate(IRequest request)
        {
            if (!TemplateChoice.TryParse(ContentAuthorization.ReadPayloadText(request, RootField), out var templateId))
            {
                return base.MayCreate(request);
            }

            return CoreHub.TemplateManager.GetTemplate(templateId) is { } template
                && ContentAuthorization.MayAdminister(template.ClassId, request);
        }

        /// <summary>
        /// Creates the query context.
        /// </summary>
        /// <returns>The context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Retrieves the hierarchies matching a query.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="context">The context.</param>
        /// <param name="request">The request.</param>
        /// <returns>The hierarchies.</returns>
        protected override IEnumerable<TemplateHierarchyEntity> Retrieve(IQuery<TemplateHierarchyEntity> query, IQueryContext context, IRequest request)
        {
            return CoreHub.TemplateHierarchyManager.GetHierarchies(query, context);
        }

        /// <summary>
        /// Retrieves a hierarchy for an update.
        /// </summary>
        /// <param name="query">The query naming the hierarchy.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result carrying the hierarchy.</returns>
        protected override IRestApiCrudResultRetrieve RetrieveForUpdate(IQuery<TemplateHierarchyEntity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();

            return RetrieveForUpdate(request, CoreHub.TemplateHierarchyManager.GetHierarchies(query, context).FirstOrDefault());
        }

        /// <summary>
        /// Retrieves a hierarchy for a delete.
        /// </summary>
        /// <param name="query">The query naming the hierarchy.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result carrying the hierarchy.</returns>
        protected override IRestApiCrudResultRetrieveDelete RetrieveForDelete(IQuery<TemplateHierarchyEntity> query, IRequest request)
        {
            using var context = ModelHub.CreateDbContext();
            var data = CoreHub.TemplateHierarchyManager.GetHierarchies(query, context).FirstOrDefault();

            return RetrieveForDelete(request, data, data?.Name);
        }

        /// <summary>
        /// Validates a create or an update.
        /// </summary>
        /// <param name="existingItem">The stored hierarchy, or null for a create.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The validation result.</returns>
        protected override IRestApiValidationResult Validate(TemplateHierarchyEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var result = base.Validate(existingItem, payload, request);
            var errors = new List<RestApiError>
            {
                ValidateName(existingItem, payload, request),
                ValidateRoot(existingItem, payload, request),
                ValidateCategories(payload, request),
                ValidateNodes(existingItem, payload, request)
            };

            errors.RemoveAll(x => x is null);

            if (errors.Count == 0)
            {
                return result;
            }

            return new RestApiValidationResult()
                .Add([.. result?.Errors ?? []])
                .Add([.. errors]);
        }

        /// <summary>
        /// Creates a hierarchy.
        /// </summary>
        /// <param name="fieldMap">The payload.</param>
        /// <param name="request">The request.</param>
        /// <param name="newItem">The created hierarchy.</param>
        /// <returns>The result.</returns>
        protected override IRestApiCrudResultCreate Create(RestApiCrudFormData fieldMap, IRequest request, out TemplateHierarchyEntity newItem)
        {
            var id = Guid.NewGuid();
            newItem = new TemplateHierarchyEntity(id)
            {
                Icon = CoreHub.GenerateIcon(id),
                State = TemplateState.Active
            };

            fieldMap.BindTo(newItem);
            newItem.Category = TemplateCategories.Normalize(newItem.Category);
            ApplyRoot(newItem, fieldMap);

            CoreHub.TemplateHierarchyManager.AddHierarchy(newItem);
            WriteNodes(newItem.Id, fieldMap, null);

            return new RestApiCrudResultCreate();
        }

        /// <summary>
        /// Updates a hierarchy.
        /// </summary>
        /// <param name="existingItem">The stored hierarchy.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result.</returns>
        protected override IRestApiCrudResultUpdate Update(TemplateHierarchyEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var stored = CoreHub.TemplateHierarchyManager.GetNodes(existingItem.Id);
            var res = base.Update(existingItem, payload, request);

            existingItem.Category = TemplateCategories.Normalize(existingItem.Category);
            existingItem.Icon ??= CoreHub.GenerateIcon(existingItem.Id);
            ApplyRoot(existingItem, payload);

            CoreHub.TemplateHierarchyManager.UpdateHierarchy(existingItem);
            WriteNodes(existingItem.Id, payload, stored);

            return res;
        }

        /// <summary>
        /// Deletes a hierarchy with its nodes; the templates it references stay.
        /// </summary>
        /// <param name="existingItem">The stored hierarchy.</param>
        /// <param name="request">The request.</param>
        /// <returns>The result.</returns>
        protected override IRestApiCrudResultDelete Delete(TemplateHierarchyEntity existingItem, IRequest request)
        {
            CoreHub.TemplateHierarchyManager.RemoveHierarchy(existingItem);

            return base.Delete(existingItem, request);
        }

        /// <summary>
        /// Writes the root the payload names onto a hierarchy: the template, the class where it is
        /// one the template serves beside its own, and the workspace of the template's class.
        /// </summary>
        /// <param name="hierarchy">The hierarchy.</param>
        /// <param name="payload">The payload.</param>
        private static void ApplyRoot(TemplateHierarchyEntity hierarchy, RestApiCrudFormData payload)
        {
            if (!payload.TryGetValue(RootField, out var value)
                || !TemplateChoice.TryParse(Text(value), out var templateId, out var classId)
                || CoreHub.TemplateManager.GetTemplate(templateId) is not { } template)
            {
                return;
            }

            hierarchy.RootTemplateId = template.Id;
            hierarchy.RootClassId = classId is Guid c && c != template.ClassId ? c : null;
            hierarchy.WorkspaceId = (template.Class ?? CoreHub.ClassManager.GetClass(template.ClassId))?.WorkspaceId ?? hierarchy.WorkspaceId;
        }

        /// <summary>
        /// Writes the nodes a payload carries.
        /// </summary>
        /// <param name="hierarchyId">The hierarchy.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="stored">The stored nodes, or null for a new hierarchy.</param>
        private static void WriteNodes(Guid hierarchyId, RestApiCrudFormData payload, IReadOnlyList<TemplateHierarchyNode> stored)
        {
            if (payload.TryGetValue(TemplateHierarchyNodeList.Field, out var value)
                && TemplateHierarchyNodeList.TryParse(value, out var rows))
            {
                CoreHub.TemplateHierarchyManager.SetNodes(hierarchyId, TemplateHierarchyNodeList.ToNodes(rows, stored));
            }
        }

        /// <summary>
        /// Validates the name: required on a create, at most 64 characters and unique within the
        /// workspace of the hierarchy.
        /// </summary>
        /// <param name="existingItem">The stored hierarchy, or null.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The error, or null.</returns>
        private static RestApiError ValidateName(TemplateHierarchyEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var field = nameof(TemplateHierarchyEntity.Name);
            var sent = payload.TryGetValue(field, out var value);
            var name = sent ? Text(value)?.Trim() : existingItem?.Name;

            if (!sent && existingItem is not null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.hierarchy.name.required"), field: field);
            }

            if (name.Length > 64)
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.hierarchy.name.length", 64), field: field);
            }

            var workspaceId = RootWorkspace(payload) ?? existingItem?.WorkspaceId;
            var taken = workspaceId is Guid ws && CoreHub.TemplateHierarchyManager
                .GetHierarchies(new Query<TemplateHierarchyEntity>().Where(x => x.WorkspaceId == ws))
                .Any(x => x.Id != existingItem?.Id && string.Equals(x.Name, name, StringComparison.CurrentCultureIgnoreCase));

            return taken
                ? new RestApiError(I18N.Translate(request, "kleenestar.core:template.hierarchy.name.taken"), field: field)
                : null;
        }

        /// <summary>
        /// Validates the root: required on a create; an existing template the caller administers,
        /// in the workspace of the hierarchy, with a class the template serves.
        /// </summary>
        /// <param name="existingItem">The stored hierarchy, or null.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The error, or null.</returns>
        private static RestApiError ValidateRoot(TemplateHierarchyEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            if (!payload.TryGetValue(RootField, out var value))
            {
                return existingItem is null
                    ? new RestApiError(I18N.Translate(request, "kleenestar.core:template.hierarchy.root.required"), field: RootField)
                    : null;
            }

            if (!TemplateChoice.TryParse(Text(value), out var templateId, out var classId)
                || CoreHub.TemplateManager.GetTemplate(templateId) is not { } template)
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.hierarchy.root.required"), field: RootField);
            }

            if (classId is Guid c && !CoreHub.TemplateManager.GetClassIds(template.Id).Contains(c))
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.children.class.unknown", template.Name), field: RootField);
            }

            var workspaceId = (template.Class ?? CoreHub.ClassManager.GetClass(template.ClassId))?.WorkspaceId;

            if (existingItem is not null && workspaceId != existingItem.WorkspaceId)
            {
                return new RestApiError(I18N.Translate(request, "kleenestar.core:template.hierarchy.root.workspace"), field: RootField);
            }

            return ContentAuthorization.MayAdminister(template.ClassId, request)
                ? null
                : new RestApiError(I18N.Translate(request, "kleenestar.core:template.classes.forbidden"), field: RootField);
        }

        /// <summary>
        /// Validates the categories: stored separated by semicolons, they have to fit the column.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The error, or null.</returns>
        private static RestApiError ValidateCategories(RestApiCrudFormData payload, IRequest request)
        {
            var field = nameof(TemplateHierarchyEntity.Category);

            if (!payload.TryGetValue(field, out var value)
                || (TemplateCategories.Normalize(value?.ToString())?.Length ?? 0) <= TemplateCategories.MaxLength)
            {
                return null;
            }

            return new RestApiError(I18N.Translate(request, "kleenestar.core:template.category.length", TemplateCategories.MaxLength), field: field);
        }

        /// <summary>
        /// Validates the nodes of the payload (<see cref="TemplateHierarchyNodeList"/>).
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///   <item>Every row references an existing template and names a parent that comes
        ///   earlier in the list, if any.</item>
        ///   <item>A referenced template serving several classes needs the class its object is
        ///   created in, and a named class must be one the template serves.</item>
        ///   <item>A hierarchy may reach into other workspaces (the onboarding of HR creates the
        ///   account request at the IT desk), but only with the consent of both sides: whoever
        ///   takes a template of another workspace into the hierarchy must administer the class
        ///   its object is created in, because the objects are created on the hierarchy's
        ///   authority, not on the filer's.</item>
        ///   <item>Every row is a sub-object, linked by a relation type of the catalog, or both.</item>
        ///   <item>An assignee is an active account; a row waits for a sibling, and the waiting
        ///   does not run in a circle.</item>
        /// </list>
        /// </remarks>
        /// <param name="existingItem">The stored hierarchy, or null.</param>
        /// <param name="payload">The payload.</param>
        /// <param name="request">The request.</param>
        /// <returns>The error, or null when the nodes are valid or not sent.</returns>
        private static RestApiError ValidateNodes(TemplateHierarchyEntity existingItem, RestApiCrudFormData payload, IRequest request)
        {
            var field = TemplateHierarchyNodeList.Field;

            if (!payload.TryGetValue(field, out var value))
            {
                return null;
            }

            RestApiError Error(string key, params object[] args) => new(I18N.Translate(request, key, args), field: field);

            if (!TemplateHierarchyNodeList.TryParse(value, out var rows))
            {
                return Error("kleenestar.core:template.children.invalid");
            }

            var workspaceId = RootWorkspace(payload) ?? existingItem?.WorkspaceId;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                var template = row.Template == Guid.Empty ? null : CoreHub.TemplateManager.GetTemplate(row.Template);

                if (template is null)
                {
                    return Error("kleenestar.core:template.children.unknown");
                }

                if (!string.IsNullOrWhiteSpace(row.Parent) && !seen.Contains(row.Parent))
                {
                    return Error("kleenestar.core:template.hierarchy.parent", template.Name);
                }

                seen.Add(row.Id);
                parents[row.Id] = row.Parent ?? string.Empty;

                var served = CoreHub.TemplateManager.GetClassIds(template.Id);
                var classId = row.Class is Guid chosen && chosen != Guid.Empty ? chosen : (Guid?)null;

                if (classId is null && served.Count > 1)
                {
                    return Error("kleenestar.core:template.children.class.required", template.Name);
                }

                if (classId is Guid c && !served.Contains(c))
                {
                    return Error("kleenestar.core:template.children.class.unknown", template.Name);
                }

                var targetClassId = classId ?? template.ClassId;

                if (workspaceId is not null
                    && CoreHub.ClassManager.GetClass(targetClassId)?.WorkspaceId != workspaceId
                    && !ContentAuthorization.MayAdminister(targetClassId, request))
                {
                    return Error("kleenestar.core:template.children.workspace", template.Name);
                }

                var (linkKey, inverse) = TemplateHierarchyNodeList.ParseLink(row.Link);

                if (linkKey is not null && !TemplateHierarchyNodeList.IsLinkable(linkKey, inverse))
                {
                    return Error("kleenestar.core:template.children.link.unknown", template.Name);
                }

                if (row.Child == false && linkKey is null)
                {
                    return Error("kleenestar.core:template.children.link.required", template.Name);
                }

                if (row.Assignee is Guid assigneeId
                    && assigneeId != Guid.Empty
                    && CoreHub.IdentityManager.GetIdentity(assigneeId) is not { State: IdentityState.Active })
                {
                    return Error("kleenestar.core:template.children.assignee", template.Name);
                }
            }

            foreach (var row in rows.Where(x => !string.IsNullOrWhiteSpace(x.WaitsFor)))
            {
                if (string.Equals(row.WaitsFor, row.Id, StringComparison.OrdinalIgnoreCase)
                    || !parents.TryGetValue(row.WaitsFor, out var waitedParent)
                    || !string.Equals(waitedParent, parents[row.Id], StringComparison.OrdinalIgnoreCase))
                {
                    return Error("kleenestar.core:template.children.waitsfor", CoreHub.TemplateManager.GetTemplate(row.Template)?.Name);
                }
            }

            if (TemplateHierarchyNodeList.HasWaitingCycle(rows))
            {
                return Error("kleenestar.core:template.children.waitsfor.cycle");
            }

            return null;
        }

        /// <summary>
        /// Returns the workspace of the root template a payload names.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <returns>The workspace id, or null.</returns>
        private static Guid? RootWorkspace(RestApiCrudFormData payload)
        {
            return payload.TryGetValue(RootField, out var value)
                && TemplateChoice.TryParse(Text(value), out var templateId)
                && CoreHub.TemplateManager.GetTemplate(templateId) is { } template
                    ? (template.Class ?? CoreHub.ClassManager.GetClass(template.ClassId))?.WorkspaceId
                    : null;
        }

        /// <summary>
        /// Reads a payload value as text.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The text, or null.</returns>
        private static string Text(object value)
        {
            return value switch
            {
                null => null,
                string s => s,
                System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } e => e.GetString(),
                System.Text.Json.JsonElement e => e.ToString(),
                _ => value.ToString()
            };
        }
    }
}
