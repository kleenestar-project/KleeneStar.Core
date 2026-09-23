using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSettingPage;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings.Identity
{
    /// <summary>
    /// Redirects to the identities index page.
    /// </summary>
    [Title("kleenestar.core:setting.identity.title")]
    [WebIcon<IconUsers>]
    [SettingGroup<SettingGroupSystemGeneral>()]
    [SettingSection(SettingSection.Secondary)]
    [Policy<global::WebExpress.WebCore.WebPolicies.AuthenticatedAccessPolicy>]
    [Scope<IScopeAdmin>]
    public sealed class Index : IPage<VisualTreeWebAppSetting>, IScopeAdmin
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebAppSetting visualTree)
        {
            throw new RedirectException
            (
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Settings.Identities.Index>()
            );
        }
    }
}
