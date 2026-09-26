using System.ComponentModel;
using Polhem.Definition.Identity;
using Polhem.Definition.Organization;
using Polhem.ObjectCaching.Services;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;
using Polhem.Repository.Abstractions.System;

namespace Polhem.ObjectCaching.UnitTests.Services
{
    /// <summary>
    /// Tests of how <c>EmployeeContextResolver.Resolve</c> chains its lookups, isolated with a fake user and employee
    /// repository: a matching employee, no matching employee, an unknown user, and an employee without a department.
    /// </summary>
    public class EmployeeContextResolverTests
    {
        private const string DbId = "company_x";

        private static readonly Guid s_userRowId = Guid.NewGuid();
        private static readonly Guid s_employeeRowId = Guid.NewGuid();
        private static readonly Guid s_deptRowId = Guid.NewGuid();

        private static EmployeeContextResolver Create(Guid userRowId, EmployeeRow? employee)
            => new(new FakeRepositoryFactory(userRowId, employee));

        [Fact]
        [DisplayName("Resolve returns the full context (user, employee, department) when a matching employee exists")]
        public void Resolve_WithEmployee_ReturnsFullContext()
        {
            var employee = new EmployeeRow(s_employeeRowId, "E001", "Alice", s_deptRowId, s_userRowId);
            var resolver = Create(s_userRowId, employee);

            var ctx = resolver.Resolve("001", DbId);

            Assert.Equal(s_userRowId, ctx.UserRowId);
            Assert.Equal(s_employeeRowId, ctx.EmployeeRowId);
            Assert.Equal(s_deptRowId, ctx.DeptRowId);
        }

        [Fact]
        [DisplayName("Resolve returns the user rowid with an empty employee and department when the user has no matching employee")]
        public void Resolve_NoEmployee_ReturnsUserOnly()
        {
            var resolver = Create(s_userRowId, employee: null);

            var ctx = resolver.Resolve("001", DbId);

            Assert.Equal(s_userRowId, ctx.UserRowId);
            Assert.Equal(Guid.Empty, ctx.EmployeeRowId);
            Assert.Equal(Guid.Empty, ctx.DeptRowId);
        }

        [Fact]
        [DisplayName("Resolve returns an empty context for an unknown user")]
        public void Resolve_UnknownUser_ReturnsEmpty()
        {
            // The user repository returns `Guid.Empty` (no such account), so the employee is not looked up.
            var resolver = Create(Guid.Empty, new EmployeeRow(s_employeeRowId, "E001", "Alice", s_deptRowId, s_userRowId));

            var ctx = resolver.Resolve("nobody", DbId);

            Assert.Equal(EmployeeContext.Empty, ctx);
        }

        [Fact]
        [DisplayName("Resolve returns an empty department when the employee has no department")]
        public void Resolve_EmployeeWithoutDept_ReturnsEmptyDept()
        {
            var employee = new EmployeeRow(s_employeeRowId, "E001", "Alice", Guid.Empty, s_userRowId);
            var resolver = Create(s_userRowId, employee);

            var ctx = resolver.Resolve("001", DbId);

            Assert.Equal(s_userRowId, ctx.UserRowId);
            Assert.Equal(s_employeeRowId, ctx.EmployeeRowId);
            Assert.Equal(Guid.Empty, ctx.DeptRowId);
        }

        /// <summary>
        /// Answers only the two interfaces <c>EmployeeContextResolver</c> uses and throws for everything else,
        /// so any unexpected repository access shows up immediately in a test.
        /// </summary>
        private sealed class FakeRepositoryFactory : IRepositoryFactory
        {
            private readonly Guid _userRowId;
            private readonly EmployeeRow? _employee;

            public FakeRepositoryFactory(Guid userRowId, EmployeeRow? employee)
            {
                _userRowId = userRowId;
                _employee = employee;
            }

            public T Create<T>(Guid accessToken = default) where T : class
            {
                if (typeof(T) == typeof(IUserRepository)) { return (T)(object)new FakeUserRepository(_userRowId); }
                if (typeof(T) == typeof(IEmployeeRepository)) { return (T)(object)new FakeEmployeeRepository(_employee); }
                throw new NotSupportedException(typeof(T).FullName);
            }

            public T CreateFormRepository<T>(Guid accessToken, string progId) where T : class, IDataFormRepository
                => throw new NotSupportedException();
        }

        private sealed class FakeUserRepository : IUserRepository
        {
            private readonly Guid _rowId;
            public FakeUserRepository(Guid rowId) { _rowId = rowId; }
            public Guid GetRowIdBySysId(string userId) => _rowId;
            public bool VerifyPassword(string userId, string password) => false;
            public UserLocale GetLocale(string userId) => UserLocale.Empty;
            public string? GetName(string userId) => string.Empty;
            public bool IsDeploymentAdmin(string userId) => false;
            public bool SetDeploymentAdmin(string userId, bool isDeploymentAdmin) => throw new NotSupportedException();
        }

        private sealed class FakeEmployeeRepository : IEmployeeRepository
        {
            private readonly EmployeeRow? _employee;
            public FakeEmployeeRepository(EmployeeRow? employee) { _employee = employee; }
            public EmployeeRow? GetByUserRowId(string databaseId, Guid userRowId) => _employee;
        }
    }
}
