using KleeneStar.Core.WebFragment.Object;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Objects
{
    /// <summary>
    /// Resolves an object addressed by id through the <c>id</c> query parameter and
    /// redirects to the reduced reading view of its kind.
    /// </summary>
    /// <remarks>
    /// The page is the sibling of <see cref="Detail"/> and bridges the same mismatch: the
    /// Kanban boards and the scrum backlog report the object <em>id</em> in their selection
    /// event, while the reduced view
    /// (<see cref="global::KleeneStar.Core.WWW.Issue._objectkey_.Preview"/>) — like every
    /// per-kind route — is keyed by the object <em>key</em>. Which of the two bridges a
    /// master-detail points its uri template at is therefore the whole choice between the
    /// full reading view and the pane-sized one.
    ///
    /// The boards and the backlog point here, at the same view their list siblings already
    /// show. A detail frame embeds a page's main content region, and that region of the full
    /// reading view is written for a full-width column: beside a board it arrives without the
    /// property column that carries the status and the people — that lives in
    /// <c>#wx-content-property</c>, a sibling of the region the frame takes — while the
    /// inline-editable field card, the description editor, the attachment list and the
    /// comment thread are squeezed into the pane. The reduced view is the opposite selection,
    /// and it carries a button that opens the full one.
    ///
    /// <see cref="Detail"/> stays for the callers that mean a navigation rather than a pane,
    /// such as the schedule entries.
    ///
    /// The page is deliberately outside every navigation scope: it is not a destination a
    /// user browses to, but the endpoint the detail frame fetches for the selected card.
    /// </remarks>
    [Title("kleenestar.core:object.preview.title")]
    public sealed class Preview : IPage<VisualTreeWebApp>
    {
        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var id = renderContext?.Request?.GetParameter("id")?.Value;
            var @object = Guid.TryParse(id, out var objectId)
                ? CoreHub.ObjectManager.GetObject(objectId)
                : null;

            var previewUri = CoreHub.ObjectKindManager.ResolvePreviewUri(@object);

            if (previewUri is not null)
            {
                throw new RedirectException(previewUri);
            }

            // an id that resolves to nothing is shown rather than redirected, so a stale
            // selection reads as a missing object instead of a broken link
            visualTree.Content.MainPanel.AddPrimary(new ControlEmptyState()
            {
                Icon = _ => new IconMagnifyingGlass(),
                Title = _ => "kleenestar.core:object.preview.unknown.title",
                Message = _ => "kleenestar.core:object.preview.unknown.id.message"
            });
        }
    }
}
