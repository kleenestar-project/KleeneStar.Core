using WebExpress.WebCore.WebScope;

namespace KleeneStar.Core.WebScope
{
    /// <summary>
    /// Marker of the overview page of an object kind an add-on contributes. A page implementing
    /// it (and naming it in a <c>Scope</c> attribute) is given the chrome every kind overview of
    /// the core carries: the workspace header and icon of the sidebar, the links to the other
    /// kinds and the workspace settings toolbar.
    /// </summary>
    /// <remarks>
    /// The fragments of the core name the core's overview pages one by one, and a plugin page is
    /// no type the core can name. The interface is the name both can: the shared fragments scope
    /// themselves to it as well, and an add-on's overview implements it. A fragment particular to
    /// one core kind (the document tree, the blog timeline, the asset class tree) is not scoped
    /// to it.
    /// </remarks>
    public interface IScopeObjectKindOverview : IScope
    {
    }
}
