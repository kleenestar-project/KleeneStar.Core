using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

using FieldEntity = KleeneStar.Model.Entities.Field;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The field values of one object as a sheet of labelled rows - the *Details* of the
    /// object page and of the preview pane beside a list.
    /// </summary>
    /// <remarks>
    /// Every row is a label (a glyph for the field type, the name, the description as its
    /// tooltip) beside the value, and the value is shown the way its type reads: a choice or a
    /// priority as a chip, tags as chips, a date in the reader's culture, a colour as a swatch,
    /// rich text as prose, a secret masked.
    /// <para>
    /// For a caller who may change the object (<see cref="Editable"/>) the value is a
    /// <see cref="ControlSmartEdit"/> around the input the field type calls for - the pen opens
    /// it in place and the check mark writes the one value through <c>/api/1/objects</c>, which
    /// asks the write permission again. The inputs follow the edit mask
    /// (<see cref="ObjectFormLayout.CreateInputForField"/>) except where a mask control is too
    /// wide for a value column: one-of-a-few options (selection, choice, radio, tile, yes/no and
    /// the priority) become the framework's selection, whose read view is a coloured chip. Uploads
    /// (attachment, avatar) are not edited in a row; they have their own surfaces.
    /// </para>
    /// <para>
    /// A secret is never handed to the page: its editor starts empty and its read view only says
    /// that it is filled.
    /// </para>
    /// </remarks>
    public sealed class ObjectFieldSheet : Control
    {
        private const string Mask = "••••••••";

        private readonly ObjectEntity _object;
        private readonly IReadOnlyList<FieldEntity> _fields;
        private readonly IReadOnlyDictionary<Guid, Value> _values;

        /// <summary>
        /// Gets or sets whether the values are edited in place.
        /// </summary>
        public bool Editable { get; set; }

        /// <summary>
        /// Gets or sets the address an inline edit is written to - the object endpoint bound to
        /// the object's id. Without one nothing is editable.
        /// </summary>
        public IUri Uri { get; set; }

        /// <summary>
        /// Gets or sets whether the fields the object has not filled are folded into one
        /// closed group after the filled ones, rather than shown in their place. The preview
        /// pane does it: there it is the filled values a reader came for, and a class with many
        /// modelled fields would bury them.
        /// </summary>
        public bool CollapseEmpty { get; set; }

        /// <summary>
        /// Returns the number of rows the sheet shows.
        /// </summary>
        public int Count => _fields.Count;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        /// <param name="object">The object whose values are shown.</param>
        /// <param name="fields">The fields to show, in order.</param>
        /// <param name="values">The object's values, keyed by field id.</param>
        public ObjectFieldSheet(string id, ObjectEntity @object, IEnumerable<FieldEntity> fields, IReadOnlyDictionary<Guid, Value> values)
            : base(id)
        {
            _object = @object;
            _fields = [.. fields ?? []];
            _values = values ?? new Dictionary<Guid, Value>();
        }

        /// <summary>
        /// Loads the values of an object, keyed by field id.
        /// </summary>
        /// <param name="objectId">The object id.</param>
        /// <returns>The values; empty when the object has none.</returns>
        public static IReadOnlyDictionary<Guid, Value> LoadValues(Guid objectId)
        {
            return CoreHub.ValueManager
                .GetValues(objectId)
                .GroupBy(x => x.FieldId)
                .ToDictionary(x => x.Key, x => x.First());
        }

        /// <summary>
        /// Builds the details of an object as the view form of its class arranges them: one
        /// sheet for a form with one tab, a tab strip with a sheet per tab for a form with
        /// several - the same on the object page and in the preview pane.
        /// </summary>
        /// <param name="id">The id prefix of the controls built.</param>
        /// <param name="object">The object.</param>
        /// <param name="include">Decides which fields of the form are shown; the surfaces differ
        /// in what their other cards already carry.</param>
        /// <param name="editable">Whether the values are edited in place.</param>
        /// <param name="uri">The address an inline edit is written to.</param>
        /// <param name="collapseEmpty">Whether unfilled fields are folded after the filled ones
        /// (see <see cref="CollapseEmpty"/>).</param>
        /// <returns>The control, or <see langword="null"/> when the class has no active view
        /// form or the form places no field <paramref name="include"/> admits.</returns>
        public static IControl BuildForm
        (
            string id,
            ObjectEntity @object,
            Func<FieldEntity, bool> include,
            bool editable,
            IUri uri,
            bool collapseEmpty
        )
        {
            var form = ObjectFormLayout.ResolveStandardForm(@object.ClassId, FormType.View);

            if (form?.Tabs is null || form.Tabs.Count == 0)
            {
                return null;
            }

            var fields = CoreHub.FieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(x => !x.Deprecated && x.State == FieldState.Active)
                .ToDictionary(x => x.Id);

            var values = LoadValues(@object.Id);
            var tabs = form.Tabs.OrderBy(x => x.Position).ToList();

            ObjectFieldSheet CreateSheet(FormTab tab) => new
            (
                $"{id}-sheet-{tab.Id:N}",
                @object,
                Flatten(tab.Elements)
                    .Select(x => fields.TryGetValue(x.FieldId, out var field) ? field : null)
                    .Where(x => x is not null && (include?.Invoke(x) ?? true)),
                values
            )
            {
                Editable = editable,
                Uri = uri,
                CollapseEmpty = collapseEmpty
            };

            if (tabs.Count == 1)
            {
                var sheet = CreateSheet(tabs[0]);

                return sheet.Count == 0 ? null : sheet;
            }

            var tabControl = new ControlTab($"{id}-tabs-{@object.Id:N}")
            {
                Layout = _ => TypeLayoutTab.Underline
            };
            var populated = false;

            foreach (var tab in tabs)
            {
                var view = new ControlTabView($"{id}-tab-{tab.Id:N}")
                {
                    Title = _ => tab.Name
                };

                var sheet = CreateSheet(tab);

                if (sheet.Count > 0)
                {
                    view.Add(sheet);
                    populated = true;
                }

                tabControl.Add(view);
            }

            // a tab strip over nothing but empty tabs is the empty section with more chrome
            return populated ? tabControl : null;
        }

        /// <summary>
        /// Determines whether a field names the description, a system attribute of the object
        /// that its own card shows.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns><see langword="true"/> for the alias.</returns>
        public static bool IsDescriptionAlias(FieldEntity field)
        {
            return string.Equals(field?.Name, nameof(ObjectEntity.Description), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Yields every field reference of an element tree in document order, descending into
        /// groups; the groups themselves are not emitted.
        /// </summary>
        /// <param name="elements">The elements to walk.</param>
        /// <returns>The field references.</returns>
        public static IEnumerable<FormFieldRefElement> Flatten(IEnumerable<FormElement> elements)
        {
            foreach (var element in (elements ?? []).OrderBy(x => x.Position))
            {
                if (element is FormFieldRefElement fieldRef)
                {
                    yield return fieldRef;
                }
                else if (element is FormGroupElement group)
                {
                    foreach (var inner in Flatten(group.Children))
                    {
                        yield return inner;
                    }
                }
            }
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var sheet = new HtmlElementTextContentDiv()
            {
                Id = Id,
                Class = Css.Concatenate("ks-field-sheet", GetClasses(renderContext)),
                Style = GetStyles(renderContext)
            };

            var editable = Editable && Uri is not null;
            var empty = new List<IHtmlNode>();

            foreach (var field in _fields)
            {
                _values.TryGetValue(field.Id, out var value);

                var filled = !IsEmpty(field, value?.Data);
                var row = RenderRow(field, value?.Data, filled, editable, renderContext, visualTree);

                if (CollapseEmpty && !filled)
                {
                    empty.Add(row);
                }
                else
                {
                    sheet.Add(row);
                }
            }

            if (empty.Count > 0)
            {
                var summary = new HtmlElementInteractiveSummary
                (
                    new HtmlText(string.Format
                    (
                        renderContext?.Request?.Culture ?? CultureInfo.InvariantCulture,
                        I18N.Translate(renderContext, "kleenestar.core:object.detail.more"),
                        empty.Count
                    ))
                )
                {
                    Class = "ks-field-sheet-more-summary"
                };

                var details = new HtmlElementInteractiveDetails(summary)
                {
                    Class = "ks-field-sheet-more"
                };

                foreach (var row in empty)
                {
                    details.Add(row);
                }

                sheet.Add(details);
            }

            return sheet;
        }

        /// <summary>
        /// Renders one row: the label and the value, editable or read.
        /// </summary>
        private IHtmlNode RenderRow
        (
            FieldEntity field,
            string data,
            bool filled,
            bool editable,
            IRenderControlContext renderContext,
            IVisualTreeControl visualTree
        )
        {
            var row = new HtmlElementTextContentDiv()
            {
                Class = Css.Concatenate("ks-field-row", filled ? null : "ks-field-row-empty")
            };

            row.AddUserAttribute("data-field-type", field.FieldType.ToString().ToLowerInvariant());

            var label = new HtmlElementTextContentDiv()
            {
                Class = "ks-field-label"
            };

            if (!ProseText.IsEmpty(field.Description))
            {
                label.AddUserAttribute("title", ProseText.ToPlainText(field.Description));
            }

            label.Add(FieldIcon(field.FieldType).Render(renderContext, visualTree, css: "ks-field-icon"));
            label.Add(new HtmlElementTextSemanticsSpan(new HtmlText(field.Name))
            {
                Class = "ks-field-name"
            });

            if (field.Required)
            {
                label.Add(new HtmlElementTextSemanticsSpan(new HtmlText("*"))
                {
                    Class = "ks-field-required"
                });
            }

            var cell = new HtmlElementTextContentDiv()
            {
                Class = "ks-field-value"
            };

            var input = editable ? CreateInlineInput(field, data) : null;

            if (input is not null)
            {
                cell.Add(RenderEditor(field, input, data, filled, renderContext, visualTree));
            }
            else
            {
                cell.Add(RenderReadView(field, data, filled, renderContext));
            }

            row.Add(label);
            row.Add(cell);

            return row;
        }

        /// <summary>
        /// Renders the in-place editor of a value.
        /// </summary>
        private IHtmlNode RenderEditor
        (
            FieldEntity field,
            IControlFormItemInput input,
            string data,
            bool filled,
            IRenderControlContext renderContext,
            IVisualTreeControl visualTree
        )
        {
            // a secret is not handed to the page, not even into an editor that starts closed
            var initial = field.FieldType == FieldType.Password
                ? null
                : CreateInputValue(input, field, data);

            if ((initial is null || !CanHold(field, data)) && filled && field.FieldType != FieldType.Password)
            {
                // a stored value the typed control cannot hold - a date that is no date, a range
                // that is no range - would read as unset there; it is shown, and edited, as the
                // text it is rather than hidden behind an empty control
                input = CreateTextInput(field);
                initial = new ControlFormInputValueString(data);
            }

            var smartEdit = new ControlSmartEdit($"{Id}-{field.Id:N}")
            {
                ObjectId = _ => _object.Id.ToString(),
                ObjectName = _ => field.Name,
                Uri = _ => Uri,
                Method = _ => RequestMethod.PUT
            };

            smartEdit.Add(input);

            if (initial is not null)
            {
                smartEdit.Initialize(args => args.SetValue(input, initial));
            }

            var html = smartEdit.Render(renderContext, visualTree);

            if (html is HtmlElement element)
            {
                // the read view of an empty value says so - and stays something to aim the pen at
                var placeholder = field.FieldType == FieldType.Password && filled
                    ? Mask
                    : I18N.Translate(renderContext, "kleenestar.core:object.detail.empty");

                element.AddUserAttribute("data-placeholder", placeholder);
            }

            return html;
        }

        /// <summary>
        /// Renders a value for a reader who may not change it, in the shape its type reads.
        /// </summary>
        private static IHtmlNode RenderReadView(FieldEntity field, string data, bool filled, IRenderControlContext renderContext)
        {
            if (!filled)
            {
                return new HtmlElementTextSemanticsSpan(new HtmlText(I18N.Translate(renderContext, "kleenestar.core:object.detail.empty")))
                {
                    Class = "ks-field-none"
                };
            }

            switch (field.FieldType)
            {
                case FieldType.Boolean:
                    {
                        var yes = FieldValueForm.IsTrue(data);
                        return Chip(ObjectValueFormat.Format(renderContext, field, data), yes ? "success" : "secondary", null);
                    }

                case FieldType.Priority:
                    {
                        var priorities = ObjectFormLayout.ResolveFieldPriorities(field).ToList();
                        var index = priorities.FindIndex(x => string.Equals(x.Name, data, StringComparison.OrdinalIgnoreCase));
                        var color = index < 0 ? null : ObjectFormLayout.SeverityColor(index, priorities.Count);
                        return Chip(data, null, color);
                    }

                case FieldType.Selection:
                case FieldType.Choice:
                case FieldType.Radio:
                case FieldType.Workflow:
                    return Chip(ObjectValueFormat.Format(renderContext, field, data), "primary", null);

                case FieldType.Tag:
                case FieldType.MultiSelection:
                case FieldType.Tile:
                case FieldType.Move:
                case FieldType.Cascading:
                    {
                        var list = new HtmlElementTextContentDiv() { Class = "ks-field-chips" };
                        foreach (var item in SplitList(data))
                        {
                            list.Add(Chip(item, field.FieldType == FieldType.Tag ? "tag" : "primary", null));
                        }
                        return list;
                    }

                case FieldType.Color:
                    {
                        var swatch = new HtmlElementTextSemanticsSpan() { Class = "ks-field-swatch" };
                        swatch.AddUserAttribute("style", "background:" + SafeColor(data));
                        return new HtmlElementTextSemanticsSpan(swatch, new HtmlText(data)) { Class = "ks-field-color" };
                    }

                case FieldType.Rating:
                    {
                        var stars = uint.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? (int)Math.Min(n, 10) : 0;
                        var max = Math.Max(5, stars);
                        var rating = new HtmlElementTextSemanticsSpan(new HtmlText(new string('★', stars) + new string('☆', max - stars)))
                        {
                            Class = "ks-field-rating"
                        };
                        rating.AddUserAttribute("title", data);
                        return rating;
                    }

                case FieldType.RichText:
                    return new HtmlElementTextContentDiv(new HtmlRaw(ProseText.ToSafeHtml(data)))
                    {
                        Class = "ks-field-prose"
                    };

                case FieldType.Multiline:
                    return new HtmlElementTextContentDiv(new HtmlText(data))
                    {
                        Class = "ks-field-multiline"
                    };

                case FieldType.Password:
                    return new HtmlElementTextSemanticsSpan(new HtmlText(Mask)) { Class = "ks-field-text" };

                default:
                    return new HtmlElementTextSemanticsSpan(new HtmlText(ObjectValueFormat.Format(renderContext, field, data)))
                    {
                        Class = "ks-field-text"
                    };
            }
        }

        /// <summary>
        /// Builds a chip, coloured either by a tone of the sheet's palette or by a colour.
        /// </summary>
        private static HtmlElementTextSemanticsSpan Chip(string text, string tone, string color)
        {
            var chip = new HtmlElementTextSemanticsSpan(new HtmlText(text))
            {
                Class = Css.Concatenate("ks-field-chip", tone is null ? null : "ks-field-chip-" + tone)
            };

            if (color is not null)
            {
                chip.AddUserAttribute("style", "--ks-chip-color:" + SafeColor(color));
            }

            return chip;
        }

        /// <summary>
        /// Returns the colour if it is a plain hexadecimal css colour, otherwise transparent -
        /// a stored value goes into a style attribute and must not be able to say more there.
        /// </summary>
        private static string SafeColor(string value)
        {
            var text = value?.Trim() ?? string.Empty;

            return text.Length is 4 or 7 && text[0] == '#' && text.Skip(1).All(System.Uri.IsHexDigit)
                ? text
                : "transparent";
        }

        /// <summary>
        /// Splits a stored list - comma-separated, or semicolon-separated as a selection submits.
        /// </summary>
        private static string[] SplitList(string data)
        {
            return (data ?? string.Empty).Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        /// <summary>
        /// Determines whether a value counts as unset.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="data">The stored value.</param>
        /// <returns><see langword="true"/> when nothing is set.</returns>
        public static bool IsEmpty(FieldEntity field, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return true;
            }

            return field?.FieldType == FieldType.RichText && ProseText.IsEmpty(data);
        }

        /// <summary>
        /// Creates the input a value of the field is edited with in place, or <see langword="null"/>
        /// for a field that is not edited in a row.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="data">The stored value. An option-typed field offers every item of it the
        /// field's options do not (any more), so a value is shown as what it is rather than as
        /// unset, and kept on a save that changes nothing.</param>
        /// <returns>The input, or <see langword="null"/>.</returns>
        public static IControlFormItemInput CreateInlineInput(FieldEntity field, string data = null)
        {
            switch (field.FieldType)
            {
                case FieldType.Attachment:
                case FieldType.Avatar:
                case FieldType.Workflow:
                    // uploads have their own surfaces, a state moves through a transition
                    return null;

                case FieldType.Boolean:
                    return CreateSelection(field,
                    [
                        ("true", "kleenestar.core:object.property.yes", TypeColorSelection.Success),
                        ("false", "kleenestar.core:object.property.no", TypeColorSelection.Secondary)
                    ], multi: false);

                case FieldType.Selection:
                case FieldType.Choice:
                case FieldType.Radio:
                case FieldType.Tile:
                    // the tile cards of the mask choose one of them, too, and are a two-column
                    // grid no value column holds
                    return CreateSelection(field, WithStored(OptionsOf(field), data), multi: false);

                case FieldType.Priority:
                    {
                        var priorities = ObjectFormLayout.ResolveFieldPriorities(field).ToList();
                        var options = priorities.Select((x, i) => (x.Name, x.Name, SeverityTone(i, priorities.Count)));
                        return CreateSelection(field, WithStored(options, data), multi: false);
                    }

                case FieldType.MultiSelection:
                    return CreateSelection(field, WithStored(OptionsOf(field), data), multi: true);

                case FieldType.User:
                    return CreateUserSelection(field, data);

                default:
                    return ObjectFormLayout.CreateInputForField(field) as IControlFormItemInput;
            }
        }

        /// <summary>
        /// Creates the input a dialog edits a value of the field with, or <see langword="null"/>
        /// for a workflow field (a state changes through a transition, a new object starts in
        /// the entry state).
        /// </summary>
        /// <remarks>
        /// The inputs of <see cref="CreateInlineInput"/>, with these differences: a boolean is the
        /// checkbox of <see cref="ObjectFormLayout.CreateInputForField"/>, which loads the JSON
        /// boolean <c>/api/1/objects</c> answers for the dialogs (<see cref="FieldValueForm"/>);
        /// avatar and attachment fields get the avatar and file input; a stored value the typed
        /// control cannot hold falls back to a text input, so a save that changes nothing keeps it.
        /// </remarks>
        /// <param name="field">The field.</param>
        /// <param name="data">The stored value the dialog starts from, or <see langword="null"/>.</param>
        /// <returns>The input, or <see langword="null"/>.</returns>
        public static IControlFormItemInput CreateDialogInput(FieldEntity field, string data)
        {
            if (field.FieldType is FieldType.Boolean or FieldType.Avatar or FieldType.Attachment)
            {
                return ObjectFormLayout.CreateInputForField(field) as IControlFormItemInput;
            }

            var input = CreateInlineInput(field, data);

            if (input is null
                || IsEmpty(field, data)
                || field.FieldType is FieldType.Password or FieldType.Date
                || CreateInputValue(input, field, data) is not null && CanHold(field, data))
            {
                return input;
            }

            return CreateTextInput(field);
        }

        /// <summary>
        /// Determines whether a typed control of the field can show a stored value as it is: a
        /// colour is <c>#rgb</c> or <c>#rrggbb</c>, a traffic light names a lamp (<c>red</c>,
        /// <c>yellow</c>, <c>green</c>), a move or cascading control names only configured
        /// options. Other types are judged by <see cref="CreateInputValue"/> alone.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="data">The stored value, not blank.</param>
        /// <returns><see langword="true"/> when the typed control holds the value.</returns>
        private static bool CanHold(FieldEntity field, string data)
        {
            var value = data.Trim();
            var options = (field.Options ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return field.FieldType switch
            {
                FieldType.Color => System.Text.RegularExpressions.Regex.IsMatch(value, "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"),
                FieldType.TrafficLight => value.ToLowerInvariant() is "red" or "yellow" or "green",
                FieldType.Move or FieldType.Cascading => SplitList(value).All(options.Contains),
                _ => true
            };
        }

        /// <summary>
        /// Creates the person selection of a user field: the active accounts by display name, each
        /// with its portrait (<see cref="WebRestApi.ObjectBoardProjection.AvatarImage"/>) or the user glyph,
        /// and a stored name that names no active account as an entry of its own.
        /// </summary>
        /// <remarks>
        /// A user field stores the display name of the person; the option ids are display names,
        /// so a selection writes what the readers (<see cref="ObjectValueFormat"/>, WQL, export)
        /// read.
        /// </remarks>
        /// <param name="field">The field.</param>
        /// <param name="data">The stored value, or <see langword="null"/>.</param>
        /// <returns>The selection.</returns>
        private static ControlFormItemInputSelection CreateUserSelection(FieldEntity field, string data)
        {
            var selection = new ControlFormItemInputSelection()
            {
                Name = _ => field.Name,
                Label = _ => field.Name,
                Placeholder = _ => field.Placeholder,
                Help = _ => field.HelpText,
                Required = _ => field.Required
            };

            var identities = CoreHub.IdentityManager
                .GetIdentities(new WebExpress.WebIndex.Queries.Query<Identity>())
                .Where(x => x.State == IdentityState.Active && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var identity in identities)
            {
                var image = WebRestApi.ObjectBoardProjection.AvatarImage(identity);

                selection.Add(new ControlFormItemInputSelectionItem(identity.Name)
                {
                    Text = _ => identity.Name,
                    Image = _ => image is null ? null : new UriEndpoint(image),
                    Icon = _ => image is null ? new IconUser() : null
                });
            }

            var stored = data?.Trim();

            if (!string.IsNullOrEmpty(stored) && !identities.Any(x => string.Equals(x.Name, stored, StringComparison.OrdinalIgnoreCase)))
            {
                selection.Add(new ControlFormItemInputSelectionItem(stored)
                {
                    Text = _ => stored,
                    Icon = _ => new IconUser(),
                    Color = _ => TypeColorSelection.Secondary
                });
            }

            return selection;
        }

        /// <summary>
        /// Returns the options configured on a field as selection entries.
        /// </summary>
        private static IEnumerable<(string Id, string Text, TypeColorSelection Color)> OptionsOf(FieldEntity field)
        {
            return (field.Options ?? []).Select(x => (x, x, TypeColorSelection.Primary));
        }

        /// <summary>
        /// Appends the items of a stored value the options do not offer, compared without case.
        /// </summary>
        private static IEnumerable<(string Id, string Text, TypeColorSelection Color)> WithStored
        (
            IEnumerable<(string Id, string Text, TypeColorSelection Color)> options,
            string data
        )
        {
            var list = options.ToList();
            var known = list.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var item in SplitList(data).Where(known.Add))
            {
                list.Add((item, item, TypeColorSelection.Secondary));
            }

            return list;
        }

        /// <summary>
        /// Creates the plain text input a value falls back to when its typed control cannot hold it.
        /// </summary>
        private static ControlFormItemInputText CreateTextInput(FieldEntity field)
        {
            return new ControlFormItemInputText()
            {
                Name = _ => field.Name,
                Label = _ => field.Name,
                Placeholder = _ => field.Placeholder,
                Help = _ => field.HelpText,
                Required = _ => field.Required
            };
        }

        /// <summary>
        /// Creates a selection over fixed options.
        /// </summary>
        private static ControlFormItemInputSelection CreateSelection
        (
            FieldEntity field,
            IEnumerable<(string Id, string Text, TypeColorSelection Color)> options,
            bool multi
        )
        {
            var selection = new ControlFormItemInputSelection()
            {
                Name = _ => field.Name,
                Label = _ => field.Name,
                Placeholder = _ => field.Placeholder,
                Help = _ => field.HelpText,
                MultiSelect = _ => multi,
                Required = _ => field.Required
            };

            foreach (var option in options.Where(x => !string.IsNullOrEmpty(x.Id)))
            {
                selection.Add(new ControlFormItemInputSelectionItem(option.Id)
                {
                    Text = ctx => I18N.Translate(ctx, option.Text),
                    Color = _ => option.Color
                });
            }

            return selection;
        }

        /// <summary>
        /// Returns the tone of a priority chip by its rank, most severe first - the same ramp
        /// <see cref="ObjectFormLayout.SeverityColor"/> runs through.
        /// </summary>
        private static TypeColorSelection SeverityTone(int index, int count)
        {
            TypeColorSelection[] ramp = [TypeColorSelection.Danger, TypeColorSelection.Warning, TypeColorSelection.Primary, TypeColorSelection.Secondary];

            if (count <= 1)
            {
                return ramp[0];
            }

            var position = (int)Math.Round(index * (ramp.Length - 1) / (double)(count - 1));

            return ramp[Math.Clamp(position, 0, ramp.Length - 1)];
        }

        /// <summary>
        /// Converts a stored value into the value type the input holds.
        /// </summary>
        /// <param name="input">The input the value is seeded into.</param>
        /// <param name="field">The field.</param>
        /// <param name="data">The stored value; may be blank.</param>
        /// <returns>The input value, or <see langword="null"/> when nothing is seeded.</returns>
        public static IControlFormInputValue CreateInputValue(IControlFormItemInput input, FieldEntity field, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            switch (input)
            {
                case ControlFormItemInputSelection:
                    // a selection names its options by id and separates several with semicolons;
                    // a boolean is stored as the parser wrote it
                    if (field.FieldType == FieldType.Boolean)
                    {
                        return new ControlFormInputValueString(FieldValueForm.IsTrue(data) ? "true" : "false");
                    }

                    return new ControlFormInputValueString(string.Join(";", SplitList(data)));

                case ControlFormItemInput<ControlFormInputValueString>:
                    return new ControlFormInputValueString(data);

                case ControlFormItemInput<ControlFormInputValueBool>:
                    return new ControlFormInputValueBool(FieldValueForm.IsTrue(data));

                case ControlFormItemInput<ControlFormInputValueDate>:
                    return DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                        ? new ControlFormInputValueDate(date)
                        : null;

                case ControlFormItemInput<ControlFormInputValueStringList>:
                    return new ControlFormInputValueStringList(SplitList(data));

                case ControlFormItemInput<ControlFormInputValueUInt>:
                    return uint.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                        ? new ControlFormInputValueUInt(number)
                        : null;

                case ControlFormItemInput<ControlFormInputValueFloat>:
                    return float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)
                        ? new ControlFormInputValueFloat(real)
                        : null;

                case ControlFormItemInput<ControlFormInputValueDualRange>:
                    // the parser reads what it cannot parse as zero, which is a value nobody stored
                    return data.Split(';').All(x => float.TryParse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        ? new ControlFormInputValueDualRange(data)
                        : null;

                case ControlFormItemInput<ControlFormInputValueDateRange>:
                    {
                        var range = new ControlFormInputValueDateRange(data, CultureInfo.InvariantCulture);
                        return range.IsEmpty ? null : range;
                    }

                default:
                    return null;
            }
        }
        /// <summary>
        /// Returns the glyph a field is labelled with. The fields are defined by an administrator
        /// in the field editor, and the icon a field row stores is only the generated mark, so
        /// the glyph says what every field has: its type.
        /// </summary>
        /// <param name="type">The field type.</param>
        /// <returns>The icon.</returns>
        public static IIcon FieldIcon(FieldType type)
        {
            return type switch
            {
                FieldType.Number => new IconHashtag(),
                FieldType.Date => new IconCalendar(),
                FieldType.DateRange or FieldType.Calendar or FieldType.CalendarRange => new IconCalendarDays(),
                FieldType.Boolean => new IconToggleOn(),
                FieldType.Selection or FieldType.Choice or FieldType.Radio or FieldType.Tile => new IconList(),
                FieldType.MultiSelection or FieldType.Move or FieldType.Cascading => new IconListCheck(),
                FieldType.Reference => new IconLink(),
                FieldType.Workflow => new IconSitemap(),
                FieldType.Attachment => new IconFile(),
                FieldType.User => new IconUser(),
                FieldType.Tag => new IconTags(),
                FieldType.Priority => new IconFlag(),
                FieldType.Multiline => new IconAlignLeft(),
                FieldType.RichText => new IconParagraph(),
                FieldType.Password => new IconKey(),
                FieldType.Color => new IconPalette(),
                FieldType.Rating => new IconStar(),
                FieldType.Slider or FieldType.Range => new IconSliders(),
                FieldType.Estimate => new IconHourglass(),
                FieldType.TrafficLight => new IconTrafficLight(),
                FieldType.Avatar => new IconImage(),
                _ => new IconFont()
            };
        }
    }
}
