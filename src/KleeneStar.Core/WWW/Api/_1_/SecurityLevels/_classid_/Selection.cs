using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

// the endpoints live in KleeneStar.Core.WWW.Api._1_.SecurityLevels, so the bare entity
// name would resolve to the namespace rather than to the type
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.SecurityLevels._classid_
{
    /// <summary>
    /// Offers the security levels of the addressed class for selection on an object form.
    /// </summary>
    /// <remarks>
    /// Only the levels the caller is cleared for are offered. Anything else would let somebody
    /// file a record and lose sight of it in the same act - which is precisely the confusion the
    /// hint on the object form exists to prevent, and the write side refuses it anyway.
    /// <para>
    /// The list leads with an entry standing for "unclassified", carrying the empty guid because
    /// that is what the form binder reads as "clear this property".
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:securitylevel.manage.label")]
    [Cache]
    public sealed class Selection : RestApiSelection<SecurityLevelEntity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Selection()
        {
        }

        /// <summary>
        /// Creates a new instance of an object that implements the IQueryContext interface.
        /// </summary>
        /// <returns>The query context.</returns>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <summary>
        /// Returns how a WQL statement is turned into a query: translatable, because the
        /// query runs against the database.
        /// </summary>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <summary>
        /// Narrows the offered levels by what was typed into the selection.
        /// </summary>
        /// <param name="filter">The typed text.</param>
        /// <param name="query">The query being built.</param>
        /// <param name="request">The request providing the operational context.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<SecurityLevelEntity> Filter(string filter, IQuery<SecurityLevelEntity> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null"
                ? query
                : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }

        /// <summary>
        /// Retrieves the levels of the class the caller may assign, led by the entry standing
        /// for "unclassified".
        /// </summary>
        /// <param name="query">The query criteria.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request whose route names the class.</param>
        /// <returns>The selectable items.</returns>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<SecurityLevelEntity> query, IQueryContext context, IRequest request)
        {
            var list = new List<RestApiSelectionItem>()
            {
                new() { Id = Guid.Empty, Text = I18N.Translate(request, "kleenestar.core:securitylevel.none.label") }
            };

            var classParameter = request.GetParameter<ClassIdParameter>();
            var classId = Guid.TryParse(classParameter?.Value, out var id) ? id : Guid.Empty;

            if (classId == Guid.Empty)
            {
                return list.AsQueryable();
            }

            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);
            var assignable = CoreHub.SecurityLevelManager
                .GetAssignableSecurityLevels(classId, identityId)
                .Select(x => x.Id)
                .ToHashSet();

            query = query.WhereEquals(x => x.ClassId, classId);

            list.AddRange(CoreHub.SecurityLevelManager.GetSecurityLevels(query, context)
                .Where(x => x.State == SecurityLevelState.Active && assignable.Contains(x.Id))
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new RestApiSelectionItem()
                {
                    Id = x.Id,
                    Text = x.Name
                }));

            return list.AsQueryable();
        }
    }
}
