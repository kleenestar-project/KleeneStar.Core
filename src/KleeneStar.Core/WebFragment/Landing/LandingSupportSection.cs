using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The help area: three columns - short how-tos, frequently asked questions, and first
    /// steps - each filled from the pages the organization has labelled for it.
    /// </summary>
    /// <remarks>
    /// The area writes no help text of its own. Help pages are ordinary objects (pages) of the
    /// installation, and the label on such a page is what files it under one of the three
    /// headings (see <see cref="LandingLabel"/>). That keeps the help editable by the people
    /// who know the answers, in the editor they already use, and versioned, searchable and
    /// translatable like every other page.
    /// <para>
    /// The three columns read the same data differently, because that is how the three kinds
    /// of question are asked: a how-to is looked up by name, so it is a list of links; a
    /// question is answered in place, so it opens beneath its heading; a first step is worked
    /// through, so it is a numbered path ending in the action it leads to.
    /// </para>
    /// </remarks>
    internal static class LandingSupportSection
    {
        /// <summary>
        /// The maximum number of pages listed per column.
        /// </summary>
        private const int MaxItems = 5;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="tagManager">The tag manager holding the label rows.</param>
        /// <param name="objectManager">The object manager used to resolve the labelled pages.</param>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The section control.</returns>
        public static IControl Build
        (
            IObjectTagManager tagManager,
            IObjectManager objectManager,
            IRenderControlContext renderContext,
            IVisualTreeControl visualTree
        )
        {
            var guides = LandingLabel.Resolve(tagManager, objectManager, LandingLabel.Help, MaxItems);
            var questions = LandingLabel.Resolve(tagManager, objectManager, LandingLabel.Faq, MaxItems);
            var steps = LandingLabel.Resolve(tagManager, objectManager, LandingLabel.FirstSteps, MaxItems);

            var section = new ControlSection("landing-support")
            {
                Header = _ => "kleenestar.core:landing.support.heading",
                HeaderIcon = _ => new IconLifeRing(),
                Note = _ => "kleenestar.core:landing.support.hint",
                Layout = _ => TypeLayoutSection.Rule
            };

            // the three columns are one statement about one subject, so they share a surface
            // and are divided by a rule rather than floating beside each other
            section.Add(new ControlGroup
            (
                "landing-support-columns",
                BuildGuides(guides),
                BuildQuestions(questions),
                BuildSteps(steps)
            )
            {
                Columns = _ => 3,
                Spacing = _ => TypeSpacingGroup.Wide
            });

            return section;
        }

        /// <summary>
        /// Builds a column: its own heading and the body beneath it.
        /// </summary>
        /// <param name="key">The short id suffix of the column.</param>
        /// <param name="title">The resource key of the column title.</param>
        /// <param name="icon">The icon beside the title.</param>
        /// <param name="body">The body of the column.</param>
        /// <returns>The column control.</returns>
        private static IControl BuildColumn(string key, string title, IIcon icon, IControl body)
        {
            var column = new ControlSection("landing-support-" + key)
            {
                Header = _ => title,
                HeaderIcon = _ => icon,
                Uppercase = _ => false,
                Layout = _ => TypeLayoutSection.Stacked,
                Classes = ["ks-landing-help-col"]
            };

            column.Add(body);

            return column;
        }

        /// <summary>
        /// Builds the how-to column: one row per labelled page.
        /// </summary>
        /// <param name="pages">The labelled pages.</param>
        /// <returns>The column control.</returns>
        private static IControl BuildGuides(IReadOnlyList<Model.Entities.Object> pages)
        {
            if (pages.Count == 0)
            {
                return BuildColumn("help", "kleenestar.core:landing.support.help.label", new IconFileLines(), BuildEmpty("kleenestar.core:landing.support.help.empty"));
            }

            var list = new ControlList("landing-support-help-list");

            foreach (var page in pages)
            {
                list.Add(new ControlListItemLink("landing-support-help-" + page.Id.ToString("N"))
                {
                    Text = _ => page.Summary,
                    Tooltip = _ => page.Key,
                    Icon = _ => new IconFileLines(),
                    Uri = _ => ObjectKindCatalog.ResolveDetailUri(page)
                });
            }

            return BuildColumn("help", "kleenestar.core:landing.support.help.label", new IconFileLines(), list);
        }

        /// <summary>
        /// Builds the questions column: each page becomes an accordion entry whose header is
        /// the question and whose body is the answer. The first one starts open, so the column
        /// shows what it is rather than a row of closed headings.
        /// </summary>
        /// <param name="pages">The labelled pages.</param>
        /// <returns>The column control.</returns>
        private static IControl BuildQuestions(IReadOnlyList<Model.Entities.Object> pages)
        {
            if (pages.Count == 0)
            {
                return BuildColumn("faq", "kleenestar.core:landing.support.faq.label", new IconCircleQuestion(), BuildEmpty("kleenestar.core:landing.support.faq.empty"));
            }

            var accordion = new ControlAccordion("landing-support-faq-list")
            {
                Flush = _ => true
            };

            var first = true;

            foreach (var page in pages)
            {
                // captured into a local: the lambda is evaluated at render time, by which point
                // the loop has long since set the flag to false and nothing would open
                var expanded = first;

                var item = new ControlAccordionItem("landing-support-faq-" + page.Id.ToString("N"))
                {
                    Header = _ => page.Summary,
                    Expanded = _ => expanded
                };

                item.Add(new ControlText()
                {
                    Text = _ => ProseText.ToPlainText(page.Description) ?? string.Empty,
                    TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                    Format = _ => TypeFormatText.Paragraph
                });

                accordion.Add(item);
                first = false;
            }

            return BuildColumn("faq", "kleenestar.core:landing.support.faq.label", new IconCircleQuestion(), accordion);
        }

        /// <summary>
        /// Builds the first-steps column: the labelled pages as a path to work through, ending
        /// in the action the column leads to.
        /// </summary>
        /// <param name="pages">The labelled pages.</param>
        /// <returns>The column control.</returns>
        private static IControl BuildSteps(IReadOnlyList<Model.Entities.Object> pages)
        {
            if (pages.Count == 0)
            {
                return BuildColumn("firststeps", "kleenestar.core:landing.support.firststeps.label", new IconShoePrints(), BuildEmpty("kleenestar.core:landing.support.firststeps.empty"));
            }

            var panel = new ControlPanel("landing-support-firststeps-body");

            // vertical, because the steps are read as a list of things to do rather than as a
            // progress bar across the top of a wizard
            var steps = new ControlSteps("landing-support-firststeps-list")
            {
                Vertical = _ => true
            };

            foreach (var page in pages)
            {
                steps.Add(new ControlStepsItem("landing-support-step-" + page.Id.ToString("N"))
                {
                    Label = _ => page.Summary,
                    Description = _ => page.Key
                });
            }

            panel.Add(steps);

            panel.Add(new ControlButton("landing-steps-create")
            {
                Text = _ => "kleenestar.core:landing.action.create",
                Icon = _ => new IconPlus(),
                Size = _ => TypeSizeButton.Small,
                BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Highlight),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.None),
                PrimaryAction = _ => new ActionModal
                (
                    "modal-form",
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Objects.Add>(),
                    TypeModalSize.ExtraLarge
                )
            });

            return BuildColumn("firststeps", "kleenestar.core:landing.support.firststeps.label", new IconShoePrints(), panel);
        }

        /// <summary>
        /// Builds the message shown while a column has nothing labelled for it.
        /// </summary>
        /// <param name="message">The resource key of the message.</param>
        /// <returns>The text control.</returns>
        private static IControl BuildEmpty(string message)
        {
            return new ControlText()
            {
                Text = _ => message,
                TextColor = _ => new PropertyColorText(TypeColorText.Secondary),
                Format = _ => TypeFormatText.Paragraph
            };
        }
    }
}
