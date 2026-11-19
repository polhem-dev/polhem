using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Definition.Storage;

namespace Polhem.Northwind.Backend.UnitTests
{
    /// <summary>
    /// A department whose parent names no department is a top-level department, and seeding stores its parent as the
    /// empty GUID.
    /// </summary>
    [Collection(NorthwindSeedCollection.Name)]
    public sealed class NorthwindSeedDepartmentParentTests
    {
        private readonly IServiceProvider _services;

        public NorthwindSeedDepartmentParentTests(NorthwindSeedFixture fixture) { _services = fixture.Services; }

        [Fact]
        [DisplayName("Seeding leaves every seeded department without a parent")]
        public void Seed_SeededDepartments_HaveNoParent()
        {
            Seed();

            Assert.All(["IT", "SALES", "HR", "FIN"], id => Assert.Equal(Guid.Empty, Parent(id)));
        }

        [Fact]
        [DisplayName("Seeding clears a parent that names no department, which a random column default left behind")]
        public void Seed_DanglingParent_IsCleared()
        {
            Seed();
            SetParent("IT", Guid.NewGuid());

            Seed();

            Assert.Equal(Guid.Empty, Parent("IT"));
        }

        [Fact]
        [DisplayName("Seeding keeps a parent that names an existing department")]
        public void Seed_ExistingParent_IsKept()
        {
            Seed();
            var sales = RowId("SALES");
            SetParent("HR", sales);
            try
            {
                Seed();

                Assert.Equal(sales, Parent("HR"));
            }
            finally
            {
                // The database is shared with the other seed tests; put the seeded parent back.
                SetParent("HR", Guid.Empty);
            }
        }

        private void Seed()
            => NorthwindSchemaSeeder.EnsureSchemaAndSeed(
                _services.GetRequiredService<IDefineAccess>(),
                _services.GetRequiredService<IDbConnectionManager>(),
                _services.GetRequiredService<IDbAccessFactory>());

        private void SetParent(string departmentId, Guid parentRowId)
            => Company().Execute(new DbCommandSpec(DbCommandKind.NonQuery,
                "UPDATE st_department SET parent_rowid = {0} WHERE sys_id = {1}", parentRowId, departmentId));

        private DbAccess Company() => _services.GetRequiredService<IDbAccessFactory>().Create("company");

        private Guid RowId(string departmentId) => ToGuid(Company().Execute(new DbCommandSpec(DbCommandKind.Scalar,
            "SELECT sys_rowid FROM st_department WHERE sys_id = {0}", departmentId)).Scalar);

        private Guid Parent(string departmentId) => ToGuid(Company().Execute(new DbCommandSpec(DbCommandKind.Scalar,
            "SELECT parent_rowid FROM st_department WHERE sys_id = {0}", departmentId)).Scalar);

        private static Guid ToGuid(object? value) => value switch
        {
            Guid guid => guid,
            byte[] bytes => new Guid(bytes),
            string text when Guid.TryParse(text, out var parsed) => parsed,
            _ => Guid.Empty,
        };
    }
}
