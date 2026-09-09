using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// Shared body of the three entry-path pages - what is mine, what was shared with me,
    /// what I am watching. Each renders the same thing: the caller's slice of the object
    /// set as a list of links, or the empty state explaining how the slice fills.
    /// </summary>
    /// <remarks>
    /// The three pages differ only in the slice they show, so a subclass supplies that
    /// slice and nothing else. They are plain lists rather than the REST-backed table the
    /// kind overviews use: these pages exist to be arrived at from the landing page and
    /// left again through one of their rows, and a search-and-page apparatus around a
    /// couple of dozen rows would be furniture in the way.
    /// <para>
    /// A row carries the object's key and summary and the route into it, which comes from
    /// <see cref="ObjectKindCatalog"/> - so a shared document opens in the document view
    /// and a watched issue in the issue view without this class knowing either kind. The
    /// row is deliberately one line of text: the client controller flattens a row to a
    /// single text node, so structure placed beneath it would be lost.
    /// </para>
    /// </remarks>
    public abstract class LandingScopeListFragment : FragmentControlPanel
    {
        /// <summary>
        /// The maximum number of rows a slice shows.
        /// </summary>
        protected const int MaxItems = 25;

        /// <summary>
        /// Gets the short id suffix identifying the slice (e.g. <c>mine</c>).
        /// </summary>
        protected abstract string Key { get; }

        /// <summary>
        /// Gets the resource key of the message shown while the slice is empty.
        /// </summary>
        protected abstract string EmptyMessage { get; }

        /// <summary>
        /// Gets the icon of the empty state.
        /// </summary>
        protected abstract IIcon EmptyIcon { get; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        protected LandingScopeListFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Returns the slice of the object set the page lists, newest change first, capped
        /// at <see cref="MaxItems"/>.
        /// </summary>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The objects of the slice. The list may be empty.</returns>
        protected abstract IReadOnlyList<Model.Entities.Object> GetObjects(Guid identityId);

        /// <summary>
        /// Returns how large the slice is in total, uncapped.
        /// </summary>
        /// <remarks>
        /// This is the figure the entry-path card on the landing page shows. The page needs
        /// it too: without it a card promising eight hundred would lead to a page showing
        /// twenty-five and saying nothing about the difference.
        /// </remarks>
        /// <param name="identityId">The calling identity.</param>
        /// <returns>The size of the slice.</returns>
        protected abstract int CountObjects(Guid identityId);

        /// <summary>
        /// Renders the slice. Returns <c>null</c> when the fragment's render conditions
        /// exclude it.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext?.Request);
            IReadOnlyList<Model.Entities.Object> entries = identityId == Guid.Empty
                ? []
                : GetObjects(identityId);

            if (entries.Count == 0)
            {
                return new ControlEmptyState("landing-" + Key + "-empty")
                {
                    Icon = _ => EmptyIcon,
                    Message = _ => EmptyMessage
                }
                    .Render(renderContext, visualTree);
            }

            var list = new ControlList("landing-" + Key + "-list");

            foreach (var entry in entries)
            {
                list.Add(BuildRow(entry));
            }

            var total = CountObjects(identityId);

            if (total <= entries.Count)
            {
                return list.Render(renderContext, visualTree);
            }

            // the slice is longer than the page shows, so say so rather than let the
            // entry-path card's figure quietly contradict the rows below it
            var shown = entries.Count.ToString("N0", renderContext?.Request?.Culture);
            var all = total.ToString("N0", renderContext?.Request?.Culture);

            var note = new ControlText("landing-" + Key + "-truncated")
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:landing.list.truncated", shown, all),
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Small,
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.None)
            };

            return new HtmlList
            (
                list.Render(renderContext, visualTree),
                note.Render(renderContext, visualTree)
            );
        }

        /// <summary>
        /// Builds the row of a single object: its key and summary as the row text, and the
        /// route into its detail view.
        /// </summary>
        /// <remarks>
        /// The key leads because it is what the object is called elsewhere - in a commit, in a
        /// notification, in a conversation - and it lines the rows up into a readable column.
        /// Both parts are data, not resource keys, so composing them here is safe. The summary
        /// repeats as the hover text, which is what a row cut off by the column width still
        /// reveals.
        /// </remarks>
        /// <param name="entry">The object to render.</param>
        /// <returns>The row.</returns>
        private ControlListItem BuildRow(Model.Entities.Object entry)
        {
            var id = entry.Id.ToString("N");
            var kind = ObjectKindCatalog.GetKind(entry.Kind);
            var text = entry.Key + "  " + entry.Summary;

            return new ControlListItemLink("landing-" + Key + "-" + id)
            {
                Text = _ => text,
                Tooltip = _ => entry.Summary,
                Icon = _ => ObjectIcon.Resolve(entry, kind?.Icon ?? new IconObject()),
                Uri = _ => ObjectKindCatalog.ResolveDetailUri(entry)
            };
        }
    }
}
