using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Storage;

namespace Polhem.Northwind.Backend.UnitTests
{
    /// <summary>
    /// Each demo account is linked to an employee, so a query condition of "me" on an order's salesperson finds the
    /// account's own orders.
    /// </summary>
    [Collection(NorthwindSeedCollection.Name)]
    public sealed class NorthwindSeedEmployeeLinkTests
    {
        private readonly IServiceProvider _services;

        public NorthwindSeedEmployeeLinkTests(NorthwindSeedFixture fixture) { _services = fixture.Services; }

        [Fact]
        [DisplayName("Seeding links the demo account to its employee")]
        public void Seed_LinksDemoAccountToEmployee()
        {
            Seed();

            Assert.Equal(UserRowId(NorthwindCredentials.UserId), EmployeeUser("E001"));
        }

        [Fact]
        [DisplayName("Seeding links an employee whose user id names no user, which is what an unlinked employee holds")]
        public void Seed_EmployeeWithDanglingUser_IsLinked()
        {
            Seed();
            var demo = UserRowId(NorthwindCredentials.UserId);
            SetEmployeeUser("E001", Guid.NewGuid());

            Seed();

            Assert.Equal(demo, EmployeeUser("E001"));
        }

        [Fact]
        [DisplayName("Seeding again leaves a link someone changed as they left it")]
        public void Seed_ChangedLink_IsKept()
        {
            Seed();
            var demo = UserRowId(NorthwindCredentials.UserId);
            SetEmployeeUser("E001", Guid.Empty);
            SetEmployeeUser("E004", demo);
            try
            {
                Seed();

                Assert.Equal(Guid.Empty, EmployeeUser("E001"));
                Assert.Equal(demo, EmployeeUser("E004"));
            }
            finally
            {
                // The database is shared with the other seed tests; put the seeded link back.
                SetEmployeeUser("E004", Guid.Empty);
                SetEmployeeUser("E001", demo);
            }
        }

        private void Seed()
            => NorthwindSchemaSeeder.EnsureSchemaAndSeed(
                _services.GetRequiredService<IDefineAccess>(),
                _services.GetRequiredService<IDbConnectionManager>(),
                _services.GetRequiredService<IDbAccessFactory>());

        private void SetEmployeeUser(string employeeId, Guid userRowId)
            => Db("company").Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "UPDATE st_employee SET user_rowid = {0} WHERE sys_id = {1}", userRowId, employeeId));

        private DbAccess Db(string id) => _services.GetRequiredService<IDbAccessFactory>().Create(id);

        private Guid UserRowId(string userId) => ToGuid(Db("common").Execute(new DbCommandSpec(DbCommandKind.Scalar,
            "SELECT sys_rowid FROM st_user WHERE sys_id = {0}", userId)).Scalar);

        private Guid EmployeeUser(string employeeId) => ToGuid(Db("company").Execute(new DbCommandSpec(DbCommandKind.Scalar,
            "SELECT user_rowid FROM st_employee WHERE sys_id = {0}", employeeId)).Scalar);

        private static Guid ToGuid(object? value) => value switch
        {
            Guid guid => guid,
            byte[] bytes => new Guid(bytes),
            string text when Guid.TryParse(text, out var parsed) => parsed,
            _ => Guid.Empty,
        };
    }
}
