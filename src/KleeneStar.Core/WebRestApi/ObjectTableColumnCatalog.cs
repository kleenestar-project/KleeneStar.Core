using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// The column catalog of an object overview table: every column a user may pick for
    /// the table of one object kind in one workspace, and the projection of an object
    /// onto those columns.
    /// </summary>
    /// <remarks>
    /// A column is either a <em>system column</em> — a property every object carries,
    /// such as its key or its assignee — or a <em>field column</em>. The fields are
    /// defined per class, so a workspace whose issues span several classes (a service
    /// desk has tickets, incidents, problems, changes, …) has several field definitions
    /// that mean the same thing. The catalog therefore folds the fields of all classes
    /// of the kind by name: one column per distinct field name, carrying the field ids
    /// it stands for, so a row reads the value of whichever of those fields its own
    /// class defines. Without that fold the catalog would offer one "Title" column per
    /// class and each would be empty for every object of another class.
    ///
    /// The catalog is built per request rather than cached, because a class, a field or
    /// a selection option added in the settings has to show up in the column picker
    /// immediately, and building it is a handful of manager reads.
    /// </remarks>
    internal sealed class ObjectTableColumnCatalog
    {
        /// <summary>
        /// The prefix that marks a column as a class field rather than a system
        /// property. The remainder of the id is the lower-cased field name, which is
        /// stable across classes and across a field being re-created, unlike a field id.
        /// </summary>
        public const string FieldColumnPrefix = "field:";

        private readonly IReadOnlyList<ObjectTableColumn> _columns;

        /// <summary>
        /// Gets the columns of the catalog in their default order.
        /// </summary>
        public IReadOnlyList<ObjectTableColumn> Columns => _columns;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="columns">The columns of the catalog, in default order.</param>
        private ObjectTableColumnCatalog(IReadOnlyList<ObjectTableColumn> columns)
        {
            _columns = columns;
        }

        /// <summary>
        /// Builds the catalog for one object kind of a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace whose classes define the fields.</param>
        /// <param name="kind">The object kind whose classes are considered.</param>
        /// <param name="request">The request used to resolve the localized labels.</param>
        /// <returns>The catalog. It always carries the system columns, even when the
        /// workspace defines no class of that kind.</returns>
        public static ObjectTableColumnCatalog Build(Guid? workspaceId, string kind, IRequest request)
        {
            return Build(ResolveClasses(workspaceId, kind), request);
        }

        /// <summary>
        /// Builds the catalog over an arbitrary set of classes - the classes the objects of an
        /// insight come from, whichever workspaces and kinds those are.
        /// </summary>
        /// <param name="classIds">The ids of the classes whose fields become columns.</param>
        /// <param name="request">The request used to localize the labels and bind the endpoint.</param>
        /// <returns>The catalog.</returns>
        public static ObjectTableColumnCatalog Build(IEnumerable<Guid> classIds, IRequest request)
        {
            var ids = (classIds ?? []).Distinct().ToList();

            if (ids.Count == 0)
            {
                return Build((IReadOnlyList<ObjectTableClassContext>)[], request);
            }

            using var context = ModelHub.CreateDbContext();
            var query = new Query<Class>()
                .Where(x => ids.Contains(x.Id));

            return Build
            (
                [
                    .. CoreHub.ClassManager
                        .GetClasses(query, context)
                        .Where(x => x.State == ClassState.Active)
                        .OrderBy(x => x.Name)
                        .Select(ObjectTableClassContext.Build)
                ],
                request
            );
        }

        /// <summary>
        /// Builds the catalog over resolved class contexts.
        /// </summary>
        /// <param name="classes">The classes whose fields become columns.</param>
        /// <param name="request">The request used to localize the labels and bind the endpoint.</param>
        /// <returns>The catalog.</returns>
        private static ObjectTableColumnCatalog Build(IReadOnlyList<ObjectTableClassContext> classes, IRequest request)
        {
            var columns = new List<ObjectTableColumn>();

            columns.AddRange(BuildSystemColumns(request));
            columns.AddRange(BuildFieldColumns(classes));

            // every cell writes an inline edit through the same endpoint, addressed at
            // the object of its row
            var endpoint = CoreHub.GetUri<WWW.Api._1_.Objects.Index>()?
                .BindParameters(request)?
                .ToString();

            foreach (var column in columns)
            {
                column.Template?.BindEndpoint(endpoint);
            }

            return new ObjectTableColumnCatalog(columns);
        }

        /// <summary>
        /// Returns the classes of the kind that live in the workspace, with their field,
        /// status and priority definitions attached.
        /// </summary>
        private static IReadOnlyList<ObjectTableClassContext> ResolveClasses(Guid? workspaceId, string kind)
        {
            if (workspaceId is null)
            {
                return [];
            }

            using var context = ModelHub.CreateDbContext();
            var query = new Query<Class>()
                .WhereEquals(x => x.WorkspaceId, workspaceId.Value)
                .WhereEquals(x => x.Kind, kind);

            return
            [
                .. CoreHub.ClassManager
                    .GetClasses(query, context)
                    .Where(x => x.State == ClassState.Active)
                    .Select(ObjectTableClassContext.Build)
            ];
        }

        /// <summary>
        /// Builds the columns backed by a property of <see cref="ObjectEntity"/> itself.
        /// </summary>
        /// <remarks>
        /// Key, class, creator, created and updated are generated or derived and are
        /// therefore offered read-only; editing them inline would either break the
        /// object's identity or be overwritten by the next save. Summary, description
        /// and assignee are the properties a user genuinely maintains, so those carry an
        /// editor.
        /// </remarks>
        private static IEnumerable<ObjectTableColumn> BuildSystemColumns(IRequest request)
        {
            yield return new ObjectTableColumn
            {
                Id = "key",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.key"),
                DefaultVisible = true,
                Template = ObjectTableColumnTemplate.ReadOnly("text"),
                Read = (o, _) => o.Key
            };

            yield return new ObjectTableColumn
            {
                Id = "summary",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.summary"),
                Name = nameof(ObjectEntity.Summary),
                DefaultVisible = true,
                Template = ObjectTableColumnTemplate.Input("text"),
                Read = (o, _) => o.Summary
            };

            yield return new ObjectTableColumn
            {
                Id = "description",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.description"),
                Name = nameof(ObjectEntity.Description),
                Template = ObjectTableColumnTemplate.Input("text"),
                Read = (o, _) => o.Description
            };

            yield return new ObjectTableColumn
            {
                Id = "class",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.class"),
                Template = ObjectTableColumnTemplate.ReadOnly("text"),
                Read = (o, ctx) => ctx.ResolveClass(o)?.Class?.Name
            };

            // the assignee travels as the identity id, because that is what the object
            // stores; the combo maps it to the display name on both sides
            var identities = ResolveIdentityItems();

            yield return new ObjectTableColumn
            {
                Id = "assignee",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.assignee"),
                Name = nameof(ObjectEntity.AssigneeId),
                DefaultVisible = true,
                Template = ObjectTableColumnTemplate.Combo(identities, editable: true),
                Read = (o, _) => o.AssigneeId?.ToString()
            };

            yield return new ObjectTableColumn
            {
                Id = "creator",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.creator"),
                Template = ObjectTableColumnTemplate.Combo(identities, editable: false),
                Read = (o, _) => o.CreatorId?.ToString()
            };

            yield return new ObjectTableColumn
            {
                Id = "created",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.created"),
                Template = ObjectTableColumnTemplate.ReadOnly("text"),
                Read = (o, _) => o.Created.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            };

            yield return new ObjectTableColumn
            {
                Id = "updated",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.updated"),
                DefaultVisible = true,
                Template = ObjectTableColumnTemplate.ReadOnly("text"),
                Read = (o, _) => o.Updated.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            };

            // read-only: the classification is what decides who sees the row at all, so it is
            // changed on the object's own form where the hint about it can be shown, not by an
            // inline edit in a list
            var securityLevels = ResolveSecurityLevelNames();

            yield return new ObjectTableColumn
            {
                Id = "securitylevel",
                Label = I18N.Translate(request, "kleenestar.core:securitylevel.object.label"),
                Template = ObjectTableColumnTemplate.ReadOnly("text"),
                Read = (o, _) => o.SecurityLevelId.HasValue && securityLevels.TryGetValue(o.SecurityLevelId.Value, out var name)
                    ? name
                    : null
            };

            yield return new ObjectTableColumn
            {
                Id = "storypoints",
                Label = I18N.Translate(request, "kleenestar.core:object.kind.issues.column.storypoints"),
                Name = nameof(ObjectEntity.StoryPoints),
                Template = ObjectTableColumnTemplate.Input("numeric"),
                Read = (o, _) => o.StoryPoints?.ToString(CultureInfo.InvariantCulture)
            };
        }

        /// <summary>
        /// Builds one column per distinct field name across the classes of the kind. The
        /// editor of a column follows the field type; when classes disagree on the type
        /// of a same-named field, the first class that declares it wins and the column
        /// falls back to a plain text editor for the others, which is the only editor
        /// that can represent every payload.
        /// </summary>
        private static IEnumerable<ObjectTableColumn> BuildFieldColumns(IReadOnlyList<ObjectTableClassContext> classes)
        {
            var groups = classes
                .SelectMany(ctx => ctx.Fields.Select(field => (Context: ctx, Field: field)))
                .GroupBy(x => x.Field.Name, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var entries = group.ToList();
                var lead = entries[0];
                var types = entries.Select(x => x.Field.FieldType).Distinct().ToList();
                var fieldType = types.Count == 1 ? types[0] : FieldType.Text;

                var fieldIds = entries
                    .Select(x => x.Field.Id)
                    .ToHashSet();

                yield return new ObjectTableColumn
                {
                    Id = FieldColumnPrefix + group.Key.ToLowerInvariant(),
                    Label = lead.Field.Name,
                    Name = lead.Field.Name,
                    FieldIds = fieldIds,
                    FieldType = fieldType,
                    Template = BuildFieldTemplate(fieldType, entries.Select(x => x.Context), entries.Select(x => x.Field)),
                    Read = (o, ctx) => ctx.ReadFieldValue(o, fieldIds, fieldType)
                };
            }
        }

        /// <summary>
        /// Chooses the cell template of a field column from its field type. Every
        /// template is editable — a field is user-maintained data by definition — and
        /// the ones backed by a fixed set of values carry that set, so the editor offers
        /// a choice instead of free text.
        /// </summary>
        private static ObjectTableColumnTemplate BuildFieldTemplate
        (
            FieldType fieldType,
            IEnumerable<ObjectTableClassContext> contexts,
            IEnumerable<Field> fields
        )
        {
            switch (fieldType)
            {
                case FieldType.Number:
                    return ObjectTableColumnTemplate.Input("numeric");

                case FieldType.Date:
                    return ObjectTableColumnTemplate.Input("date");

                case FieldType.Boolean:
                    return ObjectTableColumnTemplate.Combo
                    ([
                        new() { Id = string.Empty, Text = string.Empty },
                        new() { Id = "true", Text = "true" },
                        new() { Id = "false", Text = "false" }
                    ], editable: true);

                case FieldType.Tag:
                    return ObjectTableColumnTemplate.Input("tag");

                case FieldType.Selection:
                    return ObjectTableColumnTemplate.Combo(BuildOptionItems(fields), editable: true);

                case FieldType.Priority:
                    return ObjectTableColumnTemplate.Combo(BuildPriorityItems(contexts, fields), editable: true);

                case FieldType.Workflow:
                    // a status change runs the workflow's transitions, guards and post
                    // functions; writing the status straight into the value row would
                    // bypass all of them, so the column shows the status read-only and
                    // leaves the change to the workflow control on the object page
                    return ObjectTableColumnTemplate.Combo(BuildStatusItems(contexts), editable: false);

                case FieldType.User:
                    // a user field holds a person as it was written, not an identity id
                    // (the edit form gives it a text input too), so offering the identity
                    // list here would turn every value that names someone outside the
                    // directory into an unmatched entry
                    return ObjectTableColumnTemplate.Input("text");

                case FieldType.Attachment:
                    // attachments are maintained through their own upload pipeline
                    return ObjectTableColumnTemplate.ReadOnly("text");

                case FieldType.Reference:
                case FieldType.Text:
                default:
                    return ObjectTableColumnTemplate.Input("text");
            }
        }

        /// <summary>
        /// Returns the union of the configured options of the supplied selection fields.
        /// </summary>
        private static IReadOnlyList<RestApiTableColumnTemplateItem> BuildOptionItems(IEnumerable<Field> fields)
        {
            var items = new List<RestApiTableColumnTemplateItem>
            {
                new() { Id = string.Empty, Text = string.Empty }
            };

            items.AddRange(fields
                .SelectMany(f => f.Options ?? [])
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
                .Select(o => new RestApiTableColumnTemplateItem { Id = o, Text = o }));

            return items;
        }

        /// <summary>
        /// Returns the priorities the supplied priority fields offer, restricted to the
        /// explicitly selected ones where a field defines a selection.
        /// </summary>
        private static IReadOnlyList<RestApiTableColumnTemplateItem> BuildPriorityItems
        (
            IEnumerable<ObjectTableClassContext> contexts,
            IEnumerable<Field> fields
        )
        {
            var restricted = fields
                .SelectMany(f => f.SelectedPriorityIds ?? [])
                .ToHashSet();

            var priorities = contexts
                .SelectMany(c => c.Priorities)
                .Where(p => restricted.Count == 0 || restricted.Contains(p.Id))
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(p => p.Order).First())
                .OrderBy(p => p.Order);

            var items = new List<RestApiTableColumnTemplateItem>
            {
                new() { Id = string.Empty, Text = string.Empty }
            };

            // a priority value is stored by name (see the priority input of the edit
            // form), so the item id is the name rather than the id
            items.AddRange(priorities.Select(p => new RestApiTableColumnTemplateItem
            {
                Id = p.Name,
                Text = p.Name
            }));

            return items;
        }

        /// <summary>
        /// Returns the statuses of the supplied classes, folded by name.
        /// </summary>
        private static IReadOnlyList<RestApiTableColumnTemplateItem> BuildStatusItems(IEnumerable<ObjectTableClassContext> contexts)
        {
            return
            [
                .. contexts
                    .SelectMany(c => c.Statuses)
                    .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new RestApiTableColumnTemplateItem
                    {
                        Id = g.Key,
                        Text = g.Key
                    })
                    .OrderBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
            ];
        }

        /// <summary>
        /// Returns the active identities as selectable items, keyed by their id.
        /// </summary>
        /// <summary>
        /// Reads the names of the security levels, by id, so a row can name the level it
        /// carries without a query per row.
        /// </summary>
        /// <remarks>
        /// The catalog is installation-wide rather than per class: a table of a kind mixes
        /// classes, and each of them defines its own levels.
        /// </remarks>
        /// <returns>The level names, by id.</returns>
        private static IReadOnlyDictionary<Guid, string> ResolveSecurityLevelNames()
        {
            return CoreHub.SecurityLevelManager
                .GetSecurityLevels(new Query<Model.Entities.SecurityLevel>())
                .ToDictionary(x => x.Id, x => x.Name);
        }

        private static IReadOnlyList<RestApiTableColumnTemplateItem> ResolveIdentityItems()
        {
            var query = new Query<Identity>();

            var items = new List<RestApiTableColumnTemplateItem>
            {
                new() { Id = string.Empty, Text = string.Empty }
            };

            items.AddRange(CoreHub.IdentityManager
                .GetIdentities(query)
                .Where(x => x.State == IdentityState.Active)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new RestApiTableColumnTemplateItem
                {
                    Id = x.Id.ToString(),
                    Text = x.Name
                }));

            return items;
        }
    }
}
