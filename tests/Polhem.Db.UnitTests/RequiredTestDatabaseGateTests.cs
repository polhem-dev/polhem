using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Tests.Shared;

namespace Polhem.Db.UnitTests
{
    /// <summary>
    /// Turns "the database tests were skipped" into a failure when CI expected them to run.
    /// </summary>
    /// <remarks>
    /// <see cref="DbFactAttribute"/> skips a test when its connection string variable is not set, and a skip is
    /// green. If the workflow and <see cref="TestDbConventions.GetConnectionStringEnvVar"/> ever disagree on a
    /// variable name, every test for that database is skipped and CI stays green. This gate reads the list the
    /// workflow declares in <see cref="TestDbConventions.RequiredDatabasesEnvVar"/> and checks each entry under
    /// the name the tests read. <c>GITHUB_ACTIONS</c> is set by the runner itself, so a list that went missing
    /// because its own variable name drifted fails here too. Locally, with neither variable set, it checks nothing.
    /// </remarks>
    public class RequiredTestDatabaseGateTests
    {
        [Fact]
        [DisplayName("Every database the CI workflow requires has a connection string under the name the tests read")]
        public void RequiredDatabases_HaveConnectionStrings()
        {
            var problems = FindProblems(
                Environment.GetEnvironmentVariable(TestDbConventions.RequiredDatabasesEnvVar),
                string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase),
                name => Environment.GetEnvironmentVariable(name));

            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }

        [Fact]
        [DisplayName("The gate reports a required database whose connection string variable is not set")]
        public void FindProblems_MissingConnectionString_ReportsIt()
        {
            var problems = FindProblems("SQLServer,SQLite", true,
                name => name == TestDbConventions.GetConnectionStringEnvVar(DatabaseType.SQLServer) ? "x" : null);

            Assert.Single(problems);
            Assert.Contains(TestDbConventions.GetConnectionStringEnvVar(DatabaseType.SQLite), problems[0], StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The gate reports a CI run that declares no required databases")]
        public void FindProblems_InCiWithoutList_ReportsIt()
        {
            var problems = FindProblems(null, true, _ => "x");

            Assert.Single(problems);
        }

        [Fact]
        [DisplayName("The gate reports a required database name that is not a DatabaseType")]
        public void FindProblems_UnknownName_ReportsIt()
        {
            var problems = FindProblems("SQLServer,Informix", true, _ => "x");

            Assert.Single(problems);
            Assert.Contains("Informix", problems[0], StringComparison.Ordinal);
        }

        private static List<string> FindProblems(string? required, bool inCi, Func<string, string?> getVariable)
        {
            var problems = new List<string>();
            var names = (required ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (names.Length == 0)
            {
                if (inCi)
                    problems.Add($"Running in GitHub Actions, but {TestDbConventions.RequiredDatabasesEnvVar} is not set.");
                return problems;
            }

            foreach (var name in names)
            {
                if (!Enum.TryParse<DatabaseType>(name, ignoreCase: true, out var dbType))
                {
                    problems.Add($"{name} in {TestDbConventions.RequiredDatabasesEnvVar} is not a {nameof(DatabaseType)}.");
                    continue;
                }

                var envVar = TestDbConventions.GetConnectionStringEnvVar(dbType);
                if (string.IsNullOrEmpty(getVariable(envVar)))
                    problems.Add($"{dbType} is required, but {envVar} is not set, so its tests would be skipped.");
            }
            return problems;
        }
    }
}
