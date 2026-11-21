using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Changes the tabs (<see cref="ObjectView"/>) of a workspace overview - add, reorder, and
    /// from the tab menu rename, color, delete - for the issue and the asset tab endpoint alike.
    /// </summary>
    /// <remarks>
    /// The tabs are content of the workspace, like a board's lanes, so every change needs
    /// <see cref="ContentAuthorization.MayWriteContent"/>, the same answer the tab control
    /// is given to decide whether it offers the menu at all. A tab is reachable only through
    /// the route of its own workspace and kind; anything else is answered as not changed.
    /// </remarks>
    public static class ObjectViewTabs
    {
        /// <summary>
        /// The length of the name column; a longer label is refused rather than cut.
        /// </summary>
        public const int MaxNameLength = 64;

        /// <summary>
        /// Determines whether the caller may change the tabs of the workspace the route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the tabs may be changed.</returns>
        public static bool MayArrange(IRequest request)
        {
            return ContentAuthorization.MayWriteContent(request);
        }

        /// <summary>
        /// Refuses a caller who may not change the tabs of the workspace the route names, with
        /// a message the tab control shows.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <exception cref="RestApiRefusal">The caller may not change the tabs.</exception>
        public static void DemandArrange(IRequest request)
        {
            if (!MayArrange(request))
            {
                throw new RestApiRefusal(I18N.Translate(request, "kleenestar.core:object.tab.refused"));
            }
        }

        /// <summary>
        /// Puts the tabs of the overview the route names into the order they were dragged into.
        /// </summary>
        /// <param name="order">The tab ids in their new order.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="request">The request whose route names the workspace.</param>
        /// <returns><see langword="true"/> when the order was applied.</returns>
        public static bool Reorder(IReadOnlyList<string> order, string kind, IRequest request)
        {
            var workspaceKey = request?.GetParameter<WebParameter.WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);

            if (order is null || workspace is null || !MayArrange(request))
            {
                return false;
            }

            // an id of another workspace or kind names no tab of this overview and is ignored
            var ids = order
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .ToList();

            return CoreHub.ObjectViewManager.ReorderViews(workspace.Id, kind, ids);
        }

        /// <summary>
        /// Resolves a tab of the workspace and kind the route names, if the caller may change it.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="request">The request whose route names the workspace.</param>
        /// <returns>The tab, or <see langword="null"/>.</returns>
        public static ObjectView Resolve(string viewId, string kind, IRequest request)
        {
            if (!Guid.TryParse(viewId, out var id) || !MayArrange(request))
            {
                return null;
            }

            var workspaceKey = request?.GetParameter<WebParameter.WorkspaceKeyParameter>()?.Value;
            var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(workspaceKey);
            var view = CoreHub.ObjectViewManager.GetObjectView(id);

            return workspace is not null
                && view is not null
                && view.WorkspaceId == workspace.Id
                && string.Equals(view.Kind, kind, StringComparison.OrdinalIgnoreCase)
                ? view
                : null;
        }

        /// <summary>
        /// Renames a tab, refusing a name another tab of the same overview carries.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <param name="label">The new name, trimmed and not blank.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the tab was renamed.</returns>
        public static bool Rename(string viewId, string label, string kind, IRequest request)
        {
            var view = Resolve(viewId, kind, request);

            if (view is null || string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            // (workspace, kind, name) is a unique index; a silent suffix would leave the
            // client showing a label the store does not hold
            var taken = CoreHub.ObjectViewManager
                .GetViewsForWorkspace(view.WorkspaceId, view.Kind)
                .Any(x => x.Id != view.Id && string.Equals(x.Name, label, StringComparison.OrdinalIgnoreCase));

            if (taken)
            {
                return false;
            }

            view.Name = label;
            CoreHub.ObjectViewManager.UpdateObjectView(view);

            return true;
        }

        /// <summary>
        /// Sets or clears the color of a tab.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <param name="color">The color as a <c>#rrggbb</c> value (checked by the framework),
        /// or <see langword="null"/> to clear it.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the tab was changed.</returns>
        public static bool Recolor(string viewId, string color, string kind, IRequest request)
        {
            var view = Resolve(viewId, kind, request);

            if (view is null)
            {
                return false;
            }

            view.Color = string.IsNullOrWhiteSpace(color) ? null : color.ToLowerInvariant();
            CoreHub.ObjectViewManager.UpdateObjectView(view);

            return true;
        }

        /// <summary>
        /// Removes a tab.
        /// </summary>
        /// <param name="viewId">The id of the tab.</param>
        /// <param name="kind">The object kind of the overview.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the tab was removed.</returns>
        public static bool Remove(string viewId, string kind, IRequest request)
        {
            var view = Resolve(viewId, kind, request);

            if (view is null)
            {
                return false;
            }

            CoreHub.ObjectViewManager.RemoveObjectView(view);

            return true;
        }
    }
}
