namespace KleeneStar.Core.WebRestApi
{
    /// <summary>
    /// A catalog column together with the state the current user gave it: whether the
    /// table shows it and how wide it is. The position in the list the state belongs to
    /// is the column's display order.
    /// </summary>
    internal sealed class ObjectTableColumnState
    {
        /// <summary>
        /// Gets the column.
        /// </summary>
        public ObjectTableColumn Column { get; init; }

        /// <summary>
        /// Gets a value indicating whether the table shows the column. A hidden column
        /// still travels to the client with its content, so switching it on in the column
        /// manager shows the values without a round trip.
        /// </summary>
        public bool Visible { get; init; }

        /// <summary>
        /// Gets the user-defined column width in pixels, or null for auto.
        /// </summary>
        public uint? Width { get; init; }
    }
}
