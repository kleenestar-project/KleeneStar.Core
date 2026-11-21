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
    /// Lists what has been shared with the caller on <see cref="WWW.Shared.Index"/>.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Shared.Index>]
    [Cache]
    public sealed class LandingSharedListFragment : LandingScopeListFragment
    {
        private readonly IObjectManager _objectManager;
        private readonly IShareManager _shareManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the shared objects.</param>
        /// <param name="shareManager">The share manager naming them.</param>
        public LandingSharedListFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IShareManager shareManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _shareManager = shareManager;
        }

        /// <summary>
        /// Gets the short id suffix identifying the slice.
        /// </summary>
        protected override string Key => "shared";

        /// <summary>
        /// Gets the resource key of the message shown while the slice is empty.
        /// </summary>
        protected override string EmptyMessage => "kleenestar.core:landing.shared.empty";

        /// <summary>
        /// Gets the icon of the empty state.
        /// </summary>
        protected override IIcon EmptyIcon => new IconShareNodes();

        /// <summary>
        /// Returns the objects shared with the caller.
        /// </summary>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The objects of the slice. The list may be empty.</returns>
        protected override IReadOnlyList<Model.Entities.Object> GetObjects(Guid identityId)
        {
            var ids = LandingScope.GetSharedIds(_shareManager, identityId);

            if (ids.Length == 0)
            {
                return [];
            }

            return [.. _objectManager.GetObjects(LandingScope.BuildIdQuery(ids, MaxItems))];
        }

        /// <summary>
        /// Returns how many objects are shared with the caller in total.
        /// </summary>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The size of the slice.</returns>
        protected override int CountObjects(Guid identityId)
        {
            var ids = LandingScope.GetSharedIds(_shareManager, identityId);

            return ids.Length == 0
                ? 0
                : _objectManager.CountObjects(LandingScope.BuildIdQuery(ids));
        }
    }
}
