using System.ComponentModel;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Client.Definitions;
using Polhem.Base.Data;
using Polhem.Definition;
using Polhem.Definition.Forms;
using Polhem.Definition.Identity;

namespace Polhem.Api.Client.UnitTests.Definitions
{
    /// <summary>
    /// How <see cref="FormDefinitionLoader"/> bakes number formats.
    /// </summary>
    /// <remarks>
    /// The definition APIs serve definitions "as stored", so applying the company's decimal places falls to the
    /// consumer. These tests pin down the fact that the loader bakes them. This link broke once: in v4.x the server
    /// removed the bake with a note that the consumer would handle it, but the consumer had not done so yet, so the
    /// company's decimal places had no effect on any head.
    /// <para>
    /// Uses a fake connector rather than <c>[DbFact]</c>: what is under test is the loader's processing step, not
    /// the transport that fetches the definitions.
    /// </para>
    /// </remarks>
    public class FormDefinitionLoaderNumberFormatTests
    {
        private const string ProgId = "Order";

        /// <summary>
        /// Returns a brand new schema on every <c>GetDefine</c>, so tests do not share an instance and pollute each other.
        /// </summary>
        private sealed class SchemaConnector : SystemApiConnector
        {
            private readonly bool _numeric;

            public SchemaConnector(bool numeric = true) : base(Polhem.Tests.Shared.EmptyServiceProvider.Instance, Guid.NewGuid()) { _numeric = numeric; }

            public override Task<T> GetDefineAsync<T>(DefineType defineType, string[]? keys = null)
            {
                if (defineType != DefineType.FormSchema)
                    return Task.FromResult(Activator.CreateInstance<T>());

                var schema = new FormSchema(ProgId, "訂單") { CurrencyField = "sys_currency" };
                var table = schema.Tables!.Add(ProgId, "訂單");
                if (_numeric)
                {
                    table.Fields!.Add(new FormField("disc", "折扣", FieldDbType.Decimal) { NumberKind = NumberKind.Percent });
                    table.Fields!.Add(new FormField("amount", "金額", FieldDbType.Decimal) { NumberKind = NumberKind.Amount });
                }
                else
                {
                    table.Fields!.Add(new FormField("note", "備註", FieldDbType.String));
                }
                return Task.FromResult((T)(object)schema);
            }
        }

        private static FormDefinitionLoader CreateLoader(CompanyInfo? company)
            => new(new ClientDefineAccess(new SchemaConnector()))
            {
                CompanyAccessor = () => company,
            };

        private static CompanyInfo CompanyWithPercentDecimals(int decimals)
        {
            var company = new CompanyInfo { CompanyId = "C001" };
            company.NumberFormats.Add(new NumberFormatItem(NumberKind.Percent, decimals));
            return company;
        }

        [Fact]
        [DisplayName("GetLocalizedSchemaAsync does not look up the company for a schema without numeric fields")]
        public async Task GetLocalizedSchemaAsync_NoNumericField_SkipsCompanyLookup()
        {
            int lookups = 0;
            var loader = new FormDefinitionLoader(new ClientDefineAccess(new SchemaConnector(numeric: false)))
            {
                CompanyAccessor = () => { lookups++; return null; },
            };

            var schema = await loader.GetLocalizedSchemaAsync(ProgId, string.Empty);

            Assert.Equal(0, lookups);
            Assert.True(schema.Tables![ProgId].Fields!.Contains("note"));
        }

        [Fact]
        [DisplayName("GetLocalizedSchemaAsync looks up the company once for a schema with numeric fields")]
        public async Task GetLocalizedSchemaAsync_NumericField_LooksUpCompanyOnce()
        {
            int lookups = 0;
            var loader = new FormDefinitionLoader(new ClientDefineAccess(new SchemaConnector()))
            {
                CompanyAccessor = () => { lookups++; return null; },
            };

            await loader.GetLocalizedSchemaAsync(ProgId, string.Empty);

            Assert.Equal(1, lookups);
        }

        [Fact]
        [DisplayName("GetLocalizedSchemaAsync bakes the number format on the blank-language path too")]
        public async Task GetLocalizedSchemaAsync_BlankLang_BakesNumberFormat()
        {
            // The blank language is the most common path (`CultureInfo.InvariantCulture.Name` is an empty string),
            // and the format does not depend on the language, so this path must not skip the bake.
            var loader = CreateLoader(CompanyWithPercentDecimals(3));

            var schema = await loader.GetLocalizedSchemaAsync(ProgId, string.Empty);

            Assert.Equal("P3", schema.Tables![ProgId].Fields!["disc"].NumberFormat);
        }

        [Fact]
        [DisplayName("GetLocalizedSchemaAsync bakes the framework default format without a CompanyAccessor")]
        public async Task GetLocalizedSchemaAsync_NoCompanyAccessor_BakesFrameworkDefault()
        {
            var loader = new FormDefinitionLoader(new ClientDefineAccess(new SchemaConnector()));

            var schema = await loader.GetLocalizedSchemaAsync(ProgId, string.Empty);

            Assert.Equal("P2", schema.Tables![ProgId].Fields!["disc"].NumberFormat);
        }

        [Fact]
        [DisplayName("GetLocalizedSchemaAsync yields different formats for companies with different decimal places")]
        public async Task GetLocalizedSchemaAsync_DifferentCompanies_DifferentFormats()
        {
            var schemaA = await CreateLoader(CompanyWithPercentDecimals(2)).GetLocalizedSchemaAsync(ProgId, string.Empty);
            var schemaB = await CreateLoader(CompanyWithPercentDecimals(4)).GetLocalizedSchemaAsync(ProgId, string.Empty);

            Assert.Equal("P2", schemaA.Tables![ProgId].Fields!["disc"].NumberFormat);
            Assert.Equal("P4", schemaB.Tables![ProgId].Fields!["disc"].NumberFormat);
        }

        [Fact]
        [DisplayName("GetLocalizedSchemaAsync does not bake amount fields but has them inherit the master currency field for per-row resolution in the UI")]
        public async Task GetLocalizedSchemaAsync_AmountField_InheritsMasterCurrencyField()
        {
            var loader = CreateLoader(CompanyWithPercentDecimals(2));

            var schema = await loader.GetLocalizedSchemaAsync(ProgId, string.Empty);

            var amount = schema.Tables![ProgId].Fields!["amount"];
            Assert.Equal(string.Empty, amount.NumberFormat);
            Assert.Equal("sys_currency", amount.CurrencyField);
        }

        [Fact]
        [DisplayName("CompanyAccessor is a delegate: after the company changes, the same loader uses the new company's decimal places")]
        public async Task CompanyAccessor_IsRead_PerCall()
        {
            // A delegate rather than a value: `EnterCompany` and `LeaveCompany` change the company during the
            // session's lifetime, and a company captured at construction would keep the loader baking the previous
            // tenant's decimal places.
            CompanyInfo? current = CompanyWithPercentDecimals(2);
            var loader = new FormDefinitionLoader(new ClientDefineAccess(new SchemaConnector()))
            {
                CompanyAccessor = () => current,
            };

            var before = await loader.GetLocalizedSchemaAsync(ProgId, string.Empty);
            current = CompanyWithPercentDecimals(4);
            var after = await loader.GetLocalizedSchemaAsync(ProgId, string.Empty);

            Assert.Equal("P2", before.Tables![ProgId].Fields!["disc"].NumberFormat);
            Assert.Equal("P4", after.Tables![ProgId].Fields!["disc"].NumberFormat);
        }
    }
}
