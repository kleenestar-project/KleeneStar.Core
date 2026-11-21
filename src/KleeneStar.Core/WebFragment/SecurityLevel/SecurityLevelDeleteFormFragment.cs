using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.SecurityLevel
{
    /// <summary>
    /// Represents the delete form fragment of a security level.
    /// </summary>
    /// <remarks>
    /// Deleting a level declassifies every object that carried it, which is the one consequence
    /// somebody about to confirm the dialog cannot see from the list they came from. The count
    /// is therefore stated on the dialog.
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.SecurityLevel._securitylevelid_.Delete>]
    [Cache]
    public sealed class SecurityLevelDeleteFormFragment : FragmentControlDataFormDelete
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public SecurityLevelDeleteFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.SecurityLevels.Index>();
            ItemId = renderContext =>
            {
                var securityLevelId = renderContext.Request.GetParameter<SecurityLevelIdParameter>();
                return securityLevelId?.Value?.ToString();
            };

            // the standard confirmation says the record will be removed; what it cannot say is
            // what happens to the objects the level guards, so the sentence is extended rather
            // than replaced
            Content.Text = renderContext => Describe(renderContext);
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// States how many objects the level currently guards, so the confirmation says what
        /// will be declassified rather than only what will be removed.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The confirmation text.</returns>
        private static string Describe(IRenderControlContext renderContext)
        {
            var standard = I18N.Translate(renderContext, "webexpress.webui:delete.description");
            var parameter = renderContext?.Request?.GetParameter<SecurityLevelIdParameter>();
            var securityLevelId = Guid.TryParse(parameter?.Value, out var id) ? id : Guid.Empty;

            if (securityLevelId == Guid.Empty)
            {
                return standard;
            }

            // the count has to see every record, not only the ones the administrator reading
            // the dialog happens to be cleared for
            int count;

            using (CoreHub.SecurityLevelManager.BeginUnrestricted())
            {
                count = CoreHub.ObjectManager.CountObjects
                (
                    new Query<Model.Entities.Object>().Where(x => x.SecurityLevelId == securityLevelId)
                );
            }

            return count == 0
                ? standard
                : $"{standard} {I18N.Translate(renderContext, "kleenestar.core:securitylevel.delete.warning", count)}";
        }
    }
}
