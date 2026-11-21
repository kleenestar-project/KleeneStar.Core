using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Manages the identity of the installation: the title and the icon the application is
    /// presented under.
    /// </summary>
    /// <remarks>
    /// The record is read on startup and after every update, and the values are pushed into the
    /// application context from there - the header reads the context on every render, so nothing
    /// has to query the database per request. The record is held in memory for the settings page,
    /// which is the only reader that needs it directly.
    /// </remarks>
    public sealed class BrandingManager : IBrandingManager
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly object _sync = new();

        private Branding _cached;

        /// <summary>
        /// An event that fires when the branding is updated.
        /// </summary>
        public event EventHandler<Branding> BrandingUpdated;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private BrandingManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;
        }

        /// <summary>
        /// Returns the branding of the installation.
        /// </summary>
        /// <remarks>
        /// An installation that has never been branded is reported as an empty record rather than
        /// as nothing, so the settings page does not have to treat the first start of a fresh
        /// installation as a special case.
        /// </remarks>
        /// <returns>The branding. Never null.</returns>
        public Branding GetBranding()
        {
            var cached = _cached;

            if (cached is not null)
            {
                return cached;
            }

            lock (_sync)
            {
                var query = new Query<Branding>()
                    .WhereEquals(x => x.Id, Branding.SingletonId)
                    .WithPaging(0, 1);

                return _cached ??= ModelHub.GetBrandings(query).FirstOrDefault()
                    ?? new Branding();
            }
        }

        /// <summary>
        /// Retrieves the branding records that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <returns>The matching records; empty when none match.</returns>
        public IEnumerable<Branding> GetBrandings(IQuery<Branding> query)
        {
            return ModelHub.GetBrandings(query);
        }

        /// <summary>
        /// Retrieves the branding records that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <returns>The matching records; empty when none match.</returns>
        public IEnumerable<Branding> GetBrandings(IQuery<Branding> query, IQueryContext context)
        {
            return ModelHub.GetBrandings(query, context as KleeneStarDbContext);
        }

        /// <summary>
        /// Stores the branding of the installation and applies it to the running application.
        /// </summary>
        /// <param name="brandingEntity">The branding to store. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IBrandingManager Update(Branding brandingEntity)
        {
            ArgumentNullException.ThrowIfNull(brandingEntity);

            ModelHub.Save(brandingEntity);

            lock (_sync)
            {
                _cached = null;
            }

            Apply();

            BrandingUpdated?.Invoke(this, brandingEntity);

            // update notification
            CoreHub.AddNotification("kleenestar.core:notification.title.updated", "kleenestar.core:notification.branding.updated", brandingEntity);

            return this;
        }

        /// <summary>
        /// Applies the stored branding to the running application.
        /// </summary>
        /// <returns>The current instance to allow for method chaining.</returns>
        public IBrandingManager Apply()
        {
            var applicationContext = CoreHub.ApplicationContext;

            if (applicationContext is null)
            {
                return this;
            }

            var branding = GetBranding();

            // a blank value is not "show nothing" but "keep what the application declared", which
            // is what the application manager does with an empty argument - so clearing a field on
            // the settings page is the way back to the default
            _componentHub?.ApplicationManager?.SetApplicationName(applicationContext, branding.Title);
            _componentHub?.ApplicationManager?.SetApplicationIcon(applicationContext, ResolveIconPath(branding.Icon));

            return this;
        }

        /// <summary>
        /// Turns a stored icon into the path the application manager expects: one relative to the
        /// application, as the <c>[Icon]</c> attribute declares it.
        /// </summary>
        /// <remarks>
        /// An uploaded icon is stored with the absolute route it is served under
        /// (<c>/kleenestar/assets/icons/…</c>), because that is what every other consumer renders.
        /// The application manager assembles that route itself, so the application part is taken
        /// off again here rather than being handed over twice.
        /// </remarks>
        /// <param name="icon">The stored icon. May be null.</param>
        /// <returns>The relative path, or null when no icon is stored.</returns>
        private static string ResolveIconPath(ImageIcon icon)
        {
            var uri = icon?.Uri?.ToString();

            if (string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            var route = CoreHub.ApplicationContext?.Route?.ToString();

            return !string.IsNullOrEmpty(route) && uri.StartsWith(route, StringComparison.OrdinalIgnoreCase)
                ? uri[route.Length..]
                : uri;
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
