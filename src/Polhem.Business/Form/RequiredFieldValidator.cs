using System.Data;
using Polhem.Base.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Finds the first field marked <see cref="FormField.Required"/> that a save would store empty.
    /// </summary>
    /// <remarks>
    /// Only persisted fields are checked: a relation or virtual field is never written, and an
    /// auto-increment field is filled by the database. The emptiness rule is on
    /// <see cref="IsEmpty(FormField, object?)"/>.
    /// </remarks>
    internal static class RequiredFieldValidator
    {
        /// <summary>
        /// Returns the first table and field of <paramref name="schema"/> whose value is empty in an
        /// added or modified row of <paramref name="dataSet"/>, or <c>null</c> when every required
        /// field is filled.
        /// </summary>
        /// <remarks>
        /// The master table is checked before the detail tables. An added row that does not carry a
        /// required column is judged by the value the insert would store instead, the type's default;
        /// a modified row that does not carry it leaves the stored value alone and is not judged.
        /// </remarks>
        /// <param name="schema">The form schema.</param>
        /// <param name="dataSet">The data set about to be saved.</param>
        public static (FormTable Table, FormField Field)? FindEmpty(FormSchema schema, DataSet dataSet)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(dataSet);
            if (schema.Tables == null) { return null; }

            var master = schema.MasterTable;
            var ordered = master == null
                ? schema.Tables
                : schema.Tables.Where(t => !ReferenceEquals(t, master)).Prepend(master);

            foreach (var formTable in ordered)
            {
                if (!dataSet.Tables.Contains(formTable.TableName)) { continue; }

                var field = FindEmpty(formTable, dataSet.Tables[formTable.TableName]!);
                if (field != null) { return (formTable, field); }
            }
            return null;
        }

        /// <summary>
        /// Returns whether <paramref name="value"/> counts as empty for <paramref name="field"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Empty means: <c>null</c> or <see cref="DBNull"/>; a string that is empty or only white
        /// space; <see cref="Guid.Empty"/>, including its string form in a
        /// <see cref="FieldDbType.Guid"/> field; a zero-length <see cref="byte"/> array.
        /// </para>
        /// <para>
        /// A number, a <see cref="bool"/> and a date or time are never empty. Those columns are
        /// <c>NOT NULL</c> with a type default (<see cref="FormRowDefaults"/>), so <c>0</c>,
        /// <c>false</c> or the seeded date are values a user may mean, and nothing tells them apart
        /// from a field that was left alone.
        /// </para>
        /// </remarks>
        /// <param name="field">The field the value belongs to.</param>
        /// <param name="value">The value to test.</param>
        public static bool IsEmpty(FormField field, object? value) => value switch
        {
            null => true,
            DBNull => true,
            string text => string.IsNullOrWhiteSpace(text)
                || (field.DbType == FieldDbType.Guid && Guid.TryParse(text, out var parsed) && parsed == Guid.Empty),
            Guid guid => guid == Guid.Empty,
            byte[] bytes => bytes.Length == 0,
            _ => false,
        };

        private static FormField? FindEmpty(FormTable formTable, DataTable table)
        {
            if (formTable.Fields == null) { return null; }

            var required = formTable.Fields
                .Where(f => f.Required && f.Type == FieldType.DbField && f.DbType != FieldDbType.AutoIncrement)
                .ToList();
            if (required.Count == 0) { return null; }

            foreach (DataRow row in table.Rows)
            {
                if (row.RowState is not (DataRowState.Added or DataRowState.Modified)) { continue; }

                foreach (var field in required)
                {
                    if (IsEmptyInRow(field, row)) { return field; }
                }
            }
            return null;
        }

        private static bool IsEmptyInRow(FormField field, DataRow row)
        {
            if (row.Table.Columns.Contains(field.FieldName))
                return IsEmpty(field, row[field.FieldName]);

            return row.RowState == DataRowState.Added && IsEmpty(field, field.DbType.GetDefaultValue());
        }
    }
}
