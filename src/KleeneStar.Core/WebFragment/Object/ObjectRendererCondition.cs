using KleeneStar.Core.WebParameter;
using System;
using System.Runtime.CompilerServices;
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
        /// The renderer in effect for the object each request addresses.
        /// </summary>
        /// <remarks>
        /// Every gated fragment asks the same question of the same request, and there are
        /// several of them on one page - the reading view, the editor, the edit button, once
        /// per renderer - each of which would otherwise read the object and its class again,
        /// before the one that wins reads the object a further time in its own render. The
        /// answer depends on nothing but the request, so it is resolved once and kept beside
        /// it; the table holds no request alive, so an entry dies when the request does.
        /// </remarks>
        private static readonly ConditionalWeakTable<IRequest, string[]> _resolved = new();

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
            var effective = Resolve(request);

            return effective is not null && string.Equals
            (
                effective,
                Model.Entities.ObjectRenderer.Normalize(Renderer),
                StringComparison.OrdinalIgnoreCase
            );
        }

        /// <summary>
        /// Answers the renderer key the object addressed by the request is read and written
        /// through, resolving it at most once per request.
        /// </summary>
        /// <param name="request">The request the condition is evaluated for.</param>
        /// <returns>
        /// The effective renderer key, or <see langword="null"/> when the request addresses
        /// no object.
        /// </returns>
        private static string Resolve(IRequest request)
        {
            if (request is null)
            {
                return null;
            }

            // the box is a one-element array rather than the key itself, because "resolved to
            // nothing" has to be storable and distinguishable from "not resolved yet"
            if (_resolved.TryGetValue(request, out var cached))
            {
                return cached[0];
            }

            var @object = CoreHub.ObjectManager.GetObjectByKey(request.GetParameter<ObjectKeyParameter>()?.Value);
            var @class = @object is null ? null : CoreHub.ClassManager.GetClass(@object.ClassId);

            // a class that is gone answers nothing rather than the default of a kind nobody
            // declared, so no surface draws around an object that cannot be described - the
            // reading ObjectRendererCatalog.IsRenderedAs has always had of a null class
            var key = @class is null ? null : ObjectRendererCatalog.ResolveKey(@class);

            _resolved.AddOrUpdate(request, [key]);

            return key;
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
