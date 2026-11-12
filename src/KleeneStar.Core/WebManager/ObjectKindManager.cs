using KleeneStar.Core.WebFragment.Object;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;
using WebExpress.WebCore.WebUri;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The object kinds of the installation, collected from the plugins by WebExpress's
    /// <see cref="PluginTypeCatalog{TItem}"/>: every <b>public sealed</b> <see cref="IObjectKind"/>
    /// with a public parameterless constructor of any plugin - the core's document, blog, issue
    /// and asset among them. There is no enum of kinds. Formerly the static <c>ObjectKindCatalog</c>.
    /// </summary>
    public sealed class ObjectKindManager : IObjectKindManager
    {
        private readonly PluginTypeCatalog<IObjectKind> _catalog = new(x => Model.Entities.ObjectKind.Normalize(x.Key));

        /// <summary>
        /// Initializes a new instance of the manager. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub, or <see langword="null"/> without a host.</param>
        /// <param name="httpServerContext">The HTTP server context, or <see langword="null"/>.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private ObjectKindManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            if (componentHub?.PluginManager is not null)
            {
                _catalog.Attach(componentHub, httpServerContext);
            }
            else
            {
                // without a plugin manager (a test, a call before the host is up) the manager
                // knows its own assembly's kinds
                _catalog.Collect(typeof(ObjectKindManager).Assembly);
            }
        }

        /// <summary>
        /// Creates a manager outside a host, for <see cref="CoreHub"/> before or without one.
        /// </summary>
        /// <returns>A manager knowing the core's kinds.</returns>
        internal static ObjectKindManager CreateDetached()
        {
            return new ObjectKindManager(null, null);
        }

        /// <inheritdoc/>
        public long Version => _catalog.Version;

        /// <inheritdoc/>
        public IEnumerable<IObjectKind> Kinds => [.. _catalog.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)];

        /// <inheritdoc/>
        public void Register(IObjectKind kind)
        {
            ArgumentNullException.ThrowIfNull(kind);

            _catalog.Register(kind);
        }

        /// <inheritdoc/>
        public IObjectKind GetKind(string key)
        {
            return _catalog.Find(Model.Entities.ObjectKind.Normalize(key));
        }

        /// <inheritdoc/>
        public IUri ResolveDetailUri(Model.Entities.Object @object)
        {
            return @object is null ? null : ResolveDetailUri(@object.Kind, @object.Key);
        }

        /// <inheritdoc/>
        public IUri ResolveDetailUri(string kind, string objectKey)
        {
            return KindOrIssue(kind)?.DetailUri(objectKey);
        }

        /// <inheritdoc/>
        public IUri ResolvePreviewUri(Model.Entities.Object @object)
        {
            return @object is null ? null : ResolvePreviewUri(@object.Kind, @object.Key);
        }

        /// <inheritdoc/>
        public IUri ResolvePreviewUri(string kind, string objectKey)
        {
            return KindOrIssue(kind)?.PreviewUri(objectKey);
        }

        /// <inheritdoc/>
        public IUri ResolveDetailUriFrozen(Model.Entities.Object @object)
        {
            var uri = ResolveDetailUri(@object);

            if (uri is null)
            {
                return null;
            }

            var segments = uri.PathSegments.ToList();

            // the object-key is the trailing (and only variable) segment of a detail route;
            // keep the leading constants and re-append it as a constant
            if (segments.Count < 2)
            {
                return uri;
            }

            return uri.Take(segments.Count - 1).Concat(segments[^1].ToString());
        }

        /// <inheritdoc/>
        public IUri ResolveEditUri(Model.Entities.Object @object)
        {
            return @object is null ? null : ResolveEditUri(@object.Kind, @object.Key);
        }

        /// <inheritdoc/>
        public IUri ResolveEditUri(string kind, string objectKey)
        {
            return KindOrIssue(kind)?.EditUri(objectKey);
        }

        /// <summary>
        /// Stops following the plugins.
        /// </summary>
        public void Dispose()
        {
            _catalog.Dispose();
        }

        /// <summary>
        /// Resolves a kind, falling back to the issue kind for a key nobody provides any more.
        /// </summary>
        /// <param name="kind">The kind key.</param>
        /// <returns>The kind.</returns>
        private IObjectKind KindOrIssue(string kind)
        {
            return GetKind(kind) ?? GetKind(Model.Entities.ObjectKind.Issue);
        }
    }
}
