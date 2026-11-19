using System.ComponentModel;
using Polhem.Core.Data;
using Polhem.Definition.Collections;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.UI.Core.Criteria;

namespace Polhem.UI.Core.UnitTests.Criteria
{
    public class CriteriaFieldTests
    {
        [Theory]
        [DisplayName("FromField derives the kind from the stored type and the field name")]
        [InlineData("sys_id", FieldDbType.String, CriteriaFieldKind.Code)]
        [InlineData("ref_customer_id", FieldDbType.String, CriteriaFieldKind.Code)]
        [InlineData("SYS_ID", FieldDbType.String, CriteriaFieldKind.Code)]
        [InlineData("company_name", FieldDbType.String, CriteriaFieldKind.Text)]
        [InlineData("discontinued", FieldDbType.Boolean, CriteriaFieldKind.Boolean)]
        [InlineData("freight", FieldDbType.Currency, CriteriaFieldKind.Number)]
        [InlineData("quantity", FieldDbType.Integer, CriteriaFieldKind.Number)]
        [InlineData("order_date", FieldDbType.Date, CriteriaFieldKind.Date)]
        [InlineData("shipped_at", FieldDbType.DateTime, CriteriaFieldKind.DateTime)]
        public void FromField_PlainField_DerivesKind(string fieldName, FieldDbType dbType, CriteriaFieldKind expected)
        {
            var field = CriteriaField.FromField(new FormField(fieldName, "Caption", dbType));

            Assert.NotNull(field);
            Assert.Equal(expected, field.Kind);
            Assert.Equal(fieldName, field.FieldName);
            Assert.Equal(dbType, field.DbType);
        }

        [Theory]
        [DisplayName("FromField returns null for a field no condition can be entered for")]
        [InlineData(FieldDbType.Text)]
        [InlineData(FieldDbType.Guid)]
        [InlineData(FieldDbType.Binary)]
        [InlineData(FieldDbType.Time)]
        public void FromField_UnsupportedType_ReturnsNull(FieldDbType dbType)
        {
            Assert.Null(CriteriaField.FromField(new FormField("x", "X", dbType)));
        }

        [Fact]
        [DisplayName("A field naming a related program is a relation field offering only In")]
        public void FromField_RelationProgId_IsRelation()
        {
            var field = CriteriaField.FromField(new FormField("customer_rowid", "Customer", FieldDbType.Guid) { RelationProgId = "Customer" });

            Assert.NotNull(field);
            Assert.Equal(CriteriaFieldKind.Relation, field.Kind);
            Assert.Equal("Customer", field.RelationProgId);
            Assert.Equal([CriteriaOperator.In], field.Operators);
        }

        [Theory]
        [InlineData("Employee", ScopeRole.None, true)]
        [InlineData("employee", ScopeRole.None, true)]
        [InlineData("Customer", ScopeRole.Owner, true)]
        [InlineData("Customer", ScopeRole.None, false)]
        [DisplayName("A relation to employees, or one marked as the owner column, offers the signed-in user")]
        public void FromField_EmployeeOrOwner_OffersCurrentUser(string relationProgId, ScopeRole role, bool expected)
        {
            var field = CriteriaField.FromField(new FormField("x_rowid", "X", FieldDbType.Guid) { RelationProgId = relationProgId, ScopeRole = role });

            Assert.Equal(expected, field!.OffersCurrentUser);
        }

        [Fact]
        [DisplayName("A field with list items or a language enum is a choice field carrying them")]
        public void FromField_ListItemsOrLangEnum_IsChoice()
        {
            var listed = new FormField("status", "Status", FieldDbType.String);
            listed.ListItems!.Add(new ListItem("A", "Active"));
            listed.ListItems.Add(new ListItem("C", "Closed"));
            var enumerated = new FormField("status", "Status", FieldDbType.String) { LangEnumName = "OrderStatus" };

            var fromList = CriteriaField.FromField(listed);
            var fromEnum = CriteriaField.FromField(enumerated);

            Assert.Equal(CriteriaFieldKind.Choice, fromList!.Kind);
            Assert.Equal(["A", "C"], fromList.ListItems.Select(i => i.Value));
            Assert.Equal(CriteriaFieldKind.Choice, fromEnum!.Kind);
            Assert.Equal("OrderStatus", fromEnum.LangEnumName);
        }

        [Fact]
        [DisplayName("A code field starts with Range and a text field with Contains")]
        public void DefaultOperator_ByKind_IsFirstOperator()
        {
            Assert.Equal(CriteriaOperator.Range, CriteriaField.FromField(new FormField("sys_id", "No", FieldDbType.String))!.DefaultOperator);
            Assert.Equal(CriteriaOperator.Contains, CriteriaField.FromField(new FormField("name", "Name", FieldDbType.String))!.DefaultOperator);
        }

        [Fact]
        [DisplayName("Explicit operators set the order and the default")]
        public void Constructor_ExplicitOperators_KeepsOrder()
        {
            var field = new CriteriaField("order_date", "Date", CriteriaFieldKind.Date, FieldDbType.Date, [CriteriaOperator.Equal, CriteriaOperator.Range]);

            Assert.Equal([CriteriaOperator.Equal, CriteriaOperator.Range], field.Operators);
            Assert.Equal(CriteriaOperator.Equal, field.DefaultOperator);
        }

        [Fact]
        [DisplayName("The constructor refuses an operator the kind does not support and an empty list")]
        public void Constructor_UnsupportedOrEmptyOperators_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CriteriaField("qty", "Qty", CriteriaFieldKind.Number, FieldDbType.Integer, [CriteriaOperator.Contains]));
            Assert.Throws<ArgumentException>(() => new CriteriaField("qty", "Qty", CriteriaFieldKind.Number, FieldDbType.Integer, []));
        }

        [Fact]
        [DisplayName("An empty caption falls back to the field name")]
        public void Constructor_EmptyCaption_UsesFieldName()
        {
            Assert.Equal("qty", new CriteriaField("qty", string.Empty, CriteriaFieldKind.Number, FieldDbType.Integer).Caption);
        }

        private static FormSchema QuerySchema()
        {
            var schema = new FormSchema("Order", "Order") { QueryFields = "sys_id,remark,freight,order_date" };
            var master = schema.Tables!.Add("Order", "Order");
            master.Fields!.Add(new FormField("sys_id", "Order No", FieldDbType.String));
            master.Fields!.Add(new FormField("remark", "Remark", FieldDbType.Text));
            master.Fields!.Add(new FormField("freight", "Freight", FieldDbType.Currency) { SensitiveCategory = SensitiveCategory.Amount });
            master.Fields!.Add(new FormField("order_date", "Date", FieldDbType.Date));
            return schema;
        }

        [Fact]
        [DisplayName("FromQueryFields keeps the declared order and skips fields no condition can be entered for")]
        public void FromQueryFields_SkipsUnsupportedFields()
        {
            var names = CriteriaField.FromQueryFields(QuerySchema()).Select(f => f.FieldName);

            Assert.Equal(["sys_id", "freight", "order_date"], names);
        }

        [Fact]
        [DisplayName("FromQueryFields leaves out a sensitive field the user cannot read and keeps it when they can")]
        public void FromQueryFields_SensitiveField_FollowsReadCapability()
        {
            var denied = new Dictionary<string, PermissionActions> { ["Amount"] = PermissionActions.None };
            var allowed = new Dictionary<string, PermissionActions> { ["Amount"] = PermissionActions.Read };

            Assert.DoesNotContain(CriteriaField.FromQueryFields(QuerySchema(), denied), f => f.FieldName == "freight");
            Assert.Contains(CriteriaField.FromQueryFields(QuerySchema(), allowed), f => f.FieldName == "freight");
        }
    }
}
