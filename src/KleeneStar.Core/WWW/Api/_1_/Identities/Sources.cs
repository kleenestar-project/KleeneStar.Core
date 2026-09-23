using KleeneStar.Core.WebIdentity;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

namespace KleeneStar.Core.WWW.Api._1_.Identities
{
    /// <summary>
    /// Offers the registered authentication sources to the identity dialogs.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="AuthenticationSourceCatalog"/> on every request, so a source a
    /// plugin registers after start-up is offered without a restart. An entry's id is derived
    /// from the source key (<see cref="Model.Entities.IdentitySource.IdOf"/>), because a picker
    /// identifies its entries by guid.
    /// </remarks>
    [Title("Authentication source")]
    public sealed class Sources : RestApiSelection<Model.Entities.Identity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Sources()
        {
        }

        /// <summary>
        /// Retrieves the selection items: one per registered source.
        /// </summary>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Model.Entities.Identity> query, IQueryContext context, IRequest request)
        {
            return AuthenticationSourceCatalog.Sources
                .Select(x => new RestApiSelectionItem
                {
                    Id = Model.Entities.IdentitySource.IdOf(x.Key),
                    Text = I18N.Translate(request, x.Name)
                })
                .ToList()
                .AsQueryable();
        }

        /// <summary>
        /// Applies no filter: the list is short, and every entry is always offered.
        /// </summary>
        protected override IQuery<Model.Entities.Identity> Filter(string filter, IQuery<Model.Entities.Identity> query, IRequest request)
        {
            return query;
        }
    }
}
