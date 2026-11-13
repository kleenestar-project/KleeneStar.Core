using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using ObjectEntity = KleeneStar.Model.Entities.Object;
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The classification dialog of a single object: one tile selection
    /// (<see cref="ControlFormItemInputTile"/>) naming the security level the object carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dialog edits the object and submits <see cref="ObjectEntity.SecurityLevelId"/> to the
    /// object CRUD endpoint (<c>/api/1/objects</c>) with the object resolved from the route. The
    /// classification is not a field of the edit form.
    /// </para>
    /// <para>
    /// Tiles: <c>securitylevel.none.label</c> with the empty guid (the form binder clears the
    /// property), then the active levels of the class the caller may assign
    /// (<see cref="WebManager.ISecurityLevelManager.GetAssignableSecurityLevels"/>), ordered by
    /// <see cref="SecurityLevelEntity.Rank"/> and name. A level the object carries and the caller
    /// cannot assign is added as a tile of its own, so the posted value keeps it; the endpoint
    /// accepts an unchanged classification. Each level tile shows icon, name, description, rank,
    /// the default marker and the cleared groups (<see cref="SecurityLevelEntity.PermittedGroupIds"/>).
    /// The tile of the current classification carries the chip <c>securitylevel.object.current</c>
    /// and is preselected (<c>PreselectedInputTile</c>). The search field narrows the tiles by
    /// their text.
    /// </para>
    /// <para>
    /// Without an assignable level the selection is omitted and
    /// <see cref="ObjectFormLayout.CreateSecurityLevelNotice"/> states the reason.
    /// </para>
    /// </remarks>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Core.WWW.Issue._objectkey_.SecurityLevel>]
    [Cache]
    public sealed class ObjectSecurityLevelFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// A tile selection rendered with a preset value.
        /// </summary>
        /// <remarks>
        /// <see cref="ControlDataForm"/> does not call <see cref="ControlFormItemInput{TValue}.Initialize(IRenderControlFormContext)"/>
        /// on its items, so the initialize hook of the input never runs. The value is written into
        /// the render context before the tile renders and reaches the client as <c>data-value</c>.
        /// The form load overwrites it with the stored <see cref="ObjectEntity.SecurityLevelId"/>
        /// when that names a tile; <c>null</c> names none and keeps the preset.
        /// </remarks>
        /// <param name="value">The preset value.</param>
        private sealed class PreselectedInputTile(string value) : ControlFormItemInputTile
        {
            /// <summary>
            /// Renders the tile selection with the preset value.
            /// </summary>
            /// <param name="renderContext">The context in which the control is rendered.</param>
            /// <param name="visualTree">The visual tree representing the control's structure.</param>
            /// <returns>An HTML node representing the rendered control.</returns>
            public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
            {
                renderContext.SetValue(this, new ControlFormInputValueStringList(value));

                return base.Render(renderContext, visualTree);
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public ObjectSecurityLevelFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Core.WWW.Api._1_.Objects.Index>();

            ItemId = renderContext =>
            {
                var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();

                return CoreHub.ObjectManager.GetObjectByKey(objectKey)?.Id.ToString();
            };
        }

        /// <summary>
        /// Renders the control as an HTML node.
        /// </summary>
        /// <remarks>
        /// The items are built per render, because the tiles, the preselection and the notice
        /// depend on the object of the route and on the caller's clearance.
        /// </remarks>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var objectKey = renderContext.Request.GetParameter<ObjectKeyParameter>();
            var @object = CoreHub.ObjectManager.GetObjectByKey(objectKey);
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(renderContext.Request);

            return base.Render(renderContext, visualTree, BuildItems(renderContext, @object, identityId));
        }

        /// <summary>
        /// Builds the items of the dialog: the notice, then the tile selection.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="object">The object being classified, or <c>null</c>.</param>
        /// <param name="identityId">The identity the dialog is rendered for.</param>
        /// <returns>The form items.</returns>
        private static IEnumerable<IControlFormItem> BuildItems(IRenderControlContext renderContext, ObjectEntity @object, Guid identityId)
        {
            if (@object is null)
            {
                yield break;
            }

            var notice = ObjectFormLayout.CreateSecurityLevelNotice(@object.ClassId, identityId, @object.SecurityLevelId);

            if (notice is not null)
            {
                yield return notice;
            }

            var selection = CreateSelection(renderContext, @object, identityId);

            if (selection is not null)
            {
                yield return selection;
            }
        }

        /// <summary>
        /// Creates the tile selection, or <c>null</c> when the caller may assign no level of the
        /// object's class.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="object">The object being classified.</param>
        /// <param name="identityId">The identity the dialog is rendered for.</param>
        /// <returns>The selection, or <c>null</c>.</returns>
        private static ControlFormItemInputTile CreateSelection(IRenderControlContext renderContext, ObjectEntity @object, Guid identityId)
        {
            var assignable = CoreHub.SecurityLevelManager
                .GetAssignableSecurityLevels(@object.ClassId, identityId)
                .Where(x => x.State == SecurityLevelState.Active)
                .ToList();

            if (assignable.Count == 0)
            {
                return null;
            }

            var currentId = @object.SecurityLevelId ?? Guid.Empty;
            var levels = assignable;

            if (currentId != Guid.Empty && !assignable.Any(x => x.Id == currentId))
            {
                var kept = CoreHub.SecurityLevelManager.GetSecurityLevel(currentId);

                if (kept is not null)
                {
                    levels = [.. assignable, kept];
                }
            }

            var groupNames = GroupNames(levels);
            var tiles = new List<IControlTileCard>
            {
                CreateUnclassifiedCard(renderContext, currentId == Guid.Empty)
            };

            tiles.AddRange(levels
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => CreateLevelCard(renderContext, x, x.Id == currentId, assignable.Any(a => a.Id == x.Id), groupNames)));

            var selection = new PreselectedInputTile(currentId.ToString())
            {
                Name = _ => nameof(ObjectEntity.SecurityLevelId),
                Label = _ => "kleenestar.core:securitylevel.object.label",
                Help = _ => "kleenestar.core:securitylevel.object.help",
                Searchable = _ => true,
                SearchPlaceholder = _ => "kleenestar.core:securitylevel.object.search",
                EmptyText = _ => "kleenestar.core:securitylevel.object.empty",
                Columns = _ => 2,
                Required = _ => true
            };

            selection.Add(tiles);

            return selection;
        }

        /// <summary>
        /// Creates the tile standing for "unclassified", carrying the empty guid.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="current">Whether the object carries no level.</param>
        /// <returns>The tile.</returns>
        private static ControlTileCard CreateUnclassifiedCard(IRenderControlContext renderContext, bool current)
        {
            var card = new ControlTileCard(Guid.Empty.ToString())
            {
                Header = _ => I18N.Translate(renderContext, "kleenestar.core:securitylevel.none.label"),
                Icon = _ => new IconLockOpen(),
                Badge = _ => I18N.Translate(renderContext, "kleenestar.core:securitylevel.object.open"),
                BadgeColor = _ => new PropertyColorTile(TypeColorTile.Success),
                Chip = _ => current ? I18N.Translate(renderContext, "kleenestar.core:securitylevel.object.current") : null
            };

            card.Add(new ControlText
            {
                Text = _ => I18N.Translate(renderContext, "kleenestar.core:securitylevel.object.none.description")
            });

            return card;
        }

        /// <summary>
        /// Creates the tile of a security level.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="level">The level.</param>
        /// <param name="current">Whether the object carries the level.</param>
        /// <param name="assignable">Whether the caller may assign the level.</param>
        /// <param name="groupNames">The names of the groups, keyed by group id.</param>
        /// <returns>The tile.</returns>
        private static ControlTileCard CreateLevelCard(IRenderControlContext renderContext, SecurityLevelEntity level, bool current, bool assignable, IReadOnlyDictionary<Guid, string> groupNames)
        {
            var cleared = (level.PermittedGroupIds ?? [])
                .Select(id => groupNames.TryGetValue(id, out var name) ? name : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            var card = new ControlTileCard(level.Id.ToString())
            {
                Header = _ => level.Name,
                Icon = _ => (IIcon)level.Icon ?? new IconLock(),
                Badge = _ => level.IsDefault
                    ? I18N.Translate(renderContext, "kleenestar.core:securitylevel.isdefault.label")
                    : I18N.Translate(renderContext, "kleenestar.core:securitylevel.object.rank", level.Rank),
                BadgeColor = _ => new PropertyColorTile(level.IsDefault ? TypeColorTile.Primary : TypeColorTile.Secondary),
                Chip = _ => current ? I18N.Translate(renderContext, "kleenestar.core:securitylevel.object.current") : null
            };

            if (!ProseText.IsEmpty(level.Description))
            {
                card.Add(new ControlText { Text = _ => ProseText.ToPlainText(level.Description) });
            }

            card.AddFooter(new ControlText
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:securitylevel.object.clearance",
                    cleared.Count == 0
                        ? I18N.Translate(renderContext, "kleenestar.core:securitylevel.clearance.none")
                        : string.Join(", ", cleared)
                )
            });

            if (!assignable)
            {
                card.AddFooter(new ControlText
                {
                    Text = _ => I18N.Translate(renderContext, "kleenestar.core:securitylevel.object.kept")
                });
            }

            return card;
        }

        /// <summary>
        /// Resolves the names of the groups the supplied levels clear.
        /// </summary>
        /// <param name="levels">The levels.</param>
        /// <returns>The group names keyed by group id; unknown ids are absent.</returns>
        private static IReadOnlyDictionary<Guid, string> GroupNames(IEnumerable<SecurityLevelEntity> levels)
        {
            var names = new Dictionary<Guid, string>();

            foreach (var id in levels.SelectMany(x => x.PermittedGroupIds ?? []).Distinct())
            {
                var group = CoreHub.GroupManager.GetGroup(id);

                if (group is not null)
                {
                    names[id] = group.Name;
                }
            }

            return names;
        }
    }
}
