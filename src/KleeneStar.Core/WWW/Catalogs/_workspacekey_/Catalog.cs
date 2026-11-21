using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Catalogs._workspacekey_
{
    /// <summary>
    /// A value catalog (<c>?catalog=&lt;id&gt;</c>, none for a new one): its settings, its values,
    /// its versions with the way back to an earlier one, and the fields that draw from it.
    /// </summary>
    /// <remarks>
    /// Every form posts to <c>/api/1/catalogs/{workspacekey}</c> with its <c>action</c>. An
    /// installation-wide catalog shows its settings and values to everyone here but changes
    /// them only for the installation's administrators; binding a field stays workspace
    /// administration.
    /// </remarks>
    [WebIcon<IconList>]
    [Title("kleenestar.core:catalog.title")]
    [Scope<IScopeGeneral>]
    public sealed class Catalog : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            string Translate(string key) => I18N.Translate(renderContext, key);

            var keyParameter = renderContext.Request.GetParameter<WorkspaceKeyParameter>();
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(keyParameter?.Value);
            var catalog = Guid.TryParse(renderContext.Request.GetParameter("catalog")?.Value, out var catalogId)
                ? CoreHub.ValueCatalogManager.GetCatalog(catalogId)
                : null;

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:catalog.title";

            if (workspace is null || (catalog is not null && catalog.WorkspaceId is not null && catalog.WorkspaceId != workspace.Id))
            {
                return;
            }

            var culture = renderContext.Request.Culture ?? CultureInfo.InvariantCulture;
            var api = CoreHub.GetUri<Api._1_.Catalogs._workspacekey_.Index>()?.BindParameters(keyParameter);
            var self = catalog is null ? null : CoreHub.GetUri<Catalog>()?.BindParameters(keyParameter)?.Add(new UriQuery("catalog", catalog.Id.ToString()));
            var editable = catalog?.WorkspaceId is not null || catalog is null || AccountAuthorization.IsAdministrator(renderContext.Request);

            ControlModalForm Form(string id, string action, string submit, IUri redirect = null)
            {
                var form = new ControlModalForm(id) { Uri = _ => api, RedirectUri = _ => redirect ?? self, Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.None) };

                form.Add(new PresetHiddenInput(id + "-id") { Name = _ => "id", Value = _ => catalog?.Id.ToString() ?? string.Empty });
                form.Add(new PresetHiddenInput(id + "-action") { Name = _ => "action", Value = _ => action });
                form.AddPrimaryButton(new ControlFormItemButtonSubmit() { Text = _ => submit });

                return form;
            }

            void Heading(string key)
            {
                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => key, Format = _ => TypeFormatText.H5, Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Four, PropertySpacing.Space.Two) });
            }

            if (catalog is not null)
            {
                var values = CoreHub.ValueCatalogManager.GetValues(catalog.Id);
                var summary = string.Format(Translate("kleenestar.core:catalog.summary"), catalog.CurrentVersion, values.Count, Translate("kleenestar.core:catalog.source." + catalog.Source.ToString().ToLowerInvariant()));
                var preview = string.Join(" · ", values.Take(40).Select(x => x.Label is null ? x.Value : $"{x.Value} ({x.Label})")) + (values.Count > 40 ? " …" : string.Empty);

                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => summary });
                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => preview, TextColor = _ => new PropertyColorText(TypeColorText.Secondary), Format = _ => TypeFormatText.Small });

                if (catalog.LastError is { } error)
                {
                    visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => error, TextColor = _ => new PropertyColorText(TypeColorText.Danger) });
                }

                if (editable && catalog.Source == ValueCatalogSource.Static)
                {
                    var typed = new ControlFormItemInputText("catalog-values") { Name = _ => "values", Label = _ => "kleenestar.core:catalog.values", Format = _ => TypeEditTextFormat.Multiline, Rows = _ => 10, Help = _ => "kleenestar.core:catalog.values.help" };
                    var publish = Form("catalog-publish", "publish", "kleenestar.core:catalog.publish");
                    var text = string.Join("\n", values.Select(x => x.Label is null ? x.Value : $"{x.Value}|{x.Label}"));

                    publish.Add(typed);
                    publish.Initialize(e => e.Context.SetValue(typed, new ControlFormInputValueString(text)));
                    visualTree.Content.MainPanel.AddPrimary(publish);
                }
                else if (editable)
                {
                    visualTree.Content.MainPanel.AddPrimary(Form("catalog-refresh", "refresh", "kleenestar.core:catalog.refreshnow"));
                }
            }

            if (editable)
            {
                Heading("kleenestar.core:catalog.settings");

                var name = new ControlFormItemInputText("catalog-name") { Name = _ => "name", Label = _ => "kleenestar.core:catalog.name", Required = _ => true };
                var description = new ControlFormItemInputText("catalog-description") { Name = _ => "description", Label = _ => "kleenestar.core:catalog.description" };
                var source = new ControlFormItemInputCombo("catalog-source") { Name = _ => "source", Label = _ => "kleenestar.core:catalog.source" };
                var query = new ControlFormItemInputText("catalog-query") { Name = _ => "query", Label = _ => "kleenestar.core:catalog.query", Help = _ => "kleenestar.core:catalog.query.help" };
                var attribute = new ControlFormItemInputCombo("catalog-attribute") { Name = _ => "valueAttribute", Label = _ => "kleenestar.core:catalog.attribute" };
                var url = new ControlFormItemInputText("catalog-url") { Name = _ => "url", Label = _ => "kleenestar.core:catalog.url", Help = _ => "kleenestar.core:catalog.url.help" };
                var refresh = new ControlFormItemInputText("catalog-refreshminutes") { Name = _ => "refreshMinutes", Label = _ => "kleenestar.core:catalog.refreshminutes" };
                var state = new ControlFormItemInputCombo("catalog-state") { Name = _ => "state", Label = _ => "kleenestar.core:catalog.state" };

                foreach (var value in new[] { "static", "query", "external" })
                {
                    var text = Translate("kleenestar.core:catalog.source." + value);

                    source.Add(new ControlFormItemInputComboItem() { Value = _ => value, Text = _ => text });
                }

                attribute.Add
                (
                    new ControlFormItemInputComboItem() { Value = _ => "summary", Text = _ => Translate("kleenestar.core:catalog.attribute.summary") },
                    new ControlFormItemInputComboItem() { Value = _ => "key", Text = _ => Translate("kleenestar.core:catalog.attribute.key") }
                );
                state.Add
                (
                    new ControlFormItemInputComboItem() { Value = _ => "active", Text = _ => Translate("kleenestar.core:catalog.state.active") },
                    new ControlFormItemInputComboItem() { Value = _ => "disabled", Text = _ => Translate("kleenestar.core:catalog.state.disabled") }
                );

                var settings = Form("catalog-settings", string.Empty, "kleenestar.core:catalog.save", catalog is null ? CoreHub.GetUri<Index>()?.BindParameters(keyParameter) : null);

                settings.Add(name, description, source, query, attribute, url, refresh, state);

                if (catalog is null && AccountAuthorization.IsAdministrator(renderContext.Request))
                {
                    var global = new ControlFormItemInputCombo("catalog-global") { Name = _ => "global", Label = _ => "kleenestar.core:catalog.scope" };

                    global.Add
                    (
                        new ControlFormItemInputComboItem() { Value = _ => "false", Text = _ => Translate("kleenestar.core:catalog.scope.workspace") },
                        new ControlFormItemInputComboItem() { Value = _ => "true", Text = _ => Translate("kleenestar.core:catalog.scope.global") }
                    );
                    settings.Add(global);
                }

                if (catalog is not null)
                {
                    settings.Initialize(e =>
                    {
                        e.Context.SetValue(name, new ControlFormInputValueString(catalog.Name));
                        e.Context.SetValue(description, new ControlFormInputValueString(catalog.Description));
                        e.Context.SetValue(source, new ControlFormInputValueString(catalog.Source.ToString().ToLowerInvariant()));
                        e.Context.SetValue(query, new ControlFormInputValueString(catalog.Query));
                        e.Context.SetValue(attribute, new ControlFormInputValueString(catalog.ValueAttribute ?? "summary"));
                        e.Context.SetValue(url, new ControlFormInputValueString(catalog.Url));
                        e.Context.SetValue(refresh, new ControlFormInputValueString(catalog.RefreshMinutes?.ToString(CultureInfo.InvariantCulture)));
                        e.Context.SetValue(state, new ControlFormInputValueString(catalog.State.ToString().ToLowerInvariant()));
                    });
                }

                visualTree.Content.MainPanel.AddPrimary(settings);
            }

            if (catalog is null)
            {
                return;
            }

            Heading("kleenestar.core:catalog.versions");

            var versions = CoreHub.ValueCatalogManager.GetVersions(catalog.Id);
            var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                .AddColumn(Translate("kleenestar.core:catalog.version"))
                .AddColumn(Translate("kleenestar.core:catalog.version.created"))
                .AddColumn(Translate("kleenestar.core:catalog.version.change"))
                .AddColumn(Translate("kleenestar.core:catalog.version.comment"));

            foreach (var version in versions.Take(50))
            {
                var number = "v" + version.Number;
                var created = version.Created.ToLocalTime().ToString("g", culture);
                var change = $"+{version.Added} / -{version.Removed}";
                var comment = version.Comment ?? string.Empty;

                table.AddRow
                (
                    new ControlTableCell() { Text = _ => number },
                    new ControlTableCell() { Text = _ => created },
                    new ControlTableCell() { Text = _ => change },
                    new ControlTableCell() { Text = _ => comment }
                );
            }

            visualTree.Content.MainPanel.AddPrimary(table);

            if (editable && versions.Count > 1)
            {
                var pick = new ControlFormItemInputCombo("catalog-restore-version") { Name = _ => "version", Label = _ => "kleenestar.core:catalog.restore" };

                foreach (var version in versions.Skip(1).Take(50))
                {
                    var value = version.Number.ToString(CultureInfo.InvariantCulture);
                    var text = $"v{version.Number} · {version.Created.ToLocalTime().ToString("g", culture)}";

                    pick.Add(new ControlFormItemInputComboItem() { Value = _ => value, Text = _ => text });
                }

                var restore = Form("catalog-restore", "restore", "kleenestar.core:catalog.restore.submit");

                restore.Add(pick);
                visualTree.Content.MainPanel.AddPrimary(restore);
            }

            if (!ContentAuthorization.MayAdministerWorkspace(workspace.Id, renderContext.Request))
            {
                return;
            }

            Heading("kleenestar.core:catalog.fields");

            var classes = CoreHub.ClassManager.GetClasses(new Query<Model.Entities.Class>().WhereEquals(x => x.WorkspaceId, workspace.Id)).ToDictionary(x => x.Id, x => x.Name);
            var bindable = classes.Keys
                .SelectMany(classId => CoreHub.FieldManager.GetFields(new ClassIdParameter(classId)))
                .Where(x => ValueCatalogManager.BindableTypes.Contains(x.FieldType) && x.State == FieldState.Active)
                .OrderBy(x => classes[x.ClassId]).ThenBy(x => x.Name)
                .ToList();
            var bound = CoreHub.ValueCatalogManager.GetBindings(catalog.Id).ToDictionary(x => x.FieldId);

            foreach (var field in bindable.Where(x => bound.ContainsKey(x.Id)))
            {
                var pinned = bound[field.Id].PinnedVersion;
                var text = $"{classes[field.ClassId]} · {field.Name} - " + (pinned is int v ? string.Format(Translate("kleenestar.core:catalog.pinned"), v) : Translate("kleenestar.core:catalog.follows"));

                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => text });
            }

            var fieldPick = new ControlFormItemInputCombo("catalog-bind-field") { Name = _ => "field", Label = _ => "kleenestar.core:catalog.field" };
            var pin = new ControlFormItemInputCombo("catalog-bind-version") { Name = _ => "version", Label = _ => "kleenestar.core:catalog.pin" };

            foreach (var field in bindable)
            {
                var value = field.Id.ToString();
                var text = $"{classes[field.ClassId]} · {field.Name}";

                fieldPick.Add(new ControlFormItemInputComboItem() { Value = _ => value, Text = _ => text });
            }

            pin.Add(new ControlFormItemInputComboItem() { Value = _ => string.Empty, Text = _ => Translate("kleenestar.core:catalog.follows") });

            foreach (var version in versions.Take(50))
            {
                var value = version.Number.ToString(CultureInfo.InvariantCulture);
                var text = string.Format(Translate("kleenestar.core:catalog.pinned"), version.Number);

                pin.Add(new ControlFormItemInputComboItem() { Value = _ => value, Text = _ => text });
            }

            var bind = Form("catalog-bind", "bind", "kleenestar.core:catalog.bind");

            bind.Add(fieldPick, pin);
            visualTree.Content.MainPanel.AddPrimary(bindable.Count == 0
                ? new ControlText() { Text = _ => "kleenestar.core:catalog.nofields", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) }
                : bind);

            if (bound.Count > 0)
            {
                var release = new ControlFormItemInputCombo("catalog-unbind-field") { Name = _ => "field", Label = _ => "kleenestar.core:catalog.field" };

                foreach (var field in bindable.Where(x => bound.ContainsKey(x.Id)))
                {
                    var value = field.Id.ToString();
                    var text = $"{classes[field.ClassId]} · {field.Name}";

                    release.Add(new ControlFormItemInputComboItem() { Value = _ => value, Text = _ => text });
                }

                var unbind = Form("catalog-unbind", "unbind", "kleenestar.core:catalog.unbind");

                unbind.Add(release);
                visualTree.Content.MainPanel.AddPrimary(unbind);
            }

            if (editable)
            {
                Heading("kleenestar.core:catalog.delete.title");
                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => "kleenestar.core:catalog.delete.description", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });
                visualTree.Content.MainPanel.AddPrimary(Form("catalog-delete", "delete", "webexpress.webapp:delete.label", CoreHub.GetUri<Index>()?.BindParameters(keyParameter)));
            }
        }
    }
}
