using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Quickfilter
{
    /// <summary>
    /// Resolves which quickfilter endpoint a filter dialog reads and writes through.
    /// </summary>
    /// <remarks>
    /// The dialogs serve every bar, so the endpoint follows from the view the chip named rather
    /// than from an address handed in — a dialog cannot be pointed at something else that way.
    /// A further bar is added by naming its view here.
    ///
    /// A bar of a view that exists once per workspace needs that workspace as well, and the dialog
    /// cannot take it from its own route: the dialog is a page of its own and its route names no
    /// workspace. The chip therefore carries it along, and it is bound into the endpoint address
    /// here — without that the address keeps its <c>${workspacekey}</c> placeholder and the dialog
    /// asks for a route that does not exist.
    /// </remarks>
    internal static class QuickfilterService
    {
        /// <summary>
        /// Returns the address of the quickfilter endpoint the named bar is served by.
        /// </summary>
        /// <param name="renderContext">The context the dialog is rendered in.</param>
        /// <returns>
        /// The address, or null when the view is not one that has a bar with user-defined filters,
        /// or when a per-workspace bar was named without its workspace.
        /// </returns>
        public static string Resolve(IRenderControlContext renderContext)
        {
            var view = renderContext?.Request?.GetParameter("view")?.Value;
            var context = renderContext?.Request?.GetParameter("context")?.Value;

            switch (view)
            {
                case global::KleeneStar.Core.WWW.Api._1_.Tenants.Quickfilter.ViewKey:
                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Tenants.Quickfilter>()?.ToString();

                case global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Quickfilter.ViewKey:
                    // answering with an unbound address would send the dialog to a route that does
                    // not exist, which reads as a missing endpoint rather than a missing workspace
                    if (string.IsNullOrWhiteSpace(context))
                    {
                        return null;
                    }

                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Quickfilter>()?
                        .BindParameters(new WorkspaceKeyParameter(context))?
                        .ToString();

                case global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.KanbanQuickfilter.ViewKey:
                    if (string.IsNullOrWhiteSpace(context))
                    {
                        return null;
                    }

                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.KanbanQuickfilter>()?
                        .BindParameters(new WorkspaceKeyParameter(context))?
                        .ToString();

                case global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.Quickfilter.ViewKey:
                    // the bar of a saved search is served under the saved search, which the chip
                    // carries as its context
                    if (!Guid.TryParse(context, out var savedSearchId))
                    {
                        return null;
                    }

                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.Quickfilter>()?
                        .BindParameters(new SavedSearchIdParameter(savedSearchId))?
                        .ToString();

                default:
                    return null;
            }
        }

        /// <summary>
        /// Returns the address of the WQL prompt endpoint of the named bar, which is what
        /// the filter expression editor of the dialog draws its suggestions, its syntax
        /// check and its history from.
        /// </summary>
        /// <remarks>
        /// A filter expression is written against the entity the bar filters, so the
        /// prompt has to ask the endpoint of that entity: offering the attributes of an
        /// issue while a tenant filter is being written would suggest names that never
        /// match. The bar is named the same way as in <see cref="Resolve"/>, and a bar
        /// that exists once per workspace needs that workspace bound into the address for
        /// the same reason.
        /// </remarks>
        /// <param name="renderContext">The context the dialog is rendered in.</param>
        /// <returns>
        /// The address, or null when the view is not one that has a bar with user-defined
        /// filters, or when a per-workspace bar was named without its workspace. A prompt
        /// without an address stays a syntax-highlighting editor without suggestions.
        /// </returns>
        public static string ResolveWql(IRenderControlContext renderContext)
        {
            var view = renderContext?.Request?.GetParameter("view")?.Value;
            var context = renderContext?.Request?.GetParameter("context")?.Value;

            switch (view)
            {
                case global::KleeneStar.Core.WWW.Api._1_.Tenants.Quickfilter.ViewKey:
                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Tenants.Wql>()?.ToString();

                case global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Quickfilter.ViewKey:
                    if (string.IsNullOrWhiteSpace(context))
                    {
                        return null;
                    }

                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Issues._workspacekey_.Wql>()?
                        .BindParameters(new WorkspaceKeyParameter(context))?
                        .ToString();

                case global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.KanbanQuickfilter.ViewKey:
                    if (string.IsNullOrWhiteSpace(context))
                    {
                        return null;
                    }

                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Assets._workspacekey_.Wql>()?
                        .BindParameters(new WorkspaceKeyParameter(context))?
                        .ToString();

                case global::KleeneStar.Core.WWW.Api._1_.SavedSearch._savedsearchid_.Quickfilter.ViewKey:
                    // a saved search runs over the objects of every workspace
                    return CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.Objects.Wql>()?.ToString();

                default:
                    return null;
            }
        }
    }
}
