using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using ObjectEntity = KleeneStar.Model.Entities.Object;
using SecurityLevelEntity = KleeneStar.Model.Entities.SecurityLevel;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The tile selection of the security level of an object (<see cref="ControlFormItemInputTile"/>
    /// named <c>SecurityLevelId</c>), shared by the classification dialog and the clone wizard.
    /// </summary>
    /// <remarks>
    /// Tiles: <c>securitylevel.none.label</c> with the empty guid (the form binder clears the
    /// property), then the active levels of the class the caller may assign
    /// (<see cref="WebManager.ISecurityLevelManager.GetAssignableSecurityLevels"/>), ordered by
    /// <see cref="SecurityLevelEntity.Rank"/> and name. A level the object carries and the caller
    /// cannot assign is added as a tile of its own, so the posted value keeps it; the endpoint
    /// accepts an unchanged classification. Each level tile shows icon, name, description, rank,
    /// the default marker and the cleared groups (<see cref="SecurityLevelEntity.PermittedGroupIds"/>).
    /// The tile of the object's classification carries the chip <c>securitylevel.object.current</c>
    /// and is preselected (<see cref="PresetInputTile"/>). The search field narrows the tiles by
    /// their text; without a match it shows <c>securitylevel.object.empty</c>.
    /// </remarks>
    public static class SecurityLevelTiles
    {
        /// <summary>
        /// Creates the tile selection, or <c>null</c> when the caller may assign no level of the
        /// object's class.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="object">The object being classified.</param>
        /// <param name="identityId">The identity the dialog is rendered for.</param>
        /// <returns>The selection, or <c>null</c>.</returns>
        public static ControlFormItemInputTile Create(IRenderControlContext renderContext, ObjectEntity @object, Guid identityId)
        {
            return Create(renderContext, @object.ClassId, @object.SecurityLevelId, identityId, markCurrent: true);
        }

        /// <summary>
        /// Creates the tile selection for an object of a class, preselected with a level, or
        /// <c>null</c> when the caller may assign no level of the class.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="classId">The class.</param>
        /// <param name="levelId">The preselected level, or <see langword="null"/> for unclassified.</param>
        /// <param name="identityId">The identity the selection is rendered for.</param>
        /// <param name="markCurrent">Whether the preselected tile carries the chip
        /// <c>securitylevel.object.current</c> (an existing object), or not (a create).</param>
        /// <returns>The selection, or <c>null</c>.</returns>
        public static ControlFormItemInputTile Create(IRenderControlContext renderContext, Guid classId, Guid? levelId, Guid identityId, bool markCurrent)
        {
            var assignable = CoreHub.SecurityLevelManager
                .GetAssignableSecurityLevels(classId, identityId)
                .Where(x => x.State == SecurityLevelState.Active)
                .ToList();

            if (assignable.Count == 0)
            {
                return null;
            }

            var currentId = levelId ?? Guid.Empty;
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
                CreateUnclassifiedCard(renderContext, markCurrent && currentId == Guid.Empty)
            };

            tiles.AddRange(levels
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => CreateLevelCard(renderContext, x, markCurrent && x.Id == currentId, assignable.Any(a => a.Id == x.Id), groupNames)));

            var selection = new PresetInputTile(currentId.ToString())
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
