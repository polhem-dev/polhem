using System.Data;
using Polhem.Core;
using Polhem.Core.Exceptions;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;

namespace Polhem.Business.Form
{
    public partial class FormBusinessObject
    {
        /// <summary>
        /// Completes the payload for the master fields computed from detail rows (<c>Sum(Table.field)</c>):
        /// when the save changes an existing record, the stored detail rows the payload does not carry are added
        /// unchanged, so the total adds up every row of the record.
        /// </summary>
        /// <exception cref="UserMessageException">
        /// The payload changes rows of an aggregated detail table without carrying the master row.
        /// </exception>
        /// <remarks>
        /// The framework's clients send the whole record, so for them nothing is added. A caller of the API may send
        /// only the rows it changed; without this step the total would add up those rows alone. The stored record is
        /// read without a scope filter: the save's own scope checks have already run, and the rows read here are
        /// not returned to the caller.
        /// </remarks>
        private static void CompleteAggregatedDetails(SaveContext context)
        {
            var schema = context.Schema;
            var masterTable = schema.MasterTable;
            if (masterTable is null || schema.Tables is null) { return; }
            var map = FormExpressionCalculator.BuildAggregateDependencyMap(schema);
            if (map.Count == 0) { return; }

            var detailNames = map.Keys
                .Where(schema.Tables.Contains)
                .Select(name => schema.Tables[name].TableName)
                .ToList();
            var dataSet = context.DataSet;
            var masterRows = dataSet.Tables.Contains(masterTable.TableName)
                ? dataSet.Tables[masterTable.TableName]!.Rows.Cast<DataRow>()
                    .Where(row => row.RowState != DataRowState.Detached).ToList()
                : [];

            if (masterRows.Count == 0)
            {
                var changed = detailNames.FirstOrDefault(name => HasChangedRows(dataSet, name));
                if (changed is not null)
                {
                    throw new UserMessageException(PolhemMessages.SaveMasterRowMissing,
                        "Save must carry the '{0}' row: its totals are computed from the rows of '{1}'.",
                        masterTable.TableName, changed);
                }
                return;
            }

            // A total is computed for one record; a payload of several records, a new record or a deletion has no
            // stored rows to add.
            if (masterRows.Count != 1) { return; }
            var master = masterRows[0];
            if (master.RowState is DataRowState.Added or DataRowState.Deleted) { return; }
            if (!master.Table.Columns.Contains(SysFields.RowId)) { return; }
            var rowId = ValueUtilities.CGuid(master[SysFields.RowId]);
            if (rowId == Guid.Empty) { return; }

            using var stored = context.Repository.GetData(rowId);
            if (stored is null) { return; }
            AggregatedDetailCompletion.ImportMissingRows(dataSet, stored, detailNames);
        }

        private static bool HasChangedRows(DataSet dataSet, string tableName)
        {
            if (!dataSet.Tables.Contains(tableName)) { return false; }
            return dataSet.Tables[tableName]!.Rows.Cast<DataRow>()
                .Any(row => row.RowState is DataRowState.Added or DataRowState.Modified or DataRowState.Deleted);
        }
    }
}
