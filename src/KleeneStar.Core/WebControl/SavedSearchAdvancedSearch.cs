using KleeneStar.Core.WebFragment.Search;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebControl;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The advanced search of the global search page, extended by what makes a search
    /// something to keep: it opens on the query of the saved search the page runs, and it
    /// carries the buttons that save the query on screen and share the saved search.
    /// </summary>
    /// <remarks>
    /// Two things the framework control cannot do are done by <c>Assets/js/savedsearch.js</c>
    /// on the attributes written here:
    /// <list type="bullet">
    /// <item><see cref="WqlAttribute"/> - the control seeds only its basic box from
    /// <c>data-value</c>, so a WQL expression to open with is handed to the script, which puts
    /// it into the WQL prompt and switches to that mode.</item>
    /// <item><see cref="CarryAttribute"/> - a dialog address is fixed when the page renders,
    /// while the query to save is whatever the user typed since; the script appends the current
    /// expression to the address of every button marked so, the moment it is clicked.</item>
    /// </list>
    /// The buttons are built per request, because which of them a caller gets depends on who
    /// they are and on the saved search the page runs. They stand <em>beside</em> the search, in
    /// a bar wrapping both (<c>.ks-savedsearch-bar</c>), not inside it: the framework's
    /// <c>SearchCtrl</c> clears its host before it collects the children it means to keep, so
    /// content handed to <see cref="ControlAdvancedSearch.Add(IControl[])"/> never survives.
    /// </remarks>
    public sealed class SavedSearchAdvancedSearch : ControlAdvancedSearch
    {
        /// <summary>
        /// The attribute carrying the WQL expression the search opens with.
        /// </summary>
        public const string WqlAttribute = "data-ks-wql";

        /// <summary>
        /// The attribute marking a button whose dialog receives the current expression.
        /// </summary>
        public const string CarryAttribute = "data-ks-savedsearch-carry";

        /// <summary>
        /// The attribute marking the search host, by which the script finds it from a button.
        /// </summary>
        public const string HostAttribute = "data-ks-savedsearch-host";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The control id.</param>
        public SavedSearchAdvancedSearch(string id)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <param name="controls">The controls to render within the search control.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree, params IControl[] controls)
        {
            var request = renderContext?.Request;
            var html = base.Render(renderContext, visualTree, controls);

            if (html is HtmlElement element)
            {
                element.AddUserAttribute(HostAttribute);

                if (SavedSearchRun.ResolveWql(request) is { } wql)
                {
                    element.AddUserAttribute(WqlAttribute, wql);
                }
            }

            var buttons = BuildButtons(renderContext)
                .Select(x => x.Render(renderContext, visualTree))
                .ToArray();

            if (buttons.Length == 0)
            {
                return html;
            }

            return new HtmlElementTextContentDiv
            (
                html,
                new HtmlElementTextContentDiv(buttons) { Class = "ks-savedsearch-actions" }
            )
            {
                Class = "ks-savedsearch-bar"
            };
        }

        /// <summary>
        /// Builds what the caller is offered beside the search: the save button (a new saved
        /// search, or the one the page runs when they may change it) and a "…" menu for the rest -
        /// saving as a new search beside the running one, its permission dialog and deleting it.
        /// The menu is left out when it would hold nothing.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The controls.</returns>
        private static IEnumerable<IControl> BuildButtons(IRenderControlContext renderContext)
        {
            var request = renderContext?.Request;

            if (!SavedSearchAuthorization.MayCreate(request))
            {
                yield break;
            }

            var running = SavedSearchRun.Resolve(request);
            var mayUpdate = running is not null && SavedSearchAuthorization.MayUpdate(running, request);

            // a new saved search made while one runs takes over its columns, which the dialog
            // learns from the saved search it names
            var addUri = running is null
                ? CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearches.Add>()
                : CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearches.Add>()?
                    .Add(new UriQuery(SavedSearchRun.Parameter, running.Id.ToString()));

            var saveUri = mayUpdate
                ? CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Edit>()?
                    .BindParameters(new SavedSearchIdParameter(running.Id))
                : addUri;

            yield return new CarryingButton("savedsearch-save")
            {
                Text = _ => "kleenestar.core:search.saved.save.label",
                Icon = _ => new IconFloppyDisk(),
                Outline = _ => true,
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Secondary),
                PrimaryAction = _ => new ActionModal("modal-form", saveUri, TypeModalSize.ExtraLarge)
            };

            var more = new List<IControlDropdownItem>();

            // beside a saved search the caller may change, save means that one; a new one is
            // made from here. Otherwise save already makes a new one
            if (mayUpdate)
            {
                more.Add(new CarryingDropdownItem("savedsearch-saveas")
                {
                    Text = _ => "kleenestar.core:search.saved.saveas.label",
                    Icon = _ => new IconCopy(),
                    PrimaryAction = _ => new ActionModal("modal-form", addUri, TypeModalSize.ExtraLarge)
                });
            }

            if (running is not null && SavedSearchAuthorization.MayAdminister(running, request))
            {
                var permissionUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Permission>()?
                    .BindParameters(new SavedSearchIdParameter(running.Id));

                more.Add(new ControlDropdownItemLink("savedsearch-permission")
                {
                    Text = _ => "kleenestar.core:search.saved.permission.label",
                    Icon = _ => new IconUserShield(),
                    PrimaryAction = _ => new ActionModal("modal-form", permissionUri, TypeModalSize.ExtraLarge)
                });
            }

            if (running is not null && SavedSearchAuthorization.MayDelete(running, request))
            {
                var deleteUri = CoreHub.GetUri<global::KleeneStar.Core.WWW.SavedSearch._savedsearchid_.Delete>()?
                    .BindParameters(new SavedSearchIdParameter(running.Id));

                if (more.Count > 0)
                {
                    more.Add(new ControlDropdownItemDivider());
                }

                more.Add(new ControlDropdownItemLink("savedsearch-delete")
                {
                    Text = _ => "kleenestar.core:search.saved.delete.label",
                    Icon = _ => new IconTrash(),
                    Color = _ => TypeColorText.Danger,
                    PrimaryAction = _ => new ActionModal("modal-form", deleteUri, TypeModalSize.Small)
                });
            }

            if (more.Count > 0)
            {
                yield return new ControlDropdown("savedsearch-more", [.. more])
                {
                    Icon = _ => new IconEllipsis(),
                    Tooltip = _ => "kleenestar.core:search.saved.more.label",
                    Outline = _ => true,
                    Color = _ => new PropertyColorButton(TypeColorButton.Secondary),
                    AlignmentMenu = _ => TypeAlignmentDropdownMenu.Right
                };
            }
        }

        /// <summary>
        /// A button whose dialog address receives the expression on screen when it is clicked.
        /// </summary>
        /// <param name="id">The button id.</param>
        private sealed class CarryingButton(string id) : ControlButton(id)
        {
            /// <summary>
            /// Converts the control to an HTML representation, marked for the script.
            /// </summary>
            /// <param name="renderContext">The context in which the control is rendered.</param>
            /// <param name="visualTree">The visual tree.</param>
            /// <returns>An HTML node representing the rendered control.</returns>
            public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
            {
                var html = base.Render(renderContext, visualTree);

                (html as HtmlElement)?.AddUserAttribute(CarryAttribute, SavedSearchRun.WqlParameter);

                return html;
            }
        }

        /// <summary>
        /// A menu entry whose dialog address receives the expression on screen when it is
        /// clicked. The menu rebuilds its entries on the client and keeps their data attributes,
        /// so the mark survives.
        /// </summary>
        /// <param name="id">The entry id.</param>
        private sealed class CarryingDropdownItem(string id) : ControlDropdownItemLink(id)
        {
            /// <summary>
            /// Converts the control to an HTML representation, marked for the script.
            /// </summary>
            /// <param name="renderContext">The context in which the control is rendered.</param>
            /// <param name="visualTree">The visual tree.</param>
            /// <returns>An HTML node representing the rendered control.</returns>
            public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
            {
                var html = base.Render(renderContext, visualTree);

                (html as HtmlElement)?.AddUserAttribute(CarryAttribute, SavedSearchRun.WqlParameter);

                return html;
            }
        }
    }
}
