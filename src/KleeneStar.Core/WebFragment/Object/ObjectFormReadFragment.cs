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

            var sheet = BuildSheet(@object, id);

            if (sheet is null)
            {
                // a class whose view form was deleted or emptied still has a reading view; it
                // says so rather than rendering an empty page the reader cannot interpret
                body.Add(new ControlText("object-form-view-empty-" + id)
                {
                    Text = _ => "kleenestar.core:object.renderer.form.read.empty",
                    Format = _ => TypeFormatText.Italic,
                    TextColor = _ => new PropertyColorText(TypeColorText.Muted)
                });
            }
            else
            {
                body.Add(sheet);
            }

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

                var head = new ControlPanel("object-form-head-" + tab.Id.ToString("N"))
                {
                    Classes = ["ks-form-section-head"]
                };

                head.Add(new ControlText("object-form-head-text-" + tab.Id.ToString("N"))
                {
                    Text = _ => tab.Name,
                    Format = _ => TypeFormatText.Span
                });

                var section = new ControlPanel("object-form-section-" + tab.Id.ToString("N"))
                {
                    Classes = ["ks-form-section"]
                };

                section.Add(head, grid);
                sheet.Add(section);

                sections++;
            }

            return sections == 0 ? null : sheet;
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
        /// <returns>The control.</returns>
        private static IControl BuildLine
        (
            Model.Entities.Object @object,
            Model.Entities.Field field,
            IDictionary<Guid, Value> values,
            int line
        )
        {
            var id = field.Id.ToString("N");
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
                Text = _ => field.Name + (field.Required ? " *" : string.Empty),
                Format = _ => TypeFormatText.Span,
                Classes = ["ks-form-field-name"],

                // what the field is for is the explanation a printed form prints in its
                // margin; here it is the tooltip of the line it belongs to
                Title = _ => string.IsNullOrWhiteSpace(field.Description) ? null : field.Description
            });

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
