using System.ComponentModel;
using Polhem.Base.Data;
using Polhem.Base.Serialization;
using Polhem.Definition.Forms;

namespace Polhem.Definition.UnitTests.Forms
{
    /// <summary>
    /// Definition-layer tests: XML serialization round-trips, omission of empty values, and Clone deep copies of
    /// <see cref="FormField.ValueExpression"/> / <see cref="FormField.DefaultValueExpression"/>, <see cref="FormRule"/> and
    /// <see cref="FormSchema.Rules"/>.
    /// FormSchema uses XML as its only transport serialization path (XmlCodec.Serialize on the server, then
    /// XmlCodec.Deserialize on the client), so there are no JSON / MessagePack round-trips here.
    /// </summary>
    public class FormRuleTests
    {
        #region FormField expression properties

        [Fact]
        [DisplayName("FormField ValueExpression round-trips through XmlAttribute serialization")]
        public void ValueExpression_RoundTripsThroughXml()
        {
            var field = new FormField("amount", "金額", FieldDbType.Currency)
            {
                ValueExpression = "unit_price * qty",
            };

            var xml = XmlCodec.Serialize(field);
            var restored = XmlCodec.Deserialize<FormField>(xml);

            Assert.NotNull(restored);
            Assert.Equal("unit_price * qty", restored!.ValueExpression);
            Assert.Contains("ValueExpression=\"unit_price * qty\"", xml);
        }

        [Fact]
        [DisplayName("FormField DefaultValueExpression round-trips through XmlAttribute serialization")]
        public void DefaultValueExpression_RoundTripsThroughXml()
        {
            var field = new FormField("order_date", "訂單日期", FieldDbType.DateTime)
            {
                DefaultValueExpression = "Today()",
            };

            var xml = XmlCodec.Serialize(field);
            var restored = XmlCodec.Deserialize<FormField>(xml);

            Assert.NotNull(restored);
            Assert.Equal("Today()", restored!.DefaultValueExpression);
        }

        [Fact]
        [DisplayName("FormField expression properties at their empty defaults are omitted from serialization")]
        public void ExpressionProperties_Empty_OmitXmlAttributes()
        {
            var field = new FormField("sys_name", "名稱", FieldDbType.String);

            var xml = XmlCodec.Serialize(field);

            Assert.DoesNotContain("ValueExpression=", xml);
            Assert.DoesNotContain("DefaultValueExpression=", xml);
        }

        #endregion

        #region FormRule serialization

        [Fact]
        [DisplayName("Every FormRule property round-trips through XmlAttribute serialization")]
        public void FormRule_RoundTripsThroughXml()
        {
            var rule = new FormRule("amount_positive", "amount > 0", "已核准訂單金額必須大於 0")
            {
                Trigger = FormRuleTrigger.BeforeSave,
                TargetTable = "OrderDetail",
                When = "status == \"Approved\"",
                Enabled = true,
                Order = 5,
            };

            var xml = XmlCodec.Serialize(rule);
            var restored = XmlCodec.Deserialize<FormRule>(xml);

            Assert.NotNull(restored);
            Assert.Equal("amount_positive", restored!.RuleId);
            Assert.Equal(FormRuleTrigger.BeforeSave, restored.Trigger);
            Assert.Equal("OrderDetail", restored.TargetTable);
            Assert.Equal("status == \"Approved\"", restored.When);
            Assert.Equal("amount > 0", restored.Condition);
            Assert.Equal("已核准訂單金額必須大於 0", restored.Message);
            Assert.True(restored.Enabled);
            Assert.Equal(5, restored.Order);
        }

        [Fact]
        [DisplayName("FormRule BeforeDelete trigger round-trips through serialization")]
        public void FormRule_BeforeDeleteTrigger_RoundTripsThroughXml()
        {
            var rule = new FormRule("no_delete_closed", "status != \"Closed\"", "已結案不可刪除")
            {
                Trigger = FormRuleTrigger.BeforeDelete,
            };

            var xml = XmlCodec.Serialize(rule);
            var restored = XmlCodec.Deserialize<FormRule>(xml);

            Assert.NotNull(restored);
            Assert.Equal(FormRuleTrigger.BeforeDelete, restored!.Trigger);
        }

        [Fact]
        [DisplayName("FormRule with an empty When omits the attribute from serialization (the rule always applies)")]
        public void FormRule_EmptyWhen_OmitsXmlAttribute()
        {
            var rule = new FormRule("always", "amount > 0", "金額必須大於 0");

            var xml = XmlCodec.Serialize(rule);

            Assert.DoesNotContain("When=", xml);
        }

        #endregion

        #region FormSchema.Rules

        [Fact]
        [DisplayName("FormSchema.Rules is empty when created and serialization emits no Rules node")]
        public void Rules_Empty_OmittedFromXml()
        {
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company" };

            var xml = XmlCodec.Serialize(schema);

            Assert.DoesNotContain("<Rules", xml);
        }

        [Fact]
        [DisplayName("FormSchema.Rules round-trips through XML serialization")]
        public void Rules_RoundTripThroughXml()
        {
            var schema = BuildSchemaWithRule();

            var xml = XmlCodec.Serialize(schema);
            var restored = XmlCodec.Deserialize<FormSchema>(xml);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Rules);
            Assert.Single(restored.Rules!);
            var rule = restored.Rules!["amount_positive"];
            Assert.Equal("amount > 0", rule.Condition);
            Assert.Equal("金額必須大於 0", rule.Message);
        }

        [Fact]
        [DisplayName("FormSchema.Clone deep-copies Rules, and every entry is an independent instance")]
        public void Clone_CopiesRules()
        {
            var source = BuildSchemaWithRule();

            var clone = source.Clone();

            Assert.NotNull(clone.Rules);
            Assert.Single(clone.Rules!);
            Assert.NotSame(source.Rules, clone.Rules);
            Assert.NotSame(source.Rules!["amount_positive"], clone.Rules!["amount_positive"]);
            Assert.Equal("amount > 0", clone.Rules!["amount_positive"].Condition);

            clone.Rules!["amount_positive"].Message = "changed";
            Assert.Equal("金額必須大於 0", source.Rules!["amount_positive"].Message);
        }

        [Fact]
        [DisplayName("FormSchema.Clone deep-copies FormField.ValueExpression")]
        public void Clone_CopiesFieldValueExpression()
        {
            var source = BuildSchemaWithRule();

            var clone = source.Clone();
            var field = clone.Tables!["Order"].Fields!["amount"];

            Assert.Equal("unit_price * qty", field.ValueExpression);
            Assert.NotSame(source.Tables!["Order"].Fields!["amount"], field);
        }

        #endregion

        private static FormSchema BuildSchemaWithRule()
        {
            var schema = new FormSchema("Order", "訂單") { CategoryId = "company" };
            var table = schema.Tables!.Add("Order", "訂單");
            table.Fields!.Add("unit_price", "單價", FieldDbType.Currency);
            table.Fields!.Add("qty", "數量", FieldDbType.Decimal);
            table.Fields!.Add(new FormField("amount", "金額", FieldDbType.Currency)
            {
                ValueExpression = "unit_price * qty",
                ReadOnly = true,
            });
            schema.Rules!.Add("amount_positive", "amount > 0", "金額必須大於 0");
            return schema;
        }
    }
}
