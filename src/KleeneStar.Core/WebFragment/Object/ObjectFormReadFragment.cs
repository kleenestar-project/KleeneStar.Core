using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebManager;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebFragment.Object
{
    /// <summary>
    /// The reading view of a form-rendered object: the form of its class as a filled-in
    /// sheet. It stands on the same routes as <see cref="ObjectProseReadFragment"/> - the
    /// document and blog detail pages - and exactly one of the two draws, decided by the
    /// renderer the object's class names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On the blog route it never wins as the core ships: the blog kind names prose as the
    /// only renderer it accepts, so a post is prose whatever its class stored. The scope is
    /// kept rather than dropped because it says where this fragment <em>could</em> draw, and
    /// whether it does is the kind's business - a plugin that re-registers the blog kind with
    /// the mask among its renderers gets the sheet without touching anything here.
    /// </para>
    /// <para>
    /// It is deliberately drawn as a <em>form</em> rather than as a property list: a boxed
    /// sheet, a captioned band per tab of the class's <see cref="FormType.View"/> form, and
    /// one numbered, ruled line per field with the answer printed into it. The reader should
    /// recognize the same document they filled in, which a column of "name: value" rows does
    /// not give them - it reads as a record about the object rather than as the object.
    /// </para>
    /// <para>
    /// The tabs of the form become sections stacked on one sheet instead of a tab strip. A
    /// printed form is continuous; hiding half of its lines behind a second navigation would
    /// be a worse reading of it than a page the eye can run down. The editing side keeps the
    /// tabs (<see cref="ObjectFormEditFragment"/>) - there, one part at a time is what makes
    /// a long form fillable.
    /// </para>
    /// <para>
    /// A field the object has never filled is kept and drawn as an <b>empty box</b>, which is
    /// the opposite of what <see cref="ObjectPreviewFieldFragment"/> does in a narrow pane.
    /// The reason is what the two views are for: a pane summarizes an object, while this
    /// <em>is</em> the object - an unanswered line is information, and dropping it would
    /// silently shorten the record.
    /// </para>
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Core.WWW.Document._objectkey_.Index>]
    [Scope<global::KleeneStar.Core.WWW.Blog._objectkey_.Index>]
    [Condition<FormRendererCondition>]
    [Cache]
    public sealed class ObjectFormReadFragment : FragmentControlPanel
    {
        private readonly IObjectManager _objectManager;
        private readonly IFieldManager _fieldManager;
        private readonly IValueManager _valueManager;
        private readonly IObjectTagManager _tagManager;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The fragment context.</param>
        /// <param name="objectManager">The object manager used to resolve the addressed
        /// object from the URL-bound object key.</param>
        /// <param name="fieldManager">The field manager used to enumerate the class fields.</param>
        /// <param name="valueManager">The value manager used to read the object's field values.</param>
        /// <param name="tagManager">The tag manager used to read the tags shown under the sheet.</param>
        public ObjectFormReadFragment
        (
            IFragmentContext fragmentContext,
            IObjectManager objectManager,
            IFieldManager fieldManager,
            IValueManager valueManager,
            IObjectTagManager tagManager
        )
            : base(fragmentContext)
        {
            _objectManager = objectManager;
            _fieldManager = fieldManager;
            _valueManager = valueManager;
            _tagManager = tagManager;
        }

        /// <summary>
        /// Renders the filled-in sheet. Returns <c>null</c> when the fragment's render
        /// conditions exclude it or when no object can be resolved from the request.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The HTML node, or <c>null</c>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            if (!FragmentContext.Conditions.Check(renderContext?.Request))
            {
                return null;
            }

            var keyParameter = renderContext?.Request?.GetParameter<ObjectKeyParameter>();
            var @object = _objectManager.GetObjectByKey(keyParameter?.Value);

            if (@object is null)
            {
                return null;
            }

            var id = @object.Id.ToString("N");

            var body = new ControlPanel("object-form-view-" + id)
            {
                Classes = ["wx-kleenestar-object-form-view"]
            };

            // a class whose view form was never built - which is every class created through
            // the dialogs, because CreateStandardForm files one form of no type at all - still
            // has a record to show. The mask on the writing side answers this the same way
            // (ObjectStructuredEditFormFragmentBase falls back to the system fields when the
            // class declares no structure), so the sheet must too: a page that reported only
            // "not configured" would hide the summary and the text the author had just typed
            var sheet = BuildSheet(@object, id) ?? BuildSystemSheet(@object, id);

            body.Add(sheet);

            var tags = BuildTagRow(@object, id);

            if (tags is not null)
            {
                body.Add(tags);
            }

            return body.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds the sheet: one section per tab of the class's view form, each holding the
        /// lines the tab declares.
        /// </summary>
        /// <param name="object">The object whose values are shown.</param>
        /// <param name="id">The object id, already formatted for use in element ids.</param>
        /// <returns>The sheet, or <see langword="null"/> when the class has no active view
        /// form or the form declares no field the sheet could show.</returns>
        private IControl BuildSheet(Model.Entities.Object @object, string id)
        {
            var form = ObjectFormLayout.ResolveStandardForm(@object.ClassId, FormType.View);

            if (form?.Tabs is null || form.Tabs.Count == 0)
            {
                return null;
            }

            var fields = _fieldManager
                .GetFields(new ClassIdParameter(@object.ClassId))
                .Where(x => !x.Deprecated && x.State == FieldState.Active)
                .ToDictionary(x => x.Id);

            var values = _valueManager
                .GetValues(@object.Id)
                .GroupBy(x => x.FieldId)
                .ToDictionary(x => x.Key, x => x.First());

            var sheet = new ControlPanel("object-form-sheet-" + id)
            {
                Classes = ["ks-form-sheet"]
            };

            // the lines are numbered across the whole sheet rather than per section, the way a
            // printed form numbers them - so a line can be referred to by its number alone
            var line = 0;
            var sections = 0;

            foreach (var tab in form.Tabs.OrderBy(x => x.Position))
            {
                var lines = BuildElements(@object, tab.Elements, fields, values, ref line).ToList();

                if (lines.Count == 0)
                {
                    // a part of the form that asks nothing is not a part of the sheet
                    continue;
                }

                var grid = new ControlPanel("object-form-grid-" + tab.Id.ToString("N"))
                {
                    Classes = ["ks-form-grid"]
                };

                grid.Add(lines);
                sheet.Add(BuildSection("object-form-section-" + tab.Id.ToString("N"), tab.Name, grid));

                sections++;
            }

            return sections == 0 ? null : sheet;
        }

        /// <summary>
        /// Builds the sheet a class without a view form gets: the two attributes every object
        /// carries whatever its class models, on one section.
        /// </summary>
        /// <remarks>
        /// <see cref="Model.Entities.FormType.View"/> forms are seeded per class, but
        /// <c>FormManager.CreateStandardForm</c> - what <c>/api/1/classes</c> calls on create
        /// and on clone - files a single form and leaves its type at
        /// <see cref="Model.Entities.FormType.Default"/>, so a class an administrator makes
        /// has no view form to resolve. The writing side already answers this by falling back
        /// to the summary and the description; this is the same fallback, read.
        /// <para>
        /// The two lines are described by unsaved <see cref="Model.Entities.Field"/> instances
        /// named after the properties they stand for, so <see cref="ResolveAnswer"/> aliases
        /// them onto the object exactly as it does for a modelled field of the same name.
        /// </para>
        /// </remarks>
        /// <param name="object">The object whose values are shown.</param>
        /// <param name="id">The object id, already formatted for use in element ids.</param>
        /// <returns>The sheet.</returns>
        private static IControl BuildSystemSheet(Model.Entities.Object @object, string id)
        {
            var summary = new Model.Entities.Field
            {
                Name = nameof(Model.Entities.Object.Summary),
                FieldType = FieldType.Text,
                Required = true
            };

            var description = new Model.Entities.Field
            {
                Name = nameof(Model.Entities.Object.Description),
                FieldType = FieldType.RichText
            };

            var values = new Dictionary<Guid, Value>();

            var grid = new ControlPanel("object-form-grid-system-" + id)
            {
                Classes = ["ks-form-grid"]
            };

            grid.Add(BuildLine(@object, summary, values, 1, "kleenestar.core:object.summary.label"));
            grid.Add(BuildLine(@object, description, values, 2, "kleenestar.core:object.description.label"));

            var sheet = new ControlPanel("object-form-sheet-" + id)
            {
                Classes = ["ks-form-sheet"]
            };

            sheet.Add(BuildSection
            (
                "object-form-section-system-" + id,
                "kleenestar.core:object.renderer.form.read.system",
                grid
            ));

            return sheet;
        }

        /// <summary>
        /// Wraps a block of lines in the section the rest of the application uses for a
        /// captioned block of content.
        /// </summary>
        /// <remarks>
        /// The parts of the sheet are <see cref="ControlSection"/>s - the same control, and
        /// therefore the same caption, rule and folding, as every other captioned block on an
        /// object page. They used to be hand-built bands inside a bordered, rounded panel, which
        /// made the reading view the one card on a page that carries none: the prose renderer
        /// beside it puts the body straight onto the page, and the sections of the issue detail
        /// are ruled captions rather than boxes. A frame of its own made the record look like a
        /// widget about the object instead of the object.
        /// <para>
        /// <see cref="TypeLayoutSection.Rule"/> is what the object fragments use throughout, and
        /// it indents its body, so a part of the form lines up with the sections around it.
        /// </para>
        /// </remarks>
        /// <param name="id">The element id of the section.</param>
        /// <param name="header">The caption - the tab name, or an i18n key.</param>
        /// <param name="body">The lines the section holds.</param>
        /// <returns>The section.</returns>
        private static IControl BuildSection(string id, string header, IControl body)
        {
            var section = new ControlSection(id)
            {
                Header = _ => header,
                Layout = _ => TypeLayoutSection.Rule
            };

            section.Add(body);

            return section;
        }

        /// <summary>
        /// Walks the elements of a tab in document order and turns each into its printed
        /// counterpart: a group into a sub-caption spanning the grid followed by its
        /// children, a field reference into one numbered line.
        /// </summary>
        /// <param name="object">The object whose values are shown.</param>
        /// <param name="elements">The elements to walk.</param>
        /// <param name="fields">The active fields of the class, by id.</param>
        /// <param name="values">The object's values, by field id.</param>
        /// <param name="line">The running line number, carried across the whole sheet.</param>
        /// <returns>The controls.</returns>
        private static IEnumerable<IControl> BuildElements
        (
            Model.Entities.Object @object,
            IEnumerable<FormElement> elements,
            IDictionary<Guid, Model.Entities.Field> fields,
            IDictionary<Guid, Value> values,
            ref int line
        )
        {
            var controls = new List<IControl>();

            foreach (var element in (elements ?? []).OrderBy(x => x.Position))
            {
                if (element is FormGroupElement group)
                {
                    var children = BuildElements(@object, group.Children, fields, values, ref line).ToList();

                    if (children.Count == 0)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(group.Label))
                    {
                        controls.Add(new ControlText("object-form-group-" + group.Id.ToString("N"))
                        {
                            Text = _ => group.Label,
                            Format = _ => TypeFormatText.Span,
                            Classes = ["ks-form-group-label"]
                        });
                    }

                    controls.AddRange(children);

                    continue;
                }

                if (element is not FormFieldRefElement reference ||
                    !fields.TryGetValue(reference.FieldId, out var field))
                {
                    continue;
                }

                controls.Add(BuildLine(@object, field, values, ++line));
            }

            return controls;
        }

        /// <summary>
        /// Builds one line of the sheet: the number, what is being asked, and the ruled box
        /// the answer is printed in.
        /// </summary>
        /// <param name="object">The object the line is answered from.</param>
        /// <param name="field">The field the line asks about.</param>
        /// <param name="values">The object's values, by field id.</param>
        /// <param name="line">The line number.</param>
        /// <param name="label">
        /// What the line is captioned with, when that is not the field's own name - the system
        /// lines are named after the properties they alias so they resolve, and captioned from
        /// the internationalization the rest of the application titles them with.
        /// </param>
        /// <returns>The control.</returns>
        private static IControl BuildLine
        (
            Model.Entities.Object @object,
            Model.Entities.Field field,
            IDictionary<Guid, Value> values,
            int line,
            string label = null
        )
        {
            // the line number, not the field, is what makes an element id unique here: nothing
            // stops a form from referencing the same field on two tabs, and the sheet prints a
            // line for each occurrence - keyed by the field alone the two would share their ids
            var id = line.ToString() + "-" + field.Id.ToString("N");
            var (data, prose) = ResolveAnswer(@object, field, values);
            var empty = string.IsNullOrWhiteSpace(data);

            var caption = new ControlPanel("object-form-cap-" + id)
            {
                Classes = ["ks-form-field-cap"]
            };

            caption.Add(new ControlText("object-form-no-" + id)
            {
                Text = _ => line.ToString(),
                Format = _ => TypeFormatText.Span,
                Classes = ["ks-form-field-no"]
            });

            caption.Add(new ControlText("object-form-name-" + id)
            {
                // the caption is handed over on its own: the control translates whatever it is
                // given, and a name that is an internationalization key stops resolving the
                // moment anything is concatenated onto it
                Text = _ => label ?? field.Name,
                Format = _ => TypeFormatText.Span,
                Classes = ["ks-form-field-name"],

                // what the field is for is the explanation a printed form prints in its
                // margin; here it is the tooltip of the line it belongs to
                Title = _ => string.IsNullOrWhiteSpace(field.Description) ? null : field.Description
            });

            if (field.Required)
            {
                // the mark a form puts beside the lines that have to be filled in, in its own
                // node so it cannot become part of the name above
                caption.Add(new ControlText("object-form-req-" + id)
                {
                    Text = _ => "*",
                    Format = _ => TypeFormatText.Span,
                    Classes = ["ks-form-field-required"]
                });
            }

            var box = new ControlPanel("object-form-box-" + id)
            {
                Classes = BoxClasses(prose, empty)
            };

            if (prose)
            {
                // rich text is markup, not a line: it is handed to the client to lay out the
                // way the prose reading view hands over a document body
                box.Add(new ControlContent("object-form-prose-" + id)
                {
                    Content = _ => data,
                    Format = _ => TypeFormatContent.RichText,
                    Classes = ["ks-prose-content"]
                });
            }
            else if (!empty)
            {
                box.Add(new ControlText("object-form-value-" + id)
                {
                    Text = ctx => ObjectValueFormat.Format(ctx, field, data),
                    Format = _ => TypeFormatText.Span
                });
            }

            var wide = prose || field.FieldType == FieldType.Multiline;

            var control = new ControlPanel("object-form-field-" + id)
            {
                Classes = wide ? ["ks-form-field", "ks-form-field--wide"] : ["ks-form-field"]
            };

            control.Add(caption, box);

            return control;
        }

        /// <summary>
        /// Answers where the value of a line actually lives, and whether it is markup.
        /// </summary>
        /// <remarks>
        /// Almost always it is the field's own <see cref="Value"/> row. The exception is a
        /// field whose name aliases a system attribute of the object: the mask that writes
        /// this sheet names its inputs after the fields, and the object endpoint binds a
        /// payload key matching a property of <see cref="Model.Entities.Object"/> to the
        /// object itself - <c>UpsertFieldValues</c> skips it deliberately, so no value row is
        /// ever written for it. Reading such a line from the value rows would therefore show
        /// an empty box beside an edit form that has the text in it. The description is rich
        /// text wherever it came from, whatever type the field aliasing it declares.
        /// </remarks>
        /// <param name="object">The object the line is answered from.</param>
        /// <param name="field">The field the line asks about.</param>
        /// <param name="values">The object's values, by field id.</param>
        /// <returns>The value and whether it has to be laid out as markup.</returns>
        private static (string Data, bool Prose) ResolveAnswer
        (
            Model.Entities.Object @object,
            Model.Entities.Field field,
            IDictionary<Guid, Value> values
        )
        {
            if (string.Equals(field.Name, nameof(Model.Entities.Object.Description), StringComparison.OrdinalIgnoreCase))
            {
                return (@object.Description, true);
            }

            if (string.Equals(field.Name, nameof(Model.Entities.Object.Summary), StringComparison.OrdinalIgnoreCase))
            {
                return (@object.Summary, false);
            }

            values.TryGetValue(field.Id, out var value);

            return (value?.Data, ObjectValueFormat.IsRichText(field));
        }

        /// <summary>
        /// Names the classes of the answer box: the box itself, plus what it is holding -
        /// a paragraph, or nothing at all.
        /// </summary>
        /// <param name="prose">Whether the box holds rich text.</param>
        /// <param name="empty">Whether the field carries no value.</param>
        /// <returns>The classes.</returns>
        private static List<string> BoxClasses(bool prose, bool empty)
        {
            var classes = new List<string> { "ks-form-field-box" };

            if (prose)
            {
                classes.Add("ks-form-field-box--prose");
            }

            if (empty && !prose)
            {
                // the hatch is what says "nothing was entered here"; a rich-text box is tall
                // enough that hatching it would read as a defect rather than as a blank line
                classes.Add("ks-form-field-box--empty");
            }

            return classes;
        }

        /// <summary>
        /// Builds the row of tag badges that closes the sheet off, or <see langword="null"/>
        /// when the object carries no tags. The prose reading view of the same kinds ends the
        /// same way, so switching a class between the two renderers does not lose the tags.
        /// </summary>
        /// <param name="object">The object whose tags are shown.</param>
        /// <param name="id">The object id, already formatted for use in element ids.</param>
        /// <returns>The tag row, or <see langword="null"/>.</returns>
        private IControl BuildTagRow(Model.Entities.Object @object, string id)
        {
            var tags = _tagManager.GetTags(@object.Id).ToList();

            if (tags.Count == 0)
            {
                return null;
            }

            var row = new ControlPanel("object-form-view-tags-" + id)
            {
                Classes = ["ks-prose-tags"]
            };

            foreach (var tag in tags)
            {
                row.Add(ObjectTagBadge.Create(tag, "object-form-view-tag-"));
            }

            return row;
        }
    }
}
