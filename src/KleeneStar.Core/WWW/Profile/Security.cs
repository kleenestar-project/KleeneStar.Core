using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Profile
{
    /// <summary>
    /// Security page — password, two-factor authentication, recovery codes, login alerts.
    /// </summary>
    /// <remarks>
    /// The password card is <see cref="WebFragment.Profile.ProfilePasswordFormFragment"/>: the
    /// form for an internal account, a pointer to its source for an external one. The rows
    /// below it describe what is not built yet.
    /// </remarks>
    [Title("kleenestar.core:profile.security.title")]
    [WebIcon<IconShieldHalved>]
    [Scope<IScopeGeneral>]
    public sealed class Security : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Security()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext, "kleenestar.core:profile.security.title");

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.security.header"),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.security.description"),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            var table = new ControlTable()
            {
                Striped = _ => TypeStripedTable.Row,
                SuppressHeaders = _ => true
            }
                .AddColumn("")
                .AddColumn("");

            AddRow(table, renderContext, "kleenestar.core:profile.security.twofa.label", "kleenestar.core:profile.security.twofa.help");
            AddRow(table, renderContext, "kleenestar.core:profile.security.recovery.label", "kleenestar.core:profile.security.recovery.help");
            AddRow(table, renderContext, "kleenestar.core:profile.security.sso.label", "kleenestar.core:profile.security.sso.help");
            AddRow(table, renderContext, "kleenestar.core:profile.security.loginalerts.label", "kleenestar.core:profile.security.loginalerts.help");

            visualTree.Content.MainPanel.AddPrimary(table);
        }

        /// <summary>
        /// Adds a row with a translated label and value to the control table.
        /// </summary>
        /// <param name="table">
        /// The control table to add the row to.
        /// </param>
        /// <param name="renderContext">
        /// The render context used for translating the label and value.
        /// </param>
        /// <param name="labelKey">
        /// The translation key for the label cell.
        /// </param>
        /// <param name="valueKey">
        /// The translation key for the value cell.
        /// </param>
        private static void AddRow(IControlTable table, IRenderContext renderContext, string labelKey, string valueKey)
        {
            table.AddRow
            (
                new ControlTableCell() { Text = _ => I18N.Translate(renderContext, labelKey) },
                new ControlTableCellPanel().Add(new ControlText() { Text = _ => I18N.Translate(renderContext, valueKey) })
            );
        }
    }
}
