using System.Data;
using Polhem.Base;
using Polhem.Base.Exceptions;
using Polhem.Business.Permission;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;
using Polhem.Repository.Abstractions.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// Record-scope half of the save path: which stored rows a save may touch, and which values it may
    /// leave behind.
    /// </summary>
    /// <remarks>
    /// Split from <c>FormBusinessObject.Permission.cs</c>, which keeps the layer-1 action gate and the
    /// read-side scope filter. Everything here reasons about row versions and ownership in a
    /// caller-supplied DataSet, and none of it is needed to read the action gate.
    /// </remarks>
    public partial class FormBusinessObject
    {
        /// <summary>
        /// Enforces layer-2 record scope on the rows a save changes, by authoritatively re-querying
        /// each existing master row. <c>Deleted</c> → Delete scope; <c>Modified</c> / <c>Unchanged</c>
        /// → Update scope (a details-only edit leaves the master Unchanged but still updates the
        /// record). Each is confirmed in the caller's scope against the database, not the supplied
        /// payload, so a forged DataSet cannot relabel its way past the boundary. <c>Added</c> rows
        /// have no stored record to check; their values are checked by
        /// <see cref="EnforceNewValueScope"/> instead. A no-op when the form declares no
        /// <c>PermissionModelId</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WARNING: every check here keys on the row the repository will actually write, which for an
        /// existing row is the <b>Original</b> <see cref="SysFields.RowId"/>: the UPDATE and DELETE
        /// statements bind their WHERE clause to that version. A payload controls both versions of a
        /// row independently, so the following are load-bearing:
        /// </para>
        /// <list type="number">
        /// <item><description>A row whose Original and Current rowids differ is refused. The rowid is
        ///   the key and never changes; a row that claims to change it is pairing an in-scope Current
        ///   value with someone else's Original.</description></item>
        /// <item><description>Master scope is checked on the Original rowid.</description></item>
        /// <item><description>A payload that carries detail rows but <b>no master table at all</b> is
        ///   refused, because there would be no master to scope-check.</description></item>
        /// <item><description>Every written detail row's claimed <see cref="SysFields.MasterRowId"/> must
        ///   name a master row present here, on both versions of a modified row.</description></item>
        /// <item><description>Every modified or deleted detail row must <b>already</b> belong, in the
        ///   database, to an existing master row of this payload. The claimed owner alone proves
        ///   nothing: the statements are keyed on the detail's own rowid, so a detail row of someone
        ///   else's record could otherwise be rewritten or deleted under an in-scope master.</description></item>
        /// </list>
        /// <para>
        /// All are refusals rather than silent drops: the shapes have no legitimate use. The framework's
        /// own details-only edit carries the master row in <c>Unchanged</c> state, which is exactly why
        /// <see cref="WriteScopeActionForRowState"/> maps that state to Update. The regression tests
        /// are in <c>FormBusinessObjectWriteScopeTests</c>.
        /// </para>
        /// </remarks>
        /// <param name="dataSet">The DataSet about to be persisted.</param>
        /// <param name="repository">The repository used for the authoritative checks.</param>
        /// <exception cref="ForbiddenException">A mutated master row is outside the caller's scope, a
        /// row changes its rowid, or a detail row does not belong to a master row in this payload.</exception>
        private void EnforceWriteScope(DataSet dataSet, IDataFormRepository repository)
        {
            var schema = DefineAccess.GetFormSchema(ProgId);
            if (string.IsNullOrEmpty(schema.PermissionModelId)) { return; }

            var masterTableName = schema.MasterTable?.TableName;
            if (string.IsNullOrEmpty(masterTableName)) { return; }

            if (!dataSet.Tables.Contains(masterTableName))
            {
                if (HasPendingRows(dataSet))
                {
                    throw new ForbiddenException(
                        $"Save must carry the '{masterTableName}' row the details belong to; " +
                        $"record scope on model '{schema.PermissionModelId}' cannot be resolved without it.");
                }
                return;
            }

            RejectRekeyedRows(dataSet, schema.PermissionModelId);

            var masterTable = dataSet.Tables[masterTableName]!;
            bool hasRowId = masterTable.Columns.Contains(SysFields.RowId);

            EnforceMasterRowScope(masterTable, hasRowId, schema, repository);

            EnforceDetailOwnership(
                dataSet, schema, masterTableName,
                carriedMasterRowIds: CollectMasterRowIds(masterTable, hasRowId, includeAdded: true),
                existingMasterRowIds: CollectMasterRowIds(masterTable, hasRowId, includeAdded: false),
                repository);
        }

        /// <summary>
        /// Verifies that the values an <c>Added</c> or <c>Modified</c> master row is about to store are
        /// inside the caller's record scope for the action: Create for a new row, Update for a changed
        /// one.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="EnforceWriteScope"/> proves the caller may touch the <b>stored</b> row; this proves
        /// the row it leaves behind is still one the caller could touch. Without it a user limited to
        /// their own records could create a record owned by someone else, or move one of theirs into
        /// another department, where they could no longer see it.
        /// </para>
        /// <para>
        /// The scope filter is evaluated against the row's Current values in memory, with the same
        /// owner and department columns the database check uses. A scope column that the payload does
        /// not carry counts as empty, so the row is refused. It runs after the <c>BeforeSave</c>
        /// step, so owner or department values that a default expression, an override of
        /// <see cref="DoBeforeSave"/> or a plugin fills in are the ones checked. A no-op when the form
        /// declares no <c>PermissionModelId</c> or the action's scope is unrestricted.
        /// </para>
        /// </remarks>
        /// <param name="dataSet">The DataSet about to be persisted.</param>
        /// <exception cref="ForbiddenException">A new or changed master row falls outside the caller's scope.</exception>
        private void EnforceNewValueScope(DataSet dataSet)
        {
            var schema = DefineAccess.GetFormSchema(ProgId);
            if (string.IsNullOrEmpty(schema.PermissionModelId)) { return; }

            var masterTableName = schema.MasterTable?.TableName;
            if (string.IsNullOrEmpty(masterTableName) || !dataSet.Tables.Contains(masterTableName)) { return; }

            var scopeFilterFor = CreateScopeFilterCache(schema);
            foreach (DataRow row in dataSet.Tables[masterTableName]!.Rows)
            {
                var action = NewValueScopeActionForRowState(row.RowState);
                if (action == PermissionAction.None) { continue; }

                var scopeFilter = scopeFilterFor(action);
                if (scopeFilter != null && !ScopeFilterEvaluator.Matches(scopeFilter, row, DataRowVersion.Current))
                {
                    throw new ForbiddenException(
                        $"The saved record would fall outside the '{action}' scope on model '{schema.PermissionModelId}'.");
                }
            }
        }

        /// <summary>
        /// Returns a function that resolves the record-scope filter of an action, at most once per
        /// action.
        /// </summary>
        /// <param name="schema">The form schema, for the permission model.</param>
        /// <remarks>
        /// Resolution happens on first use, so a save that needs no scope resolves nothing, and N rows
        /// of the same action reuse one filter.
        /// </remarks>
        private Func<PermissionAction, FilterNode?> CreateScopeFilterCache(FormSchema schema)
        {
            IScopeResolver? resolver = null;
            var scopeByAction = new Dictionary<PermissionAction, FilterNode?>();

            return action =>
            {
                if (scopeByAction.TryGetValue(action, out var cached)) { return cached; }

                resolver ??= Services.GetRequiredService<IScopeResolver>();
                var resolved = resolver.ResolveFilter(AccessToken, schema.PermissionModelId, action, schema);
                scopeByAction[action] = resolved;
                return resolved;
            };
        }

        /// <summary>
        /// Collects the master rowids this payload carries, for the detail-ownership checks.
        /// </summary>
        /// <param name="masterTable">The master table in the payload.</param>
        /// <param name="hasRowId">Whether the table carries a rowid column at all.</param>
        /// <param name="includeAdded">Whether rows the save inserts count.</param>
        /// <returns>Every non-empty rowid of the selected rows.</returns>
        /// <remarks>
        /// With <paramref name="includeAdded"/> the set is where a detail row may point: the details of
        /// a brand-new master reference the rowid this payload is inserting. Without it the set is the
        /// masters that exist already and have passed the scope check, the only rows a stored detail
        /// row can belong to.
        /// </remarks>
        private static HashSet<Guid> CollectMasterRowIds(DataTable masterTable, bool hasRowId, bool includeAdded)
        {
            var rowIds = new HashSet<Guid>();
            foreach (DataRow row in masterTable.Rows)
            {
                if (!includeAdded && row.RowState == DataRowState.Added) { continue; }

                var rowId = RowIdOf(row, hasRowId);
                if (rowId != Guid.Empty) { rowIds.Add(rowId); }
            }
            return rowIds;
        }

        /// <summary>
        /// Verifies that every master row this save changes is inside the caller's record scope.
        /// </summary>
        /// <param name="masterTable">The master table in the payload.</param>
        /// <param name="hasRowId">Whether the table carries a rowid column at all.</param>
        /// <param name="schema">The form schema, for the permission model.</param>
        /// <param name="repository">The repository used for the authoritative in-scope check.</param>
        /// <exception cref="ForbiddenException">A changed row is outside the caller's scope.</exception>
        private void EnforceMasterRowScope(
            DataTable masterTable, bool hasRowId, FormSchema schema, IDataFormRepository repository)
        {
            var scopeFilterFor = CreateScopeFilterCache(schema);
            foreach (DataRow row in masterTable.Rows)
            {
                var action = WriteScopeActionForRowState(row.RowState);
                if (action == PermissionAction.None) { continue; }

                var scopeFilter = scopeFilterFor(action);
                if (scopeFilter == null) { continue; }

                if (!repository.ExistsInScope(RowIdOf(row, hasRowId), scopeFilter))
                    throw new ForbiddenException($"Record out of scope for '{action}' on model '{schema.PermissionModelId}'.");
            }
        }

        /// <summary>
        /// Reads the rowid the repository keys its statement on: the Original version for a row that
        /// already exists, the Current one for a row being inserted.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="hasRowId">Whether the table carries a rowid column at all.</param>
        /// <returns>The rowid, or <see cref="Guid.Empty"/> when there is no column to read.</returns>
        private static Guid RowIdOf(DataRow row, bool hasRowId)
        {
            if (!hasRowId) { return Guid.Empty; }

            var version = row.RowState == DataRowState.Added
                ? DataRowVersion.Current
                : DataRowVersion.Original;
            return ValueUtilities.CGuid(row[SysFields.RowId, version]);
        }

        /// <summary>
        /// Refuses a payload in which any modified row changes its <see cref="SysFields.RowId"/>.
        /// </summary>
        /// <param name="dataSet">The DataSet about to be persisted.</param>
        /// <param name="modelId">The permission model, for the failure message.</param>
        /// <exception cref="ForbiddenException">A row's Original and Current rowids differ.</exception>
        private static void RejectRekeyedRows(DataSet dataSet, string modelId)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                if (!table.Columns.Contains(SysFields.RowId)) { continue; }

                if (table.Rows.Cast<DataRow>().Any(IsRekeyed))
                {
                    throw new ForbiddenException(
                        $"A row in '{table.TableName}' changes its '{SysFields.RowId}'; the key cannot change, " +
                        $"and record scope on model '{modelId}' cannot be confirmed for it.");
                }
            }
        }

        /// <summary>
        /// Whether a modified row carries a different rowid in its Original and Current versions.
        /// </summary>
        /// <param name="row">A row of a table that has a rowid column.</param>
        private static bool IsRekeyed(DataRow row)
            => row.RowState == DataRowState.Modified
               && ValueUtilities.CGuid(row[SysFields.RowId, DataRowVersion.Original])
                  != ValueUtilities.CGuid(row[SysFields.RowId, DataRowVersion.Current]);

        /// <summary>
        /// Whether any table in the DataSet has a row the repository would write.
        /// </summary>
        /// <param name="dataSet">The DataSet about to be persisted.</param>
        private static bool HasPendingRows(DataSet dataSet)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                foreach (DataRow row in table.Rows)
                {
                    if (row.RowState != DataRowState.Unchanged) { return true; }
                }
            }
            return false;
        }

        /// <summary>
        /// Requires every written detail row to belong to a master row carried by this payload —
        /// those are the rows <see cref="EnforceWriteScope"/> has just confirmed in the caller's scope —
        /// both by the owner the payload claims and by the owner the database holds.
        /// </summary>
        /// <param name="dataSet">The DataSet about to be persisted.</param>
        /// <param name="schema">The form schema.</param>
        /// <param name="masterTableName">The master table's name.</param>
        /// <param name="carriedMasterRowIds">Every master rowid this payload carries.</param>
        /// <param name="existingMasterRowIds">The rowids of the master rows that already exist.</param>
        /// <param name="repository">The repository that reads the stored owners.</param>
        /// <exception cref="ForbiddenException">A detail row belongs, or claims to belong, to a master
        /// outside this payload.</exception>
        private static void EnforceDetailOwnership(
            DataSet dataSet, FormSchema schema, string masterTableName,
            HashSet<Guid> carriedMasterRowIds, HashSet<Guid> existingMasterRowIds, IDataFormRepository repository)
        {
            foreach (DataTable table in dataSet.Tables)
            {
                if (StringUtilities.IsEquals(table.TableName, masterTableName)) { continue; }

                if (table.Columns.Contains(SysFields.MasterRowId))
                    EnforceTableOwnership(table, carriedMasterRowIds, schema.PermissionModelId);

                // A payload table the schema does not declare is never written by the repository, so
                // it has no stored rows to verify.
                var formTable = schema.Tables?.GetOrDefault(table.TableName);
                if (formTable != null)
                    EnforceStoredOwnership(table, formTable, existingMasterRowIds, repository, schema.PermissionModelId);
            }
        }

        /// <summary>
        /// Verifies that every owner referenced by one detail table is a master row this save carries.
        /// </summary>
        /// <remarks>
        /// <c>Unchanged</c> rows are skipped because the repository never writes them. A
        /// <c>Modified</c> row is checked on both versions: the current value is where it is moving to,
        /// the original is where it is moving from, and taking a row out of someone else's record is
        /// as much a scope violation as putting one into it.
        /// </remarks>
        /// <param name="table">The detail table, already known to carry a master-rowid column.</param>
        /// <param name="savedMasterRowIds">The master rowids present in this payload.</param>
        /// <param name="modelId">The permission model, for the error message.</param>
        /// <exception cref="ForbiddenException">A row points at a master this save does not carry.</exception>
        private static void EnforceTableOwnership(
            DataTable table, HashSet<Guid> savedMasterRowIds, string modelId)
        {
            foreach (DataRow row in table.Rows)
            {
                foreach (var version in WrittenVersions(row.RowState))
                {
                    var owner = ValueUtilities.CGuid(row[SysFields.MasterRowId, version]);
                    if (owner == Guid.Empty) { continue; }
                    if (!savedMasterRowIds.Contains(owner))
                        throw DetailOutOfScope(table.TableName, modelId);
                }
            }
        }

        /// <summary>
        /// Verifies, against the database, that every detail row this save updates or deletes already
        /// belongs to an existing master row of this payload.
        /// </summary>
        /// <remarks>
        /// The stored owners are read by the rows' Original rowids, the keys the UPDATE and DELETE
        /// statements bind. A row that is no longer stored is skipped: its statement matches nothing.
        /// </remarks>
        /// <param name="table">The detail table in the payload.</param>
        /// <param name="formTable">The detail table's declaration.</param>
        /// <param name="existingMasterRowIds">The existing master rowids this payload carries.</param>
        /// <param name="repository">The repository that reads the stored owners.</param>
        /// <param name="modelId">The permission model, for the error message.</param>
        /// <exception cref="ForbiddenException">A stored detail row belongs to another master, or the
        /// table declares no master-rowid column to check it by.</exception>
        private static void EnforceStoredOwnership(
            DataTable table, FormTable formTable, HashSet<Guid> existingMasterRowIds,
            IDataFormRepository repository, string modelId)
        {
            if (!table.Columns.Contains(SysFields.RowId)) { return; }

            var storedRowIds = table.Rows.Cast<DataRow>()
                .Where(row => row.RowState is DataRowState.Modified or DataRowState.Deleted)
                .Select(row => ValueUtilities.CGuid(row[SysFields.RowId, DataRowVersion.Original]))
                .Where(rowId => rowId != Guid.Empty)
                .ToList();
            if (storedRowIds.Count == 0) { return; }

            if (formTable.Fields == null || !formTable.Fields.Contains(SysFields.MasterRowId))
                throw DetailOutOfScope(table.TableName, modelId);

            var stored = repository.GetRowsByRowId(table.TableName, SysFields.MasterRowId, storedRowIds);
            if (stored.Rows.Count == 0) { return; }
            if (!stored.Columns.Contains(SysFields.MasterRowId))
                throw DetailOutOfScope(table.TableName, modelId);

            bool foreignOwner = stored.Rows.Cast<DataRow>()
                .Any(row => !existingMasterRowIds.Contains(ValueUtilities.CGuid(row[SysFields.MasterRowId])));
            if (foreignOwner)
                throw DetailOutOfScope(table.TableName, modelId);
        }

        private static ForbiddenException DetailOutOfScope(string tableName, string modelId)
            => new($"Detail row in '{tableName}' belongs to a record this save does not carry; " +
                   $"record scope on model '{modelId}' cannot be confirmed for it.");

        /// <summary>
        /// The row versions whose <see cref="SysFields.MasterRowId"/> the repository would write for
        /// a row in the supplied state.
        /// </summary>
        /// <param name="state">The row state.</param>
        private static IEnumerable<DataRowVersion> WrittenVersions(DataRowState state) => state switch
        {
            DataRowState.Added => [DataRowVersion.Default],
            DataRowState.Deleted => [DataRowVersion.Original],
            DataRowState.Modified => [DataRowVersion.Original, DataRowVersion.Default],
            _ => [],
        };

        /// <summary>
        /// Maps a master row's <c>RowState</c> to the <see cref="PermissionAction"/> whose record
        /// scope must be enforced on write. <c>Added</c> (Create) returns <see cref="PermissionAction.None"/>
        /// because a new row has no existing scope to violate; <c>Modified</c> and <c>Unchanged</c>
        /// both map to <see cref="PermissionAction.Update"/> (a details-only edit leaves the master
        /// Unchanged but still persists the record).
        /// </summary>
        private static PermissionAction WriteScopeActionForRowState(DataRowState state) => state switch
        {
            DataRowState.Added => PermissionAction.None,
            DataRowState.Deleted => PermissionAction.Delete,
            _ => PermissionAction.Update,
        };

        /// <summary>
        /// Maps a master row's <c>RowState</c> to the <see cref="PermissionAction"/> whose record scope
        /// its new values must satisfy: <c>Added</c> → Create, <c>Modified</c> → Update. Other states
        /// store no new master values and return <see cref="PermissionAction.None"/>.
        /// </summary>
        private static PermissionAction NewValueScopeActionForRowState(DataRowState state) => state switch
        {
            DataRowState.Added => PermissionAction.Create,
            DataRowState.Modified => PermissionAction.Update,
            _ => PermissionAction.None,
        };
    }
}
