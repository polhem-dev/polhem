using Polhem.Definition;
using Polhem.Definition.Forms;

namespace Polhem.UI.Core.Connectors
{
    /// <summary>
    /// The columns a form's list asks the server for.
    /// </summary>
    /// <remarks>
    /// <see cref="FormSchema.ListFields"/> drives the server's SELECT but does not name <see cref="SysFields.RowId"/>,
    /// and a list without it cannot open the row the user picks. Every head that lists a form asks with this value,
    /// so the rule lives once.
    /// </remarks>
    public static class ListSelectFields
    {
        /// <summary>
        /// Returns <see cref="SysFields.RowId"/> followed by the form's list fields, comma separated and without
        /// duplicates (compared ignoring case, as field names are).
        /// </summary>
        /// <param name="schema">The form schema.</param>
        public static string For(FormSchema schema)
        {
            ArgumentNullException.ThrowIfNull(schema);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SysFields.RowId };
            var parts = new List<string> { SysFields.RowId };
            foreach (var name in (schema.ListFields ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = name.Trim();
                if (trimmed.Length > 0 && seen.Add(trimmed))
                    parts.Add(trimmed);
            }
            return string.Join(",", parts);
        }
    }
}
