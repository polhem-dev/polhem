using System.ComponentModel;
using System.Data.Common;
using System.Reflection;
using Polhem.Db;
using Polhem.Tests.Shared;
using Polhem.Hosting.DependencyInjection;

namespace Polhem.Hosting.UnitTests
{
    /// <summary>
    /// <c>Polhem.Hosting</c> composes the framework and runs no SQL of its own: statements live in
    /// <c>Polhem.Db</c> and <c>Polhem.Repository</c>.
    /// </summary>
    /// <remarks>
    /// The database initializer once wrote its seed <c>INSERT</c>s and the company-row <c>UPDATE</c> here. The seed's
    /// column list was then a second authority on the framework tables, which a column added to <c>st_user</c> had to
    /// be remembered in, and nothing told anyone to look in the composition root. The scan reads compiled IL, closures
    /// and async state machines included.
    /// </remarks>
    public class HostingHoldsNoSqlGateTests
    {
        // What counts as running SQL: building a framework command, running one through DbAccess (Execute*, Query*,
        // Update*), or going around DbAccess to ADO.NET (a command created from a connection, or run directly).
        private static readonly string[] s_dbAccessVerbs = ["Execute", "Query", "Update"];

        private static bool RunsSql(MemberInfo member)
            => member switch
            {
                ConstructorInfo constructor
                    => constructor.DeclaringType == typeof(DbCommandSpec) || constructor.DeclaringType == typeof(DbBatchSpec)
                       || typeof(DbCommand).IsAssignableFrom(constructor.DeclaringType),
                MethodInfo method when method.DeclaringType == typeof(DbAccess)
                    => s_dbAccessVerbs.Any(verb => method.Name.StartsWith(verb, StringComparison.Ordinal)),
                MethodInfo method when typeof(DbConnection).IsAssignableFrom(method.DeclaringType)
                    => method.Name is nameof(DbConnection.CreateCommand),
                MethodInfo method when typeof(DbCommand).IsAssignableFrom(method.DeclaringType)
                    => method.Name.StartsWith("Execute", StringComparison.Ordinal),
                _ => false,
            };

        private static IEnumerable<(string Where, MemberInfo Member)> SqlCalls(Assembly assembly)
            => assembly.GetTypes()
                .Where(t => !t.IsNested && t.Namespace?.StartsWith("Polhem.", StringComparison.Ordinal) == true)
                .SelectMany(IlMemberReferences.MethodsOf)
                .SelectMany(m => IlMemberReferences.Of(m)
                    .Where(RunsSql)
                    .Select(r => ($"{IlMemberReferences.OuterType(m)!.FullName}", r)));

        [Fact]
        [DisplayName("Polhem.Hosting builds no database command and runs none")]
        public void Hosting_RunsNoSql()
        {
            var offenders = SqlCalls(typeof(PolhemFrameworkServiceCollectionExtensions).Assembly)
                .Select(c => $"{c.Where} uses {c.Member.DeclaringType!.Name}.{c.Member.Name}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.True(offenders.Length == 0,
                "Move the statement into a repository in Polhem.Repository:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
        }

        [Fact]
        [DisplayName("The scan finds the database commands of Polhem.Repository, so it cannot pass vacuously")]
        public void Scan_SeesRepositorySql()
        {
            var repository = typeof(Polhem.Repository.RepositoryBase).Assembly;

            Assert.Contains(SqlCalls(repository), c => c.Member is ConstructorInfo);
            Assert.Contains(SqlCalls(repository), c => c.Member is MethodInfo);
        }
    }
}
