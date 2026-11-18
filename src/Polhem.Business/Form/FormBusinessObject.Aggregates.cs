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
        /// Completes the payload for the master fields computed from detail rows (<c>Sum(Table.field)</c>) and the
        /// rules that aggregate them (<c>All(Table, qty &gt; 0)</c>): when the save changes an existing record, the
        /// stored detail rows the payload does not carry are added unchanged, so a total adds up, and a rule checks,
        /// every row of the record.
        /// </summary>
        /// <exception cref="UserMessageException">
        /// The payload changes rows of an aggregated detail table without carrying the master row, carries more than
        /// one record, changes the row id of a master row or an aggregated detail row, or carries a row of an
        /// aggregated detail table that has no row id or does not belong to the record it carries.
        /// </exception>
        /// <remarks>
        /// The framework's clients send the whole record, so for them nothing is added. A caller of the API may send
        /// only the rows it changed; without this step the total would add up those rows alone, and a rule would
        /// pass by checking them alone. The stored record is
        /// read without a scope filter: the save's own scope checks have already run, and the rows read here are
        /// not returned to the caller.
        /// </remarks>
        private static void CompleteAggregatedDetails(SaveContext context)
        {
            var schema = context.Schema;
            var masterTable = schema.MasterTable;
            if (masterTable is null || schema.Tables is null) { return; }
            var aggregated = FormExpressionCalculator.GetSaveAggregatedTables(schema);
            if (aggregated.Count == 0) { return; }

            var detailNames = aggregated
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
                        "Save must carry the '{0}' row: its totals or rules read the rows of '{1}'.",
                        masterTable.TableName, changed);
                }
                return;
            }

            // An aggregate reads every row of a detail table, not only the rows of one record, so a payload that
            // carries several records would mix them: one record's total would add up another's rows, and a rule
            // could be passed by adding a record whose rows satisfy it.
            var liveMasters = masterRows.Where(row => row.RowState != DataRowState.Deleted).ToList();
            if (liveMasters.Count > 1)
            {
                throw new UserMessageException(PolhemMessages.SaveOneRecordRequired,
                    "Save must carry one '{0}' record: its totals or rules read the rows of '{1}'.",
                    masterTable.TableName, detailNames.FirstOrDefault() ?? string.Empty);
            }

            var rekeyed = AggregatedDetailCompletion.FindRekeyedTable(dataSet, detailNames.Prepend(masterTable.TableName));
            if (rekeyed is not null)
            {
                throw new UserMessageException(PolhemMessages.SaveRowKeyChanged,
                    "A row of '{0}' changed its '{1}'; a key cannot change.", rekeyed, SysFields.RowId);
            }

            // IMPORTANT: an unchanged row is never written, so its values are only the caller's word. A total or a
            // rule that read them could be given any value; the stored rows are read in their place below.
            AggregatedDetailCompletion.DropUnchangedRows(dataSet, detailNames);

            // A deletion of the whole record has no rows left to aggregate.
            if (liveMasters.Count == 0) { return; }
            var master = liveMasters[0];
            var rowId = master.Table.Columns.Contains(SysFields.RowId) ? ValueUtilities.CGuid(master[SysFields.RowId]) : Guid.Empty;
            bool isNew = master.RowState == DataRowState.Added;

            // Without a permission model nothing else ties a detail row to the record being saved, so a row that
            // belongs to another record would be written there while this record's totals and rules never saw it.
            using var stored = isNew || rowId == Guid.Empty ? null : context.Repository.GetData(rowId);
            var foreign = AggregatedDetailCompletion.FindForeignTable(dataSet, detailNames, rowId, stored);
            if (foreign is not null)
            {
                throw new UserMessageException(PolhemMessages.SaveDetailRecordMismatch,
                    "Every row of '{0}' must carry its row id and belong to the '{1}' row the save carries: its totals or rules read them.",
                    foreign, masterTable.TableName);
            }

            if (stored is not null) { AggregatedDetailCompletion.ImportMissingRows(dataSet, stored, detailNames); }
        }

        private static bool HasChangedRows(DataSet dataSet, string tableName)
        {
            if (!dataSet.Tables.Contains(tableName)) { return false; }
            return dataSet.Tables[tableName]!.Rows.Cast<DataRow>()
                .Any(row => row.RowState is DataRowState.Added or DataRowState.Modified or DataRowState.Deleted);
        }
    }
}
