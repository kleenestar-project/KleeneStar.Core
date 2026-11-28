using WebExpress.WebCore.WebScope;

namespace KleeneStar.Core.WebScope
{
    /// <summary>
    /// Marker of the detail page of an object of a kind an add-on contributes. A page
    /// implementing it (and naming it in a <c>Scope</c> attribute) is given the full object
    /// detail experience of the asset and issue kinds: the headline with its edit button and
    /// overflow menu, the description, relation, attachment and property cards, the metadata,
    /// the comment dock, the dialogs behind the menu and the shared sidebar.
    /// </summary>
    /// <remarks>
    /// The page addresses its object by the core's object-key segment, so every one of these
    /// fragments resolves the object the way it does on the asset page. The add-on adds what is
    /// particular to its kind as fragments of its own scoped to its page.
    /// </remarks>
    public interface IScopeObjectKindDetail : IScope
    {
    }
}
