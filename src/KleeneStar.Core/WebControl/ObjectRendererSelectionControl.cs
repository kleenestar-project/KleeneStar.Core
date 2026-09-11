using KleeneStar.Core.WebFragment.Object;
using System;
using System.Linq;
using System.Threading;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The renderer picker of the class dialogs: the entry that clears the field, followed by
    /// the renderers the <see cref="ObjectRendererCatalog"/> knows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a control of its own for two reasons. The first is that the three class dialogs -
    /// add, clone and edit - would otherwise each carry the same twenty lines, which is how
    /// the six avatar dialogs came to disagree with one another; there is one picker here and
    /// the dialogs name it.
    /// </para>
    /// <para>
    /// The second is that the options cannot be a snapshot. The dialogs are cached fragments
    /// built once, while a plugin registers its renderer from its own initialization - after
    /// the core's components, by construction - so a list filled in a constructor would be the
    /// list as it stood before any add-on arrived, and the catalog's whole point is that a
    /// contributed renderer becomes selectable without the dialogs knowing it exists. The
    /// options are therefore projected from the catalog when it has changed, watched through
    /// <see cref="ObjectRendererCatalog.Version"/> so an unchanged catalog costs one read.
    /// </para>
    /// <para>
    /// The list <b>is</b> narrowed to the object type chosen beside it, through the framework's
    /// dependent selection: the control names the type field
    /// (<see cref="ControlFormItemInputSelection.DependsOn"/>) and every entry names the types
    /// it belongs to (<see cref="ControlFormItemInputSelectionItem.Requires"/>), so choosing
    /// <em>blog entry</em> takes the mask out of the list as it is chosen. It is done in the
    /// browser because the type is being picked in the same dialog - there is no request
    /// between the two answers, and a server that only refuses the pairing on submit tells the
    /// user after the fact. That refusal stays where it is
    /// (<c>/api/1/classes</c> names the renderers the type offers) as the authority: a
    /// narrowed list is a courtesy to whoever fills in the dialog, not a guarantee about what
    /// reaches the endpoint.
    /// </para>
    /// <para>
    /// An entry is left <b>without</b> a condition when its renderer is offered on every
    /// registered type, so a renderer that serves everything is not silently pinned to the
    /// types that happened to exist when the list was projected. <em>Follow the object type</em>
    /// carries no condition either - it is the absence of a renderer and fits every type.
    /// </para>
    /// </remarks>
    public sealed class ObjectRendererSelectionControl : ControlFormItemInputSelection
    {
        private readonly object _sync = new();
        private int _projected = -1;

        /// <summary>
        /// Initializes a new instance of the class, labelled and explained the way the class
        /// dialogs present it.
        /// </summary>
        public ObjectRendererSelectionControl()
        {
            Name = _ => nameof(Model.Entities.Class.Renderer);
            Label = _ => "kleenestar.core:class.renderer.label";
            Help = _ => "kleenestar.core:class.renderer.help";

            // the object type of the same dialog is what narrows the list
            DependsOn = _ => nameof(Model.Entities.Class.Kind);

            // the empty state of this field is not "nothing chosen" but "follow the object
            // type", so that is what an empty picker says - including after the type was
            // changed to one the previous renderer does not belong to, which drops the value
            Placeholder = _ => "kleenestar.core:class.renderer.automatic";
        }

        /// <summary>
        /// Renders the picker, projecting the catalog first when it has changed since the
        /// last render.
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
            // both catalogs are read, because what an entry says about itself - the object
            // types it belongs to - is the answer of the two together
            var version = HashCode.Combine(ObjectRendererCatalog.Version, ObjectKindCatalog.Version);

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

                // the entry that clears the field leads the list, the way the home-document
                // selection leads with its empty one - the way back to the default has to be
                // offered, not merely reachable by never having chosen. It travels as a token
                // because an option carries its value in its element id and an empty id is
                // dropped on the client; the endpoint unwraps it back to the unset state
                Add(new ControlFormItemInputSelectionItem(ObjectRendererCatalog.Automatic)
                {
                    Text = _ => "kleenestar.core:class.renderer.automatic"
                });

                var kinds = ObjectKindCatalog.Kinds.Count();

                Add(ObjectRendererCatalog.Renderers
                    .Select(renderer =>
                    {
                        var offered = ObjectRendererCatalog.GetKinds(renderer.Key).ToList();

                        return new ControlFormItemInputSelectionItem(renderer.Key)
                        {
                            Text = _ => renderer.Label,
                            Icon = _ => renderer.Icon,
                            // a renderer every registered type offers states no condition, so
                            // it also fits a type registered after this list was projected;
                            // one that is offered on some of them names those
                            Requires = offered.Count == kinds ? null : _ => offered
                        };
                    }));

                Volatile.Write(ref _projected, version);
            }
        }
    }
}
