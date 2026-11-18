using System.ComponentModel;
using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db;
using Polhem.Db.Dml;
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Definition.Filters;
using Polhem.Definition.Storage;

namespace Polhem.Northwind.Backend.UnitTests
{
    /// <summary>
    /// The demo's seeded dates must be stored the way the framework stores a date, so a list filtered from a seeded
    /// day returns that day's rows.
    /// </summary>
    /// <remarks>
    /// SQLite compares dates as text. The seeder used to write each JSON value as a string, so <c>ft_order</c> held
    /// <c>1996-07-08</c>, while a <see cref="DateOnly"/> filter reaches SQLite as <c>1996-07-08 00:00:00</c>; the
    /// orders of the first day of a range were left out. Each test runs <see cref="NorthwindSchemaSeeder"/> over a
    /// copy of the demo's <c>Define</c> folder against a SQLite file of its own, so no database container is involved.
    /// </remarks>
    public sealed class NorthwindSeedDateFilterTests : IClassFixture<NorthwindSeedFixture>
    {
        private const string OrderProgId = "Order";

        private readonly IServiceProvider _services;

        public NorthwindSeedDateFilterTests(NorthwindSeedFixture fixture) { _services = fixture.Services; }

        [Fact]
        [DisplayName("A GetList-style filter on order_date from a seeded day returns the orders of that day on SQLite")]
        public void Seed_OrderDateRangeFromSeededDay_ReturnsThatDaysOrders()
        {
            Seed();

            var sysIds = SelectOrdersBetween(new DateOnly(1996, 7, 8), new DateOnly(1996, 7, 12));

            Assert.Equal(["10250", "10251", "10252", "10253", "10254", "10255"], sysIds);
        }

        [Fact]
        [DisplayName("Seeding again rewrites a bare date left by an earlier seeder so the filter finds its row")]
        public void Seed_ExistingBareDate_IsRewrittenToTheStoredForm()
        {
            Seed();
            CompanyDb().Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "UPDATE ft_order SET order_date = {0} WHERE sys_id = {1}", "1996-07-08", "10250"));

            Seed();

            var sysIds = SelectOrdersBetween(new DateOnly(1996, 7, 8), new DateOnly(1996, 7, 8));
            Assert.Equal(["10250", "10251"], sysIds);
        }

        private void Seed()
            => NorthwindSchemaSeeder.EnsureSchemaAndSeed(
                _services.GetRequiredService<IDefineAccess>(),
                _services.GetRequiredService<IDbConnectionManager>(),
                _services.GetRequiredService<IDbAccessFactory>());

        private DbAccess CompanyDb() => _services.GetRequiredService<IDbAccessFactory>().Create("company");

        private string[] SelectOrdersBetween(DateOnly from, DateOnly to)
        {
            var defineAccess = _services.GetRequiredService<IDefineAccess>();
            var builder = new SelectCommandBuilder(defineAccess.GetFormSchema(OrderProgId), DatabaseType.SQLite, defineAccess);
            var filter = FilterGroup.All(
                new FilterCondition("order_date", ComparisonOperator.GreaterThanOrEqual, from),
                new FilterCondition("order_date", ComparisonOperator.LessThanOrEqual, to));

            var table = CompanyDb().Execute(builder.Build(OrderProgId, "sys_id", filter)).Table!;

            return table.Rows.Cast<DataRow>()
                .Select(row => Convert.ToString(row["sys_id"], System.Globalization.CultureInfo.InvariantCulture)!)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
