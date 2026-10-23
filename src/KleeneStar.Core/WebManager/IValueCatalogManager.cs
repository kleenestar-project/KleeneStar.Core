using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// Central value catalogs: versioned lists of values that selection fields draw their
    /// options from, typed in, read by WQL from objects, or synchronized from another system.
    /// </summary>
    public interface IValueCatalogManager : IComponentManager
    {
        /// <summary>
        /// An event that fires when a catalog is added.
        /// </summary>
        event EventHandler<ValueCatalog> CatalogAdded;

        /// <summary>
        /// An event that fires when a catalog is changed.
        /// </summary>
        event EventHandler<ValueCatalog> CatalogUpdated;

        /// <summary>
        /// An event that fires when a catalog is removed.
        /// </summary>
        event EventHandler<ValueCatalog> CatalogRemoved;

        /// <summary>
        /// An event that fires when a catalog gets a new version.
        /// </summary>
        event EventHandler<ValueCatalogVersion> VersionPublished;

        /// <summary>
        /// Returns the catalogs a workspace sees - its own and the installation-wide ones - or
        /// every catalog.
        /// </summary>
        /// <param name="workspaceId">The workspace, or <see langword="null"/>.</param>
        /// <returns>The catalogs.</returns>
        IReadOnlyList<ValueCatalog> GetCatalogs(Guid? workspaceId);

        /// <summary>
        /// Returns a catalog.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <returns>The catalog, or <see langword="null"/>.</returns>
        ValueCatalog GetCatalog(Guid catalogId);

        /// <summary>
        /// Checks a catalog before it is stored.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <returns>The problems; empty when the catalog may be stored.</returns>
        IReadOnlyList<string> Validate(ValueCatalog catalog);

        /// <summary>
        /// Adds or changes a catalog.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <returns>The manager, for chaining.</returns>
        /// <exception cref="ArgumentException">The catalog is not valid.</exception>
        IValueCatalogManager SaveCatalog(ValueCatalog catalog);

        /// <summary>
        /// Removes a catalog with its versions; the bound fields keep the options they have.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <returns>The manager, for chaining.</returns>
        IValueCatalogManager RemoveCatalog(Guid catalogId);

        /// <summary>
        /// Returns the values of a catalog.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <param name="version">The version, or <see langword="null"/> for the current one.</param>
        /// <returns>The values; empty before the first version.</returns>
        IReadOnlyList<CatalogValue> GetValues(Guid catalogId, int? version = null);

        /// <summary>
        /// Returns the versions of a catalog.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <returns>The versions, the newest first.</returns>
        IReadOnlyList<ValueCatalogVersion> GetVersions(Guid catalogId);

        /// <summary>
        /// Makes values the catalog's new version and passes them to the fields that follow it.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <param name="values">The values.</param>
        /// <param name="comment">Why: typed, refreshed, restored.</param>
        /// <param name="identityId">Who, or empty for a refresh.</param>
        /// <returns>The new version, or <see langword="null"/> when the values did not change.</returns>
        ValueCatalogVersion Publish(Guid catalogId, IEnumerable<CatalogValue> values, string comment, Guid identityId);

        /// <summary>
        /// Brings an earlier version back as a new one; the history keeps going.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <param name="number">The version to bring back.</param>
        /// <param name="identityId">Who.</param>
        /// <returns>The new version, or <see langword="null"/> when nothing changed.</returns>
        ValueCatalogVersion Restore(Guid catalogId, int number, Guid identityId);

        /// <summary>
        /// Reads a query or external catalog's source again and publishes what changed.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <returns>The new version, or <see langword="null"/> when nothing changed or the source
        /// could not be read (<see cref="ValueCatalog.LastError"/> says why).</returns>
        ValueCatalogVersion Refresh(Guid catalogId);

        /// <summary>
        /// Refreshes every catalog whose interval passed.
        /// </summary>
        /// <param name="now">The moment.</param>
        /// <returns>The catalogs refreshed.</returns>
        int RefreshDue(DateTime now);

        /// <summary>
        /// Binds a selection field to a catalog and gives it the catalog's values.
        /// </summary>
        /// <param name="fieldId">The field.</param>
        /// <param name="catalogId">The catalog.</param>
        /// <param name="pinnedVersion">The version to keep, or <see langword="null"/> to follow the catalog.</param>
        /// <exception cref="ArgumentException">The field cannot draw from the catalog.</exception>
        void Bind(Guid fieldId, Guid catalogId, int? pinnedVersion);

        /// <summary>
        /// Releases a field from its catalog; it keeps the options it has.
        /// </summary>
        /// <param name="fieldId">The field.</param>
        void Unbind(Guid fieldId);

        /// <summary>
        /// Returns the fields bound to a catalog.
        /// </summary>
        /// <param name="catalogId">The catalog.</param>
        /// <returns>The bindings.</returns>
        IReadOnlyList<FieldCatalogBinding> GetBindings(Guid catalogId);
    }

    /// <summary>
    /// A value of a catalog.
    /// </summary>
    /// <param name="Value">The value a field stores.</param>
    /// <param name="Label">How it is shown, or <see langword="null"/> for the value itself.</param>
    public sealed record CatalogValue(string Value, string Label = null);
}
