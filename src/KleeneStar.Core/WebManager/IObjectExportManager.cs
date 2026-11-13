using KleeneStar.Core.WebReport;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Core.WebManager
{
    /// <summary>
    /// The file formats of the single object export (<see cref="IObjectExportFormat"/>).
    /// </summary>
    public interface IObjectExportManager : IComponentManager
    {
        /// <summary>
        /// Gets a number that changes whenever the set of formats changes.
        /// </summary>
        long Version { get; }

        /// <summary>
        /// Gets the formats ordered by <see cref="IObjectExportFormat.Order"/>, then key.
        /// </summary>
        IEnumerable<IObjectExportFormat> Formats { get; }

        /// <summary>
        /// Registers a format that is no type of a plugin (a test's stand-in, a format built from
        /// configuration). A format class of a plugin is found without this call.
        /// </summary>
        /// <param name="format">The format.</param>
        void Register(IObjectExportFormat format);

        /// <summary>
        /// Removes a registered format.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when a format was removed.</returns>
        bool Unregister(string key);

        /// <summary>
        /// Returns the format of a key, ignoring case and surrounding blanks.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The format, or <see langword="null"/>.</returns>
        IObjectExportFormat Get(string key);
    }
}
