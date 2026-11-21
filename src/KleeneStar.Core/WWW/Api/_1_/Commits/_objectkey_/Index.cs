using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebIndex.Queries;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebIndex.Wql;

namespace KleeneStar.Core.WWW.Api._1_.Commits._objectkey_
{
    using CommitEntity = KleeneStar.Model.Entities.Commit;

    /// <summary>
    /// REST list of the commit chain of a single object, newest first. Backs the master side of
    /// the version history dialog. The URL is <c>/api/1/commits/{objectkey}</c>.
    /// </summary>
    /// <remarks>
    /// It is a list endpoint rather than a second view onto <c>/api/1/history/{objectkey}</c>
    /// because the two answer to different consumers: the history endpoint is the documented API
    /// and speaks commits, states and diffs, while this one speaks the list contract the
    /// <c>ControlDataList</c> of the dialog reads — items with a text, an icon and a primary
    /// action. Keeping them apart also keeps the documented payload free of the selection
    /// plumbing the dialog needs.
    /// <para>
    /// The two cannot share a route: the history endpoint declares
    /// <see cref="IncludeSubPathsAttribute"/> for its sub-routes and would swallow any sibling
    /// beneath it.
    /// </para>
    /// <para>
    /// How many pages the dialog's pager offers depends on the total the response reports, and
    /// <c>RestApiList</c> reports the size of the page it returned unless the endpoint overrides
    /// <c>RetrieveTotal</c>. That hook exists in the framework sources but not yet in the
    /// published package this project builds against, so the override is added here once the
    /// package carries it; until then the pager walks one page at a time.
    /// </para>
    /// </remarks>
    [Title("kleenestar.core:object.history.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : RestApiList<CommitEntity>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Creates the query context the chain is read in.
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
        /// Returns the commits of the addressed object, newest first, as list items whose
        /// primary action loads the revision into the detail side of the dialog.
        /// </summary>
        /// <param name="query">The query criteria, already carrying the search and the paging.</param>
        /// <param name="context">The query context.</param>
        /// <param name="request">The request, carrying the object key.</param>
        /// <returns>The matching commits as list items.</returns>
        protected override IEnumerable<RestApiListItem> RetrieveItems(IQuery<CommitEntity> query, IQueryContext context, IRequest request)
        {
            var @object = ResolveObject(request);

            if (@object is null)
            {
                return [];
            }

            var commits = CoreHub.CommitManager
                .GetCommits(query.WhereEquals(x => x.ObjectId, @object.Id).OrderByDesc(x => x.Number), context)
                .ToList();

            var items = commits.Select(x => new RestApiListItem
            {
                Id = x.Id.ToString(),
                Text = Describe(x, request),
                Icon = new IconCodeCommit().Class,

                // the selection is handed to the master-detail composite rather than written into
                // the frame, so it stays the single owner of the selection and the highlight
                PrimaryAction = new ActionMasterDetail(ListDetailControl.ControlId)
                {
                    Uri = DetailUri(@object.Key, x, request),
                    Item = x.Id.ToString()
                }.ToJson()
            }).ToList();

            var pending = DescribeDraft(@object, request);

            if (pending is not null)
            {
                items.Insert(0, pending);
            }

            return items;
        }

        /// <summary>
        /// Narrows the chain to the commits matching the typed term.
        /// </summary>
        /// <remarks>
        /// The term is matched against what the entry shows - the revision number, the action,
        /// the author - plus the commit message, which is not on the entry but is what a user
        /// looking for a particular change most often remembers. A term that reads as a revision
        /// number addresses that revision alone, because "#3" can only mean one thing.
        /// </remarks>
        /// <param name="search">The typed term.</param>
        /// <param name="query">The query to narrow.</param>
        /// <param name="request">The request providing the operational context.</param>
        /// <returns>The narrowed query.</returns>
        protected override IQuery<CommitEntity> Filter(string search, IQuery<CommitEntity> query, IRequest request)
        {
            if (string.IsNullOrWhiteSpace(search) || search == "null")
            {
                return query;
            }

            var term = search.Trim();

            if (int.TryParse(term.TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return query.WhereEquals(x => x.Number == number);
            }

            var type = ParseType(term, request);

            var narrowed = query
                .WhereContainsIgnoreCase(x => x.CreatedByName, term)
                .Or(x => x.Message != null && x.Message.ToLower().Contains(term.ToLower()));

            return type.HasValue
                ? narrowed.Or(x => x.Type == type.Value)
                : narrowed;
        }

        /// <summary>
        /// Returns the commit type whose localized label starts with the typed term, so typing
        /// "trans" finds the transitions without the user having to know the stored ordinal.
        /// </summary>
        /// <param name="term">The typed term.</param>
        /// <param name="request">The request, used to localize the labels.</param>
        /// <returns>The matching commit type, or <c>null</c>.</returns>
        private static Model.Entities.CommitType? ParseType(string term, IRequest request)
        {
            foreach (var candidate in Enum.GetValues<Model.Entities.CommitType>())
            {
                var label = I18N.Translate(request, Model.Entities.CommitTypeExtensions.Text(candidate));

                if (!string.IsNullOrEmpty(label) && label.StartsWith(term, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the single line an entry shows: the revision, what happened, who did it and
        /// when.
        /// </summary>
        /// <param name="commit">The commit.</param>
        /// <param name="request">The request, used to localize the action.</param>
        /// <returns>The entry text.</returns>
        private static string Describe(CommitEntity commit, IRequest request)
        {
            var parts = new List<string>
            {
                string.Concat("#", commit.Number.ToString(CultureInfo.InvariantCulture), " ", I18N.Translate(request, Model.Entities.CommitTypeExtensions.Text(commit.Type)))
            };

            var author = commit.CreatedBy?.Name ?? commit.CreatedByName;

            if (!string.IsNullOrWhiteSpace(author))
            {
                parts.Add(author);
            }

            parts.Add(commit.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            return string.Join(" · ", parts);
        }

        /// <summary>
        /// Builds the entry that stands for the unpublished draft of the object, or
        /// <see langword="null"/> when there is none.
        /// </summary>
        /// <remarks>
        /// A draft is not a revision - it has no commit, no number and nothing to replay - so it
        /// cannot be one of the entries below it, and it deliberately carries no primary action:
        /// there is no state to load into the detail side, and offering one would suggest the
        /// draft can be restored to, which is what publishing is for.
        /// <para>
        /// It is shown all the same, at the head of the chain, because the question a version
        /// history is opened with is "what is the current state of this text", and an answer that
        /// stopped at the last publication would omit the part somebody is still writing. The
        /// entry is what tells a second author that the document has unpublished changes before
        /// they start their own.
        /// </para>
        /// <para>
        /// It sits outside the paging and the search of the chain on purpose: it is one row, it
        /// is always the newest, and hiding it behind a typed term would hide exactly the state
        /// the search was meant to find.
        /// </para>
        /// </remarks>
        /// <param name="object">The object whose chain is listed.</param>
        /// <param name="request">The request, used to localize the label.</param>
        /// <returns>The entry, or <see langword="null"/>.</returns>
        private static RestApiListItem DescribeDraft(Model.Entities.Object @object, IRequest request)
        {
            var draft = CoreHub.ObjectDraftManager.GetDraft(@object.Id);

            if (draft is null)
            {
                return null;
            }

            var parts = new List<string>
            {
                I18N.Translate(request, "kleenestar.core:object.history.type.drafted")
            };

            var author = draft.UpdaterId.HasValue
                ? CoreHub.IdentityManager.GetIdentity(draft.UpdaterId.Value)?.Name
                : null;

            if (!string.IsNullOrWhiteSpace(author))
            {
                parts.Add(author);
            }

            parts.Add(draft.Updated.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            return new RestApiListItem
            {
                Id = draft.Id.ToString(),
                Text = string.Join(" · ", parts),
                Icon = new IconPen().Class
            };
        }

        /// <summary>
        /// Builds the address of the detail page of one revision.
        /// </summary>
        /// <param name="objectKey">The key of the object.</param>
        /// <param name="commit">The commit.</param>
        /// <param name="request">The request.</param>
        /// <returns>The bound detail uri.</returns>
        private static IUri DetailUri(string objectKey, CommitEntity commit, IRequest request)
        {
            // GetUri hands out a fresh instance per call, which matters because Add mutates the
            // uri in place - a shared instance would accumulate one query per item
            return CoreHub.GetUri<global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryDetail>()
                .BindParameters(new ObjectKeyParameter(objectKey))
                .BindParameters(request)
                .Add(new UriQuery(global::KleeneStar.Core.WWW.Issue._objectkey_.HistoryDetail.CommitParameter, commit.Number.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Resolves the object addressed by the URL <c>{objectkey}</c> segment.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The object, or <c>null</c>.</returns>
        private static Model.Entities.Object ResolveObject(IRequest request)
        {
            return CoreHub.ObjectManager.GetObjectByKey(request?.GetParameter<ObjectKeyParameter>()?.Value);
        }
    }
}
