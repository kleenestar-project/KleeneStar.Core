using KleeneStar.Model.Entities;
using KleeneStar.Model.Forms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebIcon;
using Calendar = KleeneStar.Model.Entities.Calendar;

namespace KleeneStar.Core.WebWorkspaceTemplate
{
    /// <summary>
    /// Writes the structure a <see cref="WorkspaceTemplateClass"/> declares into the class
    /// created from it: priorities, states, workflow, fields, forms, calendars and agreements.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the order of dependence. The states come before the workflow that places
    /// them, the workflow before the field bound to it, the fields before the forms that
    /// reference them, and the calendars before the agreements whose clocks run in them.
    /// </para>
    /// <para>
    /// Nothing here decides what a class should have - that is the template's. What is declared
    /// is written through the managers, the same way the administration pages write it, so the
    /// audit log records a template-built class like any other.
    /// </para>
    /// <para>
    /// Every text is resolved once, here, in the language of whoever creates the workspace -
    /// names and option values as well as descriptions, because a template names them as
    /// internationalization keys and a stored key would be read raw on every surface. Free text
    /// passes through unchanged. The parts refer to each other by the name the template
    /// declared - a transition's ends, an agreement's calendar and pause states - so those
    /// references are matched <em>before</em> anything is translated, and what an agreement
    /// stores as its pause states is the translated name of the state it meant.
    /// </para>
    /// </remarks>
    internal static class WorkspaceTemplateStructure
    {
        /// <summary>
        /// The horizontal distance between two states on the designer canvas.
        /// </summary>
        private const int StatusSpacing = 220;

        /// <summary>
        /// Writes the declared structure into a freshly created class.
        /// </summary>
        /// <param name="class">The class the structure belongs to.</param>
        /// <param name="descriptor">What the template declares for it.</param>
        /// <param name="culture">The language the descriptions are resolved in.</param>
        /// <param name="result">Collects what was created.</param>
        public static void Apply(Class @class, WorkspaceTemplateClass descriptor, CultureInfo culture, WorkspaceTemplateStructureResult result)
        {
            if (@class is null || descriptor is null)
            {
                return;
            }

            ApplyPriorities(@class, descriptor, culture, result);

            var workflow = ApplyWorkflow(@class, descriptor, culture, result, out var statuses);
            var fields = ApplyFields(@class, descriptor, workflow, culture, result);

            ApplyForms(@class, descriptor, fields, culture, result);

            var calendars = ApplyCalendars(@class, descriptor, culture, result);

            ApplySlas(@class, descriptor, calendars, statuses, culture, result);
        }

        /// <summary>
        /// Creates the priority scale, ordered as declared.
        /// </summary>
        private static void ApplyPriorities(Class @class, WorkspaceTemplateClass descriptor, CultureInfo culture, WorkspaceTemplateStructureResult result)
        {
            var order = 0;

            foreach (var item in (descriptor.Priorities ?? []).Where(x => !string.IsNullOrWhiteSpace(x?.Name)))
            {
                var id = Guid.NewGuid();
                var priority = new Priority
                {
                    Id = id,
                    Name = Describe(item.Name, culture),
                    Description = Describe(item.Description, culture),
                    Icon = ToIcon(item.Icon, id),
                    State = PriorityState.Active,
                    Order = order++,
                    ClassId = @class.Id,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                CoreHub.PriorityManager.Add(priority);
                result.Priorities.Add(priority);
            }
        }

        /// <summary>
        /// Creates the states and the workflow placing them.
        /// </summary>
        /// <remarks>
        /// A state names its category by name, because the categories are the installation's
        /// and carry no id a template could know. An unknown category falls back to the
        /// installation's default one; an installation without any category gets no workflow,
        /// because a state without a category cannot be stored.
        /// </remarks>
        /// <param name="statuses">Receives the states written, by the name the template declared
        /// them under.</param>
        /// <returns>The workflow, or null when none was declared or none could be written.</returns>
        private static Workflow ApplyWorkflow
        (
            Class @class,
            WorkspaceTemplateClass descriptor,
            CultureInfo culture,
            WorkspaceTemplateStructureResult result,
            out IReadOnlyDictionary<string, Status> statuses
        )
        {
            statuses = new Dictionary<string, Status>(StringComparer.OrdinalIgnoreCase);

            var declared = descriptor.Workflow;
            var declaredStatuses = (declared?.Statuses ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x?.Name))
                .DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (declared is null || declaredStatuses.Count == 0)
            {
                return null;
            }

            var written = (Dictionary<string, Status>)statuses;

            var categories = CoreHub.StatusManager
                .GetStatusCategories(new Query<StatusCategory>())
                .ToList();

            var fallback = categories.FirstOrDefault(x => x.IsDefault) ?? categories.FirstOrDefault();

            if (fallback is null)
            {
                return null;
            }

            var workflowId = Guid.NewGuid();
            var workflow = new Workflow
            {
                Id = workflowId,
                Name = Describe(string.IsNullOrWhiteSpace(declared.Name) ? "kleenestar.core:workspace.template.workflow.name" : declared.Name, culture),
                Description = Describe(declared.Description, culture),
                Icon = ToIcon("/kleenestar/assets/icons/workflow.svg", workflowId),
                State = WorkflowState.Active,
                ClassId = @class.Id,
                WorkflowStatuses = [],
                Transitions = [],
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            for (var i = 0; i < declaredStatuses.Count; i++)
            {
                var item = declaredStatuses[i];
                var category = categories.FirstOrDefault(x => string.Equals(x.Name, item.Category, StringComparison.OrdinalIgnoreCase))
                    ?? fallback;

                var statusId = Guid.NewGuid();
                var status = new Status
                {
                    Id = statusId,
                    Name = Describe(item.Name, culture),
                    Description = Describe(item.Description, culture),
                    Icon = ToIcon(item.Icon, statusId),
                    State = StatusState.Active,
                    CategoryId = category.Id,
                    ClassId = @class.Id,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                CoreHub.StatusManager.Add(status);
                result.Statuses.Add(status);
                written[item.Name] = status;

                // laid out left to right along the declared order and aligned to the designer's
                // grid, so the workflow opens on a readable canvas
                workflow.WorkflowStatuses.Add(new WorkflowStatus
                {
                    WorkflowId = workflow.Id,
                    StatusId = status.Id,
                    X = 80 + i * StatusSpacing,
                    Y = 180,
                    IsStart = i == 0,
                    IsEnd = item.IsEnd
                });
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in declared.Transitions ?? [])
            {
                // a transition naming a state that is not declared connects nothing; it is
                // dropped rather than stored with a dangling end
                if (item is null
                    || !written.TryGetValue(item.From ?? string.Empty, out var source)
                    || !written.TryGetValue(item.To ?? string.Empty, out var target)
                    || source.Id == target.Id)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(item.Name) ? target.Name : Describe(item.Name, culture);

                // transition names are unique per workflow, as the designer keeps them
                if (!names.Add(name))
                {
                    name = $"{name} ({source.Name})";
                    names.Add(name);
                }

                workflow.Transitions.Add(new Transition
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = Describe(item.Description, culture),
                    State = TransitionState.Active,
                    WorkflowId = workflow.Id,
                    SourceId = source.Id,
                    TargetId = target.Id,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                });
            }

            CoreHub.WorkflowManager.Add(workflow);
            result.Workflows.Add(workflow);

            return workflow;
        }

        /// <summary>
        /// Creates the fields. The workflow field is bound to the class's workflow; a second
        /// workflow field would be bound to the same one, which is why a template declares one.
        /// </summary>
        /// <returns>The created fields paired with what declared them, in declaration order.</returns>
        private static IReadOnlyList<(Field Field, WorkspaceTemplateField Declared)> ApplyFields
        (
            Class @class,
            WorkspaceTemplateClass descriptor,
            Workflow workflow,
            CultureInfo culture,
            WorkspaceTemplateStructureResult result
        )
        {
            var created = new List<(Field, WorkspaceTemplateField)>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in descriptor.Fields ?? [])
            {
                if (string.IsNullOrWhiteSpace(item?.Name) || !names.Add(item.Name))
                {
                    continue;
                }

                // a workflow field without a workflow could never hold a state
                if (item.Type == FieldType.Workflow && workflow is null)
                {
                    continue;
                }

                var id = Guid.NewGuid();
                var field = new Field
                {
                    Id = id,
                    Name = Describe(item.Name, culture),
                    Description = Describe(item.Description, culture),
                    Icon = ToIcon(item.Icon, id),
                    State = FieldState.Active,
                    FieldType = item.Type,
                    Cardinality = FieldCardinality.Single,
                    Options = [.. (item.Options ?? []).Select(x => Describe(x, culture))],
                    ValidationRules = [],
                    Required = item.Required,
                    WorkflowId = item.Type == FieldType.Workflow ? workflow?.Id : null,
                    AccessModifier = AccessModifier.Public,
                    ClassId = @class.Id,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                CoreHub.FieldManager.Add(field);
                result.Fields.Add(field);
                created.Add((field, item));
            }

            return created;
        }

        /// <summary>
        /// Creates the create, edit and view forms from the fields, and the self-service form of
        /// a portal-visible class.
        /// </summary>
        /// <remarks>
        /// The three standard forms are what every object surface resolves by type - the create
        /// wizard, the edit mask, the reading view and the detail card - so a class with fields
        /// and without them would render its fields nowhere. Each field lands on the tab it
        /// names, the tabs in the order their first field is declared. The create form leaves
        /// out what only the team fills in later. The self-service form is the portal's request
        /// template and asks only what a customer can answer.
        /// </remarks>
        private static void ApplyForms
        (
            Class @class,
            WorkspaceTemplateClass descriptor,
            IReadOnlyList<(Field Field, WorkspaceTemplateField Declared)> fields,
            CultureInfo culture,
            WorkspaceTemplateStructureResult result
        )
        {
            if (fields.Count == 0)
            {
                return;
            }

            AddForm(@class, "Create", "kleenestar.core:workspace.template.form.create.description", FormType.Create, false,
                [.. fields.Where(x => x.Declared.OnCreate)], culture, result);
            AddForm(@class, "Edit", "kleenestar.core:workspace.template.form.edit.description", FormType.Edit, false,
                fields, culture, result);
            AddForm(@class, "View", "kleenestar.core:workspace.template.form.view.description", FormType.View, false,
                fields, culture, result);

            var portal = fields.Where(x => x.Declared.Portal).ToList();

            if (descriptor.PortalVisible && portal.Count > 0)
            {
                // one tab: a customer filing a request reads a short form, not the team's layout
                AddForm(@class, "kleenestar.core:workspace.template.form.selfservice.name", "kleenestar.core:workspace.template.form.selfservice.description", FormType.Default, true,
                    [.. portal.Select(x => (x.Field, new WorkspaceTemplateField { Name = x.Declared.Name, Tab = "kleenestar.core:workspace.template.form.selfservice.tab" }))], culture, result);
            }
        }

        /// <summary>
        /// Lays one form out, creating it where the class has none of that type.
        /// </summary>
        /// <remarks>
        /// A class is born with an empty create, edit and view form - <c>ModelHub.Add</c> writes
        /// them beside every class - so the standard forms are filled rather than added: a second
        /// form of the same type would stand beside the empty one, and every surface that
        /// resolves a form by its type takes the first it finds. The self-service form is not
        /// one of them and is always added.
        /// </remarks>
        private static void AddForm
        (
            Class @class,
            string name,
            string description,
            FormType type,
            bool portalTemplate,
            IReadOnlyList<(Field Field, WorkspaceTemplateField Declared)> fields,
            CultureInfo culture,
            WorkspaceTemplateStructureResult result
        )
        {
            name = Describe(name, culture);
            description = Describe(description, culture);

            var form = type == FormType.Default
                ? null
                : CoreHub.FormManager
                    .GetForms(new Query<Form>().WhereEquals(x => x.ClassId, @class.Id))
                    .FirstOrDefault(x => x.FormType == type && x.State == FormState.Active && !x.PortalTemplate);

            if (form is null)
            {
                var formId = Guid.NewGuid();

                form = new Form
                {
                    Id = formId,
                    Name = name,
                    Description = description,
                    FormType = type,
                    State = FormState.Active,
                    Icon = ToIcon(null, formId),
                    PortalTemplate = portalTemplate,
                    ClassId = @class.Id,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                CoreHub.FormManager.Add(form);
            }

            var snapshot = new FormStructureSnapshot
            {
                FormName = form.Name,
                FormDescription = description,
                Tabs =
                [
                    .. fields
                        .GroupBy(x => Describe(string.IsNullOrWhiteSpace(x.Declared.Tab) ? WorkspaceTemplateField.GeneralTab : x.Declared.Tab, culture), StringComparer.OrdinalIgnoreCase)
                        .Select(g => new TabSnapshot
                        {
                            Name = g.Key,
                            Children = [.. g.Select(x => (NodeSnapshot)new FieldRefSnapshot { FieldId = x.Field.Id })]
                        })
                ]
            };

            CoreHub.FormManager.SaveFormStructure(form.Id, snapshot, form.Version);
            result.Forms.Add(form);
        }

        /// <summary>
        /// Creates the calendars.
        /// </summary>
        /// <returns>The calendars by name.</returns>
        private static IReadOnlyDictionary<string, Calendar> ApplyCalendars(Class @class, WorkspaceTemplateClass descriptor, CultureInfo culture, WorkspaceTemplateStructureResult result)
        {
            var created = new Dictionary<string, Calendar>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in descriptor.Calendars ?? [])
            {
                if (string.IsNullOrWhiteSpace(item?.Name) || created.ContainsKey(item.Name))
                {
                    continue;
                }

                var hours = (item.BusinessHours ?? []).ToDictionary(x => x.Day);

                var calendarId = Guid.NewGuid();
                var calendar = new Calendar
                {
                    Id = calendarId,
                    Name = Describe(item.Name, culture),
                    Description = Describe(item.Description, culture),
                    TimeZone = item.TimeZone,
                    Region = item.Region,
                    IsDefault = item.IsDefault,
                    State = CalendarState.Active,
                    Icon = ToIcon(null, calendarId),
                    ClassId = @class.Id,
                    // every weekday is written, the ones not worked disabled, so the calendar
                    // dialog shows a whole week rather than the days somebody remembered
                    BusinessHours =
                    [
                        .. Enum.GetValues<DayOfWeek>().Select(day => hours.TryGetValue(day, out var slot)
                            ? new BusinessHourSlot { Id = Guid.NewGuid(), DayOfWeek = day, Enabled = true, StartTime = slot.Start, EndTime = slot.End }
                            : new BusinessHourSlot { Id = Guid.NewGuid(), DayOfWeek = day, Enabled = false, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0, 0) })
                    ],
                    Holidays =
                    [
                        .. (item.Holidays ?? []).Select(x => new Holiday
                        {
                            Id = Guid.NewGuid(),
                            Date = x.Date,
                            Name = Describe(x.Name, culture),
                            Region = item.Region,
                            Enabled = true
                        })
                    ],
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                CoreHub.CalendarManager.Add(calendar);
                result.Calendars.Add(calendar);
                created[item.Name] = calendar;
            }

            return created;
        }

        /// <summary>
        /// Creates the service-level agreements, each running in the calendar it names or else
        /// in the class's default calendar.
        /// </summary>
        private static void ApplySlas
        (
            Class @class,
            WorkspaceTemplateClass descriptor,
            IReadOnlyDictionary<string, Calendar> calendars,
            IReadOnlyDictionary<string, Status> statuses,
            CultureInfo culture,
            WorkspaceTemplateStructureResult result
        )
        {
            var fallback = calendars.Values.FirstOrDefault(x => x.IsDefault) ?? calendars.Values.FirstOrDefault();

            foreach (var item in descriptor.Slas ?? [])
            {
                if (string.IsNullOrWhiteSpace(item?.Name))
                {
                    continue;
                }

                var calendar = !string.IsNullOrWhiteSpace(item.Calendar) && calendars.TryGetValue(item.Calendar, out var named)
                    ? named
                    : fallback;

                var level = 1;

                var policyId = Guid.NewGuid();
                var policy = new SlaPolicy
                {
                    Id = policyId,
                    Name = Describe(item.Name, culture),
                    Description = Describe(item.Description, culture),
                    State = SlaPolicyState.Active,
                    Priority = item.Priority,
                    CalendarId = calendar?.Id,
                    Notifications = item.Notifications,
                    // the clock compares the stored names of the states, so a pause state is
                    // stored as the name the state it means was written under
                    PauseOn = string.Join(", ", (item.PauseOn ?? []).Select(x => statuses.TryGetValue(x, out var paused) ? paused.Name : Describe(x, culture))),
                    Icon = ToIcon(null, policyId),
                    ClassId = @class.Id,
                    Targets =
                    [
                        .. (item.Targets ?? []).Select(x => new SlaTarget
                        {
                            Id = Guid.NewGuid(),
                            Name = Describe(x.Name, culture),
                            Kind = x.Kind,
                            TargetValue = x.Value,
                            Unit = x.Unit,
                            Created = DateTime.UtcNow,
                            Updated = DateTime.UtcNow
                        })
                    ],
                    Scope =
                    [
                        .. (item.Scope ?? []).Select(x => new SlaScopeRule
                        {
                            Id = Guid.NewGuid(),
                            RuleType = x.Type,
                            Value = Describe(x.Value, culture)
                        })
                    ],
                    Escalations =
                    [
                        .. (item.Escalations ?? []).Select(x => new SlaEscalationLevel
                        {
                            Id = Guid.NewGuid(),
                            Level = level++,
                            AfterValue = x.AfterValue,
                            Unit = x.Unit,
                            Notify = Describe(x.Notify, culture)
                        })
                    ],
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

                CoreHub.SlaManager.Add(policy);
                result.Slas.Add(policy);
            }
        }

        /// <summary>
        /// Resolves a text the template names - an internationalization key or plain text - into
        /// the text the record carries, in the language of the workspace's creator.
        /// </summary>
        private static string Describe(string description, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            return culture is null
                ? I18N.Translate(description)
                : I18N.Translate(culture, description);
        }

        /// <summary>
        /// Turns the icon path a template names into an icon, or - where it names none - gives
        /// the record the generated mark the administration dialogs give a record created
        /// there.
        /// </summary>
        /// <param name="icon">The path the template names, or null.</param>
        /// <param name="id">The id of the record, which the generated mark is derived from.</param>
        /// <returns>The icon, or null when none could be generated (a host without a data
        /// directory, as in a unit test).</returns>
        private static ImageIcon ToIcon(string icon, Guid id)
        {
            if (!string.IsNullOrWhiteSpace(icon))
            {
                return ImageIcon.FromString(icon);
            }

            try
            {
                return CoreHub.GenerateIcon(id);
            }
            catch
            {
                return null;
            }
        }
    }
}
