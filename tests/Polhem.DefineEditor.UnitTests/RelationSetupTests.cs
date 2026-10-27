using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.DefineEditor.Services;

namespace Polhem.DefineEditor.UnitTests
{
    /// <summary>
    /// The window-free half of the relation dialog: the rows it lists, what each would do, and what applying them writes.
    /// </summary>
    public sealed class RelationSetupTests
    {
        private readonly FormSchema _order;
        private readonly FormTable _orderTable;
        private readonly FormField _customer;
        private readonly FormSchema _customerForm;

        public RelationSetupTests()
        {
            _order = new FormSchema("Order", "Order") { CategoryId = "company" };
            _orderTable = _order.Tables!.Add("Order", "Order");
            _orderTable.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
            _orderTable.Fields!.Add("sys_id", "Order No", FieldDbType.String);
            _customer = _orderTable.Fields!.Add("customer_rowid", "Customer", FieldDbType.Guid);
            _orderTable.Fields!.Add("zone_rowid", "Zone", FieldDbType.Guid);
            _orderTable.Fields!.Add("amount", "Amount", FieldDbType.Decimal);

            _customerForm = new FormSchema("Customer", "Customer") { CategoryId = "company" };
            var customers = _customerForm.Tables!.Add("Customer", "Customer");
            customers.Fields!.Add("sys_rowid", "Row ID", FieldDbType.Guid);
            customers.Fields!.Add("sys_no", "Sequence", FieldDbType.AutoIncrement);
            customers.Fields!.Add("sys_id", "Customer No", FieldDbType.String).MaxLength = 20;
            customers.Fields!.Add("sys_name", "Customer Name", FieldDbType.String).MaxLength = 100;
            customers.Fields!.Add("credit_limit", "Credit Limit", FieldDbType.Decimal);
            customers.Fields!.Add("zone_rowid", "Zone", FieldDbType.Guid);
        }

        private FormTable CustomerMaster => _customerForm.MasterTable!;

        private static RelationSetupRow Row(IReadOnlyList<RelationSetupRow> rows, string source) =>
            rows.Single(r => r.SourceField == source);

        [Theory]
        [InlineData("customer_rowid", "sys_id", "ref_customer_id")]
        [InlineData("customer_rowid", "sys_name", "ref_customer_name")]
        [InlineData("customer_rowid", "credit_limit", "ref_customer_credit_limit")]
        [InlineData("customer_rowid", "ref_dept_name", "ref_customer_dept_name")]
        [InlineData("owner", "sys_name", "ref_owner_name")]
        [DisplayName("The default destination is ref_<base>_<source> without the foreign key's _rowid and the source's sys_ or ref_ prefix")]
        public void DefaultDestination_FollowsConvention(string foreignKey, string source, string expected)
        {
            Assert.Equal(expected, RelationSetup.DefaultDestination(foreignKey, source));
        }

        [Fact]
        [DisplayName("A field without mappings lists every master field but sys_rowid and includes sys_id and sys_name")]
        public void CreateRows_NoMappings_IncludesIdAndName()
        {
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);

            Assert.Equal(["sys_no", "sys_id", "sys_name", "credit_limit", "zone_rowid"], rows.Select(r => r.SourceField));
            Assert.Equal(["sys_id", "sys_name"], rows.Where(r => r.IsIncluded).Select(r => r.SourceField));
            Assert.Equal("ref_customer_id", Row(rows, "sys_id").DestinationField);
            Assert.Equal("String(20)", Row(rows, "sys_id").SourceTypeText);
            Assert.Equal(RelationSetupRowStatus.New, Row(rows, "sys_id").Status);
            Assert.Equal(RelationSetupRowStatus.None, Row(rows, "credit_limit").Status);
        }

        [Fact]
        [DisplayName("Existing mappings come back included with their destination and lookup-only flag")]
        public void CreateRows_ExistingMappings_AreIncluded()
        {
            _orderTable.Fields!.Add(new FormField("ref_cust_name", "Customer Name", FieldDbType.String, FieldType.RelationField));
            _customer.RelationProgId = "Customer";
            _customer.RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_cust_name"));
            _customer.RelationFieldMappings!.Add(new FieldMapping("zone_rowid", "zone_rowid") { LookupOnly = true });

            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);

            Assert.Equal(["sys_name", "zone_rowid"], rows.Where(r => r.IsIncluded).Select(r => r.SourceField));
            Assert.Equal("ref_cust_name", Row(rows, "sys_name").DestinationField);
            Assert.Equal(RelationSetupRowStatus.Existing, Row(rows, "sys_name").Status);
            Assert.True(Row(rows, "zone_rowid").LookupOnly);
            Assert.Equal("zone_rowid", Row(rows, "zone_rowid").DestinationField);
        }

        [Fact]
        [DisplayName("A mapping whose source the form no longer has stays included on the current form and is left out on another")]
        public void CreateRows_MissingSource_DependsOnForm()
        {
            _customer.RelationFieldMappings!.Add(new FieldMapping("old_code", "ref_customer_code"));

            var current = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            var other = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: false);

            var missing = current[^1];
            Assert.True(missing.IsSourceMissing);
            Assert.Equal("old_code", missing.SourceField);
            Assert.True(missing.IsIncluded);
            Assert.False(other[^1].IsIncluded);
            Assert.Equal(["sys_id", "sys_name"], other.Where(r => r.IsIncluded).Select(r => r.SourceField));
        }

        [Fact]
        [DisplayName("Switching a row to lookup-only targets the stored field of the same name and creates a DbField")]
        public void LookupOnly_Switched_FollowsDefaults()
        {
            var row = Row(RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true), "zone_rowid");

            row.LookupOnly = true;
            Assert.Equal("zone_rowid", row.DestinationField);
            Assert.Equal(FieldType.DbField, row.NewFieldType);

            row.LookupOnly = false;
            Assert.Equal("ref_customer_zone_rowid", row.DestinationField);
            Assert.Equal(FieldType.RelationField, row.NewFieldType);
        }

        [Fact]
        [DisplayName("An empty destination, the foreign key itself and two rows on one destination are errors")]
        public void Evaluate_Errors_RefuseApplying()
        {
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            Row(rows, "sys_id").DestinationField = "customer_rowid";
            Row(rows, "credit_limit").IsIncluded = true;
            Row(rows, "credit_limit").DestinationField = "ref_customer_name";

            Assert.True(RelationSetup.Evaluate(_customer, rows));
            Assert.Equal(RelationSetupRowStatus.DestinationIsForeignKey, Row(rows, "sys_id").Status);
            Assert.Equal(RelationSetupRowStatus.DuplicateDestination, Row(rows, "sys_name").Status);

            Row(rows, "credit_limit").DestinationField = " ";
            RelationSetup.Evaluate(_customer, rows);
            Assert.Equal(RelationSetupRowStatus.MissingDestination, Row(rows, "credit_limit").Status);
            Assert.Throws<InvalidOperationException>(() => RelationSetup.Apply(_customer, "Customer", rows, removeOrphans: false));
        }

        [Fact]
        [DisplayName("An existing destination whose type differs from the source is a warning, not an error")]
        public void Evaluate_TypeMismatch_IsNotError()
        {
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            Row(rows, "credit_limit").IsIncluded = true;
            Row(rows, "credit_limit").DestinationField = "zone_rowid";

            Assert.False(RelationSetup.Evaluate(_customer, rows));
            Assert.Equal(RelationSetupRowStatus.TypeMismatch, Row(rows, "credit_limit").Status);
        }

        [Fact]
        [DisplayName("Applying writes the mappings and adds the missing destinations after the foreign key with the source's type")]
        public void Apply_AddsFieldsAfterForeignKey()
        {
            _customer.DbType = FieldDbType.String;
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            Row(rows, "credit_limit").IsIncluded = true;

            var changes = RelationSetup.Apply(_customer, "Customer", rows, removeOrphans: false);

            Assert.Equal("Customer", _customer.RelationProgId);
            Assert.Equal(FieldDbType.Guid, _customer.DbType);
            Assert.True(changes.ForeignKeyRetyped);
            Assert.Equal(["sys_id -> ref_customer_id", "sys_name -> ref_customer_name", "credit_limit -> ref_customer_credit_limit"],
                _customer.RelationFieldMappings!.Select(m => m.ToString()));
            Assert.Equal(["sys_rowid", "sys_id", "customer_rowid", "ref_customer_id", "ref_customer_name",
                "ref_customer_credit_limit", "zone_rowid", "amount"], _orderTable.Fields!.Select(f => f.FieldName));

            var id = _orderTable.Fields!["ref_customer_id"];
            Assert.Equal(FieldType.RelationField, id.Type);
            Assert.Equal(FieldDbType.String, id.DbType);
            Assert.Equal(20, id.MaxLength);
            Assert.Equal("Customer Code", id.Caption);
            Assert.Equal("Customer Name", _orderTable.Fields!["ref_customer_name"].Caption);
            Assert.Equal("Customer Credit Limit", _orderTable.Fields!["ref_customer_credit_limit"].Caption);
            Assert.Equal(FieldDbType.Decimal, _orderTable.Fields!["ref_customer_credit_limit"].DbType);
            Assert.Equal(3, changes.AddedFields.Count);
        }

        [Fact]
        [DisplayName("A new lookup-only destination is a stored field captioned like its source, and an auto-increment source becomes an integer")]
        public void Apply_LookupOnly_CreatesDbField()
        {
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            var sequence = Row(rows, "sys_no");
            sequence.IsIncluded = true;
            sequence.LookupOnly = true;
            sequence.DestinationField = "customer_no";

            RelationSetup.Apply(_customer, "Customer", rows, removeOrphans: false);

            var created = _orderTable.Fields!["customer_no"];
            Assert.Equal(FieldType.DbField, created.Type);
            Assert.Equal(FieldDbType.Integer, created.DbType);
            Assert.Equal("Sequence", created.Caption);
            Assert.True(_customer.RelationFieldMappings!.Single(m => m.SourceField == "sys_no").LookupOnly);
        }

        [Fact]
        [DisplayName("Unchecking a mapping removes it but keeps its field unless removing orphans is asked for")]
        public void Apply_Unchecked_RemovesMappingAndOptionallyField()
        {
            RelationSetup.Apply(_customer, "Customer", RelationSetup.CreateRows(_customer, CustomerMaster, true), removeOrphans: false);
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            Row(rows, "sys_name").IsIncluded = false;

            Assert.Equal(["ref_customer_name"], RelationSetup.OrphanedFields(_customer, rows));
            RelationSetup.Apply(_customer, "Customer", rows, removeOrphans: false);
            Assert.True(_orderTable.Fields!.Contains("ref_customer_name"));
            Assert.Single(_customer.RelationFieldMappings!);

            _customer.RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_customer_name"));
            var again = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            Row(again, "sys_name").IsIncluded = false;
            var changes = RelationSetup.Apply(_customer, "Customer", again, removeOrphans: true);
            Assert.False(_orderTable.Fields!.Contains("ref_customer_name"));
            Assert.Equal("ref_customer_name", Assert.Single(changes.RemovedFields).FieldName);
        }

        [Fact]
        [DisplayName("A relation field another foreign key also writes is not offered for removal, nor is a stored field")]
        public void OrphanedFields_SharedOrStored_AreKept()
        {
            _orderTable.Fields!.Add(new FormField("ref_shared", "Shared", FieldDbType.String, FieldType.RelationField));
            _customer.RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_shared"));
            _customer.RelationFieldMappings!.Add(new FieldMapping("credit_limit", "amount"));
            _orderTable.Fields!["zone_rowid"].RelationFieldMappings!.Add(new FieldMapping("sys_name", "ref_shared"));
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            foreach (var row in rows) row.IsIncluded = false;

            Assert.Empty(RelationSetup.OrphanedFields(_customer, rows));
        }

        [Fact]
        [DisplayName("An empty ProgId removes the relation and every mapping")]
        public void Apply_EmptyProgId_RemovesRelation()
        {
            RelationSetup.Apply(_customer, "Customer", RelationSetup.CreateRows(_customer, CustomerMaster, true), removeOrphans: false);

            RelationSetup.Apply(_customer, "", RelationSetup.CreateRows(_customer, CustomerMaster, true), removeOrphans: true);

            Assert.Equal(string.Empty, _customer.RelationProgId);
            Assert.Empty(_customer.RelationFieldMappings!);
            Assert.False(_orderTable.Fields!.Contains("ref_customer_id"));
        }

        [Fact]
        [DisplayName("A source the related form's lookup query does not return is reported, whether the row is lookup-only or not")]
        public void SourcesMissingFromLookup_ReportsUnlistedSource()
        {
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            Row(rows, "zone_rowid").IsIncluded = true;

            Assert.Equal(["zone_rowid"], RelationSetup.SourcesMissingFromLookup(_customerForm, rows));
            Row(rows, "zone_rowid").LookupOnly = true;

            Assert.Equal(["zone_rowid"], RelationSetup.SourcesMissingFromLookup(_customerForm, rows));
            _customerForm.LookupFields = "sys_id,sys_name,zone_rowid";
            Assert.Empty(RelationSetup.SourcesMissingFromLookup(_customerForm, rows));
        }

        [Fact]
        [DisplayName("NewIssues reports a relation field only lookup-only mappings write and leaves the schema unchanged")]
        public void NewIssues_LookupOnlyRelationField_IsReported()
        {
            var rows = RelationSetup.CreateRows(_customer, CustomerMaster, isCurrentForm: true);
            foreach (var row in rows) row.IsIncluded = false;
            var name = Row(rows, "sys_name");
            name.IsIncluded = true;
            name.LookupOnly = true;
            name.NewFieldType = FieldType.RelationField;
            name.DestinationField = "ref_customer_name";

            var issues = RelationSetup.NewIssues(_order, _customer, "Customer", rows, ["Customer", "Order"]);

            Assert.Contains(issues, i => i.Contains("ref_customer_name", StringComparison.Ordinal));
            Assert.False(_orderTable.Fields!.Contains("ref_customer_name"));
            Assert.Empty(_customer.RelationFieldMappings!);
        }
    }
}
