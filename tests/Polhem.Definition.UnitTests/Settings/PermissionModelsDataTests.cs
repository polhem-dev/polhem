using System.ComponentModel;
using Polhem.Base.Serialization;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests.Settings
{
    /// <summary>
    /// Tests for the permission definition classes PermissionModels, PermissionModel and PermissionRule.
    /// </summary>
    public class PermissionModelsDataTests
    {
        /// <summary>
        /// Builds a sample permission definition (PurchaseOrder / Vendor / Requisition) used by the tests.
        /// </summary>
        private static PermissionModels BuildSample()
        {
            var models = new PermissionModels();

            var po = models.Models!.Add("PurchaseOrder", "採購單");
            po.Rules!.Add(PermissionActions.Read, ScopeStrategy.DeptAndSub);
            po.Rules!.Add(PermissionActions.Create, ScopeStrategy.All);
            po.Rules!.Add(PermissionActions.Update, ScopeStrategy.Own);
            po.Rules!.Add(PermissionActions.Delete, ScopeStrategy.Own);
            po.Rules!.Add(PermissionActions.Print);
            po.Rules!.Add(PermissionActions.Export);

            var vendor = models.Models!.Add("Vendor", "廠商");
            vendor.Rules!.Add(PermissionActions.Read, ScopeStrategy.All);
            vendor.Rules!.Add(PermissionActions.Update, ScopeStrategy.Own);

            var req = models.Models!.Add("Requisition", "請購單");
            req.Rules!.Add(PermissionActions.Read, ScopeStrategy.DeptAndSub);
            req.Rules!.Add(PermissionActions.Create, ScopeStrategy.All);

            return models;
        }

        [Fact]
        [DisplayName("PermissionModel parameterized constructor sets ModelId and DisplayName")]
        public void PermissionModel_ParameterizedConstructor_SetsProperties()
        {
            var model = new PermissionModel("PurchaseOrder", "採購單");

            Assert.Equal("PurchaseOrder", model.ModelId);
            Assert.Equal("採購單", model.DisplayName);
            Assert.Equal("PurchaseOrder", model.Key);
        }

        [Fact]
        [DisplayName("PermissionModel.ToString returns 'ModelId - DisplayName'")]
        public void PermissionModel_ToString_ReturnsFormatted()
        {
            var model = new PermissionModel("PurchaseOrder", "採購單");

            Assert.Equal("PurchaseOrder - 採購單", model.ToString());
        }

        [Fact]
        [DisplayName("PermissionRule Action is also set as the collection Key")]
        public void PermissionRule_Action_SetsAsKey()
        {
            var rule = new PermissionRule(PermissionActions.Update, ScopeStrategy.Own);

            Assert.Equal(PermissionActions.Update, rule.Action);
            Assert.Equal(ScopeStrategy.Own, rule.Scope);
            Assert.Equal("Update", rule.Key);
        }

        [Fact]
        [DisplayName("PermissionRule Scope defaults to Inherit")]
        public void PermissionRule_DefaultScope_IsInherit()
        {
            var rule = new PermissionRule(PermissionActions.Print);

            Assert.Equal(ScopeStrategy.Inherit, rule.Scope);
        }

        [Fact]
        [DisplayName("PermissionRuleCollection is indexable by Action")]
        public void PermissionRuleCollection_IndexedByAction()
        {
            var model = new PermissionModel("PurchaseOrder", "採購單");
            model.Rules!.Add(PermissionActions.Read, ScopeStrategy.DeptAndSub);

            Assert.Equal(ScopeStrategy.DeptAndSub, model.Rules!["Read"].Scope);
        }

        [Fact]
        [DisplayName("An egress action (Print) without a scope does not write the Scope attribute to XML")]
        public void PermissionRule_EgressInheritScope_OmittedFromXml()
        {
            var models = new PermissionModels();
            var po = models.Models!.Add("PurchaseOrder", "採購單");
            po.Rules!.Add(PermissionActions.Print);

            var xml = XmlCodec.Serialize(models);

            Assert.Contains("Action=\"Print\"", xml);
            Assert.DoesNotContain("Scope=\"Inherit\"", xml);
        }

        [Fact]
        [DisplayName("An action with a scope writes the Scope attribute to XML")]
        public void PermissionRule_ExplicitScope_WrittenToXml()
        {
            var models = new PermissionModels();
            var po = models.Models!.Add("PurchaseOrder", "採購單");
            po.Rules!.Add(PermissionActions.Update, ScopeStrategy.Own);

            var xml = XmlCodec.Serialize(models);

            Assert.Contains("Scope=\"Own\"", xml);
        }

        [Fact]
        [DisplayName("A PermissionModels round-trip keeps the models, rules and scopes")]
        public void PermissionModels_RoundTripsThroughXml()
        {
            var models = BuildSample();

            var xml = XmlCodec.Serialize(models);
            var restored = XmlCodec.Deserialize<PermissionModels>(xml);

            Assert.NotNull(restored);
            Assert.Equal(3, restored!.Models!.Count);

            var po = restored.Models!["PurchaseOrder"];
            Assert.Equal("採購單", po.DisplayName);
            Assert.Equal(ScopeStrategy.DeptAndSub, po.Rules!["Read"].Scope);
            Assert.Equal(ScopeStrategy.All, po.Rules!["Create"].Scope);
            Assert.Equal(ScopeStrategy.Own, po.Rules!["Update"].Scope);
            Assert.Equal(ScopeStrategy.Inherit, po.Rules!["Print"].Scope);
            Assert.Equal(PermissionActions.Export, po.Rules!["Export"].Action);

            Assert.Equal(ScopeStrategy.All, restored.Models!["Vendor"].Rules!["Read"].Scope);
        }

        [Fact]
        [DisplayName("Validate returns an empty list for a valid registry")]
        public void Validate_ValidRegistry_ReturnsEmpty()
        {
            var models = BuildSample();

            var errors = models.Validate();

            Assert.Empty(errors);
        }

        [Fact]
        [DisplayName("Validate reports an error for an egress action with an explicit scope")]
        public void Validate_EgressWithScope_ReturnsError()
        {
            var models = new PermissionModels();
            var po = models.Models!.Add("PurchaseOrder", "採購單");
            po.Rules!.Add(PermissionActions.Print, ScopeStrategy.Own);

            var errors = models.Validate();

            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("Print"));
        }
    }
}
