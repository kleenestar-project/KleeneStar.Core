using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Template
{
    /// <summary>
    /// The shared base of the category section of the template and hierarchy sidebars: a section
    /// header, an entry that clears the selection, and one entry per category the listed records
    /// of the workspace use (<see cref="GetCategoryValues"/>). Selecting an entry narrows the
    /// overview to that category.
    /// </summary>
    /// <remarks>
    /// The entries drive the table through the client-side filter registry rather than through a
    /// link: each carries an exclusive filter action of the shared group, so picking one replaces
    /// the previous selection and the bound table re-queries with the new
    /// <see cref="TemplateCategoryFilter">filter id</see>. The header is always visible; without
    /// categories a disabled empty entry is shown instead, so the section communicates where
    /// categories will appear. Header and entries are emitted as siblings via
    /// <see cref="HtmlList"/> so the sidebar parser picks each of them up as a regular sidebar
    /// item (a wrapper element would be skipped).
    /// </remarks>
    public abstract class TemplateSidebarCategoryFragmentBase : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// The filter group the category entries share. Membership makes the selection exclusive:
        /// activating one entry deactivates the others.
        /// </summary>
        private const string FilterGroup = "template-category";

        private readonly IWorkspaceManager _workspaceManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">
        /// The context associated with the fragment, providing necessary data and services for its
        /// operation. Cannot be null.
        /// </param>
        /// <param name="workspaceManager">
        /// The workspace manager used to resolve the workspace from the request. Cannot be null.
        /// </param>
        protected TemplateSidebarCategoryFragmentBase(IFragmentContext fragmentContext, IWorkspaceManager workspaceManager)
            : base(fragmentContext)
        {
            _workspaceManager = workspaceManager;
        }

        /// <summary>
        /// Returns the i18n key of the entry that clears the selection.
        /// </summary>
        protected abstract string AllLabel { get; }

        /// <summary>
        /// Renders the section: the header, the "all" entry and one entry per category, or, when
        /// the listed records carry no category, a disabled empty entry. Returns
        /// <c>null</c> only when the fragment's render conditions exclude it.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>An HTML node representing the rendered fragment, or <c>null</c> when suppressed.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var header = new ControlSidebarItemHeader(Id + "-header")
            {
                Text = _ => "kleenestar.core:template.categories.label"
            };

            var all = new ControlSidebarItemLink(Id + "-all")
            {
                Text = _ => AllLabel,
                PrimaryAction = _ => new ActionFilterReset()
                {
                    Exclusive = true,
                    Group = FilterGroup
                }
            };

            var nodes = new HtmlList
            (
                header.Render(renderContext, visualTree),
                all.Render(renderContext, visualTree)
            );

            var categories = GetCategories(renderContext);

            if (categories.Count == 0)
            {
                var empty = new ControlSidebarItemLink(Id + "-empty")
                {
                    Text = _ => "kleenestar.core:template.categories.none.label",
                    Active = _ => TypeActive.Disabled
                };

                nodes.Add(empty.Render(renderContext, visualTree));

                return nodes;
            }

            foreach (var category in categories)
            {
                var link = new ControlSidebarItemLink(TemplateCategoryFilter.ToFilterId(category))
                {
                    Text = _ => category,
                    PrimaryAction = _ => new ActionFilter()
                    {
                        Exclusive = true,
                        Group = FilterGroup
                    }
                };

                nodes.Add(link.Render(renderContext, visualTree));
            }

            return nodes;
        }

        /// <summary>
        /// Collects the distinct, non-empty categories the listed records of the addressed
        /// workspace use, in alphabetical order. Returns an empty list when no workspace can be
        /// resolved.
        /// </summary>
        /// <param name="renderContext">The render context carrying the request.</param>
        /// <returns>The categories in use. The list may be empty.</returns>
        private IReadOnlyList<string> GetCategories(IRenderControlContext renderContext)
        {
            var keyParameter = renderContext?.Request?.GetParameter<WorkspaceKeyParameter>();
            var workspace = _workspaceManager.GetWorkspaceByKey(keyParameter?.Value);

            if (workspace is null)
            {
                return [];
            }

            return [.. GetCategoryValues(workspace.Id)
                .SelectMany(TemplateCategories.Parse)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Returns the stored category values of the records the page lists, one per record;
        /// a value may name several categories.
        /// </summary>
        /// <param name="workspaceId">The workspace whose records are listed.</param>
        /// <returns>The category values.</returns>
        protected abstract IEnumerable<string> GetCategoryValues(Guid workspaceId);
    }
}
