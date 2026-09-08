using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

using ClassEntity = KleeneStar.Model.Entities.Class;
using FieldEntity = KleeneStar.Model.Entities.Field;
using FormEntity = KleeneStar.Model.Entities.Form;
using ObjectEntity = KleeneStar.Model.Entities.Object;
using PriorityEntity = KleeneStar.Model.Entities.Priority;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Builds the form items of an object from the form the form manager holds for its
    /// class, so every surface that shows an object form — the edit dialog and the last
    /// step of the creation wizard — reproduces the same structure from the same source.
    /// </summary>
    /// <remarks>
    /// Tabs, layout groups and field references defined on the form are reproduced
    /// one-to-one as <see cref="IControlFormItem"/> instances. A class without an active
    /// form of the requested type yields nothing; the caller then falls back to the system
    /// properties every object carries.
    /// </remarks>
    public static class ObjectFormLayout
    {
        /// <summary>
        /// Creates the input for the title of an object. The title is a system property
        /// rather than a configured field, so it is emitted before the form structure and
        /// is not part of it.
        /// </summary>
        /// <returns>The input control.</returns>
        public static ControlFormItemInputText CreateSummaryInput()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(ObjectEntity.Summary),
                Label = _ => "kleenestar.core:object.add.summary.label",
                Placeholder = _ => "kleenestar.core:object.add.summary.placeholder",
                Help = _ => "kleenestar.core:object.add.summary.help",
                Required = _ => true
            };
        }

        /// <summary>
        /// Creates the input for the description of an object, used when the class has no
        /// form to take the structure from.
        /// </summary>
        /// <returns>The input control.</returns>
        public static ControlFormItemInputText CreateDescriptionInput()
        {
            return new ControlFormItemInputText()
            {
                Name = _ => nameof(ObjectEntity.Description),
                Label = _ => "kleenestar.core:object.add.description.label",
                Placeholder = _ => "kleenestar.core:object.add.description.placeholder",
                Format = _ => TypeEditTextFormat.Wysiwyg,
                Required = _ => false
            };
        }

        /// <summary>
        /// Creates the input for the security level of an object, or <c>null</c> when the
        /// class classifies nothing or the caller is cleared for no level of it.
        /// </summary>
        /// <remarks>
        /// Like the title, the classification is a system property rather than a configured
        /// field, so it is emitted alongside the form structure rather than being part of it.
        /// The options come from the class-scoped selection endpoint, which offers only the
        /// levels the caller may assign.
        /// </remarks>
        /// <param name="classId">The class the object belongs to.</param>
        /// <param name="identityId">The identity filling the form in.</param>
        /// <returns>The input control, or <c>null</c> when there is nothing to choose.</returns>
        public static ControlDataFormItemInputSelection CreateSecurityLevelInput(Guid classId, Guid identityId)
        {
            if (classId == Guid.Empty || !HasSecurityLevels(classId))
            {
                return null;
            }

            // a caller cleared for none of the class's levels is offered no input at all; the
            // notice beside it says why, and the only value they could pick is "unclassified",
            // which is what the object gets anyway
            if (CoreHub.SecurityLevelManager.GetAssignableSecurityLevels(classId, identityId).Count == 0)
            {
                return null;
            }

            return new ControlDataFormItemInputSelection()
            {
                Name = _ => nameof(ObjectEntity.SecurityLevelId),
                Label = _ => "kleenestar.core:securitylevel.object.label",
                Placeholder = _ => "kleenestar.core:securitylevel.object.placeholder",
                Help = _ => "kleenestar.core:securitylevel.object.help",
                StickySelection = _ => true,
                ServiceFactory = _ => DataServiceDescriptor.QueryData
                (
                    CoreHub.GetUri<global::KleeneStar.Core.WWW.Api._1_.SecurityLevels._classid_.Selection>()
                        .BindParameters(new ClassIdParameter(classId))
                        .ToString()
                )
            };
        }

        /// <summary>
        /// Creates the notice the form shows when the caller and the classification of the
        /// object do not agree, or <c>null</c> when there is nothing to say.
        /// </summary>
        /// <remarks>
        /// Two situations are worth a sentence, and both are ones the form would otherwise
        /// leave to be discovered by the record disappearing:
        /// <list type="bullet">
        ///   <item>The class classifies its objects, but the caller is cleared for none of its
        ///   levels. The input is then absent, and the notice says why.</item>
        ///   <item>The object already carries a level the caller cannot assign. Saving keeps
        ///   the classification, and with it the chance that the record leaves their view.</item>
        /// </list>
        /// </remarks>
        /// <param name="classId">The class the object belongs to.</param>
        /// <param name="identityId">The identity filling the form in.</param>
        /// <param name="currentLevelId">The level the object carries, or <c>null</c>.</param>
        /// <returns>The notice, or <c>null</c>.</returns>
        public static IControlFormItem CreateSecurityLevelNotice(Guid classId, Guid identityId, Guid? currentLevelId)
        {
            if (classId == Guid.Empty)
            {
                return null;
            }

            var securityLevelManager = CoreHub.SecurityLevelManager;
            var message = (string)null;

            if (currentLevelId.HasValue
                && currentLevelId.Value != Guid.Empty
                && !securityLevelManager.IsCleared(identityId, currentLevelId))
            {
                message = "kleenestar.core:securitylevel.object.hint";
            }
            else if (HasSecurityLevels(classId)
                && securityLevelManager.GetAssignableSecurityLevels(classId, identityId).Count == 0)
            {
                message = "kleenestar.core:securitylevel.object.unavailable";
            }

            if (message is null)
            {
                return null;
            }

            return new ControlFormItemPanel
            (
                null,
                new ControlAlert()
                {
                    Head = _ => "kleenestar.core:securitylevel.object.hint.title",
                    Text = _ => message,
                    BackgroundColor = _ => new PropertyColorBackgroundAlert(TypeColorBackgroundAlert.Warning)
                }
            );
        }

        /// <summary>
        /// Determines whether a class classifies its objects at all.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns><see langword="true"/> when the class defines an active security level.</returns>
        public static bool HasSecurityLevels(Guid classId)
        {
            return CoreHub.SecurityLevelManager
                .GetSecurityLevels(new ClassIdParameter(classId))
                .Any(x => x.State == SecurityLevelState.Active);
        }

        /// <summary>
        /// Resolves the active standard form of the requested type for the given class.
        /// </summary>
        /// <param name="classId">The unique identifier of the class.</param>
        /// <param name="type">The desired form type.</param>
        /// <returns>
        /// The form together with its full structural tree, or <c>null</c> when no
        /// matching active form exists.
        /// </returns>
        public static FormEntity ResolveStandardForm(Guid classId, FormType type)
        {
            var form = CoreHub.FormManager
                .GetForms(new ClassIdParameter(classId))
                .FirstOrDefault(f => f.FormType == type && f.State == FormState.Active);

            return form is null ? null : CoreHub.FormManager.GetFormWithStructure(form.Id);
        }

        /// <summary>
        /// Builds the form items of the given form. Single-tab forms emit their elements
        /// directly, so the form reads naturally without a tab bar for one tab.
        /// </summary>
        /// <param name="form">The form whose structure is reproduced, or null.</param>
        /// <param name="classId">The class the form belongs to.</param>
        /// <param name="inputs">
        /// Receives every input the structure contains, in the order they were created.
        /// A tab view keeps its items to itself, so an input nested in one cannot be
        /// reached from the built tree; a caller that has to address the inputs — to seed
        /// their values, for instance — collects them here instead.
        /// </param>
        /// <returns>The form items, which are empty when the form is null or has no tabs.</returns>
        public static IEnumerable<IControlFormItem> BuildItems(FormEntity form, Guid classId, ICollection<IControlFormItemInput> inputs = null)
        {
            if (form?.Tabs is null || form.Tabs.Count == 0)
            {
                yield break;
            }

            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(classId))
                .Where(f => !f.Deprecated && f.State == FieldState.Active)
                .ToDictionary(f => f.Id);

            var orderedTabs = form.Tabs.OrderBy(t => t.Position).ToList();

            if (orderedTabs.Count == 1)
            {
                foreach (var element in orderedTabs[0].Elements.OrderBy(e => e.Position))
                {
                    var item = BuildElement(element, fields, inputs);
                    if (item is not null)
                    {
                        yield return item;
                    }
                }
                yield break;
            }

            var tabGroup = new ControlFormItemGroupTab()
            {
                Layout = _ => TypeLayoutTab.Underline
            };

            foreach (var t in orderedTabs)
            {
                var view = new ControlFormItemGroupTabView("tab-" + t.Id.ToString("N"))
                {
                    Title = _ => t.Name
                };

                foreach (var element in t.Elements.OrderBy(e => e.Position))
                {
                    var item = BuildElement(element, fields, inputs);
                    if (item is not null)
                    {
                        view.Add(item);
                    }
                }

                tabGroup.AddView(view);
            }

            yield return tabGroup;
        }

        /// <summary>
        /// Recursively maps a form element from the model to a form item control. Field
        /// references resolve to a typed input matching the referenced
        /// <see cref="FieldEntity.FieldType"/>; group elements map to the layout group control
        /// matching <see cref="FormGroupElement.Layout"/> and recurse into their children.
        /// </summary>
        /// <param name="element">The form element to convert.</param>
        /// <param name="fields">
        /// Lookup of active fields of the class, used to decorate field references with
        /// their display metadata. Inactive or deprecated fields are skipped.
        /// </param>
        /// <param name="inputs">Receives the inputs the element contains, or null.</param>
        /// <returns>
        /// The corresponding form item, or <c>null</c> when the element type is not
        /// supported or the referenced field is not visible on the form.
        /// </returns>
        public static IControlFormItem BuildElement(FormElement element, IDictionary<Guid, FieldEntity> fields, ICollection<IControlFormItemInput> inputs = null)
        {
            if (element is FormFieldRefElement fieldRef)
            {
                if (!fields.TryGetValue(fieldRef.FieldId, out var field))
                {
                    return null;
                }

                var input = CreateInputForField(field);

                if (input is IControlFormItemInput typed)
                {
                    inputs?.Add(typed);
                }

                return input;
            }

            if (element is FormGroupElement group)
            {
                var groupControl = CreateGroupForLayout(group.Layout);

                foreach (var child in group.Children.OrderBy(c => c.Position))
                {
                    var childItem = BuildElement(child, fields, inputs);
                    if (childItem is not null)
                    {
                        groupControl.Add(childItem);
                    }
                }

                return groupControl;
            }

            return null;
        }

        /// <summary>
        /// Creates an empty layout group matching the requested <see cref="FormGroupLayout"/>.
        /// </summary>
        /// <param name="layout">The layout of the group.</param>
        /// <returns>The group control.</returns>
        public static ControlFormItemGroup CreateGroupForLayout(FormGroupLayout layout)
        {
            return layout switch
            {
                FormGroupLayout.Horizontal => new ControlFormItemGroupHorizontal(),
                FormGroupLayout.Mix => new ControlFormItemGroupMix(),
                FormGroupLayout.ColumnVertical => new ControlFormItemGroupColumnVertical(),
                FormGroupLayout.ColumnHorizontal => new ControlFormItemGroupColumnHorizontal(),
                FormGroupLayout.ColumnMix => new ControlFormItemGroupColumnMix(),
                _ => new ControlFormItemGroupVertical(),
            };
        }

        /// <summary>
        /// Creates a typed input control for the given field, mapping
        /// <see cref="FieldEntity.FieldType"/> to the matching WebUI form item.
        /// </summary>
        /// <param name="field">The field to create the input for.</param>
        /// <returns>The input control.</returns>
        public static IControlFormItem CreateInputForField(FieldEntity field)
        {
            switch (field.FieldType)
            {
                case FieldType.Boolean:
                    return new ControlFormItemInputCheck()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Date:
                    return new ControlFormItemInputDate()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Selection:
                    var combo = new ControlFormItemInputCombo()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
                    foreach (var option in field.Options ?? [])
                    {
                        combo.Add(new ControlFormItemInputComboItem()
                        {
                            Text = _ => option,
                            Value = _ => option
                        });
                    }
                    return combo;

                case FieldType.Priority:
                    // a priority is a short, ordered set that is worth reading at a glance,
                    // so it is offered as a segmented choice rather than folded into a list
                    var priorities = ResolveFieldPriorities(field).ToList();
                    var choice = new ControlFormItemInputChoice()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
                    for (var i = 0; i < priorities.Count; i++)
                    {
                        var priority = priorities[i];
                        var color = SeverityColor(i, priorities.Count);

                        choice.Add(new ControlFormItemInputChoiceItem()
                        {
                            Text = _ => ShortLabel(priority.Name),
                            Value = _ => priority.Name,
                            Description = _ => priority.Description ?? priority.Name,
                            Color = _ => new PropertyColorTile(color)
                        });
                    }
                    return choice;

                case FieldType.Tag:
                    return new ControlFormItemInputTag()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Attachment:
                    return new ControlFormItemInputFile()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Text:
                    // the field that carries the description of an object is written as
                    // prose, so it gets the rich editor rather than a single-line input
                    var description = string.Equals(field.Name, nameof(ObjectEntity.Description), StringComparison.OrdinalIgnoreCase);

                    return new ControlFormItemInputText()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Format = _ => description ? TypeEditTextFormat.Wysiwyg : TypeEditTextFormat.Default,
                        Required = _ => field.Required
                    };

                case FieldType.Multiline:
                    return new ControlFormItemInputText()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Format = _ => TypeEditTextFormat.Multiline,
                        Required = _ => field.Required
                    };

                case FieldType.RichText:
                    return new ControlFormItemInputText()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Format = _ => TypeEditTextFormat.Wysiwyg,
                        Required = _ => field.Required
                    };

                case FieldType.Password:
                    return new ControlFormItemInputPassword()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Color:
                    return new ControlFormItemInputColor()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Rating:
                    return new ControlFormItemInputRating()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Slider:
                    return new ControlFormItemInputSlider()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Range:
                    return new ControlFormItemInputRange()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Estimate:
                    return new ControlFormItemInputEstimate()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.TrafficLight:
                    return new ControlFormItemInputTrafficLight()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.DateRange:
                    return new ControlFormItemInputDateRange()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Calendar:
                    return new ControlFormItemInputCalendar()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.CalendarRange:
                    return new ControlFormItemInputCalendarRange()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Avatar:
                    return new ControlFormItemInputAvatar()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };

                case FieldType.Choice:
                case FieldType.Radio:
                    // both offer one of a few options; the radio keeps the classic control,
                    // the choice puts them side by side as a segmented row
                    return CreateOptionInput(field, field.FieldType == FieldType.Radio);

                case FieldType.Tile:
                    var tile = new ControlFormItemInputTile()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Columns = _ => 2,
                        Required = _ => field.Required
                    };
                    foreach (var option in field.Options ?? [])
                    {
                        var card = new ControlTileCard(option) { Header = _ => option };
                        tile.Add(card);
                    }
                    return tile;

                case FieldType.Move:
                    var move = new ControlFormItemInputMove()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
                    foreach (var option in field.Options ?? [])
                    {
                        move.Add(new ControlFormItemInputMoveItem(option) { Text = _ => option });
                    }
                    return move;

                case FieldType.Cascading:
                    var cascading = new ControlFormItemInputCascading()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
                    foreach (var option in field.Options ?? [])
                    {
                        cascading.Add(new ControlFormItemInputCascadingItem(option) { Text = _ => option });
                    }
                    return cascading;

                case FieldType.MultiSelection:
                    var selection = new ControlFormItemInputSelection()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        MultiSelect = _ => true,
                        Required = _ => field.Required
                    };
                    foreach (var option in field.Options ?? [])
                    {
                        selection.Add(new ControlFormItemInputSelectionItem(option) { Text = _ => option });
                    }
                    return selection;

                case FieldType.Number:
                case FieldType.Reference:
                case FieldType.Workflow:
                case FieldType.User:
                default:
                    return new ControlFormItemInputText()
                    {
                        Name = _ => field.Name,
                        Label = _ => field.Name,
                        Placeholder = _ => field.Placeholder,
                        Help = _ => field.HelpText,
                        Required = _ => field.Required
                    };
            }
        }

        /// <summary>
        /// Creates the input of a field whose options are configured on the field itself,
        /// offered either as radio buttons or as a segmented choice.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="radio">Whether the options are offered as radio buttons.</param>
        /// <returns>The input control.</returns>
        private static IControlFormItem CreateOptionInput(FieldEntity field, bool radio)
        {
            var options = field.Options ?? [];

            if (!radio)
            {
                var choice = new ControlFormItemInputChoice()
                {
                    Name = _ => field.Name,
                    Label = _ => field.Name,
                    Help = _ => field.HelpText,
                    Required = _ => field.Required
                };

                foreach (var option in options)
                {
                    choice.Add(new ControlFormItemInputChoiceItem()
                    {
                        Text = _ => option,
                        Value = _ => option
                    });
                }

                return choice;
            }

            // a radio group is one control per option, so the options share a name and are
            // wrapped in a group that carries the label of the field
            var group = new ControlFormItemGroupVertical();

            foreach (var option in options)
            {
                group.Add(new ControlFormItemInputRadio()
                {
                    Name = _ => field.Name,
                    Option = _ => option,
                    Description = _ => option,
                    Inline = _ => true
                });
            }

            return group;
        }

        /// <summary>
        /// Resolves the priorities offered for a priority-typed field. When the field
        /// configuration restricts the field to a specific set of priorities
        /// (<see cref="FieldEntity.SelectedPriorityIds"/>), only those are returned;
        /// otherwise every active priority of the field's class is offered. The result is
        /// ordered by the priority display order.
        /// </summary>
        /// <param name="field">The priority-typed field whose options are resolved.</param>
        /// <returns>The priorities to present, in display order.</returns>
        public static IEnumerable<PriorityEntity> ResolveFieldPriorities(FieldEntity field)
        {
            // Load the field's class priorities in a single round-trip and filter in memory.
            // Priorities are class-scoped, so the selected ids are a subset of the class
            // priorities loaded here.
            var priorities = CoreHub.PriorityManager
                .GetPriorities(new ClassIdParameter(field.ClassId));

            if (field.SelectedPriorityIds is { Count: > 0 })
            {
                var selected = field.SelectedPriorityIds.ToHashSet();
                return priorities
                    .Where(p => selected.Contains(p.Id))
                    .OrderBy(p => p.Order);
            }

            return priorities
                .Where(p => p.State == PriorityState.Active)
                .OrderBy(p => p.Order);
        }

        /// <summary>
        /// Returns the colour of a priority marker.
        /// </summary>
        /// <remarks>
        /// A priority is read as a severity rather than as an identity, so its colour comes
        /// from its rank within the class rather than from its id: the most severe entry is
        /// red, the least severe grey, and the ones between them run through the ramp.
        /// </remarks>
        /// <param name="index">The zero-based rank of the priority, most severe first.</param>
        /// <param name="count">The number of priorities offered.</param>
        /// <returns>The colour as a hexadecimal css value.</returns>
        public static string SeverityColor(int index, int count)
        {
            string[] ramp = ["#dc3545", "#fd7e14", "#0d6efd", "#6c757d"];

            if (count <= 1)
            {
                return ramp[0];
            }

            // spread the ranks across the ramp, so its ends always mark the extremes
            var position = (int)Math.Round(index * (ramp.Length - 1) / (double)(count - 1));

            return ramp[Math.Clamp(position, 0, ramp.Length - 1)];
        }

        /// <summary>
        /// Shortens a priority name to the token it is commonly addressed by, so a segmented
        /// control stays readable: "P2 - High" becomes "P2", a single-word name stays as it is.
        /// </summary>
        /// <param name="name">The priority name.</param>
        /// <returns>The short label.</returns>
        public static string ShortLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var separator = name.IndexOf(" - ", StringComparison.Ordinal);

            return separator > 0 ? name[..separator] : name;
        }

        /// <summary>
        /// Returns the class the given id addresses, or null.
        /// </summary>
        /// <param name="classId">The class id.</param>
        /// <returns>The class, or null when none matches.</returns>
        public static ClassEntity GetClass(Guid classId)
        {
            return classId == Guid.Empty ? null : CoreHub.ClassManager.GetClass(classId);
        }
    }
}
