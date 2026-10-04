using KleeneStar.Core.WebInsight;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The type picker of the insight create dialog: the insight types the
    /// <see cref="InsightTypeCatalog"/> knows.
    /// </summary>
    /// <remarks>
    /// The options cannot be a snapshot taken in a constructor: the dialog is a cached fragment
    /// built once, while a plugin registers its type from its own initialization - after the
    /// core's components, by construction. They are therefore projected from the catalog when
    /// it has changed, watched through <see cref="InsightTypeCatalog.Version"/>, the way the
    /// renderer picker of the class dialogs follows its catalog.
    /// <para>
    /// The picker stands on the create dialog only. The type is chosen once: the content of one
    /// type means nothing to another, so the edit dialog does not offer it and the endpoint
    /// refuses a change.
    /// </para>
    /// </remarks>
    public sealed class InsightTypeSelectionControl : ControlFormItemInputSelection
    {
        private readonly object _sync = new();
        private int _projected = -1;

        /// <summary>
        /// Initializes a new instance of the class, labelled and explained the way the create
        /// dialog presents it.
        /// </summary>
        public InsightTypeSelectionControl()
        {
            Name = _ => nameof(Model.Entities.Insight.Type);
            Label = _ => "kleenestar.core:insight.type.label";
            Help = _ => "kleenestar.core:insight.type.help";
            Placeholder = _ => "kleenestar.core:insight.type.placeholder";
            Required = _ => true;
        }

        /// <summary>
        /// Renders the picker, projecting the catalog first when it has changed since the last
        /// render.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            Project();

            return base.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Replaces the options with the current contents of the catalog, unless they already
        /// describe it.
        /// </summary>
        private void Project()
        {
            var version = InsightTypeCatalog.Version;

            if (Volatile.Read(ref _projected) == version)
            {
                return;
            }

            lock (_sync)
            {
                if (_projected == version)
                {
                    return;
                }

                foreach (var stale in Options.ToList())
                {
                    Remove(stale);
                }

                Add(InsightTypeCatalog.Types
                    .Select(type => new ControlFormItemInputSelectionItem(type.Key)
                    {
                        Text = _ => type.Label,
                        Icon = _ => type.Icon,
                        // a fresh insight is a dashboard unless told otherwise; the form also
                        // loads the default from the endpoint, this covers a form opened bare
                        Selected = _ => InsightTypeCatalog.Normalize(type.Key) == InsightTypeCatalog.Default
                    }));

                Volatile.Write(ref _projected, version);
            }
        }
    }
}
