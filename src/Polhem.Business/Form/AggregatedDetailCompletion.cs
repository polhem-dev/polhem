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

        /// <summary>
        /// Removes the unchanged rows of each named table from <paramref name="payload"/>.
        /// </summary>
        /// <param name="payload">The save payload, changed in place.</param>
        /// <param name="tableNames">The detail tables a total or a rule aggregates.</param>
        /// <remarks>
        /// The repository writes no unchanged row, so its values are whatever the caller sent, not what is stored.
        /// Removed here, an existing record gets the stored rows back from <see cref="ImportMissingRows"/>, and a new
        /// record has none to count.
        /// </remarks>
        public static void DropUnchangedRows(DataSet payload, IEnumerable<string> tableNames)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(tableNames);
            foreach (var tableName in tableNames)
            {
                if (!payload.Tables.Contains(tableName)) { continue; }
                var table = payload.Tables[tableName]!;
                foreach (var row in table.Rows.Cast<DataRow>().Where(row => row.RowState == DataRowState.Unchanged).ToList())
                    table.Rows.Remove(row);
            }
        }

        /// <summary>
        /// Returns the first named table of <paramref name="payload"/> that has a row which does not belong to the
        /// record being saved, or <c>null</c> when every row does.
        /// </summary>
        /// <param name="payload">The save payload, after <see cref="DropUnchangedRows"/>.</param>
        /// <param name="tableNames">The detail tables a total or a rule aggregates.</param>
        /// <param name="masterRowId">The row id of the record's master row; <see cref="Guid.Empty"/> when it has none.</param>
        /// <param name="stored">The record as stored; <c>null</c> for a new record or one that is not stored.</param>
        /// <remarks>
        /// An added row must name the master row as its master. A modified or deleted row must be one of the stored
        /// rows of the record, judged by the stored data rather than by the original values the caller sent, and a
        /// modified row must keep naming the master row. Every row must carry its row id.
        /// </remarks>
        public static string? FindForeignTable(DataSet payload, IEnumerable<string> tableNames, Guid masterRowId,
            DataSet? stored)
        {
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(tableNames);
            foreach (var tableName in tableNames)
            {
                if (!payload.Tables.Contains(tableName)) { continue; }
                var table = payload.Tables[tableName]!;
                var rows = table.Rows.Cast<DataRow>().Where(row => row.RowState != DataRowState.Detached).ToList();
                if (rows.Count == 0) { continue; }
                if (masterRowId == Guid.Empty || !table.Columns.Contains(SysFields.RowId)
                    || !table.Columns.Contains(SysFields.MasterRowId))
                    return table.TableName;

                var storedRowIds = StoredRowIds(stored, tableName);
                foreach (var row in rows)
                {
                    var version = row.RowState == DataRowState.Deleted ? DataRowVersion.Original : DataRowVersion.Current;
                    var rowId = ValueUtilities.CGuid(row[SysFields.RowId, version]);
                    if (rowId == Guid.Empty) { return table.TableName; }
                    bool belongs = row.RowState switch
                    {
                        DataRowState.Added => ValueUtilities.CGuid(row[SysFields.MasterRowId]) == masterRowId,
                        DataRowState.Modified => storedRowIds.Contains(rowId)
                            && ValueUtilities.CGuid(row[SysFields.MasterRowId]) == masterRowId,
                        DataRowState.Deleted => storedRowIds.Contains(rowId),
                        _ => true,
                    };
                    if (!belongs) { return table.TableName; }
                }
            }
            return null;
        }

        private static HashSet<Guid> StoredRowIds(DataSet? stored, string tableName)
        {
            var rowIds = new HashSet<Guid>();
            if (stored is null || !stored.Tables.Contains(tableName)) { return rowIds; }
            var table = stored.Tables[tableName]!;
            if (!table.Columns.Contains(SysFields.RowId)) { return rowIds; }
            foreach (DataRow row in table.Rows)
            {
                var rowId = ValueUtilities.CGuid(row[SysFields.RowId]);
                if (rowId != Guid.Empty) { rowIds.Add(rowId); }
            }
            return rowIds;
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
