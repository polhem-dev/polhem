using System.Data;
using System.Globalization;
using Polhem.Core;
using Polhem.Definition;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Repository.Abstractions.Form;

namespace Polhem.Repository.Form
{
    public partial class DataFormRepository
    {
        /// <inheritdoc/>
        public DataFormListResult GetList(
            string selectFields,
            FilterNode? filter,
            SortFieldCollection? sortFields,
            PagingOptions? paging = null)
        {
            // FormSchema.MasterTable.TableName == ProgId (framework invariant), so we
            // pass ProgId directly as the target table name.
            var resolvedSelectFields = StringUtilities.IsNotEmpty(selectFields)
                ? selectFields
                : _schema.ListFields;  // empty falls through to SelectCommandBuilder as "all fields"

            var connInfo = Context.ConnectionManager.GetConnectionInfo(DatabaseId);
            var builder = CreateCommandBuilder(connInfo.DatabaseType);
            var dbAccess = Context.DbAccessFactory.Create(DatabaseId);

            if (paging == null)
            {
                var spec = builder.BuildSelect(ProgId, resolvedSelectFields, filter, sortFields);
                return new DataFormListResult { Table = ReadFormTable(dbAccess, spec, _schema.MasterTable) };
            }

            // Paged path: clamp PageSize, supply a deterministic ORDER BY, run optional
            // COUNT, then the paged SELECT. When IncludeTotalCount is false we take an
            // extra probe row (PageSize + 1) to compute HasMore without a COUNT round-trip.
            // Clamped so a caller cannot load a huge result set through the page size (for example
            // `int.MaxValue`).
            var pageSize = Math.Clamp(paging.PageSize, 1, PagingOptions.MaxPageSize);
            var page = Math.Max(paging.Page, 1);
            var skip = PagingOffset.Compute(page, pageSize);

            // Paging requires a deterministic ORDER BY. SQL Server and Oracle reject
            // OFFSET/FETCH without ORDER BY; PG/SQLite/MySQL allow it but return rows
            // in undefined order. Falling back here is the Repository's job — the SQL
            // layer does not know about the `sys_no` convention.
            var effectiveSort = ResolvePagingSort(_schema, sortFields);

            int? totalCount = null;
            if (paging.IncludeTotalCount)
            {
                var countSpec = builder.BuildCount(ProgId, filter);
                totalCount = Convert.ToInt32(dbAccess.Execute(countSpec).Scalar, CultureInfo.InvariantCulture);
            }

            int take = paging.IncludeTotalCount ? pageSize : pageSize + 1;
            var pagedSpec = builder.BuildSelect(ProgId, resolvedSelectFields, filter, effectiveSort, skip, take);
            var table = ReadFormTable(dbAccess, pagedSpec, _schema.MasterTable)!;

            bool hasMore;
            if (paging.IncludeTotalCount)
            {
                hasMore = totalCount > skip + table.Rows.Count;
            }
            else
            {
                hasMore = table.Rows.Count > pageSize;
                if (hasMore)
                {
                    // Trim the probe row so the caller never sees the extra record.
                    table.Rows.RemoveAt(table.Rows.Count - 1);
                }
            }

            return new DataFormListResult
            {
                Table = table,
                Paging = new PagingInfo
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    HasMore = hasMore,
                },
            };
        }

        /// <summary>
        /// Returns the sort a paged query runs with: the default when the caller supplies none, otherwise the
        /// caller's sort followed by a unique column it does not already name. Rows that tie on a non-unique
        /// sort have no fixed order, so without that column they could repeat or go missing across pages.
        /// </summary>
        /// <param name="schema">The form schema the query reads.</param>
        /// <param name="sortFields">The caller's sort, which is left unchanged.</param>
        internal static SortFieldCollection ResolvePagingSort(FormSchema schema, SortFieldCollection? sortFields)
        {
            if (sortFields == null || sortFields.Count == 0) { return DefaultSortForPaging(schema); }

            var fields = schema.MasterTable?.Fields;
            var uniqueField = new[] { SysFields.No, SysFields.RowId }.FirstOrDefault(f => fields?.Contains(f) == true);
            if (uniqueField == null || sortFields.Any(f => StringUtilities.IsEquals(f.FieldName, uniqueField)))
            {
                return sortFields;
            }

            var result = new SortFieldCollection();
            foreach (var field in sortFields)
            {
                result.Add(new SortField(field.FieldName, field.Direction));
            }
            result.Add(new SortField(uniqueField, SortDirection.Asc));
            return result;
        }

        /// <summary>
        /// Returns the default sort applied to paged queries when the caller does not
        /// supply a <see cref="SortFieldCollection"/>. Uses <c>sys_no ASC</c> when the
        /// master table defines it; otherwise throws to force the caller to provide
        /// an explicit sort (Guid-based <c>sys_rowid</c> would be deterministic but
        /// meaningless to humans).
        /// </summary>
        private static SortFieldCollection DefaultSortForPaging(FormSchema schema)
        {
            var masterTable = schema.MasterTable
                ?? throw new InvalidOperationException(
                    $"Schema '{schema.ProgId}' has no master table; cannot derive a default paging sort.");

            if (masterTable.Fields == null || !masterTable.Fields.Contains(SysFields.No))
            {
                throw new InvalidOperationException(
                    $"Cannot derive a default paging sort for schema '{schema.ProgId}': " +
                    $"the master table does not define '{SysFields.No}'. " +
                    $"Supply an explicit SortFields when calling GetList with paging.");
            }

            return [new SortField(SysFields.No, SortDirection.Asc)];
        }

        /// <inheritdoc/>
        public DataSet GetNewData(string timeZoneId = "")
        {
            var dataSet = new DataSet(ProgId);

            var masterTable = _schema.MasterTable
                ?? throw new InvalidOperationException(
                    $"FormSchema '{ProgId}' has no master table; cannot build a new-data skeleton.");

            // Master skeleton + one Added row seeded with FormSchema defaults
            // and a server-issued sys_rowid.
            var masterDataTable = BuildEmptyDataTable(masterTable);
            dataSet.Tables.Add(masterDataTable);

            var masterRow = masterDataTable.NewRow();
            // Schema-driven non-null seeding (fresh sys_rowid + type defaults), then the
            // FormSchema field defaults (e.g. a status of "Draft") which take precedence. The data set
            // is in UTC on this side (ADR-032 D3), so "now" is seeded in UTC while "today" still follows
            // the user's zone.
            FormRowDefaults.Apply(masterTable, masterRow, null, timeZoneId, DateTimeBasis.Utc);
            ApplyMasterDefaults(masterRow, masterTable);
            masterDataTable.Rows.Add(masterRow);

            foreach (var detail in EnumerateDetailTables())
                dataSet.Tables.Add(BuildEmptyDataTable(detail));

            return dataSet;
        }

        /// <inheritdoc/>
        public DataSet? GetData(Guid rowId, FilterNode? scopeFilter = null)
        {
            var connInfo = Context.ConnectionManager.GetConnectionInfo(DatabaseId);
            var builder = CreateCommandBuilder(connInfo.DatabaseType);
            var dbAccess = Context.DbAccessFactory.Create(DatabaseId);

            // Master row by sys_rowid, AND-combined with the record-scope filter when supplied so
            // an out-of-scope row reads as "not found" (null).
            var masterFilter = CombineWithScope(FilterCondition.Equal(SysFields.RowId, rowId), scopeFilter);
            var masterSpec = builder.BuildSelect(ProgId, string.Empty, masterFilter);
            var masterDataTable = ReadFormTable(dbAccess, masterSpec, _schema.MasterTable);
            if (masterDataTable == null || masterDataTable.Rows.Count == 0)
                return null;

            masterDataTable.TableName = ProgId;

            var dataSet = new DataSet(ProgId);
            dataSet.Tables.Add(masterDataTable);

            var masterRowId = CoerceToGuid(masterDataTable.Rows[0][SysFields.RowId]);
            var detailFilter = FilterCondition.Equal(SysFields.MasterRowId, masterRowId);

            foreach (var detail in EnumerateDetailTables())
            {
                var detailTableName = detail.TableName;
                var detailSpec = builder.BuildSelect(detailTableName, string.Empty, detailFilter);
                var detailDataTable = ReadFormTable(dbAccess, detailSpec, detail)
                    ?? new DataTable(detailTableName);
                detailDataTable.TableName = detailTableName;
                dataSet.Tables.Add(detailDataTable);
            }

            dataSet.AcceptChanges();
            return dataSet;
        }

        /// <summary>
        /// The most row identifiers bound into one <c>IN</c> list.
        /// </summary>
        /// <remarks>
        /// Oracle rejects an <c>IN</c> list longer than 1000 expressions and SQL Server a command with
        /// more than 2100 parameters, so a large batch is read in chunks below both limits.
        /// </remarks>
        private const int RowIdBatchSize = 500;

        /// <inheritdoc/>
        public DataTable GetRowsByRowId(string tableName, string selectFields, IReadOnlyCollection<Guid> rowIds)
        {
            ArgumentException.ThrowIfNullOrEmpty(tableName);
            ArgumentNullException.ThrowIfNull(rowIds);

            var formTable = _schema.Tables != null && _schema.Tables.Contains(tableName)
                ? _schema.Tables[tableName]
                : throw new InvalidOperationException($"FormSchema '{ProgId}' has no table '{tableName}'.");

            var result = new DataTable(tableName);
            if (rowIds.Count == 0) { return result; }

            var connInfo = Context.ConnectionManager.GetConnectionInfo(DatabaseId);
            var builder = CreateCommandBuilder(connInfo.DatabaseType);
            var dbAccess = Context.DbAccessFactory.Create(DatabaseId);
            var fields = IncludeRowId(selectFields);

            foreach (var chunk in rowIds.Distinct().Chunk(RowIdBatchSize))
            {
                var filter = FilterCondition.In(SysFields.RowId, chunk.Cast<object>());
                var spec = builder.BuildSelect(tableName, fields, filter);
                var table = ReadFormTable(dbAccess, spec, formTable);
                if (table != null) { result.Merge(table); }
            }

            result.TableName = tableName;
            result.AcceptChanges();
            return result;
        }

        /// <summary>
        /// Adds <c>sys_rowid</c> to a field list that names fields but leaves it out.
        /// </summary>
        /// <param name="selectFields">The comma-separated field list; empty means every field.</param>
        private static string IncludeRowId(string selectFields)
        {
            if (StringUtilities.IsEmpty(selectFields)) { return string.Empty; }

            var names = selectFields.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return names.Contains(SysFields.RowId, StringComparer.OrdinalIgnoreCase)
                ? selectFields
                : SysFields.RowId + "," + selectFields;
        }
    }
}
