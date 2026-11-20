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

            // A name that is the master table is not a detail table (validation reports it); left in, its unchanged
            // master row would be dropped below.
            var detailNames = aggregated
                .Where(schema.Tables.Contains)
                .Select(name => schema.Tables[name].TableName)
                .Where(name => !string.Equals(name, masterTable.TableName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var dataSet = context.DataSet;
            var masterRows = dataSet.Tables.Contains(masterTable.TableName)
                ? dataSet.Tables[masterTable.TableName]!.Rows.Cast<DataRow>()
                    .Where(row => row.RowState != DataRowState.Detached).ToList()
                : [];

            if (masterRows.Count == 0)
            {
                ThrowIfDetailsChangeWithoutMaster(dataSet, detailNames, masterTable);
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

            // A deletion of the whole record runs no total and no before-save rule, so the payload may only delete
            // the record's own detail rows: anything else would change another record whose totals and rules
            // never ran.
            if (liveMasters.Count == 0)
            {
                ThrowIfDeletionReachesOtherRecords(context, detailNames, masterRows, masterTable);
                return;
            }
            var master = liveMasters[0];
            var rowId = master.Table.Columns.Contains(SysFields.RowId) ? ValueUtilities.CGuid(master[SysFields.RowId]) : Guid.Empty;
            bool isNew = master.RowState == DataRowState.Added;

            // Without a permission model nothing else ties a detail row to the record being saved, so a row that
            // belongs to another record would be written there while this record's totals and rules never saw it.
            using var stored = isNew || rowId == Guid.Empty ? null : context.Repository.GetData(rowId);
            // A master row that claims to be stored but is not: its update would change nothing, while its detail
            // rows would be written for a record that does not exist, checked against values nobody stored.
            if (!isNew && rowId != Guid.Empty && stored is null)
                ThrowIfDetailsOfMissingRecord(dataSet, detailNames);
            var foreign = AggregatedDetailCompletion.FindForeignTable(dataSet, detailNames, rowId, stored);
            if (foreign is not null)
            {
                throw new UserMessageException(PolhemMessages.SaveDetailRecordMismatch,
                    "Every row of '{0}' must carry its row id and belong to the '{1}' row the save carries: its totals or rules read them.",
                    foreign, masterTable.TableName);
            }

            if (stored is not null)
            {
                AggregatedDetailCompletion.ImportMissingRows(dataSet, stored, detailNames);
                // An unchanged master row is not written either, so a rule reading its fields would read the caller's
                // values; it takes the stored ones, as the unchanged detail rows do.
                if (master.RowState == DataRowState.Unchanged)
                    AggregatedDetailCompletion.RestoreStoredValues(master, stored, masterTable.TableName);
            }
        }

        private static void ThrowIfDetailsChangeWithoutMaster(DataSet dataSet, List<string> detailNames, FormTable masterTable)
        {
            var changed = detailNames.FirstOrDefault(name => HasChangedRows(dataSet, name));
            if (changed is not null)
            {
                throw new UserMessageException(PolhemMessages.SaveMasterRowMissing,
                    "Save must carry the '{0}' row: its totals or rules read the rows of '{1}'.",
                    masterTable.TableName, changed);
            }
        }

        private static void ThrowIfDeletionReachesOtherRecords(SaveContext context, List<string> detailNames,
            List<DataRow> masterRows, FormTable masterTable)
        {
            var deleted = AggregatedDetailCompletion.FindOutsideDeletion(context.DataSet, detailNames,
                masterRows.Select(row => ValueUtilities.CGuid(row[SysFields.RowId, DataRowVersion.Original]))
                    .Where(id => id != Guid.Empty)
                    .Select(id => context.Repository.GetData(id)));
            if (deleted is not null)
            {
                throw new UserMessageException(PolhemMessages.SaveDetailRecordMismatch,
                    "Every row of '{0}' must carry its row id and belong to the '{1}' row the save carries: its totals or rules read them.",
                    deleted, masterTable.TableName);
            }
        }

        private static void ThrowIfDetailsOfMissingRecord(DataSet dataSet, List<string> detailNames)
        {
            var carried = detailNames.FirstOrDefault(name => dataSet.Tables.Contains(name)
                && dataSet.Tables[name]!.Rows.Cast<DataRow>().Any(row => row.RowState != DataRowState.Detached));
            if (carried is not null)
            {
                // The usual cause is a record someone else deleted while it was open.
                throw new UserMessageException(PolhemMessages.SaveConcurrencyConflict,
                    "The record was changed or deleted by someone else while it was open. Reload it and try again.");
            }
        }

        private static bool HasChangedRows(DataSet dataSet, string tableName)
        {
            if (!dataSet.Tables.Contains(tableName)) { return false; }
            return dataSet.Tables[tableName]!.Rows.Cast<DataRow>()
                .Any(row => row.RowState is DataRowState.Added or DataRowState.Modified or DataRowState.Deleted);
        }
    }
}
