using System.Data;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Polhem.Base.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Language;

namespace Polhem.Definition.Forms
{
    /// <summary>
    /// Decides which fields marked <see cref="FormField.Required"/> a save would store empty: the one
    /// rule the server enforces on save and the UI heads check before sending.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The server's save (<c>FormBusinessObject</c> in <c>Polhem.Business</c>) refuses the first field
    /// this reports; the UI heads (the Avalonia <c>FormView</c>, the Blazor <c>FormPage</c>) name every
    /// field it reports before the save leaves the client. Both call this class, so a record the client
    /// lets through is not refused by the server for an empty required field unless the data changes in
    /// between (the server fills its own values, such as default-value expressions, before it checks).
    /// </para>
    /// <para>
    /// Only persisted fields are checked: a relation or virtual field is never written, and an
    /// auto-increment field is filled by the database. The meaning of empty is on <see cref="IsEmpty"/>.
    /// </para>
    /// </remarks>
    public static class RequiredFieldCheck
    {
        /// <summary>
        /// Returns the required fields of <paramref name="schema"/> that are empty in an added or
        /// modified row of <paramref name="dataSet"/>, each field once: the master table's first, then
        /// each detail table's, in the schema's table and field order.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <param name="dataSet">The data set about to be saved.</param>
        /// <returns>The empty required fields; an empty list when every required field is filled.</returns>
        /// <remarks>
        /// An unchanged or deleted row is not judged: the save does not write it. An added row that
        /// lacks a required column is judged by the value an insert would store, the type's default;
        /// a modified row that lacks it keeps its stored value and is not judged.
        /// </remarks>
        public static IReadOnlyList<MissingRequiredField> FindMissing(FormSchema schema, DataSet dataSet)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(dataSet);

            var missing = new List<MissingRequiredField>();
            if (schema.Tables is null) { return missing; }

            var master = schema.MasterTable;
            var ordered = master is null
                ? schema.Tables
                : schema.Tables.Where(t => !ReferenceEquals(t, master)).Prepend(master);

            foreach (var formTable in ordered)
            {
                if (formTable.Fields is null || !dataSet.Tables.Contains(formTable.TableName)) { continue; }

                var table = dataSet.Tables[formTable.TableName]!;
                bool isDetail = !ReferenceEquals(formTable, master);
                var empty = formTable.Fields
                    .Where(IsChecked)
                    .Where(field => table.Rows.Cast<DataRow>().Any(row => IsEmptyInRow(field, row)));
                missing.AddRange(empty.Select(field => new MissingRequiredField(formTable, field, isDetail)));
            }
            return missing;
        }

        /// <summary>
        /// Returns whether <paramref name="value"/> counts as empty for <paramref name="field"/>.
        /// </summary>
        /// <param name="field">The field the value belongs to.</param>
        /// <param name="value">The value to test.</param>
        /// <returns><c>true</c> when a required field holding the value would be reported.</returns>
        /// <remarks>
        /// <para>
        /// Empty means <c>null</c> or <see cref="DBNull"/>; a string that is empty or only white space;
        /// <see cref="Guid.Empty"/>, including its string form in a <see cref="FieldDbType.Guid"/> field;
        /// a zero-length <see cref="byte"/> array.
        /// </para>
        /// <para>
        /// A number, a <see cref="bool"/> and a date or time are never empty. Those columns are
        /// <c>NOT NULL</c> with a type default (<see cref="FormRowDefaults"/>), so <c>0</c>, <c>false</c> or the seeded date are
        /// values a user may mean, and nothing tells them apart from a field that was left alone.
        /// </para>
        /// </remarks>
        public static bool IsEmpty(FormField field, object? value)
        {
            ArgumentNullException.ThrowIfNull(field);

            return value switch
            {
                null => true,
                DBNull => true,
                string text => string.IsNullOrWhiteSpace(text)
                    || (field.DbType == FieldDbType.Guid && Guid.TryParse(text, out var parsed) && parsed == Guid.Empty),
                Guid guid => guid == Guid.Empty,
                byte[] bytes => bytes.Length == 0,
                _ => false,
            };
        }

        /// <summary>
        /// Builds the prompt that names <paramref name="missing"/> to the user, in the culture of
        /// <paramref name="localizer"/>.
        /// </summary>
        /// <param name="localizer">
        /// The localizer of <see cref="PolhemUIText"/> the head reads its own text from.
        /// </param>
        /// <param name="missing">The empty required fields, as <see cref="FindMissing"/> returns them.</param>
        /// <returns>
        /// The <see cref="PolhemUIText.RequiredFieldsEmpty"/> text with the fields'
        /// <see cref="MissingRequiredField.DisplayText"/> listed in order.
        /// </returns>
        public static string FormatPrompt(IStringLocalizer localizer, IReadOnlyList<MissingRequiredField> missing)
        {
            ArgumentNullException.ThrowIfNull(localizer);
            ArgumentNullException.ThrowIfNull(missing);

            string template = PolhemUIText.Get(localizer, PolhemUIText.RequiredFieldsEmpty);
            string fields = string.Join(", ", missing.Select(m => m.DisplayText));
            return string.Format(CultureInfo.CurrentCulture, template, fields);
        }

        private static bool IsChecked(FormField field)
            => field.Required && field.Type == FieldType.DbField && field.DbType != FieldDbType.AutoIncrement;

        private static bool IsEmptyInRow(FormField field, DataRow row)
        {
            if (row.RowState is not (DataRowState.Added or DataRowState.Modified)) { return false; }
            if (row.Table.Columns.Contains(field.FieldName))
                return IsEmpty(field, row[field.FieldName]);

            return row.RowState == DataRowState.Added && IsEmpty(field, field.DbType.GetDefaultValue());
        }
    }
}
