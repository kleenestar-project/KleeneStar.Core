using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// Shared base for the conditions that gate the renderer-specific surfaces: a fragment
    /// that draws one renderer is rendered only where the object the request addresses is
    /// rendered by that one.
    /// </summary>
    /// <remarks>
    /// This is what keeps the renderers apart without any of them knowing about the others.
    /// Two fragments may sit on the same route, both scoped to it, and exactly one of them
    /// will draw - so adding a third renderer means adding fragments and a condition, never
    /// editing the ones already there.
    /// <para>
    /// A concrete subclass names one renderer. The subclasses exist because a condition is
    /// bound through <c>[Condition&lt;T&gt;]</c>, which takes a type rather than a value -
    /// they carry no logic of their own, and an add-on renderer contributes one of exactly
    /// this shape.
    /// </para>
    /// </remarks>
    public abstract class ObjectRendererCondition : ICondition
    {
        /// <summary>
        /// Gets the renderer key the condition asks about.
        /// </summary>
        protected abstract string Renderer { get; }

        /// <summary>
        /// Determines whether the object addressed by the request is rendered by this
        /// condition's renderer.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>
        /// True when the addressed object's class renders through the renderer. A request
        /// that addresses no object answers false, so a surface is left out rather than
        /// drawn around nothing.
        /// </returns>
        public bool Fulfillment(IRequest request)
        {
            var @object = CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);

            if (@object is null)
            {
                return false;
            }

            var @class = CoreHub.ClassManager.GetClass(@object.ClassId);

            return ObjectRendererCatalog.IsRenderedAs(@class, Renderer);
        }
    }

    /// <summary>
    /// Gates the surfaces of the prose renderer: the WYSIWYG editor and the reading view
    /// that presents what it wrote.
    /// </summary>
    public sealed class ProseRendererCondition : ObjectRendererCondition
    {
        /// <inheritdoc/>
        protected override string Renderer => Model.Entities.ObjectRenderer.Prose;
    }

    /// <summary>
    /// Gates the surfaces of the form renderer: the structured input mask and the
    /// unchangeable view of what it captured.
    /// </summary>
    public sealed class FormRendererCondition : ObjectRendererCondition
    {
        /// <inheritdoc/>
        protected override string Renderer => Model.Entities.ObjectRenderer.Form;
    }
}
