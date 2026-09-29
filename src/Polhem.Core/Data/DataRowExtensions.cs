using System.Data;
using System.Globalization;

namespace Polhem.Core.Data
{
    /// <summary>
    /// Extension methods for <see cref="DataRow"/>.
    /// </summary>
    public static class DataRowExtensions
    {
        /// <summary>
        /// Gets the value of the specified column and converts it to the target type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="row">The data row.</param>
        /// <param name="columnName">The column name.</param>
        /// <returns>The column value converted to <typeparamref name="T"/>, or <c>default(T)</c> if the value is <see cref="DBNull"/>.</returns>
        /// <exception cref="InvalidOperationException">The column does not exist or the conversion fails.</exception>
        public static T GetFieldValue<T>(this DataRow row, string columnName)
        {
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName), "Parameter cannot be null or empty.");

            if (!row.Table.Columns.Contains(columnName))
                throw new InvalidOperationException($"Unable to get field value: Column '{columnName}' does not exist.");

            object value = row[columnName];

            if (value == DBNull.Value) { return default!; }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert field '{columnName}' to type {typeof(T).Name}: {ex.Message}", ex
                );
            }
        }

        /// <summary>
        /// Gets the value of the specified column and converts it to the target type, returning a default value if the column does not exist.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="row">The data row.</param>
        /// <param name="columnName">The column name.</param>
        /// <param name="defaultValue">The default value to return if the column does not exist.</param>
        /// <returns>The column value converted to <typeparamref name="T"/>, or <c>default(T)</c> if the value is <see cref="DBNull"/>.</returns>
        public static T GetFieldValue<T>(this DataRow row, string columnName, T defaultValue)
        {
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName), "Parameter cannot be null or empty.");

            if (!row.Table.Columns.Contains(columnName))
                return defaultValue;

            object value = row[columnName];

            if (value == DBNull.Value) { return default!; }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to convert field '{columnName}' to type {typeof(T).Name}: {ex.Message}", ex
                );
            }
        }

        /// <summary>
        /// Rewrites both the Original and the Current version of a modified row, leaving it Modified
        /// with every value the rewrite does not change intact.
        /// </summary>
        /// <param name="row">A row in the <see cref="DataRowState.Modified"/> state.</param>
        /// <param name="rewrite">
        /// Given a column, the version being rewritten and the value that version holds, returns the
        /// value the version should hold. Return the value unchanged to keep it.
        /// </param>
        /// <remarks>
        /// ADO.NET offers no way to write the Original version directly. The row is therefore rejected
        /// back to Original, given the new Original values, accepted so those become the new Original,
        /// then given its new Current values.
        /// <para>
        /// <see cref="DataRow.RejectChanges"/> reverts every column, so both versions are captured across
        /// the whole row before it runs. Restoring only the rewritten columns would silently discard
        /// every other edit on the row.
        /// </para>
        /// <para>
        /// Only values that differ from what the row holds are written. That lets the rewrite run over
        /// the whole row: an expression column computes its own value and rejects a write, and a
        /// read-only column that did not change is never assigned, so it cannot raise
        /// <see cref="ReadOnlyException"/>.
        /// </para>
        /// <para>
        /// A row whose two versions end up equal is set back to Modified, because callers that pick an
        /// UPDATE from the row state alone would otherwise skip it.
        /// </para>
        /// </remarks>
        public static void RewriteVersions(this DataRow row, Func<DataColumn, DataRowVersion, object, object> rewrite)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(rewrite);

            var original = CaptureVersion(row, DataRowVersion.Original, rewrite);
            var current = CaptureVersion(row, DataRowVersion.Current, rewrite);

            row.RejectChanges();
            WriteChangedValues(row, original);
            row.AcceptChanges();
            WriteChangedValues(row, current);

            if (row.RowState == DataRowState.Unchanged) { row.SetModified(); }
        }

        private static object[] CaptureVersion(DataRow row, DataRowVersion version,
            Func<DataColumn, DataRowVersion, object, object> rewrite)
        {
            var values = new object[row.Table.Columns.Count];
            foreach (DataColumn column in row.Table.Columns)
            {
                values[column.Ordinal] = rewrite(column, version, row[column, version]);
            }
            return values;
        }

        private static void WriteChangedValues(DataRow row, object[] values)
        {
            foreach (DataColumn column in row.Table.Columns)
            {
                if (column.Expression.Length > 0 || Equals(row[column], values[column.Ordinal])) { continue; }
                row[column] = values[column.Ordinal];
            }
        }
    }
}
