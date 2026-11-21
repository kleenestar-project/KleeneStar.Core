using KleeneStar.Core.WebManager;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// Lists what the caller is watching on <see cref="WWW.Watched.Index"/>.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Watched.Index>]
    [Cache]
    public sealed class LandingWatchedListFragment : LandingScopeListFragment
    {
        private readonly IObjectManager _objectManager;
        private readonly IWatcherManager _watcherManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the watched objects.</param>
        /// <param name="watcherManager">The watcher manager naming them.</param>
        public LandingWatchedListFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IWatcherManager watcherManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _watcherManager = watcherManager;
        }

        /// <summary>
        /// Gets the short id suffix identifying the slice.
        /// </summary>
        protected override string Key => "watched";

        /// <summary>
        /// Gets the resource key of the message shown while the slice is empty.
        /// </summary>
        protected override string EmptyMessage => "kleenestar.core:landing.watched.empty";

        /// <summary>
        /// Gets the icon of the empty state.
        /// </summary>
        protected override IIcon EmptyIcon => new IconEye();

        /// <summary>
        /// Returns the objects the caller is watching.
        /// </summary>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The objects of the slice. The list may be empty.</returns>
        protected override IReadOnlyList<Model.Entities.Object> GetObjects(Guid identityId)
        {
            var ids = LandingScope.GetWatchedIds(_watcherManager, identityId);

            if (ids.Length == 0)
            {
                return [];
            }

            return [.. _objectManager.GetObjects(LandingScope.BuildIdQuery(ids, MaxItems))];
        }

        /// <summary>
        /// Returns how many objects the caller is watching in total.
        /// </summary>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The size of the slice.</returns>
        protected override int CountObjects(Guid identityId)
        {
            var ids = LandingScope.GetWatchedIds(_watcherManager, identityId);

            return ids.Length == 0
                ? 0
                : _objectManager.CountObjects(LandingScope.BuildIdQuery(ids));
        }
    }
}
