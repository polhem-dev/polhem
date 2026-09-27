using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Polhem.Base.Data;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Storage;
using Polhem.Repository.Form;

using Polhem.Tests.Shared;
namespace Polhem.Repository.UnitTests
{
    /// <summary>
    /// Covers the private static methods of <see cref="DataFormRepository"/> not covered elsewhere
    /// (<c>DefaultSortForPaging</c>, <c>ExtractMasterRowId</c>)
    /// and the detail table and default value paths of <see cref="DataFormRepository.GetNewData"/>.
    /// No database connection is needed.
    /// </summary>
    public class DataFormRepositoryAdditionalTests
    {
        #region Stubs

        private sealed class StubDefineAccess : IDefineAccess
        {
            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotImplementedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotImplementedException();
            public SystemSettings GetSystemSettings() => throw new NotImplementedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotImplementedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotImplementedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotImplementedException();
            public ProgramSettings GetProgramSettings() => throw new NotImplementedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotImplementedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotImplementedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotImplementedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotImplementedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotImplementedException();
            public FormSchema GetFormSchema(string progId) => throw new NotImplementedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotImplementedException();
            public FormLayout GetFormLayout(string layoutId) => throw new NotImplementedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotImplementedException();
            public LanguageResource GetLanguage(string lang, string ns) => throw new NotImplementedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotImplementedException();
        }

        private sealed class StubDbAccessFactory : IDbAccessFactory
        {
            public DbAccess Create(string databaseId) => throw new NotImplementedException();
        }

        private sealed class StubConnectionManager : IDbConnectionManager
        {
            public DbConnectionInfo GetConnectionInfo(string databaseId) => throw new NotImplementedException();
            public DbConnection CreateConnection(string databaseId) => throw new NotImplementedException();
            public bool Remove(string databaseId) => false;
            public void Clear() { }
            public bool Contains(string databaseId) => false;
            public int Count => 0;
        }

        private static readonly Type[] s_formSchemaParam = [typeof(FormSchema)];
        private static readonly Type[] s_extractMasterRowIdParams = [typeof(DataSet), typeof(string)];

        private static DataFormRepository CreateRepository(FormSchema schema)
        {
            return new DataFormRepository(TestRepositoryContext.Create(new StubConnectionManager(), defineAccess: new StubDefineAccess(), dbAccessFactory: new StubDbAccessFactory()), schema.ProgId, schema, "testdb");
        }

        #endregion

        #region DefaultSortForPaging (private static method)

        [Fact]
        [DisplayName("DefaultSortForPaging throws InvalidOperationException for a schema without a master table")]
        public void DefaultSortForPaging_SchemaWithoutMasterTable_ThrowsInvalidOperationException()
        {
            var method = typeof(DataFormRepository).GetMethod(
                "DefaultSortForPaging",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                s_formSchemaParam,
                null);
            Assert.NotNull(method);
            var schema = new FormSchema("NoMaster", "NoMaster");
            var ex = Record.Exception(() => method!.Invoke(null, new object[] { schema }));
            var innerEx = Assert.IsType<TargetInvocationException>(ex).InnerException;
            Assert.IsType<InvalidOperationException>(innerEx);
        }

        [Fact]
        [DisplayName("DefaultSortForPaging throws InvalidOperationException when the master table has no sys_no field")]
        public void DefaultSortForPaging_MasterTableWithoutSysNoField_ThrowsInvalidOperationException()
        {
            var method = typeof(DataFormRepository).GetMethod(
                "DefaultSortForPaging",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                s_formSchemaParam,
                null);
            Assert.NotNull(method);
            var schema = new FormSchema("Employee", "Employee");
            schema.Tables!.Add("Employee", "Employee");
            var ex = Record.Exception(() => method!.Invoke(null, new object[] { schema }));
            var innerEx = Assert.IsType<TargetInvocationException>(ex).InnerException;
            Assert.IsType<InvalidOperationException>(innerEx);
        }

        [Fact]
        [DisplayName("DefaultSortForPaging returns a SortFieldCollection when the master table has a sys_no field")]
        public void DefaultSortForPaging_MasterTableWithSysNoField_ReturnsSortFieldCollection()
        {
            var method = typeof(DataFormRepository).GetMethod(
                "DefaultSortForPaging",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                s_formSchemaParam,
                null);
            Assert.NotNull(method);
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add(SysFields.No, "No", FieldDbType.Integer);
            var result = Assert.IsType<Polhem.Definition.Sorting.SortFieldCollection>(method!.Invoke(null, new object[] { schema }));
            var sort = Assert.Single(result);
            Assert.Equal(SysFields.No, sort.FieldName);
            Assert.Equal(Polhem.Definition.Sorting.SortDirection.Asc, sort.Direction);
        }

        #endregion

        #region ExtractMasterRowId (private static method)

        [Fact]
        [DisplayName("ExtractMasterRowId returns null when the DataSet does not contain the given table")]
        public void ExtractMasterRowId_DataSetWithoutMasterTable_ReturnsNull()
        {
            var method = typeof(DataFormRepository).GetMethod(
                "ExtractMasterRowId",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                s_extractMasterRowIdParams,
                null);
            Assert.NotNull(method);
            var dataSet = new DataSet("Test");
            var result = method!.Invoke(null, new object[] { dataSet, "Employee" });
            Assert.Null(result);
        }

        [Fact]
        [DisplayName("ExtractMasterRowId returns the Guid of a row with a valid sys_rowid")]
        public void ExtractMasterRowId_MasterTableWithValidRowId_ReturnsGuid()
        {
            var method = typeof(DataFormRepository).GetMethod(
                "ExtractMasterRowId",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                s_extractMasterRowIdParams,
                null);
            Assert.NotNull(method);
            var dataSet = new DataSet("Test");
            var table = dataSet.Tables.Add("Employee");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            var expectedId = Guid.NewGuid();
            var row = table.NewRow();
            row[SysFields.RowId] = expectedId;
            table.Rows.Add(row);
            var result = method!.Invoke(null, new object[] { dataSet, "Employee" });
            Assert.Equal(expectedId, (Guid)result!);
        }

        [Fact]
        [DisplayName("ExtractMasterRowId returns null when the table holds only Deleted rows")]
        public void ExtractMasterRowId_OnlyDeletedRows_ReturnsNull()
        {
            var method = typeof(DataFormRepository).GetMethod(
                "ExtractMasterRowId",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                s_extractMasterRowIdParams,
                null);
            Assert.NotNull(method);
            var dataSet = new DataSet("Test");
            var table = dataSet.Tables.Add("Employee");
            table.Columns.Add(SysFields.RowId, typeof(Guid));
            var row = table.NewRow();
            row[SysFields.RowId] = Guid.NewGuid();
            table.Rows.Add(row);
            table.AcceptChanges();
            row.Delete();
            var result = method!.Invoke(null, new object[] { dataSet, "Employee" });
            Assert.Null(result);
        }

        #endregion

        #region GetNewData (additional paths)

        [Fact]
        [DisplayName("GetNewData includes the detail table in the DataSet when the schema has one")]
        public void GetNewData_SchemaWithDetailTable_DataSetContainsDetailTable()
        {
            var schema = new FormSchema("Order", "Order");
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            var detail = schema.Tables.Add("OrderItem", "OrderItem");
            detail.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            detail.Fields.Add(SysFields.MasterRowId, "Master Row Id", FieldDbType.Guid);

            var repo = CreateRepository(schema);
            var dataSet = repo.GetNewData();

            Assert.True(dataSet.Tables.Contains("Order"));
            Assert.True(dataSet.Tables.Contains("OrderItem"));
        }

        [Fact]
        [DisplayName("GetNewData skeleton includes RelationField columns (so lookup write-back lands) and excludes VirtualField")]
        public void GetNewData_Skeleton_IncludesRelationFieldsExcludesVirtual()
        {
            // Regression: without the `ref_*` columns in the skeleton, the client's lookup write-back is silently
            // skipped by `SetField`, and the display value picked while adding a record never reaches the form.
            var schema = new FormSchema("Project", "Project");
            var master = schema.Tables!.Add("Project", "Project");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            var deptField = master.Fields.Add("owner_dept_rowid", "Owner Department", FieldDbType.Guid);
            deptField.RelationProgId = "Department";
            master.Fields!.Add(new FormField("ref_dept_name", "Department Name", FieldDbType.String, FieldType.RelationField));
            master.Fields.Add(new FormField("calc_total", "Total", FieldDbType.Decimal, FieldType.VirtualField));

            var repo = CreateRepository(schema);
            var dataSet = repo.GetNewData();

            var columns = dataSet.Tables["Project"]!.Columns;
            Assert.True(columns.Contains("owner_dept_rowid"));
            Assert.True(columns.Contains("ref_dept_name"));
            Assert.False(columns.Contains("calc_total"));
        }

        [Fact]
        [DisplayName("GetNewData applies a field's string default value to the master row")]
        public void GetNewData_FieldWithStringDefaultValue_AppliesDefault()
        {
            var schema = new FormSchema("Employee", "Employee");
            var master = schema.Tables!.Add("Employee", "Employee");
            master.Fields!.Add(SysFields.RowId, "Row Id", FieldDbType.Guid);
            var nameField = master.Fields.Add("emp_status", "Status", FieldDbType.String);
            nameField.DefaultValue = "Active";

            var repo = CreateRepository(schema);
            var dataSet = repo.GetNewData();

            var masterRow = dataSet.Tables["Employee"]!.Rows[0];
            Assert.Equal("Active", masterRow["emp_status"]);
        }

        #endregion
    }
}
