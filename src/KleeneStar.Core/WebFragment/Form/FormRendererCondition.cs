using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebCore.WebCondition;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Core.WebFragment.Form
{
    /// <summary>
    /// Decides, for a route that names a form, whether the class the form belongs to renders
    /// its objects as prose.
    /// </summary>
    /// <remarks>
    /// A form describes the fields a structured input mask asks for and a filled-in sheet
    /// shows; a class whose objects open as prose in the WYSIWYG editor reads neither, so its
    /// forms arrange nothing that is ever drawn. The form editor is therefore withheld on such a
    /// class and a notice stands in its place - two fragments scoped to the same page, gated on
    /// the two answers this condition gives, the way the reading views of an object are gated
    /// on <see cref="ObjectRendererCondition"/>.
    /// <para>
    /// The answer is the effective renderer of the class, <see cref="ObjectRendererCatalog.ResolveKey(Model.Entities.Class)"/>,
    /// not the stored key: a class that names nothing follows its kind, and a blog class that
    /// stored <c>form</c> before its kind declined it reads as prose. A route that names no
    /// form, or a form whose class is gone, is neither prose nor structured - both fragments
    /// stay away and the page reports the form as not found on its own.
    /// </para>
    /// </remarks>
    public abstract class FormRendererCondition : ICondition
    {
        /// <summary>
        /// Gets whether the condition is fulfilled by a class that renders as prose
        /// (<see langword="true"/>) or by one that does not (<see langword="false"/>).
        /// </summary>
        protected abstract bool Prose { get; }

        /// <summary>
        /// Determines whether the condition is fulfilled for the request.
        /// </summary>
        /// <param name="request">The request whose route names the form.</param>
        /// <returns><see langword="true"/> when the form's class answers the expected way.</returns>
        public bool Fulfillment(IRequest request)
        {
            var @class = ResolveClass(request);

            if (@class is null)
            {
                return false;
            }

            return ObjectRendererCatalog.IsRenderedAs(@class, Model.Entities.ObjectRenderer.Prose) == Prose;
        }

        /// <summary>
        /// Resolves the class of the form the route names.
        /// </summary>
        /// <param name="request">The request whose route names the form.</param>
        /// <returns>The class, or <see langword="null"/> when the route names no form or the
        /// form's class cannot be resolved.</returns>
        private static Model.Entities.Class ResolveClass(IRequest request)
        {
            if (!Guid.TryParse(request?.GetParameter<FormIdParameter>()?.Value, out var formId) || formId == Guid.Empty)
            {
                return null;
            }

            var form = CoreHub.FormManager.GetForm(formId);

            return form is null ? null : form.Class ?? CoreHub.ClassManager.GetClass(form.ClassId);
        }
    }

    /// <summary>
    /// Fulfilled when the class of the form the route names renders its objects as prose - the
    /// case in which the form editor has nothing to edit.
    /// </summary>
    public sealed class FormProseRendererCondition : FormRendererCondition
    {
        /// <inheritdoc/>
        protected override bool Prose => true;
    }

    /// <summary>
    /// Fulfilled when the class of the form the route names renders its objects through a
    /// structured mask - the case in which the form editor is offered.
    /// </summary>
    public sealed class FormFieldRendererCondition : FormRendererCondition
    {
        /// <inheritdoc/>
        protected override bool Prose => false;
    }
}
