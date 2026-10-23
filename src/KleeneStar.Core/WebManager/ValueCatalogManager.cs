using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Central value catalogs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A version is published only when the values changed - a refresh that reads the same list
    /// writes nothing - and versions are numbered without gaps under a unique index, so two
    /// refreshes racing cannot fork the history. A source that cannot be read keeps the current
    /// version and records why (<see cref="ValueCatalog.LastError"/>): a list never empties
    /// because another system was down.
    /// </para>
    /// <para>
    /// The values reach a field by being written into its options, so forms, filters and
    /// renderers need not know catalogs. A value that leaves a catalog leaves the options; the
    /// objects that hold it keep it.
    /// </para>
    /// <para>
    /// A query catalog reads as its creator, so it lists only what they may see - the values of
    /// a catalog are shown to everyone who fills in a bound field.
    /// </para>
    /// </remarks>
    public sealed class ValueCatalogManager : IValueCatalogManager
    {
        /// <summary>
        /// The most values a catalog holds.
        /// </summary>
        public const int MaxValues = 5000;

        /// <summary>
        /// The field types that draw their options from a list.
        /// </summary>
        public static readonly IReadOnlySet<FieldType> BindableTypes = new HashSet<FieldType>
        {
            FieldType.Selection, FieldType.MultiSelection, FieldType.Choice, FieldType.Radio, FieldType.Tile
        };

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private HttpClient _client;
        private HttpMessageHandler _transport;

        /// <inheritdoc/>
        public event EventHandler<ValueCatalog> CatalogAdded;

        /// <inheritdoc/>
        public event EventHandler<ValueCatalog> CatalogUpdated;

        /// <inheritdoc/>
        public event EventHandler<ValueCatalog> CatalogRemoved;

        /// <inheritdoc/>
        public event EventHandler<ValueCatalogVersion> VersionPublished;

        /// <summary>
        /// Gets or sets the handler external catalogs are read through - replaced by a test, or by
        /// a host that routes them through a proxy.
        /// </summary>
        public HttpMessageHandler Transport
        {
            get => _transport;
            set
            {
                _transport = value;
                _client = null;
            }
        }

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The HTTP server context.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ValueCatalogManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ValueCatalog> GetCatalogs(Guid? workspaceId)
        {
            return ModelHub.GetValueCatalogs(workspaceId);
        }

        /// <inheritdoc/>
        public ValueCatalog GetCatalog(Guid catalogId)
        {
            return ModelHub.GetValueCatalog(catalogId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> Validate(ValueCatalog catalog)
        {
            var problems = new List<string>();

            if (catalog is null)
            {
                problems.Add("The catalog is missing.");
                return problems;
            }

            if (string.IsNullOrWhiteSpace(catalog.Name))
            {
                problems.Add("The catalog needs a name.");
            }
            else if (ModelHub.GetValueCatalogs(null).Any(x => x.Id != catalog.Id && x.WorkspaceId == catalog.WorkspaceId && string.Equals(x.Name, catalog.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add($"There already is a catalog named '{catalog.Name}'.");
            }

            switch (catalog.Source)
            {
                case ValueCatalogSource.Query:
                    if (string.IsNullOrWhiteSpace(catalog.Query) || !WqlFilter.TryValidate<ObjectEntity>(catalog.Query, out _))
                    {
                        problems.Add("A query catalog needs a valid WQL query.");
                    }

                    if (catalog.ValueAttribute is not ("summary" or "key"))
                    {
                        problems.Add("A query catalog lists the summary or the key of its objects.");
                    }

                    break;
                case ValueCatalogSource.External:
                    if (!Uri.TryCreate(catalog.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        problems.Add("An external catalog needs an absolute http or https address.");
                    }

                    break;
            }

            if (catalog.RefreshMinutes is < 5 or > 10080)
            {
                problems.Add("A catalog is refreshed every 5 minutes to every week.");
            }

            return problems;
        }

        /// <inheritdoc/>
        public IValueCatalogManager SaveCatalog(ValueCatalog catalog)
        {
            var problems = Validate(catalog);

            if (problems.Count > 0)
            {
                throw new ArgumentException(string.Join(" ", problems), nameof(catalog));
            }

            var existing = catalog.Id != Guid.Empty && ModelHub.GetValueCatalog(catalog.Id) is not null;

            catalog.Id = catalog.Id == Guid.Empty ? Guid.NewGuid() : catalog.Id;
            catalog.Name = catalog.Name.Trim();
            catalog.Created = catalog.Created == default ? DateTime.UtcNow : catalog.Created;
            catalog.Updated = DateTime.UtcNow;

            if (catalog.Source == ValueCatalogSource.Static)
            {
                catalog.Query = null;
                catalog.Url = null;
                catalog.RefreshMinutes = null;
            }

            ModelHub.SaveValueCatalog(catalog);
            (existing ? CatalogUpdated : CatalogAdded)?.Invoke(this, catalog);

            return this;
        }

        /// <inheritdoc/>
        public IValueCatalogManager RemoveCatalog(Guid catalogId)
        {
            var catalog = ModelHub.GetValueCatalog(catalogId);

            if (catalog is not null)
            {
                ModelHub.RemoveValueCatalog(catalogId);
                CatalogRemoved?.Invoke(this, catalog);
            }

            return this;
        }

        /// <inheritdoc/>
        public IReadOnlyList<CatalogValue> GetValues(Guid catalogId, int? version = null)
        {
            var catalog = ModelHub.GetValueCatalog(catalogId);
            var number = version ?? catalog?.CurrentVersion ?? 0;

            return number == 0 ? [] : Deserialize(ModelHub.GetValueCatalogVersion(catalogId, number)?.Values);
        }

        /// <inheritdoc/>
        public IReadOnlyList<ValueCatalogVersion> GetVersions(Guid catalogId)
        {
            return ModelHub.GetValueCatalogVersions(catalogId);
        }

        /// <inheritdoc/>
        public ValueCatalogVersion Publish(Guid catalogId, IEnumerable<CatalogValue> values, string comment, Guid identityId)
        {
            var next = Normalize(values);

            if (next.Count > MaxValues)
            {
                throw new ArgumentException($"A catalog holds at most {MaxValues} values.", nameof(values));
            }

            // a racing publish took the number; read the new state and try once more
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var catalog = ModelHub.GetValueCatalog(catalogId) ?? throw new ArgumentException("The catalog does not exist.", nameof(catalogId));
                var current = GetValues(catalogId);

                if (current.SequenceEqual(next))
                {
                    return null;
                }

                var before = current.Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
                var after = next.Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
                var version = new ValueCatalogVersion
                {
                    Id = Guid.NewGuid(),
                    CatalogId = catalogId,
                    Number = catalog.CurrentVersion + 1,
                    Values = JsonSerializer.Serialize(next, JsonOptions),
                    Added = after.Count(x => !before.Contains(x)),
                    Removed = before.Count(x => !after.Contains(x)),
                    Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                    CreatedById = identityId == Guid.Empty ? null : identityId,
                    Created = DateTime.UtcNow
                };

                if (!ModelHub.AddValueCatalogVersion(version))
                {
                    continue;
                }

                VersionPublished?.Invoke(this, version);

                foreach (var binding in ModelHub.GetFieldCatalogBindings(catalogId).Where(x => x.PinnedVersion is null))
                {
                    Apply(binding.FieldId, next);
                }

                return version;
            }

            throw new InvalidOperationException("The catalog is being changed by somebody else; try again.");
        }

        /// <inheritdoc/>
        public ValueCatalogVersion Restore(Guid catalogId, int number, Guid identityId)
        {
            var version = ModelHub.GetValueCatalogVersion(catalogId, number) ?? throw new ArgumentException("The version does not exist.", nameof(number));

            return Publish(catalogId, Deserialize(version.Values), $"restored version {number}", identityId);
        }

        /// <inheritdoc/>
        public ValueCatalogVersion Refresh(Guid catalogId)
        {
            var catalog = ModelHub.GetValueCatalog(catalogId);

            if (catalog is null || catalog.Source == ValueCatalogSource.Static)
            {
                return null;
            }

            IReadOnlyList<CatalogValue> values;

            try
            {
                values = catalog.Source == ValueCatalogSource.Query ? ReadQuery(catalog) : ReadExternal(catalog);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException or ArgumentException)
            {
                catalog.LastRefresh = DateTime.UtcNow;
                catalog.LastError = Truncate(ex.Message, 1024);
                ModelHub.SaveValueCatalog(catalog);

                return null;
            }

            var version = Publish(catalog.Id, values, "refreshed", Guid.Empty);
            var stored = ModelHub.GetValueCatalog(catalogId);

            stored.LastRefresh = DateTime.UtcNow;
            stored.LastError = null;
            ModelHub.SaveValueCatalog(stored);

            return version;
        }

        /// <inheritdoc/>
        public int RefreshDue(DateTime now)
        {
            var refreshed = 0;

            foreach (var catalog in ModelHub.GetValueCatalogs(null).Where(x => x.State == ValueCatalogState.Active && x.Source != ValueCatalogSource.Static && x.RefreshMinutes is not null))
            {
                if (catalog.LastRefresh is DateTime last && last.AddMinutes(catalog.RefreshMinutes.Value) > now)
                {
                    continue;
                }

                Refresh(catalog.Id);
                refreshed++;
            }

            return refreshed;
        }

        /// <inheritdoc/>
        public void Bind(Guid fieldId, Guid catalogId, int? pinnedVersion)
        {
            var field = CoreHub.FieldManager.GetField(fieldId) ?? throw new ArgumentException("The field does not exist.", nameof(fieldId));
            var catalog = ModelHub.GetValueCatalog(catalogId) ?? throw new ArgumentException("The catalog does not exist.", nameof(catalogId));

            if (!BindableTypes.Contains(field.FieldType))
            {
                throw new ArgumentException("Only a selection field draws its options from a catalog.", nameof(fieldId));
            }

            if (catalog.WorkspaceId is Guid workspaceId && CoreHub.ClassManager.GetClass(field.ClassId)?.WorkspaceId != workspaceId)
            {
                throw new ArgumentException("The catalog belongs to another workspace.", nameof(catalogId));
            }

            if (pinnedVersion is int pinned && ModelHub.GetValueCatalogVersion(catalogId, pinned) is null)
            {
                throw new ArgumentException("The catalog has no such version.", nameof(pinnedVersion));
            }

            ModelHub.SaveFieldCatalogBinding(new FieldCatalogBinding { Id = Guid.NewGuid(), FieldId = fieldId, CatalogId = catalogId, PinnedVersion = pinnedVersion });
            Apply(fieldId, GetValues(catalogId, pinnedVersion));
        }

        /// <inheritdoc/>
        public void Unbind(Guid fieldId)
        {
            ModelHub.RemoveFieldCatalogBinding(fieldId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<FieldCatalogBinding> GetBindings(Guid catalogId)
        {
            return ModelHub.GetFieldCatalogBindings(catalogId);
        }

        /// <summary>
        /// Reads the values of an external source: a JSON array of strings or of
        /// <c>{ value, label }</c>, an object carrying such an array as <c>values</c>, or text
        /// with one value per line (the first column of a CSV).
        /// </summary>
        /// <param name="body">The body as received.</param>
        /// <returns>The values.</returns>
        public static IReadOnlyList<CatalogValue> ParseExternal(byte[] body)
        {
            var text = Encoding.UTF8.GetString(body ?? []).Trim().TrimStart('﻿');

            if (text.StartsWith('[') || text.StartsWith('{'))
            {
                var root = JsonDocument.Parse(text).RootElement;

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("values", out var inner))
                {
                    root = inner;
                }

                if (root.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException("The document holds no list of values.");
                }

                return [.. root.EnumerateArray().Select(item => item.ValueKind switch
                {
                    JsonValueKind.String => new CatalogValue(item.GetString()),
                    JsonValueKind.Number => new CatalogValue(item.GetRawText()),
                    JsonValueKind.Object => new CatalogValue(Property(item, "value") ?? Property(item, "id") ?? Property(item, "name"), Property(item, "label") ?? Property(item, "name")),
                    _ => null
                }).Where(x => x is not null)];
            }

            return [.. text.Split('\n')
                .Select(line => line.TrimEnd('\r'))
                .Select(line => line.Split(line.Contains(';') ? ';' : ',')[0].Trim().Trim('"'))
                .Select(value => new CatalogValue(value))];
        }

        /// <summary>
        /// Reads the values of a query catalog: the summary or key of every object it selects,
        /// as its creator may see them.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <returns>The values.</returns>
        private static IReadOnlyList<CatalogValue> ReadQuery(ValueCatalog catalog)
        {
            using var identity = catalog.CreatedById is Guid creator ? CoreHub.SessionManager.BeginIdentity(creator) : null;

            var query = new Query<ObjectEntity>().Where(WqlFilter.Compile<ObjectEntity>(catalog.Query));

            if (catalog.WorkspaceId is Guid workspaceId)
            {
                query = query.WhereEquals(x => x.WorkspaceId, workspaceId);
            }

            return [.. CoreHub.ObjectManager.GetObjects(query)
                .Where(x => x.State == WorkspaceState.Active)
                .Select(x => catalog.ValueAttribute == "key" ? new CatalogValue(x.Key, $"{x.Key} · {x.Summary}") : new CatalogValue(x.Summary))
                .OrderBy(x => x.Value, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Reads the values of an external catalog.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <returns>The values.</returns>
        private IReadOnlyList<CatalogValue> ReadExternal(ValueCatalog catalog)
        {
            using var response = Client.Send(new HttpRequestMessage(HttpMethod.Get, catalog.Url));

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"The source answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            using var stream = response.Content.ReadAsStream();
            using var buffer = new System.IO.MemoryStream();

            stream.CopyTo(buffer);

            return ParseExternal(buffer.ToArray());
        }

        /// <summary>
        /// Writes values into a field's options.
        /// </summary>
        /// <param name="fieldId">The field.</param>
        /// <param name="values">The values.</param>
        private static void Apply(Guid fieldId, IReadOnlyList<CatalogValue> values)
        {
            var field = CoreHub.FieldManager.GetField(fieldId);

            if (field is null)
            {
                return;
            }

            var options = values.Select(x => x.Value).ToList();

            if (field.Options?.SequenceEqual(options) == true)
            {
                return;
            }

            field.Options = options;
            CoreHub.FieldManager.Update(field);
        }

        /// <summary>
        /// Trims the values, drops empty ones and repeats, and keeps the order.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>The values.</returns>
        private static List<CatalogValue> Normalize(IEnumerable<CatalogValue> values)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            return [.. (values ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x?.Value))
                .Select(x => new CatalogValue(x.Value.Trim(), string.IsNullOrWhiteSpace(x.Label) || x.Label.Trim() == x.Value.Trim() ? null : x.Label.Trim()))
                .Where(x => seen.Add(x.Value))];
        }

        /// <summary>
        /// Reads the values of a version.
        /// </summary>
        /// <param name="json">The stored values.</param>
        /// <returns>The values.</returns>
        private static List<CatalogValue> Deserialize(string json)
        {
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<CatalogValue>>(json, JsonOptions) ?? [];
        }

        /// <summary>
        /// Reads a string property, ignoring case.
        /// </summary>
        /// <param name="item">The object.</param>
        /// <param name="name">The property.</param>
        /// <returns>The value, or <see langword="null"/>.</returns>
        private static string Property(JsonElement item, string name)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
                }
            }

            return null;
        }

        /// <summary>
        /// Shortens a text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="length">The length.</param>
        /// <returns>The text.</returns>
        private static string Truncate(string text, int length)
        {
            return text is null || text.Length <= length ? text : text[..length];
        }

        /// <summary>
        /// Gets the client of the external sources.
        /// </summary>
        private HttpClient Client => _client ??= new HttpClient(_transport ?? new SocketsHttpHandler(), disposeHandler: _transport is null)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Releases the client of the external sources.
        /// </summary>
        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
