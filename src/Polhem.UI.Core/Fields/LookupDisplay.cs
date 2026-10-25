namespace Polhem.UI.Core.Fields
{
    /// <summary>
    /// Composes the text a lookup field shows: the values of its display fields, the same in a form
    /// editor and a grid cell, in every UI head.
    /// </summary>
    public static class LookupDisplay
    {
        /// <summary>
        /// Separator between the display-field values. A plain space would be ambiguous because names
        /// themselves contain spaces (e.g. "Alice Chen"), so the id and name segments are joined as
        /// "E001 - Alice Chen".
        /// </summary>
        public const string Separator = " - ";

        /// <summary>
        /// Joins the non-empty display-field values with <see cref="Separator"/>.
        /// </summary>
        /// <param name="values">The display-field values in declaration order.</param>
        /// <returns>The display text, or an empty string when every value is empty.</returns>
        public static string Compose(IEnumerable<string> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            return string.Join(Separator, values.Where(v => !string.IsNullOrEmpty(v)));
        }
    }
}
