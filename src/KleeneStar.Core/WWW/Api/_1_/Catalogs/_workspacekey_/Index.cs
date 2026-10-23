using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Core.WWW.Api._1_.Automation;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WWW.Api._1_.Catalogs._workspacekey_
{
    /// <summary>
    /// The value catalogs a workspace uses: <c>GET /api/1/catalogs/{workspacekey}</c> lists its
    /// own and the installation-wide ones; <c>POST</c> adds or changes one, and with an
    /// <c>action</c> works on one: <c>publish</c> (static values, one per line, <c>value|label</c>),
    /// <c>refresh</c>, <c>restore</c> (<c>version</c>), <c>bind</c> (<c>field</c>, <c>version</c>
    /// to pin), <c>unbind</c> (<c>field</c>), <c>delete</c>.
    /// </summary>
    /// <remarks>
    /// A workspace's catalogs are workspace administration; an installation-wide catalog
    /// (<c>global=true</c>) is changed by the installation's administrators only, and bound to
    /// a field by the field's workspace administrators.
    /// </remarks>
    [Title("kleenestar.core:catalog.api.title")]
    [WorkspaceKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Handles <c>GET</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The catalogs.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            return AutomationApi.Json(CoreHub.ValueCatalogManager.GetCatalogs(workspace.Id).Select(x => new
            {
                id = x.Id,
                name = x.Name,
                description = x.Description,
                global = x.WorkspaceId is null,
                source = x.Source.ToString().ToLowerInvariant(),
                query = x.Query,
                valueAttribute = x.ValueAttribute,
                url = x.Url,
                refreshMinutes = x.RefreshMinutes,
                state = x.State.ToString().ToLowerInvariant(),
                version = x.CurrentVersion,
                lastRefresh = x.LastRefresh,
                lastError = x.LastError,
                values = CoreHub.ValueCatalogManager.GetValues(x.Id).Select(v => new { value = v.Value, label = v.Label }),
                bindings = CoreHub.ValueCatalogManager.GetBindings(x.Id).Select(b => new { fieldId = b.FieldId, pinnedVersion = b.PinnedVersion })
            }));
        }

        /// <summary>
        /// Handles <c>POST</c>.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The catalog, or why the request was refused.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Save(IRequest request)
        {
            var refusal = AutomationApi.Authorize(request, out var workspace);

            if (refusal is not null)
            {
                return refusal;
            }

            var payload = AutomationApi.Read<CatalogPayload>(request);

            if (payload is null)
            {
                return AutomationApi.Refuse("The request body must describe a catalog.");
            }

            var callerId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var id = AutomationApi.ParseId(payload.Id);
            var catalog = id is { } catalogId ? CoreHub.ValueCatalogManager.GetCatalog(catalogId) : null;

            if (id is not null && (catalog is null || (catalog.WorkspaceId is not null && catalog.WorkspaceId != workspace.Id)))
            {
                return new ResponseNotFound();
            }

            var action = payload.Action?.Trim().ToLowerInvariant();

            // binding works on a field of this workspace and only reads the catalog
            if (action is "bind" or "unbind")
            {
                return Bind(payload, catalog, workspace.Id, action == "unbind");
            }

            var global = catalog is null ? string.Equals(payload.Global, "true", StringComparison.OrdinalIgnoreCase) : catalog.WorkspaceId is null;

            if (global && !AccountAuthorization.IsAdministrator(request))
            {
                return new ResponseForbidden();
            }

            try
            {
                switch (action)
                {
                    case "publish":
                        return catalog is null ? new ResponseNotFound() : Version(CoreHub.ValueCatalogManager.Publish(catalog.Id, ParseValues(payload.Values), "typed", callerId), catalog);
                    case "refresh":
                        return catalog is null ? new ResponseNotFound() : Version(CoreHub.ValueCatalogManager.Refresh(catalog.Id), catalog);
                    case "restore":
                        return catalog is null || !int.TryParse(payload.Version, out var number)
                            ? new ResponseNotFound()
                            : Version(CoreHub.ValueCatalogManager.Restore(catalog.Id, number, callerId), catalog);
                    case "delete":
                        if (catalog is null)
                        {
                            return new ResponseNotFound();
                        }

                        CoreHub.ValueCatalogManager.RemoveCatalog(catalog.Id);

                        return new ResponseNoContent();
                }

                catalog ??= new ValueCatalog { WorkspaceId = global ? null : workspace.Id, CreatedById = callerId == Guid.Empty ? null : callerId };
                catalog.Name = payload.Name;
                catalog.Description = string.IsNullOrWhiteSpace(payload.Description) ? null : payload.Description.Trim();
                catalog.Source = Enum.TryParse<ValueCatalogSource>(payload.Source, true, out var source) ? source : ValueCatalogSource.Static;
                catalog.Query = string.IsNullOrWhiteSpace(payload.Query) ? null : payload.Query.Trim();
                catalog.ValueAttribute = string.IsNullOrWhiteSpace(payload.ValueAttribute) ? "summary" : payload.ValueAttribute.Trim().ToLowerInvariant();
                catalog.Url = string.IsNullOrWhiteSpace(payload.Url) ? null : payload.Url.Trim();
                catalog.RefreshMinutes = int.TryParse(payload.RefreshMinutes, out var minutes) ? minutes : null;
                catalog.State = string.Equals(payload.State, "disabled", StringComparison.OrdinalIgnoreCase) ? ValueCatalogState.Disabled : ValueCatalogState.Active;

                var problems = CoreHub.ValueCatalogManager.Validate(catalog);

                if (problems.Count > 0)
                {
                    return AutomationApi.Refuse(string.Join(" ", problems));
                }

                CoreHub.ValueCatalogManager.SaveCatalog(catalog);

                // a new source is read at once, so the catalog is not empty until the scheduler comes by
                if (catalog.Source != ValueCatalogSource.Static && catalog.CurrentVersion == 0)
                {
                    CoreHub.ValueCatalogManager.Refresh(catalog.Id);
                }

                return AutomationApi.Json(new { id = catalog.Id, name = catalog.Name });
            }
            catch (ArgumentException ex)
            {
                return AutomationApi.Refuse(ex.Message);
            }
        }

        /// <summary>
        /// Binds a field of the workspace to a catalog it sees, or releases it.
        /// </summary>
        /// <param name="payload">The body.</param>
        /// <param name="catalog">The catalog.</param>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="release">Whether to release the field.</param>
        /// <returns>The response.</returns>
        private static IResponse Bind(CatalogPayload payload, ValueCatalog catalog, Guid workspaceId, bool release)
        {
            var field = Guid.TryParse(payload.Field, out var fieldId) ? CoreHub.FieldManager.GetField(fieldId) : null;

            if (field is null || CoreHub.ClassManager.GetClass(field.ClassId)?.WorkspaceId != workspaceId)
            {
                return new ResponseNotFound();
            }

            if (release)
            {
                CoreHub.ValueCatalogManager.Unbind(field.Id);

                return new ResponseNoContent();
            }

            if (catalog is null)
            {
                return new ResponseNotFound();
            }

            try
            {
                CoreHub.ValueCatalogManager.Bind(field.Id, catalog.Id, int.TryParse(payload.Version, out var pinned) ? pinned : null);

                return new ResponseNoContent();
            }
            catch (ArgumentException ex)
            {
                return AutomationApi.Refuse(ex.Message);
            }
        }

        /// <summary>
        /// Answers with a version, or with the current one when nothing changed.
        /// </summary>
        /// <param name="version">The new version, or <see langword="null"/>.</param>
        /// <param name="catalog">The catalog.</param>
        /// <returns>The response.</returns>
        private static IResponse Version(ValueCatalogVersion version, ValueCatalog catalog)
        {
            var stored = CoreHub.ValueCatalogManager.GetCatalog(catalog.Id);

            return AutomationApi.Json(new
            {
                changed = version is not null,
                version = stored?.CurrentVersion,
                added = version?.Added ?? 0,
                removed = version?.Removed ?? 0,
                lastError = stored?.LastError
            });
        }

        /// <summary>
        /// Reads typed values: one per line, <c>value|label</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The values.</returns>
        internal static IEnumerable<CatalogValue> ParseValues(string text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Split('|', 2))
                .Select(parts => new CatalogValue(parts[0].Trim(), parts.Length > 1 ? parts[1].Trim() : null));
        }

        /// <summary>
        /// The body of a catalog request; every value is a string, as a page's form sends it.
        /// </summary>
        internal sealed class CatalogPayload
        {
            /// <summary>Gets or sets the catalog, empty for a new one.</summary>
            public string Id { get; set; }

            /// <summary>Gets or sets what to do: publish, refresh, restore, bind, unbind, delete, or empty to save.</summary>
            public string Action { get; set; }

            /// <summary>Gets or sets the name.</summary>
            public string Name { get; set; }

            /// <summary>Gets or sets the description.</summary>
            public string Description { get; set; }

            /// <summary>Gets or sets whether a new catalog is installation-wide (<c>true</c>).</summary>
            public string Global { get; set; }

            /// <summary>Gets or sets the source: static, query or external.</summary>
            public string Source { get; set; }

            /// <summary>Gets or sets the WQL query.</summary>
            public string Query { get; set; }

            /// <summary>Gets or sets the listed attribute: summary or key.</summary>
            public string ValueAttribute { get; set; }

            /// <summary>Gets or sets the external address.</summary>
            public string Url { get; set; }

            /// <summary>Gets or sets the refresh interval in minutes.</summary>
            public string RefreshMinutes { get; set; }

            /// <summary>Gets or sets the state: active or disabled.</summary>
            public string State { get; set; }

            /// <summary>Gets or sets the typed values, one per line.</summary>
            public string Values { get; set; }

            /// <summary>Gets or sets a version number.</summary>
            public string Version { get; set; }

            /// <summary>Gets or sets a field id.</summary>
            public string Field { get; set; }
        }
    }
}
