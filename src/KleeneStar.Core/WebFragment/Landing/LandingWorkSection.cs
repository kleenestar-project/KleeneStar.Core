using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Landing
{
    /// <summary>
    /// The first section of the wide column: the open issues assigned to the reader, changed
    /// last first, each with its state.
    /// </summary>
    /// <remarks>
    /// The page used to lead with the organization's news and to name the reader's work only
    /// as one of four entry-path cards - which repeated the sidebar links beside it word for
    /// word and showed a count, not a single issue. What a reader arriving in the morning wants
    /// first is what is waiting for them, so that is what the column starts with; the sidebar
    /// keeps the paths.
    /// <para>
    /// "Open" is read from the workflow: an issue whose state falls into the <c>done</c>
    /// category is finished and left out, one of a class without a workflow has no state that
    /// could close it and stays. The state lives in a value row, not in a column, so it cannot
    /// be part of the query; the section therefore reads the assigned issues newest first in
    /// pages and stops as soon as it has enough open ones, bounded by <see cref="ScanLimit"/>.
    /// </para>
    /// </remarks>
    internal static class LandingWorkSection
    {
        /// <summary>
        /// The number of issues shown.
        /// </summary>
        public const int MaxItems = 6;

        /// <summary>
        /// The number of assigned issues read at most while looking for open ones.
        /// </summary>
        public const int ScanLimit = 150;

        /// <summary>
        /// The number of issues read per page of the scan.
        /// </summary>
        private const int PageSize = 30;

        /// <summary>
        /// Builds the section.
        /// </summary>
        /// <param name="objectManager">The object manager.</param>
        /// <param name="renderContext">The render context.</param>
        /// <returns>The section.</returns>
        public static IControl Build(IObjectManager objectManager, IRenderControlContext renderContext)
        {
            var identityId = CoreHub.SessionManager?.GetCurrentIdentityId(renderContext?.Request) ?? Guid.Empty;
            var open = identityId == Guid.Empty ? [] : GetOpen(objectManager, identityId, MaxItems);

            var section = new ControlSection("landing-work")
            {
                Header = _ => "kleenestar.core:landing.work.card",
                HeaderIcon = _ => new IconListCheck(),
                Note = _ => "kleenestar.core:landing.work.hint",
                Layout = _ => TypeLayoutSection.Rule
            };

            if (open.Count == 0)
            {
                section.Add(LandingRow.Empty("landing-work-empty", "kleenestar.core:landing.work.empty"));
            }
            else
            {
                var list = LandingRow.List("landing-work-list");

                foreach (var item in open)
                {
                    list.Add(BuildEntry(item, renderContext));
                }

                section.Add(list);
            }

            section.Add(LandingRow.More
            (
                "landing-work-more",
                "kleenestar.core:landing.work.all",
                CoreHub.GetUri<global::KleeneStar.Core.WWW.Mine.Index>()
            ));

            return section;
        }

        /// <summary>
        /// Reads the open issues assigned to an identity, changed last first.
        /// </summary>
        /// <param name="objectManager">The object manager.</param>
        /// <param name="identityId">The identity.</param>
        /// <param name="max">The number of issues wanted.</param>
        /// <returns>The issues with their status category (<see langword="null"/> when the class has no workflow).</returns>
        public static IReadOnlyList<(Model.Entities.Object Object, StatusCategory Category)> GetOpen
        (
            IObjectManager objectManager,
            Guid identityId,
            int max
        )
        {
            var result = new List<(Model.Entities.Object, StatusCategory)>();
            var contexts = new Dictionary<Guid, ObjectBoardClassContext>();
            var categories = CoreHub.StatusManager
                .GetStatusCategories(new Query<StatusCategory>())
                .ToDictionary(x => x.Id);

            for (var offset = 0; offset < ScanLimit && result.Count < max; offset += PageSize)
            {
                var page = objectManager.GetObjects(new Query<Model.Entities.Object>()
                    .WhereEquals(x => x.Kind, ObjectKind.Issue)
                    .Where(x => x.State == WorkspaceState.Active)
                    .Where(x => x.AssigneeId == identityId)
                    .OrderByDesc(x => x.Updated)
                    .WithPaging(offset, PageSize))
                    .ToList();

                foreach (var @object in page)
                {
                    if (!contexts.TryGetValue(@object.ClassId, out var context))
                    {
                        var @class = CoreHub.ClassManager.GetClass(@object.ClassId);
                        context = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
                        contexts[@object.ClassId] = context;
                    }

                    var category = ObjectBoardProjection.ResolveCategory(@object.Id, context, categories);

                    if (ObjectBoardProjection.Normalize(category?.Name) == "done")
                    {
                        continue;
                    }

                    result.Add((@object, category));

                    if (result.Count == max)
                    {
                        break;
                    }
                }

                if (page.Count < PageSize)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Builds the row of one issue: key and workspace beneath the summary, the state at
        /// the end.
        /// </summary>
        private static IControl BuildEntry((Model.Entities.Object Object, StatusCategory Category) item, IRenderControlContext renderContext)
        {
            var @object = item.Object;
            var kind = ObjectKindCatalog.GetKind(@object.Kind);
            var workspace = CoreHub.WorkspaceManager?.GetWorkspace(@object.WorkspaceId)?.Name;

            return LandingRow.Build
            (
                "landing-work-" + @object.Id.ToString("N"),
                ObjectIcon.Resolve(@object, kind?.Icon ?? new IconObject()),
                @object.Summary,
                ObjectKindCatalog.ResolveDetailUri(@object),
                meta: LandingHtml.Join(@object.Key, workspace, LandingHtml.Age(@object.Updated, renderContext)),
                trailing: LandingHtml.StateChip(item.Category)
            );
        }
    }
}
