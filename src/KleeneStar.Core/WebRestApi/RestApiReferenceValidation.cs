using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// Validates the references a class-scoped record cannot be stored without.
    /// </summary>
    /// <remarks>
    /// Without these checks a missing reference only surfaces when the database rejects the
    /// insert, which reaches the caller as a bare "validation failed" naming neither the
    /// field nor the reason. Checking it here turns that into a message the form can put on
    /// the field it belongs to.
    /// </remarks>
    public static class RestApiReferenceValidation
    {
        /// <summary>
        /// Ensures the payload names a class that exists.
        /// </summary>
        /// <remarks>
        /// A record that already exists carries its class, so an update or a clone needs no
        /// class in the payload — only a create does.
        /// </remarks>
        /// <param name="result">The validation result to add to.</param>
        /// <param name="fieldMap">The payload.</param>
        /// <param name="request">The request, for the language of the message.</param>
        /// <param name="existingClassId">The class of the persisted record, if there is one.</param>
        /// <returns>The validation result, for chaining.</returns>
        public static IRestApiValidationResult ValidateClass(this IRestApiValidationResult result, RestApiCrudFormData fieldMap, IRequest request, Guid? existingClassId = null)
        {
            if (existingClassId is not null && existingClassId != Guid.Empty)
            {
                return result;
            }

            if (!fieldMap.TryGetGuid(nameof(Model.Entities.Field.ClassId), out var classId) || classId == Guid.Empty)
            {
                return result.Add
                (
                    I18N.Translate(request, "kleenestar.core:validation.class.missing"),
                    nameof(Model.Entities.Field.ClassId),
                    "class.missing"
                );
            }

            if (CoreHub.ClassManager.GetClass(classId) is null)
            {
                return result.Add
                (
                    I18N.Translate(request, "kleenestar.core:validation.class.unknown"),
                    nameof(Model.Entities.Field.ClassId),
                    "class.unknown"
                );
            }

            return result;
        }

        /// <summary>
        /// Ensures the payload names a status category that exists.
        /// </summary>
        /// <remarks>
        /// The category selection submits its value under the name of the navigation
        /// property, so both spellings are accepted.
        /// </remarks>
        /// <param name="result">The validation result to add to.</param>
        /// <param name="fieldMap">The payload.</param>
        /// <param name="request">The request, for the language of the message.</param>
        /// <param name="existingCategoryId">The category of the persisted record, if there is one.</param>
        /// <returns>The validation result, for chaining.</returns>
        public static IRestApiValidationResult ValidateStatusCategory(this IRestApiValidationResult result, RestApiCrudFormData fieldMap, IRequest request, Guid? existingCategoryId = null)
        {
            if (existingCategoryId is not null && existingCategoryId != Guid.Empty)
            {
                return result;
            }

            var named = fieldMap.TryGetGuid(nameof(Model.Entities.Status.Category), out var categoryId)
                || fieldMap.TryGetGuid(nameof(Model.Entities.Status.CategoryId), out categoryId);

            if (!named || categoryId == Guid.Empty)
            {
                return result.Add
                (
                    I18N.Translate(request, "kleenestar.core:validation.statuscategory.missing"),
                    nameof(Model.Entities.Status.Category),
                    "statuscategory.missing"
                );
            }

            var known = CoreHub.StatusManager
                .GetStatusCategories(new WebExpress.WebIndex.Queries.Query<Model.Entities.StatusCategory>()
                    .WhereEquals(x => x.Id, categoryId))
                .Any();

            if (!known)
            {
                return result.Add
                (
                    I18N.Translate(request, "kleenestar.core:validation.statuscategory.unknown"),
                    nameof(Model.Entities.Status.Category),
                    "statuscategory.unknown"
                );
            }

            return result;
        }
    }
}
