using System.ComponentModel;
using Polhem.Api.Core.MessagePack;
using Polhem.Definition;
using Polhem.Definition.Collections;
using Polhem.Definition.Organization;
using Polhem.Definition.Sorting;

namespace Polhem.Api.Core.UnitTests
{
    /// <summary>
    /// Round-trip and shape tests for the hand-written wire formatters.
    /// </summary>
    /// <remarks>
    /// What is verified here is round-trip fidelity. **Whether a type and its formatter agree on the member list is
    /// not verified here**; that is the job of <c>WireContractDriftTests</c>, which compares
    /// <c>IWireContract.WireMemberNames</c> with the current shape of the type.
    /// <para>
    /// There used to be a set of <c>WireMemberCount</c> assertions here, claimed to be "the only link between a type
    /// and its formatter". They read back the map header the formatter itself wrote and compared it with a constant
    /// the formatter itself declared, which is <c>Assert.Equal(X, X)</c> and can never fail. The target types of the
    /// eight hand-written formatters therefore never had a shape guard. Those formatters now implement
    /// <c>IWireContract</c> instead.
    /// </para>
    /// </remarks>
    public class WireFormatterTests
    {
        [Fact]
        [DisplayName("SortField round-trips")]
        public void SortField_RoundTripsWithDeclaredMemberCount()
        {
            var source = new SortField("cust_id", SortDirection.Desc);

            var bytes = MessagePackCodec.Serialize(source);
            var result = MessagePackCodec.Deserialize<SortField>(bytes);

            Assert.Equal("cust_id", result.FieldName);
            Assert.Equal(SortDirection.Desc, result.Direction);
        }

        [Fact]
        [DisplayName("Framework-managed members do not travel over the wire")]
        public void FrameworkManagedMembers_DoNotTravel()
        {
            var source = new SortField("cust_id", SortDirection.Asc) { Tag = "should-not-travel" };

            var result = MessagePackCodec.Deserialize<SortField>(MessagePackCodec.Serialize(source));

            Assert.Null(result.Tag);
            Assert.Null(result.Collection);
        }

        [Fact]
        [DisplayName("DepartmentNode round-trips, including nested children")]
        public void DepartmentNode_RoundTripsWithNestedChildren()
        {
            var source = new DepartmentNode
            {
                RowId = Guid.NewGuid(),
                DeptId = "D01",
                DeptName = "業務部",
                ManagerRowId = Guid.NewGuid(),
                Children = [new DepartmentNode { DeptId = "D01-1", DeptName = "北區" }],
            };

            var bytes = MessagePackCodec.Serialize(source);
            var result = MessagePackCodec.Deserialize<DepartmentNode>(bytes);

            Assert.Equal(source.RowId, result.RowId);
            Assert.Equal("業務部", result.DeptName);
            Assert.Equal(source.ManagerRowId, result.ManagerRowId);
            Assert.NotNull(result.Children);
            Assert.Single(result.Children);
            Assert.Equal("北區", result.Children[0].DeptName);
        }

        [Fact]
        [DisplayName("DepartmentTree round-trips the whole tree")]
        public void DepartmentTree_RoundTrips()
        {
            var source = new DepartmentTree
            {
                CompanyId = "C01",
                Roots = [new DepartmentNode { DeptId = "D01", DeptName = "總部" }],
            };

            var result = MessagePackCodec.Deserialize<DepartmentTree>(MessagePackCodec.Serialize(source));

            Assert.Equal("C01", result.CompanyId);
            Assert.NotNull(result.Roots);
            Assert.Single(result.Roots);
            Assert.Equal("總部", result.Roots[0].DeptName);
        }

        [Fact]
        [DisplayName("NumberFormatItem round-trips")]
        public void NumberFormatItem_RoundTrips()
        {
            var source = new NumberFormatItem { Kind = NumberKind.Amount, Decimals = 4 };

            var bytes = MessagePackCodec.Serialize(source);
            var result = MessagePackCodec.Deserialize<NumberFormatItem>(bytes);

            Assert.Equal(NumberKind.Amount, result.Kind);
            Assert.Equal(4, result.Decimals);
        }

        [Fact]
        [DisplayName("CashRoundingItem round-trips")]
        public void CashRoundingItem_RoundTrips()
        {
            var source = new CashRoundingItem { CurrencyCode = "TWD", Unit = 0.5m };

            var bytes = MessagePackCodec.Serialize(source);
            var result = MessagePackCodec.Deserialize<CashRoundingItem>(bytes);

            Assert.Equal("TWD", result.CurrencyCode);
            Assert.Equal(0.5m, result.Unit);
        }

        [Fact]
        [DisplayName("AllowedCurrencyItem round-trips")]
        public void AllowedCurrencyItem_RoundTrips()
        {
            var source = new AllowedCurrencyItem { Code = "USD" };

            var bytes = MessagePackCodec.Serialize(source);
            var result = MessagePackCodec.Deserialize<AllowedCurrencyItem>(bytes);

            Assert.Equal("USD", result.Code);
        }

        [Fact]
        [DisplayName("Parameter round-trips values of allowlisted types")]
        public void Parameter_RoundTripsAllowedValueTypes()
        {
            var source = new ParameterCollection
            {
                new Parameter("s", "hello"),
                new Parameter("i", 42),
                new Parameter("g", Guid.NewGuid()),
            };

            var result = MessagePackCodec.Deserialize<ParameterCollection>(
                MessagePackCodec.Serialize(source));

            Assert.Equal("hello", result["s"].Value);
            Assert.Equal(42, result["i"].Value);
            Assert.Equal(source["g"].Value, result["g"].Value);
        }

        [Fact]
        [DisplayName("Parameter.Value of a type outside the allowlist is blocked")]
        public void Parameter_DisallowedValueType_IsBlocked()
        {
            var source = new ParameterCollection
            {
                new Parameter("evil", new Version(1, 2, 3, 4)),
            };

            // This is a security boundary: a named-type channel that lets through types outside the allowlist is a
            // gadget hole. `WireValueFormatter` blocks it on the writing side, one step earlier than the old
            // read-side-only check. The read-side check is still there (see the hand-built envelope tests in
            // `WireValueFormatterTests`).
            Assert.NotNull(Record.Exception(() => MessagePackCodec.Serialize(source)));
        }

        [Fact]
        [DisplayName("CompanyInfo round-trips, including its transitive collections")]
        public void CompanyInfo_RoundTripsTransitiveCollections()
        {
            var source = new Polhem.Definition.Identity.CompanyInfo
            {
                CompanyId = "C01",
                CompanyName = "示範公司",
                DefaultCurrency = "TWD",
                NumberFormats = [new NumberFormatItem { Kind = NumberKind.Amount, Decimals = 2 }],
                CashRounding = [new CashRoundingItem { CurrencyCode = "TWD", Unit = 1m }],
                AllowedCurrencies = [new AllowedCurrencyItem { Code = "USD" }],
            };

            var result = MessagePackCodec.Deserialize<Polhem.Definition.Identity.CompanyInfo>(
                MessagePackCodec.Serialize(source));

            Assert.Equal("示範公司", result.CompanyName);
            Assert.Single(result.NumberFormats);
            Assert.Equal(2, result.NumberFormats[0].Decimals);
            Assert.Single(result.CashRounding);
            Assert.Equal(1m, result.CashRounding[0].Unit);
            Assert.Single(result.AllowedCurrencies);
            Assert.Equal("USD", result.AllowedCurrencies[0].Code);
        }
    }
}
