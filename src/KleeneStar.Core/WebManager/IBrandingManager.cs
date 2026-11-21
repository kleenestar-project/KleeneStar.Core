using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Defines the contract for managing the identity of the installation: the title and the icon
    /// the application is presented under.
    /// </summary>
    /// <remarks>
    /// The identity is a singleton, so the manager exposes it as a single record rather than as a
    /// collection. The query-based accessors exist for the REST endpoint, which addresses the
    /// record the same way it addresses any other entity.
    /// </remarks>
    public interface IBrandingManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when the branding is updated.
        /// </summary>
        event EventHandler<Branding> BrandingUpdated;

        /// <summary>
        /// Returns the branding of the installation.
        /// </summary>
        /// <returns>
        /// The branding. Never null; an empty record is returned when nothing has been stored yet,
        /// so callers do not have to distinguish "not configured" from "configured as the default".
        /// </returns>
        Branding GetBranding();

        /// <summary>
        /// Retrieves the branding records that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <returns>The matching records; empty when none match.</returns>
        IEnumerable<Branding> GetBrandings(IQuery<Branding> query);

        /// <summary>
        /// Retrieves the branding records that satisfy the specified filter criteria.
        /// </summary>
        /// <param name="query">The query criteria. Must not be null.</param>
        /// <param name="context">The context in which the query is executed. Cannot be null.</param>
        /// <returns>The matching records; empty when none match.</returns>
        IEnumerable<Branding> GetBrandings(IQuery<Branding> query, IQueryContext context);

        /// <summary>
        /// Stores the branding of the installation and applies it to the running application.
        /// </summary>
        /// <param name="brandingEntity">The branding to store. Cannot be null.</param>
        /// <returns>The current instance to allow for method chaining.</returns>
        IBrandingManager Update(Branding brandingEntity);

        /// <summary>
        /// Applies the stored branding to the running application, so the header shows the title
        /// and the icon the installation chose rather than the ones the application declared.
        /// </summary>
        /// <remarks>
        /// Called once at startup and again after every update. A field the installation left
        /// empty restores what the application declared, so clearing the setting is a way back to
        /// the default rather than a way to an empty header.
        /// </remarks>
        /// <returns>The current instance to allow for method chaining.</returns>
        IBrandingManager Apply();
    }
}
