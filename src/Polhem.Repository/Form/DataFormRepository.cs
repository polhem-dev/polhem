using System.Data;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Repository.Abstractions.Form;

namespace Polhem.Repository.Form
{
    /// <summary>
    /// Repository implementation for data forms; executes FormSchema-driven SELECT
    /// statements against the master table via the dialect-specific
    /// <see cref="Polhem.Db.Dml.IFormCommandBuilder"/>.
    /// </summary>
    public partial class DataFormRepository : RepositoryBase, IDataFormRepository
    {
        private readonly FormSchema _schema;

        /// <summary>
        /// Initializes a new <see cref="DataFormRepository"/> for the supplied progId, routed to the
        /// database its form schema's category resolves to.
        /// </summary>
        /// <param name="ctx">The shared repository context.</param>
        /// <param name="accessToken">The current request's access token; required when the schema's category resolves to a company database.</param>
        /// <param name="progId">The program identifier (also the master table name).</param>
        /// <remarks>
        /// WARNING: A subclass adding its own dependencies must not add a second <c>string</c> or
        /// <c>Guid</c> parameter. The factory builds it with <c>ActivatorUtilities</c>, which matches
        /// the supplied arguments to parameters by type: a second parameter of a type already
        /// supplied leaves nothing to bind it to and the container is asked for a <c>string</c>,
        /// which fails at construction. Interface-typed dependencies are resolved from DI and are
        /// the intended way to extend this.
        /// </remarks>
        public DataFormRepository(IRepositoryContext ctx, Guid accessToken, string progId)
            : this(ctx, accessToken, progId, Factories.RepositoryFactory.LoadSchema(
                (ctx ?? throw new ArgumentNullException(nameof(ctx))).DefineAccess, progId))
        {
        }

        private DataFormRepository(IRepositoryContext ctx, Guid accessToken, string progId, FormSchema schema)
            : base(ctx, accessToken, progId, Factories.RepositoryFactory.ParseCategoryId(schema.CategoryId))
        {
            _schema = schema;
            RequireCompanyForCompanyForm();
        }

        /// <summary>
        /// Initializes a new <see cref="DataFormRepository"/> against an explicit schema and
        /// database, skipping both the definition lookup and the router.
        /// </summary>
        /// <param name="ctx">The shared repository context.</param>
        /// <param name="progId">The program identifier (also the master table name).</param>
        /// <param name="schema">The form schema to drive SELECT construction with.</param>
        /// <param name="databaseId">The database identifier used for connection and dialect resolution.</param>
        /// <remarks>
        /// For tests, and for callers holding a schema that is not (or not yet) in the define store.
        /// A separate constructor rather than optional parameters, so the two construction paths are
        /// distinguishable at the call site. A company form names its company through the overload
        /// that takes one.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="schema"/> is a company form.</exception>
        public DataFormRepository(IRepositoryContext ctx, string progId, FormSchema schema, string databaseId)
            : base(ctx, progId, databaseId)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
            RequireCompanyForCompanyForm();
        }

        /// <summary>
        /// Initializes a new <see cref="DataFormRepository"/> against an explicit schema, database and
        /// company, skipping the definition lookup, the router and the session.
        /// </summary>
        /// <param name="ctx">The shared repository context.</param>
        /// <param name="progId">The program identifier (also the master table name).</param>
        /// <param name="schema">The form schema to drive SELECT construction with.</param>
        /// <param name="databaseId">The database identifier used for connection and dialect resolution.</param>
        /// <param name="companyId">The company whose rows a company form reads and writes.</param>
        public DataFormRepository(IRepositoryContext ctx, string progId, FormSchema schema, string databaseId, string companyId)
            : base(ctx, progId, databaseId, companyId)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        /// <summary>
        /// Gets whether the form is a company form, whose rows carry <see cref="SysFields.CompanyId"/>.
        /// </summary>
        private bool IsCompanyForm
            => string.Equals(_schema.CategoryId, DbCategoryIds.Company, StringComparison.Ordinal);

        // IMPORTANT: Every statement this repository runs on a company form is confined to
        // `CompanyId`. A company form without a company would have nothing to confine them to, so it is
        // refused while the repository is built rather than allowed to read every company's rows.
        private void RequireCompanyForCompanyForm()
        {
            if (IsCompanyForm && string.IsNullOrEmpty(CompanyId))
            {
                throw new InvalidOperationException(
                    $"FormSchema '{_schema.ProgId}' is a company form, so its repository needs a company; " +
                    "build it from a session that has entered one, or pass the company id.");
            }
        }

        /// <summary>
        /// Creates the command builder every SELECT, COUNT and DELETE of this repository goes through,
        /// confined to <see cref="RepositoryBase.CompanyId"/> on a company form.
        /// </summary>
        private Polhem.Db.Dml.IFormCommandBuilder CreateCommandBuilder(DatabaseType databaseType)
        {
            var builder = DbDialectRegistry.Get(databaseType).CreateFormCommandBuilder(_schema, Context.DefineAccess);
            return IsCompanyForm ? new CompanyScopedFormCommandBuilder(builder, CompanyId) : builder;
        }

        /// <inheritdoc/>
        public (DataSet? Refreshed, Dictionary<string, int> AffectedRows) Save(DataSet dataSet)
        {
            ArgumentNullException.ThrowIfNull(dataSet);

            var connInfo = Context.ConnectionManager.GetConnectionInfo(DatabaseId);
            var dbType = connInfo.DatabaseType;

            var masterTable = _schema.MasterTable
                ?? throw new InvalidOperationException(
                    $"FormSchema '{ProgId}' has no master table; cannot Save.");

            // Build one DataTableUpdateSpec per changed table, master before details so a new
            // master is inserted before the detail rows that reference it. Each spec drives an
            // ADO.NET DataAdapter: Added rows INSERT, Deleted rows DELETE, and Modified rows
            // UPDATE the full column set — a Modified row whose values are unchanged simply
            // re-writes the same values, so there is no "empty UPDATE" to special-case.
            var specs = new List<DataTableUpdateSpec>();
            var specTableNames = new List<string>();
            foreach (var formTable in EnumerateTablesMasterFirst(masterTable))
            {
                if (!dataSet.Tables.Contains(formTable.TableName)) { continue; }
                var dataTable = dataSet.Tables[formTable.TableName]!;
                // A row-state scan rather than `GetChanges()`, which copies every changed row into a new
                // table only for the result to be tested for null.
                if (!dataTable.Rows.Cast<DataRow>().Any(row => row.RowState != DataRowState.Unchanged)) { continue; }

                var tableSchema = formTable.GenerateDbTable();
                RemoveProtectedFields(tableSchema);
                var commandBuilder = new Polhem.Db.Dml.TableSchemaCommandBuilder(dbType, tableSchema);
                // The owning company is not the caller's to choose. On a company form the column is pinned
                // to this repository's company: a new row is stamped with it, and an update or delete of a
                // row id that belongs to another company touches nothing. Anywhere else a form that
                // declares the column still cannot write it.
                if (IsCompanyForm)
                    commandBuilder.PinField(SysFields.CompanyId, CompanyId);
                else
                    tableSchema.Fields!.Remove(SysFields.CompanyId);
                var spec = commandBuilder.BuildUpdateSpec(dataTable);
                specs.Add(spec);
                specTableNames.Add(formTable.TableName);
            }

            // No pending changes anywhere is a no-op (zero rows affected), not an error.
            var affected = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (specs.Count > 0)
            {
                var dbAccess = Context.DbAccessFactory.Create(DatabaseId);
                var counts = dbAccess.UpdateDataTables(specs);
                for (int i = 0; i < specs.Count; i++)
                    affected[specTableNames[i]] = counts[i];
            }

            // Re-fetch the saved master so server-generated columns surface
            // back to the caller.
            var masterRowId = ExtractMasterRowId(dataSet, masterTable.TableName);
            DataSet? refreshed = masterRowId.HasValue ? GetData(masterRowId.Value) : null;

            return (refreshed, affected);
        }

        /// <inheritdoc/>
        public bool ExistsInScope(Guid rowId, FilterNode? scopeFilter)
        {
            var connInfo = Context.ConnectionManager.GetConnectionInfo(DatabaseId);
            var builder = CreateCommandBuilder(connInfo.DatabaseType);
            var dbAccess = Context.DbAccessFactory.Create(DatabaseId);

            var filter = CombineWithScope(FilterCondition.Equal(SysFields.RowId, rowId), scopeFilter);
            var spec = builder.BuildSelect(ProgId, SysFields.RowId, filter);
            var table = dbAccess.Execute(spec).Table;
            return table != null && table.Rows.Count > 0;
        }

        /// <inheritdoc/>
        public int Delete(Guid rowId, FilterNode? scopeFilter = null)
        {
            var connInfo = Context.ConnectionManager.GetConnectionInfo(DatabaseId);
            var builder = CreateCommandBuilder(connInfo.DatabaseType);

            // Record-scope gate: when a scope filter is supplied, confirm the master row is in scope
            // before touching anything. Out of scope → delete nothing (no cascade), report zero.
            if (scopeFilter != null && !ExistsInScope(rowId, scopeFilter))
                return 0;

            var batch = new DbBatchSpec { UseTransaction = true };

            // Cascade delete details first (FK on sys_master_rowid), then master.
            var detailFilter = FilterCondition.Equal(SysFields.MasterRowId, rowId);
            foreach (var detail in EnumerateDetailTables())
                batch.Commands.Add(builder.BuildDelete(detail.TableName, detailFilter));

            var masterFilter = FilterCondition.Equal(SysFields.RowId, rowId);
            batch.Commands.Add(builder.BuildDelete(ProgId, masterFilter));

            var dbAccess = Context.DbAccessFactory.Create(DatabaseId);
            var result = dbAccess.ExecuteBatch(batch);

            // The master DELETE is the last command; its RowsAffected drives
            // the caller-visible count.
            var lastIndex = result.Results.Count - 1;
            return lastIndex >= 0 ? result.Results[lastIndex].RowsAffected : 0;
        }

        /// <summary>
        /// Drops any <see cref="ProtectedFields"/> column from the schema that drives the INSERT and
        /// UPDATE commands, so a form declaring one cannot write it.
        /// </summary>
        /// <remarks>
        /// WARNING: this is a privilege boundary, not a tidy-up. It is applied here rather than in
        /// `TableSchemaCommandBuilder` on purpose — the builder is a general DML tool with no
        /// business of knowing which columns the framework reserves, whereas this path is exactly
        /// the one a deployment's own FormSchema reaches. The generated schema is a fresh object per
        /// call (`FormTable.GenerateDbTable`), so removing from it never touches cached definitions.
        /// </remarks>
        private static void RemoveProtectedFields(TableSchema tableSchema)
        {
            var fields = tableSchema.Fields;
            if (fields == null) { return; }

            for (int i = fields.Count - 1; i >= 0; i--)
            {
                if (ProtectedFields.IsProtected(tableSchema.TableName, fields[i].FieldName))
                    fields.RemoveAt(i);
            }
        }


        private static Guid? ExtractMasterRowId(DataSet dataSet, string masterTableName)
        {
            if (!dataSet.Tables.Contains(masterTableName))
                return null;

            var table = dataSet.Tables[masterTableName]!;
            foreach (DataRow row in table.Rows)
            {
                if (row.RowState == DataRowState.Deleted)
                    continue;
                if (!row.Table.Columns.Contains(SysFields.RowId))
                    continue;
                var coerced = TryCoerceToGuid(row[SysFields.RowId]);
                if (coerced.HasValue && coerced.Value != Guid.Empty)
                    return coerced.Value;
            }
            return null;
        }

        /// <summary>
        /// Coerces a value loaded from a <see cref="DataRow"/> column into a
        /// <see cref="Guid"/>. Providers disagree on how a GUID comes back: SQL Server,
        /// PostgreSQL and MySQL return a native <see cref="Guid"/>, SQLite (and the legacy
        /// <c>System.Data.SQLite</c>) stores it as TEXT and surfaces a string, and Oracle has no
        /// UUID type at all — the framework maps it to <c>RAW(16)</c>, which reads back as
        /// <see cref="byte"/>[]. This helper hides that distinction so repository callers never
        /// need to branch on the underlying provider.
        /// </summary>
        private static Guid CoerceToGuid(object value)
        {
            return TryCoerceToGuid(value)
                ?? throw new InvalidOperationException(
                    $"Cannot coerce value of type '{value?.GetType().FullName ?? "null"}' into Guid.");
        }

        /// <remarks>
        /// Accepts the same shapes as <see cref="Polhem.Core.ValueUtilities.CGuid(object)"/>, the
        /// framework-wide converter, and differs from it only in the answer for a value that is
        /// no kind of Guid: null here, <see cref="Guid.Empty"/> there. That distinction is what
        /// <see cref="ExtractMasterRowId"/> uses to skip a row rather than read it as unset.
        /// Keep the accepted shapes in step with that method.
        /// </remarks>
        private static Guid? TryCoerceToGuid(object? value)
        {
            return value switch
            {
                Guid g => g,
                string s when Guid.TryParse(s, out var parsed) => parsed,
                // Oracle RAW(16). The byte order is whatever `Guid.ToByteArray` produced on the
                // way in (see `DbCommandSpec.NormalizeParameterValue`), so the matching
                // constructor is what round-trips it — not a re-ordered read.
                byte[] { Length: 16 } bytes => new Guid(bytes),
                _ => null,
            };
        }

        // AND-combines the row-id predicate with an optional record-scope filter.
        private static FilterNode CombineWithScope(FilterNode baseFilter, FilterNode? scopeFilter)
            => scopeFilter == null ? baseFilter : FilterGroup.All(baseFilter, scopeFilter);
    }
}
