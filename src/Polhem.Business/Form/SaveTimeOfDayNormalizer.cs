using System.Data;
using Polhem.Core;
using Polhem.Core.Data;
using Polhem.Core.Exceptions;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Writes the <see cref="FieldDbType.Time"/> values of a data set about to be saved in their fixed-width
    /// <c>HH:mm</c> storage form.
    /// </summary>
    /// <remarks>
    /// The fixed width is what makes a time-of-day column sort and range-scan in time order (ADR-033). The UI heads
    /// normalize as the user types, but a caller of the API does not have to go through them, so the save does it
    /// again. An empty string means "not filled in" and is kept, and a value of white space becomes one. A modified row
    /// keeps a value the save does not change, even one stored before the server normalized.
    /// </remarks>
    internal static class SaveTimeOfDayNormalizer
    {
        /// <summary>
        /// Normalizes every <see cref="FieldDbType.Time"/> value of the added and modified rows.
        /// </summary>
        /// <param name="schema">The form schema.</param>
        /// <param name="dataSet">The data set about to be saved; rewritten in place.</param>
        /// <param name="fromCaller">
        /// Whether the values came from the caller of the save. A value the caller sent that is not a time of day is
        /// refused as an invalid request; one that server code wrote afterwards is a fault of that code.
        /// </param>
        /// <exception cref="InvalidRequestValueException">
        /// <paramref name="fromCaller"/> is <c>true</c> and a value is not a time of day.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="fromCaller"/> is <c>false</c> and a value is not a time of day.
        /// </exception>
        public static void Normalize(FormSchema schema, DataSet dataSet, bool fromCaller)
        {
            if (schema.Tables == null) { return; }

            foreach (var formTable in schema.Tables)
            {
                if (formTable.Fields == null || !dataSet.Tables.Contains(formTable.TableName)) { continue; }

                var table = dataSet.Tables[formTable.TableName]!;
                foreach (var field in formTable.Fields)
                {
                    if (field.Type != FieldType.DbField || field.DbType != FieldDbType.Time) { continue; }
                    var column = table.Columns[field.FieldName];
                    // A column with an expression computes its own value and rejects a write.
                    if (column == null || column.Expression.Length > 0) { continue; }

                    foreach (DataRow row in table.Rows)
                    {
                        if (row.RowState is not (DataRowState.Added or DataRowState.Modified)) { continue; }
                        NormalizeCell(row, column, formTable.TableName, fromCaller);
                    }
                }
            }
        }

        private static void NormalizeCell(DataRow row, DataColumn column, string tableName, bool fromCaller)
        {
            var value = row[column];
            if (value is DBNull) { return; }
            // A value this save does not change is left as stored. A value saved before the server normalized may not
            // be a time of day, and refusing it would stop a save that only edits another field of the record.
            if (row.RowState == DataRowState.Modified && Equals(row[column, DataRowVersion.Original], value)) { return; }

            var text = ValueUtilities.CStr(value);
            if (text.Trim().Length == 0)
            {
                if (text.Length > 0) row[column] = string.Empty;
                return;
            }

            // The cell's own value, so a TimeSpan or DateTime a typed codec carried converts as such.
            var normalized = ValueUtilities.CTimeString(value);
            if (normalized.Length == 0)
            {
                // The message names the field the schema declares, never the value the caller sent.
                var message = $"The value of the field '{column.ColumnName}' in '{tableName}' is not a valid time of day. Expected HH:mm or H:mm.";
                if (fromCaller) throw new InvalidRequestValueException(message);
                throw new InvalidOperationException(message);
            }
            if (!string.Equals(normalized, text, StringComparison.Ordinal)) row[column] = normalized;
        }
    }
}
