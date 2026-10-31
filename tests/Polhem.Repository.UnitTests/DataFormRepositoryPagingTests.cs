using System.ComponentModel;
using System.Data;
using Polhem.Core.Data;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Paging;
using Polhem.Definition.Sorting;
using Polhem.Repository.Form;
using Polhem.Tests.Shared;

namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Paged reads of <see cref="DataFormRepository"/> keep a fixed row order across pages and return no rows for a
    /// page beyond what the server can compute.
    /// </summary>
    public class DataFormRepositoryPagingTests : IClassFixture<SharedDbFixture>
    {
        private const string GroupColumn = "group_no";

        private static readonly bool[] s_totalCountModes = [false, true];
        private static readonly int[] s_sortGroups = [2, 1, 3, 1, 2, 3, 1, 2];

        private readonly SharedDbFixture _fx;

        public DataFormRepositoryPagingTests(SharedDbFixture fx) { _fx = fx; }

        [Fact]
        [DisplayName("ResolvePagingSort appends sys_no to a sort that does not name it")]
        public void ResolvePagingSort_SortWithoutSysNo_AppendsSysNo()
        {
            var result = DataFormRepository.ResolvePagingSort(
                Schema(SysFields.No, SysFields.RowId), [new SortField(GroupColumn, SortDirection.Desc)]);

            Assert.Equal(2, result.Count);
            Assert.Equal(GroupColumn, result[0].FieldName);
            Assert.Equal(SortDirection.Desc, result[0].Direction);
            Assert.Equal(SysFields.No, result[1].FieldName);
            Assert.Equal(SortDirection.Asc, result[1].Direction);
        }

        [Fact]
        [DisplayName("ResolvePagingSort leaves the caller's sort unchanged")]
        public void ResolvePagingSort_SortWithoutSysNo_DoesNotChangeCallerSort()
        {
            SortFieldCollection sort = [new SortField(GroupColumn, SortDirection.Asc)];

            DataFormRepository.ResolvePagingSort(Schema(SysFields.No), sort);

            Assert.Single(sort);
        }

        [Fact]
        [DisplayName("ResolvePagingSort does not add sys_no to a sort that already names it, whatever the casing")]
        public void ResolvePagingSort_SortNamesSysNo_ReturnsSameSort()
        {
            SortFieldCollection sort = [new SortField("SYS_NO", SortDirection.Desc)];

            Assert.Same(sort, DataFormRepository.ResolvePagingSort(Schema(SysFields.No), sort));
        }

        [Fact]
        [DisplayName("ResolvePagingSort falls back to sys_rowid on a table without sys_no")]
        public void ResolvePagingSort_TableWithoutSysNo_AppendsRowId()
        {
            var result = DataFormRepository.ResolvePagingSort(
                Schema(SysFields.RowId), [new SortField(GroupColumn, SortDirection.Asc)]);

            Assert.Equal(SysFields.RowId, result[1].FieldName);
        }

        [Fact]
        [DisplayName("ResolvePagingSort keeps the sort of a table with neither unique column")]
        public void ResolvePagingSort_TableWithoutUniqueColumn_ReturnsSameSort()
        {
            SortFieldCollection sort = [new SortField(GroupColumn, SortDirection.Asc)];

            Assert.Same(sort, DataFormRepository.ResolvePagingSort(Schema(), sort));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        [DisplayName("ResolvePagingSort applies sys_no when the sort is missing or empty")]
        public void ResolvePagingSort_NoSort_UsesSysNo(bool empty)
        {
            SortFieldCollection? sort = empty ? [] : null;

            var result = DataFormRepository.ResolvePagingSort(Schema(SysFields.No), sort);

            Assert.Equal(SysFields.No, Assert.Single(result).FieldName);
        }

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: paging on a non-unique sort neither repeats nor skips rows")]
        public void GetList_SqlServer_NonUniqueSort_ReturnsEveryRowOnce() => RunWalk(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: paging on a non-unique sort neither repeats nor skips rows")]
        public void GetList_Sqlite_NonUniqueSort_ReturnsEveryRowOnce() => RunWalk(DatabaseType.SQLite);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: paging on a non-unique sort neither repeats nor skips rows")]
        public void GetList_PostgreSql_NonUniqueSort_ReturnsEveryRowOnce() => RunWalk(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: paging on a non-unique sort neither repeats nor skips rows")]
        public void GetList_MySql_NonUniqueSort_ReturnsEveryRowOnce() => RunWalk(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: paging on a non-unique sort neither repeats nor skips rows")]
        public void GetList_Oracle_NonUniqueSort_ReturnsEveryRowOnce() => RunWalk(DatabaseType.Oracle);

        [DbFact(DatabaseType.SQLServer)]
        [DisplayName("SQL Server: a page beyond the computable offset returns the columns and no rows")]
        public void GetList_SqlServer_PageBeyondOffset_ReturnsNoRows() => RunBeyondOffset(DatabaseType.SQLServer);

        [DbFact(DatabaseType.SQLite)]
        [DisplayName("SQLite: a page beyond the computable offset returns the columns and no rows")]
        public void GetList_Sqlite_PageBeyondOffset_ReturnsNoRows() => RunBeyondOffset(DatabaseType.SQLite);

        [DbFact(DatabaseType.PostgreSQL)]
        [DisplayName("PostgreSQL: a page beyond the computable offset returns the columns and no rows")]
        public void GetList_PostgreSql_PageBeyondOffset_ReturnsNoRows() => RunBeyondOffset(DatabaseType.PostgreSQL);

        [DbFact(DatabaseType.MySQL)]
        [DisplayName("MySQL: a page beyond the computable offset returns the columns and no rows")]
        public void GetList_MySql_PageBeyondOffset_ReturnsNoRows() => RunBeyondOffset(DatabaseType.MySQL);

        [DbFact(DatabaseType.Oracle)]
        [DisplayName("Oracle: a page beyond the computable offset returns the columns and no rows")]
        public void GetList_Oracle_PageBeyondOffset_ReturnsNoRows() => RunBeyondOffset(DatabaseType.Oracle);

        [DbTheory(DatabaseType.SQLServer)]
        [InlineData(SortDirection.Asc)]
        [InlineData(SortDirection.Desc)]
        [DisplayName("SQL Server: pages read in a sort join into the fully sorted rows, in order")]
        public void GetList_SqlServer_SortedPages_MatchSortedRows(SortDirection direction)
            => RunSortedWalk(DatabaseType.SQLServer, direction);

        [DbTheory(DatabaseType.SQLite)]
        [InlineData(SortDirection.Asc)]
        [InlineData(SortDirection.Desc)]
        [DisplayName("SQLite: pages read in a sort join into the fully sorted rows, in order")]
        public void GetList_Sqlite_SortedPages_MatchSortedRows(SortDirection direction)
            => RunSortedWalk(DatabaseType.SQLite, direction);

        [DbTheory(DatabaseType.PostgreSQL)]
        [InlineData(SortDirection.Asc)]
        [InlineData(SortDirection.Desc)]
        [DisplayName("PostgreSQL: pages read in a sort join into the fully sorted rows, in order")]
        public void GetList_PostgreSql_SortedPages_MatchSortedRows(SortDirection direction)
            => RunSortedWalk(DatabaseType.PostgreSQL, direction);

        [DbTheory(DatabaseType.MySQL)]
        [InlineData(SortDirection.Asc)]
        [InlineData(SortDirection.Desc)]
        [DisplayName("MySQL: pages read in a sort join into the fully sorted rows, in order")]
        public void GetList_MySql_SortedPages_MatchSortedRows(SortDirection direction)
            => RunSortedWalk(DatabaseType.MySQL, direction);

        [DbTheory(DatabaseType.Oracle)]
        [InlineData(SortDirection.Asc)]
        [InlineData(SortDirection.Desc)]
        [DisplayName("Oracle: pages read in a sort join into the fully sorted rows, in order")]
        public void GetList_Oracle_SortedPages_MatchSortedRows(SortDirection direction)
            => RunSortedWalk(DatabaseType.Oracle, direction);

        private void RunSortedWalk(DatabaseType databaseType, SortDirection direction)
            => WithForm(databaseType, form =>
            {
                // Groups repeat and are inserted out of order, so only the sort and its `sys_no` tiebreak fix the rows.
                var inserted = s_sortGroups.Select(group => (Group: group, RowId: Insert(form, group))).ToList();
                var groupOrder = direction == SortDirection.Asc
                    ? inserted.OrderBy(row => row.Group)
                    : inserted.OrderByDescending(row => row.Group);
                // `OrderBy` is stable, so rows that tie stay in insertion order, which is `sys_no` ascending.
                var expected = groupOrder.Select(row => row.RowId).ToList();
                SortFieldCollection sort = [new SortField(GroupColumn, direction)];
                var seen = new List<string>();

                for (var page = 1; ; page++)
                {
                    var result = form.Repository.GetList(
                        string.Empty, null, sort, new PagingOptions { Page = page, PageSize = 3 });
                    seen.AddRange(RowIds(result.Table!));
                    if (!result.Paging!.HasMore) { break; }
                    Assert.True(page < s_sortGroups.Length, "HasMore never turned false.");
                }

                Assert.Equal(expected, seen);
            });

        private void RunWalk(DatabaseType databaseType)
            => WithForm(databaseType, form =>
            {
                var expected = Enumerable.Range(0, 5).Select(_ => Insert(form)).ToHashSet();
                var seen = new List<string>();
                SortFieldCollection sort = [new SortField(GroupColumn, SortDirection.Asc)];

                for (var page = 1; page <= 3; page++)
                {
                    var result = form.Repository.GetList(
                        string.Empty, null, sort, new PagingOptions { Page = page, PageSize = 2 });
                    seen.AddRange(RowIds(result.Table!));
                    Assert.Equal(page < 3, result.Paging!.HasMore);
                }

                Assert.Equal(5, seen.Count);
                Assert.True(expected.SetEquals(seen));
            });

        private void RunBeyondOffset(DatabaseType databaseType)
            => WithForm(databaseType, form =>
            {
                Insert(form);

                foreach (var includeTotalCount in s_totalCountModes)
                {
                    var result = form.Repository.GetList(string.Empty, null, null, new PagingOptions
                    {
                        Page = int.MaxValue,
                        PageSize = PagingOptions.MaxPageSize,
                        IncludeTotalCount = includeTotalCount,
                    });

                    Assert.Empty(result.Table!.Rows);
                    Assert.True(result.Table.Columns.Contains(GroupColumn));
                    Assert.False(result.Paging!.HasMore);
                }
            });

        private static IEnumerable<string> RowIds(DataTable table)
            => table.Rows.Cast<DataRow>().Select(row => row[SysFields.RowId].ToString()!.ToUpperInvariant());

        private static FormSchema Schema(params string[] uniqueFields)
        {
            var schema = new FormSchema("PagingSort", "Paging sort");
            var table = schema.Tables!.Add("PagingSort", "Paging sort");
            foreach (var field in uniqueFields)
            {
                table.Fields!.Add(field, field, field == SysFields.No ? FieldDbType.AutoIncrement : FieldDbType.Guid);
            }
            table.Fields!.Add(GroupColumn, "Group", FieldDbType.Integer);
            return schema;
        }

        private void WithForm(DatabaseType databaseType, Action<TransientForm> body)
        {
            string progId = TransientForm.NewTableName("tb_pgn_");
            var schema = new FormSchema(progId, "Paging") { CategoryId = TransientForm.CategoryId };
            var table = schema.Tables!.Add(progId, "Paging");
            table.Fields!.Add(SysFields.No, "No", FieldDbType.AutoIncrement);
            table.Fields.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            table.Fields.Add(GroupColumn, "Group", FieldDbType.Integer);

            var form = new TransientForm(_fx, databaseType, schema);
            form.CreateTables();
            try
            {
                body(form);
            }
            finally
            {
                form.DropTables();
            }
        }

        private static string Insert(TransientForm form, int group = 1)
        {
            var rowId = Guid.NewGuid();
            form.DbAccess.ExecuteNonQuery(
                $"INSERT INTO {form.Quote(form.Schema.ProgId)} ({form.Quote(SysFields.RowId)}, {form.Quote(GroupColumn)}) " +
                "VALUES ({0}, {1})",
                rowId, group);
            return rowId.ToString().ToUpperInvariant();
        }
    }
}
