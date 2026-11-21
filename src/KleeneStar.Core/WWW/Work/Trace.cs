using KleeneStar.Core.WebPlanning;
using KleeneStar.Core.WebParameter;
using System.Linq;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Work
{
    /// <summary>
    /// The traceability of an object (<c>?object=KEY</c>): what it comes from, upstream, and
    /// what follows from it, downstream - requirement, task, change, release - over every
    /// relation the installation uses.
    /// </summary>
    [WebIcon<IconSitemap>]
    [Title("kleenestar.core:planning.trace.title")]
    [Scope<IScopeGeneral>]
    public sealed class Trace : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            string Translate(string key) => I18N.Translate(renderContext, key);

            var @object = CoreHub.ObjectManager.GetObjectByKey(renderContext.Request.GetParameter("object")?.Value ?? string.Empty);

            visualTree.Content.MainPanel.Headline.Title = "kleenestar.core:planning.trace.title";

            if (@object is null || Traceability.Trace(@object.Id) is not { } trace)
            {
                return;
            }

            var heading = $"{@object.Key} · {@object.Summary}";

            visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => heading, Format = _ => TypeFormatText.H5 });

            foreach (var upstream in new[] { true, false })
            {
                var title = Translate(upstream ? "kleenestar.core:planning.trace.upstream" : "kleenestar.core:planning.trace.downstream");
                var nodes = trace.Nodes.Where(x => x.Upstream == upstream).ToList();

                visualTree.Content.MainPanel.AddPrimary(new ControlText()
                {
                    Text = _ => title,
                    Format = _ => TypeFormatText.H6,
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Three, PropertySpacing.Space.One)
                });

                if (nodes.Count == 0)
                {
                    visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => "-", TextColor = _ => new PropertyColorText(TypeColorText.Secondary) });
                    continue;
                }

                var table = new ControlTable() { Striped = _ => TypeStripedTable.Row }
                    .AddColumn(Translate("kleenestar.core:planning.trace.distance"))
                    .AddColumn(Translate("kleenestar.core:planning.trace.relation"))
                    .AddColumn(Translate("kleenestar.core:planning.trace.object"))
                    .AddColumn(Translate("kleenestar.core:planning.trace.class"))
                    .AddColumn(Translate("kleenestar.core:planning.trace.done"));

                foreach (var node in nodes)
                {
                    var distance = new string('›', node.Distance);
                    var via = Translate(node.Via);
                    var label = $"{node.Object.Key} · {node.Object.Summary}";
                    var uri = CoreHub.GetUri<Issue._objectkey_.Index>()?.BindParameters(new ObjectKeyParameter(node.Object.Key));
                    var @class = CoreHub.ClassManager.GetClass(node.Object.ClassId)?.Name ?? string.Empty;
                    var done = WebManager.PlanningManager.IsDone(node.Object) ? "✓" : string.Empty;

                    table.AddRow
                    (
                        new ControlTableCell() { Text = _ => distance },
                        new ControlTableCell() { Text = _ => via },
                        new ControlTableCellPanel().Add(new ControlLink() { Text = _ => label, Uri = _ => uri }),
                        new ControlTableCell() { Text = _ => @class },
                        new ControlTableCell() { Text = _ => done }
                    );
                }

                visualTree.Content.MainPanel.AddPrimary(table);
            }

            if (trace.Hidden > 0 || trace.Truncated)
            {
                var notice = string.Format(Translate("kleenestar.core:planning.trace.hidden"), trace.Hidden);

                visualTree.Content.MainPanel.AddPrimary(new ControlText() { Text = _ => notice, TextColor = _ => new PropertyColorText(TypeColorText.Secondary), Format = _ => TypeFormatText.Small });
            }
        }
    }
}
