using KleeneStar.Core.WebPolicies;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Workspace
{
    /// <summary>
    /// The workspace cog of the sidebar: only a container. Its entries are the
    /// <see cref="WorkspaceSettingItemFragment"/>s of <see cref="SectionWorkspaceSettingPrimary"/> and
    /// <see cref="SectionWorkspaceSettingSecondary"/> - the core's under <c>Setting/</c>, a plugin's in its
    /// own assembly - so a plugin adds an entry without the core knowing it.
    /// </summary>
    [Section<SectionSidebarToolbarPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Documents._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blogs._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Assets._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Templates._workspacekey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Asset._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    // the class administration pages carry the same sidebar and offer the workspace's menu too;
    // the class's own actions are on its page (edit) and in the class list (clone, delete)
    [Scope<global::KleeneStar.Core.WWW.Class._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Fields._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.SecurityLevels._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Forms._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Priorities._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Workflows._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Statuses._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Slas._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Calendars._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Relations._classid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Form._formid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Workflow._workflowid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Sla._slaid_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Calendar._calendarid_.Index>]
    // every entry of the cog administers the workspace, so the cog is its administrators'
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceAdminPolicy>>]
    [Cache]
    public sealed class WorkspaceSidebarSettingFragment : FragmentControlToolbarItemDropdown
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public WorkspaceSidebarSettingFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Alignment = _ => TypeToolbarItemAlignment.Right;
            Icon = _ => new IconCog();
        }

        /// <summary>
        /// Renders the cog with the entries the fragments of its two sections contribute - the
        /// core's and every plugin's, ordered by their <c>[Order]</c>, the way the framework's
        /// avatar menu collects its entries.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The cog.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            // the base checks the fragment's conditions only in the overload without items, which
            // this one bypasses - without the check every signed-in reader saw the cog
            if (!FragmentContext.Check(renderContext?.Request))
            {
                return null;
            }

            var fragments = WebEx.ComponentHub.FragmentManager;
            var primary = fragments.GetFragments<FragmentControlDropdownItemLink, SectionWorkspaceSettingPrimary>(renderContext?.PageContext, renderContext?.Request).ToList();
            var secondary = fragments.GetFragments<FragmentControlDropdownItemLink, SectionWorkspaceSettingSecondary>(renderContext?.PageContext, renderContext?.Request).ToList();

            var items = new List<IControlDropdownItem>
            {
                new ControlDropdownItemHeader()
                {
                    Text = _ => "kleenestar.core:workspace.dropdown.label"
                }
            };

            items.AddRange(primary);

            if (primary.Count > 0 && secondary.Count > 0)
            {
                items.Add(new ControlDropdownItemDivider());
            }

            items.AddRange(secondary);

            return base.Render(renderContext, visualTree, items);
        }
    }
}
