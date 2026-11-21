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
    /// Notification preferences — which events deliver to which channel.
    /// </summary>
    [Title("kleenestar.core:profile.notifications.title")]
    [WebIcon<IconBell>]
    [Scope<IScopeGeneral>]
    public sealed class Notifications : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Notifications()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(renderContext, "kleenestar.core:profile.notifications.title");

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.notifications.header"),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:profile.notifications.description"),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            var table = new ControlTable()
            {
                Striped = _ => TypeStripedTable.Row
            }
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:profile.notifications.column.event"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:profile.notifications.column.email"))
                .AddColumn(I18N.Translate(renderContext, "kleenestar.core:profile.notifications.column.inapp"));

            AddRow(table, renderContext, "kleenestar.core:profile.notifications.event.mention");
            AddRow(table, renderContext, "kleenestar.core:profile.notifications.event.assign");
            AddRow(table, renderContext, "kleenestar.core:profile.notifications.event.comment");
            AddRow(table, renderContext, "kleenestar.core:profile.notifications.event.status");
            AddRow(table, renderContext, "kleenestar.core:profile.notifications.event.review");
            AddRow(table, renderContext, "kleenestar.core:profile.notifications.event.digest");

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
        private static void AddRow(IControlTable table, IRenderContext renderContext, string labelKey)
        {
            var on = I18N.Translate(renderContext, "kleenestar.core:profile.notifications.on");

            table.AddRow
            (
                new ControlTableCell() { Text = _ => I18N.Translate(renderContext, labelKey) },
                new ControlTableCellPanel().Add(new ControlText() { Text = _ => on }),
                new ControlTableCellPanel().Add(new ControlText() { Text = _ => on })
            );
        }
    }
}
