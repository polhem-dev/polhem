using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Db.Dml;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Forms;
using Polhem.Definition.Language;
using Polhem.Definition.Layouts;
using Polhem.Definition.Settings;
using Polhem.Definition.Sorting;
using Polhem.Definition.Storage;

namespace Polhem.Db.UnitTests.Dml
{
    /// <summary>
    /// Coverage-focused tests for <see cref="SelectContextBuilder"/>. All DB-free: a stub
    /// <see cref="IDefineAccess"/> supplies the relation form schemas, so no database or
    /// fixture is required and these run during every coverage collection.
    /// </summary>
    public class SelectContextBuilderCoverageTests
    {
        // ---- helpers -------------------------------------------------------

        private static FormTable NewMainTable()
        {
            var table = new FormTable("Order", "Order") { DbTableName = "ft_order" };
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            return table;
        }

        private static FormField AddForeignKey(FormTable table, string fieldName, string relationProgId)
        {
            var field = table.Fields!.Add(fieldName, fieldName, FieldDbType.Guid);
            field.RelationProgId = relationProgId;
            return field;
        }

        private static FormSchema NewRelationSchema(string progId, params string[] sourceFields)
        {
            var schema = new FormSchema(progId, progId);
            var table = schema.Tables!.Add(progId, progId);
            table.DbTableName = "ft_" + progId.ToLowerInvariant();
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            foreach (var name in sourceFields)
                table.Fields!.Add(new FormField(name, name, FieldDbType.String) { MaxLength = 50 });
            return schema;
        }

        // ---- tests ---------------------------------------------------------

        [Fact]
        [DisplayName("The constructor throws ArgumentNullException for a null defineAccess")]
        public void Constructor_NullDefineAccess_Throws()
        {
            var table = NewMainTable();
            Assert.Throws<ArgumentNullException>(() =>
                new SelectContextBuilder(table, [], null!));
        }

        [Fact]
        [DisplayName("Build creates a JOIN and a QueryFieldMapping when the foreign key field resolves")]
        public void Build_ResolvableForeignKey_ProducesJoinAndMapping()
        {
            var table = NewMainTable();
            var fk = AddForeignKey(table, "category_rowid", "Category");
            fk.RelationFieldMappings!.Add("sys_id", "ref_category_id");
            fk.RelationFieldMappings!.Add("sys_name", "ref_category_name");

            var used = new HashSet<string> { "ref_category_id" };
            var define = new StubDefineAccess(_ => NewRelationSchema("Category", "sys_id", "sys_name"));
            var context = new SelectContextBuilder(table, used, define).Build();

            Assert.Single(context.Joins);
            Assert.Equal("B", context.Joins[0].RightAlias);
            Assert.Single(context.FieldMappings);
        }

        [Fact]
        [DisplayName("Build joins on TableName when DbTableName is blank, on both the main and the relation side")]
        public void Build_BlankDbTableName_FallsBackToTableName()
        {
            var table = NewMainTable();
            table.DbTableName = string.Empty;
            var fk = AddForeignKey(table, "category_rowid", "Category");
            fk.RelationFieldMappings!.Add("sys_id", "ref_category_id");

            var used = new HashSet<string> { "ref_category_id" };
            var define = new StubDefineAccess(_ =>
            {
                var schema = NewRelationSchema("Category", "sys_id");
                schema.MasterTable!.DbTableName = string.Empty;
                return schema;
            });
            var context = new SelectContextBuilder(table, used, define).Build();

            var join = Assert.Single(context.Joins);
            Assert.Equal("Order", join.LeftTable);
            Assert.Equal("Category", join.RightTable);
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException when the relation FormSchema does not exist")]
        public void Build_RelationSchemaNotFound_Throws()
        {
            var table = NewMainTable();
            var fk = AddForeignKey(table, "category_rowid", "Missing");
            fk.RelationFieldMappings!.Add("sys_id", "ref_category_id");

            var used = new HashSet<string> { "ref_category_id" };
            var define = new StubDefineAccess(_ => null);
            var builder = new SelectContextBuilder(table, used, define);

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        [DisplayName("Build throws InvalidOperationException when the source field is not in the relation table")]
        public void Build_SourceFieldNotFound_Throws()
        {
            var table = NewMainTable();
            var fk = AddForeignKey(table, "category_rowid", "Category");
            fk.RelationFieldMappings!.Add("no_such_field", "ref_category_id");

            var used = new HashSet<string> { "ref_category_id" };
            // Relation schema does NOT contain "no_such_field".
            var define = new StubDefineAccess(_ => NewRelationSchema("Category", "sys_id"));
            var builder = new SelectContextBuilder(table, used, define);

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        [DisplayName("Build skips a foreign key field whose RelationFieldMappings is empty")]
        public void Build_ForeignKeyWithEmptyMappings_SkipsField()
        {
            var table = NewMainTable();
            var fk = AddForeignKey(table, "category_rowid", "Category");
            Assert.Empty(fk.RelationFieldMappings!);

            var used = new HashSet<string> { "ref_category_id" };
            var define = new StubDefineAccess(_ => NewRelationSchema("Category", "sys_id"));
            var context = new SelectContextBuilder(table, used, define).Build();

            Assert.Empty(context.Joins);
        }

        [Fact]
        [DisplayName("Build skips the SQL reserved word alias (BY) when it produces enough JOINs")]
        public void Build_ManyJoins_SkipsReservedKeywordAlias()
        {
            // Alias progression is A, B..Z, BA..BZ. The 50th generated alias would be "BY",
            // a reserved SQL keyword, which the builder must skip (advancing to "BZ").
            const int fieldCount = 50;
            var table = NewMainTable();
            for (int i = 0; i < fieldCount; i++)
            {
                var fk = AddForeignKey(table, "fk" + i, "Ref");
                fk.RelationFieldMappings!.Add("sys_id", "d" + i);
            }

            var used = new HashSet<string>();
            for (int i = 0; i < fieldCount; i++)
                used.Add("d" + i);

            var define = new StubDefineAccess(_ => NewRelationSchema("Ref", "sys_id"));
            var context = new SelectContextBuilder(table, used, define).Build();

            Assert.Equal(fieldCount, context.Joins.Count);
            Assert.DoesNotContain(context.Joins, j => j.RightAlias == "BY");
            Assert.Contains(context.Joins, j => j.RightAlias == "BZ");
        }

        // ---- lookup-only mappings -------------------------------------------

        // A leave request stores both the employee and the department. Picking an employee copies the employee's
        // department along (lookup-only mappings), while each foreign key still joins its own reference fields.
        private static FormSchema BuildLeaveSchema()
        {
            var schema = new FormSchema("Leave", "Leave");
            var table = schema.Tables!.Add("Leave", "Leave");
            table.DbTableName = "ft_leave";
            table.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            foreach (var name in new[] { "ref_emp_id", "ref_emp_name", "ref_dept_id", "ref_dept_name" })
                table.Fields!.Add(new FormField(name, name, FieldDbType.String) { Type = FieldType.RelationField });

            var emp = AddForeignKey(table, "emp_rowid", "Employee");
            emp.RelationFieldMappings!.Add("sys_id", "ref_emp_id");
            emp.RelationFieldMappings!.Add("sys_name", "ref_emp_name");
            foreach (var name in new[] { "dept_rowid", "ref_dept_id", "ref_dept_name" })
                emp.RelationFieldMappings!.Add(new FieldMapping(name, name) { LookupOnly = true });

            var dept = AddForeignKey(table, "dept_rowid", "Department");
            dept.RelationFieldMappings!.Add("sys_id", "ref_dept_id");
            dept.RelationFieldMappings!.Add("sys_name", "ref_dept_name");
            return schema;
        }

        private static StubDefineAccess LeaveDefineAccess() => new(progId => progId switch
        {
            "Employee" => NewRelationSchema("Employee", "sys_id", "sys_name", "dept_rowid", "ref_dept_id", "ref_dept_name"),
            _ => NewRelationSchema(progId, "sys_id", "sys_name"),
        });

        [Fact]
        [DisplayName("Build leaves lookup-only mappings out of the join, so each reference field comes from its own foreign key")]
        public void Build_LookupOnlyMappings_AreNotJoined()
        {
            var table = BuildLeaveSchema().MasterTable!;
            var used = new HashSet<string>(table.Fields!.Select(f => f.FieldName));

            var context = new SelectContextBuilder(table, used, LeaveDefineAccess()).Build();

            Assert.Equal(2, context.Joins.Count);
            Assert.Null(context.FieldMappings.GetOrDefault("dept_rowid"));
            Assert.Equal("dept_rowid", context.FieldMappings.GetOrDefault("ref_dept_name")!.TableJoin!.LeftField);
            Assert.Equal("emp_rowid", context.FieldMappings.GetOrDefault("ref_emp_name")!.TableJoin!.LeftField);
        }

        [Fact]
        [DisplayName("A filter and a sort on a field a lookup-only mapping fills read the main table's column")]
        public void BuildSelect_LookupOnlyDestination_FiltersAndSortsOnMainTable()
        {
            var builder = new SqliteFormCommandBuilder(BuildLeaveSchema(), LeaveDefineAccess());
            var sort = new SortFieldCollection { new SortField("dept_rowid", SortDirection.Asc) };

            var sql = builder.BuildSelect("Leave", string.Empty, FilterCondition.Equal("dept_rowid", Guid.NewGuid()), sort).CommandText;

            Assert.Contains("WHERE A.\"dept_rowid\"", sql);
            Assert.Contains("ORDER BY A.\"dept_rowid\"", sql);
            Assert.DoesNotContain("B.\"dept_rowid\"", sql);
        }

        [Fact]
        [DisplayName("A relation to another form over the same table joins that table")]
        public void Build_RelationToFormOverSameTable_JoinsItsTable()
        {
            var table = NewMainTable();
            var fk = AddForeignKey(table, "dept_rowid", "DeptActive");
            fk.RelationFieldMappings!.Add("sys_name", "ref_dept_name");

            var define = new StubDefineAccess(_ =>
            {
                var schema = NewRelationSchema("DeptActive", "sys_name");
                schema.MasterTable!.DbTableName = "ft_department";
                return schema;
            });
            var context = new SelectContextBuilder(table, ["ref_dept_name"], define).Build();

            Assert.Equal("ft_department", Assert.Single(context.Joins).RightTable);
        }

        // ---- stub ----------------------------------------------------------

        /// <summary>
        /// Minimal <see cref="IDefineAccess"/> that only serves form schemas via a delegate;
        /// every other member is unused by <see cref="SelectContextBuilder"/> and throws.
        /// </summary>
        private sealed class StubDefineAccess : IDefineAccess
        {
            private readonly Func<string, FormSchema?> _getFormSchema;
            public StubDefineAccess(Func<string, FormSchema?> getFormSchema) => _getFormSchema = getFormSchema;

            public FormSchema GetFormSchema(string progId) => _getFormSchema(progId)!;

            public object GetDefine(DefineType defineType, string[]? keys = null) => throw new NotSupportedException();
            public void SaveDefine(DefineType defineType, object defineObject, string[]? keys = null) => throw new NotSupportedException();
            public SystemSettings GetSystemSettings() => throw new NotSupportedException();
            public void SaveSystemSettings(SystemSettings settings) => throw new NotSupportedException();
            public DatabaseSettings GetDatabaseSettings() => throw new NotSupportedException();
            public void SaveDatabaseSettings(DatabaseSettings settings) => throw new NotSupportedException();
            public ProgramSettings GetProgramSettings() => throw new NotSupportedException();
            public void SaveProgramSettings(ProgramSettings settings) => throw new NotSupportedException();
            public DbCategorySettings GetDbCategorySettings() => throw new NotSupportedException();
            public void SaveDbCategorySettings(DbCategorySettings settings) => throw new NotSupportedException();
            public TableSchema GetTableSchema(string categoryId, string tableName) => throw new NotSupportedException();
            public void SaveTableSchema(string categoryId, TableSchema tableSchema) => throw new NotSupportedException();
            public void SaveFormSchema(FormSchema formSchema) => throw new NotSupportedException();
            public FormLayout GetFormLayout(string layoutId) => throw new NotSupportedException();
            public void SaveFormLayout(FormLayout formLayout) => throw new NotSupportedException();
            public LanguageResource GetLanguage(string lang, string ns) => throw new NotSupportedException();
            public void SaveLanguage(LanguageResource resource) => throw new NotSupportedException();
        }
    }
}
