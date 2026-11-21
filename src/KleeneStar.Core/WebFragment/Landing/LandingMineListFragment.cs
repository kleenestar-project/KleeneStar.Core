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
    /// Lists the caller's own issues on <see cref="WWW.Mine.Index"/>.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Mine.Index>]
    [Cache]
    public sealed class LandingMineListFragment : LandingScopeListFragment
    {
        private readonly IObjectManager _objectManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the slice.</param>
        public LandingMineListFragment(IFragmentContext fragmentContext, IObjectManager objectManager)
            : base(fragmentContext)
        {
            _objectManager = objectManager;
        }

        /// <summary>
        /// Gets the short id suffix identifying the slice.
        /// </summary>
        protected override string Key => "mine";

        /// <summary>
        /// Gets the resource key of the message shown while the slice is empty.
        /// </summary>
        protected override string EmptyMessage => "kleenestar.core:landing.mine.empty";

        /// <summary>
        /// Gets the icon of the empty state.
        /// </summary>
        protected override IIcon EmptyIcon => new IconListCheck();

        /// <summary>
        /// Returns the issues assigned to the caller plus the ones they raised.
        /// </summary>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The objects of the slice. The list may be empty.</returns>
        protected override IReadOnlyList<Model.Entities.Object> GetObjects(Guid identityId)
        {
            return [.. _objectManager.GetObjects(LandingScope.BuildMineQuery(identityId, MaxItems))];
        }

        /// <summary>
        /// Returns how many issues belong to the caller in total.
        /// </summary>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The size of the slice.</returns>
        protected override int CountObjects(Guid identityId)
        {
            return _objectManager.CountObjects(LandingScope.BuildMineQuery(identityId));
        }
    }
}
