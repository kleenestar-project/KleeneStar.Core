using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebRestApi;
using System;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Class
{
    /// <summary>
    /// The start page of a class: how it is used, where its objects stand, what changed last and
    /// what is missing in its setup (<see cref="ClassOverview"/>).
    /// </summary>
    /// <remarks>
    /// It replaced a dashboard of big numbers that repeated the counts of the sidebar beside it.
    /// Three blocks, laid out as a grid in <c>kleenestar.css</c> (<c>.ks-class-overview</c>):
    /// usage (key figures, a stacked bar of the status categories, a twelve-week column chart of
    /// new objects), the setup check list (each line links where it is fixed) and the objects
    /// changed last.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Class._classid_.Index>]
    [Cache]
    public sealed class ClassOverviewFragment : FragmentControlPanel
    {
        private const string Prefix = "kleenestar.core:class.overview.";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ClassOverviewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the overview of the class the route names.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The overview, or <see langword="null"/> when the route names no class.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var @class = CoreHub.ClassManager.GetClass(renderContext?.Request?.GetParameter<ClassIdParameter>());

            if (@class is null)
            {
                return null;
            }

            var overview = ClassOverview.Build(@class, DateTime.UtcNow);

            var grid = new ControlPanel("class-overview")
            {
                Classes = ["ks-class-overview"]
            };

            grid.Add
            (
                Block("class-overview-usage", "ks-co-usage", "usage.header", new IconChartColumn(), Usage(renderContext, overview)),
                Block("class-overview-setup", "ks-co-setup", "setup.header", new IconListCheck(), Setup(renderContext, overview)),
                Block("class-overview-recent", "ks-co-recent", "recent.header", new IconClockRotateLeft(), Recent(renderContext, overview))
            );

            return grid.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Wraps a block of the overview into a section of its own.
        /// </summary>
        private static IControl Block(string id, string css, string header, IIcon icon, IControl content)
        {
            var section = new ControlSection(id)
            {
                Header = _ => Prefix + header,
                HeaderIcon = _ => icon,
                Layout = _ => TypeLayoutSection.Rule,
                Classes = [css]
            };

            section.Add(content);

            return section;
        }

        /// <summary>
        /// Builds the usage block: key figures, the status distribution and the weekly history.
        /// </summary>
        private static IControl Usage(IRenderControlContext renderContext, ClassOverview overview)
        {
            var panel = new ControlPanel();

            var figures = new ControlPanel { Classes = ["ks-co-figures"] };

            figures.Add(Figure(overview.Total, "kpi.total"));

            if (overview.Open is { } open)
            {
                figures.Add(Figure(open, "kpi.open"));
            }

            figures.Add(Figure(overview.CreatedRecently, "kpi.created"));
            figures.Add(Figure(overview.UpdatedRecently, "kpi.updated"));
            panel.Add(figures);

            if (overview.Total > 0 && overview.Categories.Count > 0)
            {
                panel.Add(Caption("status.header"));

                var bar = new ControlPanel { Classes = ["ks-co-bar"] };
                var legend = new ControlPanel { Classes = ["ks-co-legend"] };

                foreach (var share in overview.Categories)
                {
                    var percent = 100.0 * share.Count / overview.Total;
                    var tone = Tone(share.ColorCss);
                    var label = share.Label ?? I18N.Translate(renderContext, Prefix + "status.none");

                    bar.Add(new ControlText
                    {
                        Text = _ => string.Empty,
                        Title = _ => label + ": " + share.Count,
                        Format = _ => TypeFormatText.Span,
                        Classes = ["ks-co-seg", tone],
                        Styles = ["width: " + percent.ToString("0.##", CultureInfo.InvariantCulture) + "%;"]
                    });

                    legend.Add(new ControlText
                    {
                        Text = _ => label + " " + share.Count.ToString(CultureInfo.CurrentCulture),
                        Format = _ => TypeFormatText.Span,
                        Classes = ["ks-co-legend-item", tone]
                    });
                }

                panel.Add(bar, legend);
            }

            panel.Add(Caption("weekly.header"));

            var max = Math.Max(1, overview.WeeklyCreated.DefaultIfEmpty(0).Max());
            var columns = new ControlPanel { Classes = ["ks-co-weeks"] };
            var weekStart = DateTime.UtcNow.Date.AddDays(-(((int)DateTime.UtcNow.DayOfWeek + 6) % 7) - 7 * (ClassOverview.Weeks - 1));

            for (var i = 0; i < overview.WeeklyCreated.Count; i++)
            {
                var count = overview.WeeklyCreated[i];
                var start = weekStart.AddDays(7 * i);
                var height = count == 0 ? 0 : Math.Max(4, 100.0 * count / max);

                var week = new ControlPanel { Classes = ["ks-co-week"] };

                week.Add(new ControlText
                {
                    Text = _ => string.Empty,
                    Title = _ => start.ToString("d", renderContext.Request?.Culture ?? CultureInfo.CurrentCulture) + ": " + count,
                    Format = _ => TypeFormatText.Span,
                    Classes = ["ks-co-week-bar"],
                    Styles = ["height: " + height.ToString("0.##", CultureInfo.InvariantCulture) + "%;"]
                });

                columns.Add(week);
            }

            panel.Add(columns);

            return panel;
        }

        /// <summary>
        /// Builds one key figure.
        /// </summary>
        private static IControl Figure(int value, string label)
        {
            return new ControlPanel
            (
                null,
                new ControlText { Text = _ => value.ToString("N0", CultureInfo.CurrentCulture), Classes = ["ks-co-figure-value"] },
                new ControlText { Text = _ => Prefix + label, Format = _ => TypeFormatText.Small, Classes = ["ks-co-figure-label"] }
            )
            {
                Classes = ["ks-co-figure"]
            };
        }

        /// <summary>
        /// Builds a small caption above a chart.
        /// </summary>
        private static IControl Caption(string key)
        {
            return new ControlText { Text = _ => Prefix + key, Format = _ => TypeFormatText.Small, Classes = ["ks-co-caption"] };
        }

        /// <summary>
        /// Builds the setup check list.
        /// </summary>
        private static IControl Setup(IRenderControlContext renderContext, ClassOverview overview)
        {
            var list = new ControlPanel { Classes = ["ks-co-checks"] };
            var culture = renderContext.Request?.Culture ?? CultureInfo.CurrentCulture;

            foreach (var check in overview.Checks)
            {
                var text = string.Format(culture, I18N.Translate(renderContext, Prefix + "check." + check.Key), check.Args ?? []);
                var state = check.State.ToString().ToLowerInvariant();

                IIcon icon = check.State switch
                {
                    SetupCheckState.Warning => new IconTriangleExclamation(),
                    SetupCheckState.Info => new IconCircleInfo(),
                    _ => new IconCircleCheck()
                };

                var row = new ControlPanel { Classes = ["ks-co-check", "ks-co-check-" + state] };
                row.Add(new ControlIcon { Icon = _ => icon, Classes = ["ks-co-check-icon"] });

                if (check.Uri is null)
                {
                    row.Add(new ControlText { Text = _ => text, Format = _ => TypeFormatText.Span });
                }
                else
                {
                    var uri = check.Uri;

                    row.Add(new ControlLink
                    {
                        Text = _ => text,
                        Uri = check.Modal ? null : _ => uri,
                        PrimaryAction = check.Modal ? _ => new ActionModal("modal-form", uri, TypeModalSize.ExtraLarge) : null
                    });
                }

                list.Add(row);
            }

            return list;
        }

        /// <summary>
        /// Builds the list of the objects changed last.
        /// </summary>
        private static IControl Recent(IRenderControlContext renderContext, ClassOverview overview)
        {
            if (overview.Recent.Count == 0)
            {
                return new ControlText { Text = _ => Prefix + "recent.empty", Format = _ => TypeFormatText.Paragraph, Classes = ["ks-co-empty"] };
            }

            var culture = renderContext.Request?.Culture ?? CultureInfo.CurrentCulture;
            var list = new ControlPanel { Classes = ["ks-co-recent-list"] };

            foreach (var item in overview.Recent)
            {
                var @object = item.Object;
                var uri = ObjectKindCatalog.ResolveDetailUri(@object);
                var row = new ControlPanel { Classes = ["ks-co-recent-row"] };

                row.Add
                (
                    new ControlText { Text = _ => @object.Key, Format = _ => TypeFormatText.Small, Classes = ["ks-co-recent-key"] },
                    new ControlLink { Text = _ => @object.Summary, Uri = _ => uri, Classes = ["ks-co-recent-summary"] }
                );

                if (item.Category is not null)
                {
                    var tone = Tone(ObjectBoardProjection.CategoryColorCss(item.Category));

                    row.Add(new ControlText
                    {
                        Text = _ => ObjectBoardProjection.CategoryLabel(item.Category),
                        Format = _ => TypeFormatText.Span,
                        Classes = ["ks-co-chip", tone]
                    });
                }

                row.Add(new ControlText
                {
                    Text = _ => @object.Updated.ToLocalTime().ToString("g", culture),
                    Format = _ => TypeFormatText.Small,
                    Classes = ["ks-co-recent-when"]
                });

                list.Add(row);
            }

            return list;
        }

        /// <summary>
        /// Maps the boards' category color class onto the overview's tone class.
        /// </summary>
        private static string Tone(string colorCss)
        {
            return "ks-co-tone-" + (colorCss ?? "wx-color-secondary").Replace("wx-color-", string.Empty);
        }
    }
}
