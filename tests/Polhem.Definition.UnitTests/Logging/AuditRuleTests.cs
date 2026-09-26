using System.ComponentModel;
using Polhem.Definition.Logging;

namespace Polhem.Definition.UnitTests.Logging
{
    /// <summary>
    /// Unit tests for resolving the three states of <see cref="AuditRuleMode"/> and for the <see cref="CompanyAuditRules"/> lookup.
    /// Pure logic that never touches the database: this is the semantic core of per-form audit rules.
    /// </summary>
    public class AuditRuleTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [DisplayName("Inherit uses the deployment default of that axis")]
        public void Resolve_Inherit_ReturnsInheritedValue(bool inherited)
        {
            Assert.Equal(inherited, AuditRuleMode.Inherit.Resolve(inherited));
        }

        [Fact]
        [DisplayName("On overrides a deployment default of false (the main purpose of per-form rules)")]
        public void Resolve_On_OverridesDisabledDefault()
        {
            Assert.True(AuditRuleMode.On.Resolve(false));
        }

        [Fact]
        [DisplayName("Off overrides a deployment default of true")]
        public void Resolve_Off_OverridesEnabledDefault()
        {
            Assert.False(AuditRuleMode.Off.Resolve(true));
        }

        [Fact]
        [DisplayName("The enum value of Inherit is 0, so an unset database column falls into the inherit semantics")]
        public void AuditRuleMode_Inherit_IsZero()
        {
            Assert.Equal(0, (int)AuditRuleMode.Inherit);
        }

        [Fact]
        [DisplayName("Find returns the rule by its declared progId")]
        public void Find_KnownProgId_ReturnsRule()
        {
            var rules = new CompanyAuditRules("C001",
                [new AuditRule("Order", AuditRuleMode.On, AuditRuleMode.Off, true)]);

            var rule = rules.Find("Order");

            Assert.NotNull(rule);
            Assert.Equal(AuditRuleMode.On, rule.ChangeMode);
            Assert.Equal(AuditRuleMode.Off, rule.AccessMode);
            Assert.True(rule.IsSensitive);
        }

        [Fact]
        [DisplayName("Find returns null when there is no rule (an undeclared form inherits on every axis)")]
        public void Find_UnknownProgId_ReturnsNull()
        {
            var rules = new CompanyAuditRules("C001",
                [new AuditRule("Order", AuditRuleMode.On, AuditRuleMode.On, false)]);

            Assert.Null(rules.Find("Customer"));
        }

        [Fact]
        [DisplayName("Find is case-sensitive (Ordinal), because a progId is an identifier, not display text")]
        public void Find_DifferentCasing_ReturnsNull()
        {
            var rules = new CompanyAuditRules("C001",
                [new AuditRule("Order", AuditRuleMode.On, AuditRuleMode.On, false)]);

            Assert.Null(rules.Find("ORDER"));
        }

        [Fact]
        [DisplayName("An empty rule set can be created and every lookup returns null")]
        public void EmptyRules_FindAlwaysReturnsNull()
        {
            var rules = new CompanyAuditRules("C001", []);

            Assert.Equal(0, rules.Count);
            Assert.Null(rules.Find("Order"));
        }

        [Fact]
        [DisplayName("Find returns null instead of throwing for an empty string")]
        public void Find_EmptyProgId_ReturnsNull()
        {
            var rules = new CompanyAuditRules("C001", []);

            Assert.Null(rules.Find(string.Empty));
        }

        [Fact]
        [DisplayName("A duplicate progId keeps the first entry, so one bad row does not break auditing for the whole company")]
        public void DuplicateProgId_KeepsFirstRule()
        {
            var rules = new CompanyAuditRules("C001",
            [
                new AuditRule("Order", AuditRuleMode.On, AuditRuleMode.On, true),
                new AuditRule("Order", AuditRuleMode.Off, AuditRuleMode.Off, false),
            ]);

            Assert.Equal(1, rules.Count);
            Assert.Equal(AuditRuleMode.On, rules.Find("Order")!.ChangeMode);
        }
    }
}
