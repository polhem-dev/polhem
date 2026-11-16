using System.Data;
using Polhem.Core;
using Polhem.Definition;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Adds to a save payload the stored detail rows it does not carry, so a master total computed from a detail
    /// table (<c>Sum(Table.field)</c>) adds up every row of the record and not only the rows the caller sent.
    /// </summary>
    /// <remarks>
    /// The added rows are <see cref="DataRowState.Unchanged"/>: the repository writes no unchanged row, the change
    /// audit reads only changes, and the save pass recomputes only changed rows. They only complete what the
    /// aggregates read. A row is matched by <see cref="SysFields.RowId"/>, so a row the payload deletes or edits is
    /// not added a second time.
    /// </remarks>
    internal static class AggregatedDetailCompletion
    {
        /// <summary>
        /// Imports the rows of each named table of <paramref name="stored"/> that <paramref name="payload"/> does not
        /// carry.
        /// </summary>
        /// <param name="payload">The save payload, completed in place.</param>
        /// <param name="stored">The record as stored.</param>
        /// <param name="tableNames">The detail tables to complete.</param>
        public static void ImportMissingRows(DataSet payload, DataSet stored, IEnumerable<string> tableNames)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(stored);
            ArgumentNullException.ThrowIfNull(tableNames);

            foreach (var tableName in tableNames)
            {
                if (!stored.Tables.Contains(tableName)) { continue; }
                var source = stored.Tables[tableName]!;
                if (!source.Columns.Contains(SysFields.RowId)) { continue; }

                DataTable target;
                if (payload.Tables.Contains(tableName))
                {
                    target = payload.Tables[tableName]!;
                    // Without a row id the carried rows cannot be told apart from the stored ones, and importing
                    // them all would count a carried row twice.
                    if (target.Rows.Count > 0 && !target.Columns.Contains(SysFields.RowId)) { continue; }
                }
                else
                {
                    target = source.Clone();
                    payload.Tables.Add(target);
                }

                var carried = CarriedRowIds(target);
                foreach (DataRow row in source.Rows)
                {
                    var rowId = ValueUtilities.CGuid(row[SysFields.RowId]);
                    if (rowId != Guid.Empty && carried.Contains(rowId)) { continue; }
                    target.ImportRow(row);
                }
            }
        }

        private static HashSet<Guid> CarriedRowIds(DataTable table)
        {
            var rowIds = new HashSet<Guid>();
            if (!table.Columns.Contains(SysFields.RowId)) { return rowIds; }
            foreach (DataRow row in table.Rows)
            {
                if (row.RowState == DataRowState.Detached) { continue; }
                var version = row.RowState == DataRowState.Deleted ? DataRowVersion.Original : DataRowVersion.Current;
                var rowId = ValueUtilities.CGuid(row[SysFields.RowId, version]);
                if (rowId != Guid.Empty) { rowIds.Add(rowId); }
            }
            return rowIds;
        }
    }
}
