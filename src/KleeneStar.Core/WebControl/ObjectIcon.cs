using KleeneStar.Core.WebManager;
using System;
using System.Collections.Concurrent;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The picture an object is shown under - in a table, a list, a tile, a dropdown, the
    /// document tree, the blog timeline and everywhere else a row stands for a record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The class decides.</b> Nothing in the application lets anybody give an <em>object</em>
    /// a picture: <c>Object.Icon</c> is filled once at creation and never again - with the
    /// generated mark (<c>CoreHub.GenerateIcon</c>, the product star in a colour derived from
    /// the id) for an object created through the UI or the API, and with a <em>copy</em> of the
    /// class icon for a seeded one. Both are stale by construction. The copy is the one that
    /// misleads: it looks right until an administrator changes the class avatar, and then every
    /// object of that class goes on showing the picture the class used to have.
    /// </para>
    /// <para>
    /// So the icon of the class is what these surfaces show. It is the one an administrator
    /// actually picks, and it is what a reader needs in order to tell a specification from an
    /// article standing beside it in the same tree. The object's own icon is kept only as the
    /// fallback for a class that carries none.
    /// </para>
    /// </remarks>
    public static class ObjectIcon
    {
        /// <summary>
        /// The icon of each class, kept so a list of fifty rows costs one lookup per class
        /// rather than one per row. The class of an object is deliberately <b>not</b> included
        /// in the object queries: the relationship is required, so an <c>Include</c> joins it
        /// inwards, and an object whose class row has gone would then disappear from every
        /// list it belongs to. Losing a row is a far worse failure than looking up an icon.
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, ImageIcon> _icons = new();

        private static readonly object _sync = new();

        /// <summary>
        /// The class manager the cache is currently subscribed to, or null while nothing is
        /// subscribed. It is the manager instance rather than a flag so a rebuilt component
        /// graph - a plugin reload, a second fixture in one test assembly - is noticed: the
        /// events of the old manager reach nobody, and a cache still answering from it would
        /// go stale with no way to clear it.
        /// </summary>
        private static volatile IClassManager _connected;

        /// <summary>
        /// Returns the icon that stands for the object: the icon of its class, or the one the
        /// object carries when the class has none.
        /// </summary>
        /// <param name="object">The object. May be null.</param>
        /// <returns>The icon, or <see langword="null"/> when neither carries one.</returns>
        public static ImageIcon Resolve(Model.Entities.Object @object)
        {
            if (@object is null)
            {
                return null;
            }

            return ClassIcon(@object) ?? @object.Icon;
        }

        /// <summary>
        /// Returns the icon of the object's class, from the navigation when the caller loaded
        /// it and from the cache otherwise.
        /// </summary>
        /// <param name="object">The object whose class is asked for.</param>
        /// <returns>The icon, or <see langword="null"/>.</returns>
        private static ImageIcon ClassIcon(Model.Entities.Object @object)
        {
            if (@object.Class is not null)
            {
                return @object.Class.Icon;
            }

            if (@object.ClassId == Guid.Empty)
            {
                return null;
            }

            var manager = Connect();

            // nothing is cached while the manager is not up: an entry written now could never
            // be invalidated, because the subscription that would drop it does not exist yet.
            // A list rendered during startup would otherwise poison the cache with nulls for
            // the lifetime of the process, and every object of those classes would go on
            // showing the stale picture it carries - the exact failure this type exists to end
            if (manager is null)
            {
                return null;
            }

            return _icons.GetOrAdd(@object.ClassId, id => manager.GetClass(id)?.Icon);
        }

        /// <summary>
        /// Subscribes to the class manager, so a changed or deleted class drops its cached
        /// icon, and answers the manager the lookup may use.
        /// </summary>
        /// <remarks>
        /// Without this the cache would be the third place in a row where a newly saved avatar
        /// did not appear until a restart. The subscription is made on first use rather than at
        /// startup because this is a static helper with no place in the component graph; until
        /// the manager exists the lookup simply is not cached. A manager that is not the one
        /// subscribed to is a rebuilt component graph: the cache is dropped and the events are
        /// taken up on the new one.
        /// </remarks>
        /// <returns>The subscribed manager, or <see langword="null"/> when there is none.</returns>
        private static IClassManager Connect()
        {
            var manager = CoreHub.ClassManager;

            if (manager is null || ReferenceEquals(_connected, manager))
            {
                return manager;
            }

            lock (_sync)
            {
                if (ReferenceEquals(_connected, manager))
                {
                    return manager;
                }

                // whatever the previous manager answered belongs to a graph that is gone
                _icons.Clear();

                manager.ClassUpdated += (_, @class) => Forget(@class);
                manager.ClassRemoved += (_, @class) => Forget(@class);
                manager.ClassAdded += (_, @class) => Forget(@class);

                _connected = manager;
            }

            return manager;
        }

        /// <summary>
        /// Drops the cached icon of a class.
        /// </summary>
        /// <param name="class">The class that changed. May be null.</param>
        private static void Forget(Model.Entities.Class @class)
        {
            if (@class is not null)
            {
                _icons.TryRemove(@class.Id, out _);
            }
        }

        /// <summary>
        /// Returns the icon that stands for the object, falling back to the supplied glyph
        /// when neither the object nor its class carries a picture.
        /// </summary>
        /// <param name="object">The object. May be null.</param>
        /// <param name="fallback">The glyph to use instead, e.g. the icon of the kind.</param>
        /// <returns>The icon.</returns>
        public static IIcon Resolve(Model.Entities.Object @object, IIcon fallback)
        {
            return Resolve(@object) ?? fallback;
        }

        /// <summary>
        /// Returns the address the icon of the object is served from, for the surfaces that
        /// hand a picture to the client as a url rather than as a control.
        /// </summary>
        /// <param name="object">The object. May be null.</param>
        /// <returns>The address, or <see langword="null"/>.</returns>
        public static string Uri(Model.Entities.Object @object)
        {
            return Resolve(@object)?.Uri?.ToString();
        }
    }
}
