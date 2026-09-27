namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Per-class fixture that opts into <see cref="PolhemTestFixtureBuilder.UseSharedDatabases"/>:
    /// drives <see cref="SharedDatabaseState.EnsureSchemaAndSeed"/> once-per-process for every
    /// database type whose <c>POLHEM_TEST_CONNSTR_*</c> env var is set, ensuring the
    /// <c>st_user</c> / <c>st_session</c> schemas + seed user exist before any <c>[DbFact]</c>
    /// / <c>[DbTheory]</c> in the consuming class runs.
    /// </summary>
    /// <remarks>
    /// Use as <c>IClassFixture&lt;SharedDbFixture&gt;</c> for any test class whose tests touch
    /// the shared databases (e.g. via <c>SystemApiConnector.CreateSession</c>), and gate each such test with
    /// <see cref="DbFactAttribute"/> so it skips where that database is not configured.
    /// </remarks>
    public sealed class SharedDbFixture : PolhemTestFixture
    {
        /// <summary>
        /// Creates a fixture pointing at the shared <c>tests/Define</c> directory with
        /// <see cref="PolhemTestFixtureBuilder.UseSharedDatabases"/> enabled.
        /// </summary>
        public SharedDbFixture() : base(b => b.UseSharedDatabases()) { }
    }
}
