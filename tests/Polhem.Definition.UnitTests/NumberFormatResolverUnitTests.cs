using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Settings;

namespace Polhem.Definition.UnitTests
{
    /// <summary>
    /// NumberFormatResolver with units of measure: quantity and weight resolve their decimals from the unit in the UNIT column,
    /// with round-then-sum within one unit. Without a unit code nothing is rounded; a unit code missing from the master, or no
    /// master at all, falls back to the framework default. Company decimals never take part.
    /// </summary>
    public class NumberFormatResolverUnitTests
    {
        private static UnitSettings Units() =>
        [
            new UnitItem("PCS", 0, "count", "Pieces"),
            new UnitItem("KG", 3, "weight", "Kilogram"),
            new UnitItem("M", 2, "length", "Metre"),
        ];

        private static RoundingContext Ctx(CompanyInfo? company = null) =>
            new() { Company = company, UnitSettings = Units() };

        private static CompanyInfo CompanyWithOverride(NumberKind kind, int decimals)
        {
            var company = new CompanyInfo { CompanyId = "C001" };
            company.NumberFormats.Add(new NumberFormatItem(kind, decimals));
            return company;
        }

        [Theory]
        [InlineData(NumberKind.Quantity, "PCS", 0)]
        [InlineData(NumberKind.Quantity, "KG", 3)]
        [InlineData(NumberKind.Weight, "KG", 3)]
        [InlineData(NumberKind.Weight, "M", 2)]
        [DisplayName("ResolveDecimals gives quantity and weight different decimals by the unit in the UNIT column")]
        public void ResolveDecimals_ByUnit(NumberKind kind, string code, int expected)
        {
            Assert.Equal(expected, NumberFormatResolver.ResolveDecimals(kind, Ctx(), code));
        }

        [Theory]
        [InlineData("PCS", "N0")]
        [InlineData("KG", "N3")]
        [DisplayName("ResolveFormat gives quantity a different format string by unit")]
        public void ResolveFormat_ByUnit(string code, string expected)
        {
            Assert.Equal(expected, NumberFormatResolver.ResolveFormat(NumberKind.Quantity, Ctx(), code));
        }

        [Theory]
        [InlineData(NumberKind.Quantity, 0)]
        [InlineData(NumberKind.Weight, 3)]
        [DisplayName("ResolveDecimals without a unit code returns the framework default and ignores the company override")]
        public void ResolveDecimals_NoUnit_IgnoresCompanyOverride(NumberKind kind, int expected)
        {
            var ctx = Ctx(CompanyWithOverride(kind, 5));

            Assert.Equal(expected, NumberFormatResolver.ResolveDecimals(kind, ctx, null));
        }

        [Theory]
        [InlineData(NumberKind.Quantity, null)]
        [InlineData(NumberKind.Quantity, "")]
        [InlineData(NumberKind.Weight, null)]
        [InlineData(NumberKind.Weight, "")]
        [DisplayName("RoundByKind returns the value unchanged (no rounding) when the row's unit code is empty, even with a company override")]
        public void RoundByKind_NoUnit_ReturnsValueUnchanged(NumberKind kind, string? unitCode)
        {
            var ctx = Ctx(CompanyWithOverride(kind, 0));

            Assert.Equal(1.23456m, NumberFormatResolver.RoundByKind(1.23456m, kind, ctx, unitCode));
        }

        [Fact]
        [DisplayName("ResolveDecimals falls back to the framework default without a unit master (Quantity 0, Weight 3)")]
        public void ResolveDecimals_NoUnitMaster_FrameworkDefault()
        {
            var ctx = new RoundingContext { Company = null, UnitSettings = null };

            Assert.Equal(0, NumberFormatResolver.ResolveDecimals(NumberKind.Quantity, ctx, "KG"));
            Assert.Equal(3, NumberFormatResolver.ResolveDecimals(NumberKind.Weight, ctx, "KG"));
        }

        [Fact]
        [DisplayName("ResolveDecimals ignores the company override without a unit master (company sets Quantity to 2, framework gives 0)")]
        public void ResolveDecimals_NoUnitMaster_IgnoresCompanyOverride()
        {
            var ctx = new RoundingContext { Company = CompanyWithOverride(NumberKind.Quantity, 2), UnitSettings = null };

            Assert.Equal(0, NumberFormatResolver.ResolveDecimals(NumberKind.Quantity, ctx, "KG"));
        }

        [Theory]
        [InlineData(NumberKind.Quantity, 0)]
        [InlineData(NumberKind.Weight, 3)]
        [DisplayName("ResolveDecimals falls back to the framework default for a unit code not in the master (Quantity 0, Weight 3)")]
        public void ResolveDecimals_UnknownUnit_FrameworkDefault(NumberKind kind, int expected)
        {
            Assert.Equal(expected, NumberFormatResolver.ResolveDecimals(kind, Ctx(), "XXX"));
        }

        [Fact]
        [DisplayName("RoundByKind rounds to the framework default for a unit code not in the master (Weight 3, Quantity 0)")]
        public void RoundByKind_UnknownUnit_RoundsToFrameworkDefault()
        {
            Assert.Equal(1.235m, NumberFormatResolver.RoundByKind(1.2345m, NumberKind.Weight, Ctx(), "XXX"));
            Assert.Equal(2m, NumberFormatResolver.RoundByKind(1.5m, NumberKind.Quantity, Ctx(), "XXX"));
        }

        [Fact]
        [DisplayName("ResolveDecimals company overload returns the framework default for quantity and ignores the company override")]
        public void ResolveDecimals_CompanyOverload_Quantity_FrameworkDefault()
        {
            var company = CompanyWithOverride(NumberKind.Quantity, 2);

            Assert.Equal(0, NumberFormatResolver.ResolveDecimals(NumberKind.Quantity, company));
        }

        [Fact]
        [DisplayName("RoundByKind company overload returns a quantity unchanged because there is no unit code")]
        public void RoundByKind_CompanyOverload_Quantity_ReturnsValueUnchanged()
        {
            var company = CompanyWithOverride(NumberKind.Quantity, 0);

            Assert.Equal(1.5m, NumberFormatResolver.RoundByKind(1.5m, NumberKind.Quantity, company));
        }

        [Fact]
        [DisplayName("RoundByKind rounds by unit (PCS to 0 decimals, KG to 3)")]
        public void RoundByKind_ByUnit()
        {
            Assert.Equal(12m, NumberFormatResolver.RoundByKind(12.345m, NumberKind.Quantity, Ctx(), "PCS"));
            Assert.Equal(12.345m, NumberFormatResolver.RoundByKind(12.3454m, NumberKind.Weight, Ctx(), "KG"));
        }

        [Fact]
        [DisplayName("Round-then-sum with KG (3 decimals) in one column: the sum of the rounded lines equals the header total")]
        public void RoundThenSum_SameUnit_InvariantHolds()
        {
            decimal[] details = [1.2345m, 1.2345m, 1.2345m];
            var ctx = Ctx();

            decimal total = 0m;
            foreach (var d in details)
                total += NumberFormatResolver.RoundByKind(d, NumberKind.Weight, ctx, "KG");

            // Each 1.2345 rounds AwayFromZero to 1.235 at the KG scale of 3, and three of them make 3.705.
            Assert.Equal(3.705m, total);

            // For contrast, summing at full precision and rounding afterwards (forbidden) gives round(3.7035, KG) = 3.704, not 3.705.
            decimal sumThenRound = NumberFormatResolver.RoundByKind(
                details[0] + details[1] + details[2], NumberKind.Weight, ctx, "KG");
            Assert.NotEqual(total, sumThenRound);
        }

        [Fact]
        [DisplayName("Rows with different units in the same column each resolve their own unit's decimals (PCS 0, KG 3)")]
        public void SameColumn_DifferentUnits_DifferentDecimals()
        {
            var ctx = Ctx();

            Assert.Equal(0, NumberFormatResolver.ResolveDecimals(NumberKind.Quantity, ctx, "PCS"));
            Assert.Equal(3, NumberFormatResolver.ResolveDecimals(NumberKind.Quantity, ctx, "KG"));
        }
    }
}
