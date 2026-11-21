using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebApp.WebRestApi;

using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// One offerable column of an object overview table: how it is labelled, how its
    /// cell is drawn and edited, and how an object's value for it is read.
    /// </summary>
    internal sealed class ObjectTableColumn
    {
        /// <summary>
        /// Gets the stable column id. System columns use the property name; field
        /// columns use <see cref="ObjectTableColumnCatalog.FieldColumnPrefix"/> followed
        /// by the lower-cased field name. The id is what the stored per-user layout
        /// refers to, so it must survive a field being edited or a class being added.
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// Gets the column header.
        /// </summary>
        public string Label { get; init; }

        /// <summary>
        /// Gets the payload key an inline edit of this column writes, which is the
        /// property name for a system column and the field name for a field column.
        /// Null for a column that cannot be written.
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Gets a value indicating whether the column is shown to a user who has not
        /// configured the table yet.
        /// </summary>
        public bool DefaultVisible { get; init; }

        /// <summary>
        /// Gets the cell template.
        /// </summary>
        public ObjectTableColumnTemplate Template { get; init; }

        /// <summary>
        /// Gets the ids of the class fields this column stands for. A field column folds
        /// the same-named fields of every class of the kind, so an object contributes the
        /// value of whichever of them its own class defines. Empty for a system column.
        /// </summary>
        public IReadOnlySet<Guid> FieldIds { get; init; } = new HashSet<Guid>();

        /// <summary>
        /// Gets the field type of a field column, or null for a system column.
        /// </summary>
        public FieldType? FieldType { get; init; }

        /// <summary>
        /// Gets the projection of an object onto the cell content of this column.
        /// </summary>
        public Func<ObjectEntity, ObjectTableProjection, string> Read { get; init; }

        /// <summary>
        /// Projects the column onto the REST payload the client renders it from.
        /// </summary>
        /// <param name="visible">Whether the user has the column shown.</param>
        /// <param name="width">The user-defined width, or null for auto.</param>
        /// <returns>The REST column.</returns>
        public RestApiTableColumn ToRestApiColumn(bool visible, uint? width)
        {
            return new RestApiTableColumn
            {
                Id = Id,
                Label = Label,
                Name = Name,
                Visible = visible,
                Width = width,
                Template = Template
            };
        }
    }
}
