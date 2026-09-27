using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for FormSchema.PermissionModelId, FormField.ScopeRole and PermissionBindingValidator.
    /// </summary>
    public class PermissionBindingTests
    {
        /// <summary>
        /// Builds a sample FormSchema with a permission binding (PO001 bound to PurchaseOrder, with Owner and Dept columns marked on the master table).
        /// </summary>
        private static FormSchema BuildForm(string modelId = "PurchaseOrder")
        {
            var schema = new FormSchema("PO001", "採購單建立")
            {
                CategoryId = "company",
                PermissionModelId = modelId,
            };
            var table = schema.Tables!.Add("PO001", "採購單");
            var buyer = table.Fields!.Add("buyer_rowid", "採購員", FieldDbType.Guid);
            buyer.ScopeRole = ScopeRole.Owner;
            var dept = table.Fields!.Add("dept_rowid", "部門", FieldDbType.Guid);
            dept.ScopeRole = ScopeRole.Dept;
            return schema;
        }

        private static PermissionModels BuildRegistry()
        {
            var models = new PermissionModels();
            var po = models.Models!.Add("PurchaseOrder", "採購單");
            po.Rules!.Add(PermissionActions.Read, ScopeStrategy.DeptAndSub);
            return models;
        }

        [Fact]
        [DisplayName("FormSchema.PermissionModelId round-trips through XML as an XmlAttribute")]
        public void FormSchema_PermissionModelId_RoundTrips()
        {
            var schema = BuildForm();

            var xml = XmlCodec.Serialize(schema);
            var restored = XmlCodec.Deserialize<FormSchema>(xml);

            Assert.Contains("PermissionModelId=\"PurchaseOrder\"", xml);
            Assert.NotNull(restored);
            Assert.Equal("PurchaseOrder", restored!.PermissionModelId);
        }

        [Fact]
        [DisplayName("FormField.ScopeRole round-trips through XML as an XmlAttribute")]
        public void FormField_ScopeRole_RoundTrips()
        {
            var schema = BuildForm();

            var xml = XmlCodec.Serialize(schema);
            var restored = XmlCodec.Deserialize<FormSchema>(xml);

            Assert.Contains("ScopeRole=\"Owner\"", xml);
            var table = restored!.Tables!["PO001"];
            Assert.Equal(ScopeRole.Owner, table.Fields!["buyer_rowid"].ScopeRole);
            Assert.Equal(ScopeRole.Dept, table.Fields!["dept_rowid"].ScopeRole);
        }

        [Fact]
        [DisplayName("FormField.ScopeRole is not written to XML when it is None")]
        public void FormField_ScopeRoleNone_OmittedFromXml()
        {
            var schema = new FormSchema("PO001", "採購單") { CategoryId = "company" };
            var table = schema.Tables!.Add("PO001", "採購單");
            table.Fields!.Add("sys_id", "單號", FieldDbType.String);

            var xml = XmlCodec.Serialize(schema);

            Assert.DoesNotContain("ScopeRole=", xml);
        }

        [Fact]
        [DisplayName("FormSchema.Clone keeps PermissionModelId")]
        public void FormSchema_Clone_PreservesPermissionModelId()
        {
            var schema = BuildForm();

            var clone = schema.Clone();

            Assert.Equal("PurchaseOrder", clone.PermissionModelId);
        }

        [Fact]
        [DisplayName("FormField.Clone keeps ScopeRole")]
        public void FormField_Clone_PreservesScopeRole()
        {
            var schema = BuildForm();

            var clone = schema.Clone();

            Assert.Equal(ScopeRole.Owner, clone.Tables!["PO001"].Fields!["buyer_rowid"].ScopeRole);
        }

        [Fact]
        [DisplayName("Validator returns an empty list for a valid binding")]
        public void Validate_ValidBinding_ReturnsEmpty()
        {
            var schemas = new FormSchema[] { BuildForm() };

            var errors = PermissionBindingValidator.Validate(schemas, BuildRegistry());

            Assert.Empty(errors);
        }

        [Fact]
        [DisplayName("Validator reports an error for a PermissionModelId that does not exist")]
        public void Validate_UnknownModelId_ReturnsError()
        {
            var schemas = new FormSchema[] { BuildForm("NoSuchModel") };

            var errors = PermissionBindingValidator.Validate(schemas, BuildRegistry());

            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("NoSuchModel"));
        }

        [Fact]
        [DisplayName("Validator accepts several Owner columns on the master table (OR union, no longer limited to one column)")]
        public void Validate_MultipleOwnerColumns_Allowed()
        {
            var schema = BuildForm();
            schema.Tables!["PO001"].Fields!.Add("creator_rowid", "建立者", FieldDbType.Guid).ScopeRole = ScopeRole.Owner;

            var errors = PermissionBindingValidator.Validate([schema], BuildRegistry());

            Assert.Empty(errors);
        }

        [Fact]
        [DisplayName("Validator accepts several Dept columns on the master table (such as the from and to departments of a transfer form)")]
        public void Validate_MultipleDeptColumns_Allowed()
        {
            var schema = BuildForm();
            schema.Tables!["PO001"].Fields!.Add("to_dept_rowid", "調入部門", FieldDbType.Guid).ScopeRole = ScopeRole.Dept;

            var errors = PermissionBindingValidator.Validate([schema], BuildRegistry());

            Assert.Empty(errors);
        }

        [Fact]
        [DisplayName("Validator reports an error for ScopeRole on a detail table (record scope is master table only)")]
        public void Validate_DetailScopeRole_ReturnsError()
        {
            var schema = BuildForm();
            // ScopeRole on a detail table (not the master) is a violation, because scope applies to the master table only.
            var detail = schema.Tables!.Add("PO001_Item", "採購單明細");
            var item = detail.Fields!.Add("owner_rowid", "擁有者", FieldDbType.Guid);
            item.ScopeRole = ScopeRole.Owner;
            var schemas = new FormSchema[] { schema };

            var errors = PermissionBindingValidator.Validate(schemas, BuildRegistry());

            Assert.Contains(errors, e => e.Contains("detail table") && e.Contains("PO001_Item"));
        }

        [Fact]
        [DisplayName("FormField.SensitiveCategory round-trips through XML as an XmlAttribute")]
        public void FormField_SensitiveCategory_RoundTrips()
        {
            var schema = BuildForm();
            schema.Tables!["PO001"].Fields!.Add("total_cost", "成本", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;

            var xml = XmlCodec.Serialize(schema);
            var restored = XmlCodec.Deserialize<FormSchema>(xml);

            Assert.Contains("SensitiveCategory=\"Cost\"", xml);
            Assert.Equal(SensitiveCategory.Cost, restored!.Tables!["PO001"].Fields!["total_cost"].SensitiveCategory);
        }

        [Fact]
        [DisplayName("FormField.SensitiveCategory is not written to XML when it is None")]
        public void FormField_SensitiveCategoryNone_OmittedFromXml()
        {
            var schema = new FormSchema("PO001", "採購單") { CategoryId = "company" };
            schema.Tables!.Add("PO001", "採購單").Fields!.Add("sys_id", "單號", FieldDbType.String);

            var xml = XmlCodec.Serialize(schema);

            Assert.DoesNotContain("SensitiveCategory=", xml);
        }

        [Fact]
        [DisplayName("FormField.Clone keeps SensitiveCategory")]
        public void FormField_Clone_PreservesSensitiveCategory()
        {
            var schema = BuildForm();
            schema.Tables!["PO001"].Fields!.Add("total_cost", "成本", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;

            var clone = schema.Clone();

            Assert.Equal(SensitiveCategory.Cost, clone.Tables!["PO001"].Fields!["total_cost"].SensitiveCategory);
        }

        [Fact]
        [DisplayName("Validator reports an error when the well-known model for a sensitive category does not exist")]
        public void Validate_SensitiveCategoryWithoutModel_ReturnsError()
        {
            var schema = BuildForm();
            schema.Tables!["PO001"].Fields!.Add("total_cost", "成本", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;
            var schemas = new FormSchema[] { schema };

            // `BuildRegistry` only has PurchaseOrder and no well-known Cost model, so an error is expected.
            var errors = PermissionBindingValidator.Validate(schemas, BuildRegistry());

            Assert.Contains(errors, e => e.Contains("Cost") && e.Contains("total_cost"));
        }

        [Fact]
        [DisplayName("Validator accepts a sensitive category when its well-known model exists")]
        public void Validate_SensitiveCategoryWithModel_ReturnsEmpty()
        {
            var schema = BuildForm();
            schema.Tables!["PO001"].Fields!.Add("total_cost", "成本", FieldDbType.Decimal).SensitiveCategory = SensitiveCategory.Cost;
            var schemas = new FormSchema[] { schema };

            var registry = BuildRegistry();
            registry.Models!.Add("Cost", "成本");

            var errors = PermissionBindingValidator.Validate(schemas, registry);

            Assert.Empty(errors);
        }
    }
}
