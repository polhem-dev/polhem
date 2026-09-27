using Polhem.Db;
using Polhem.Definition;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Tests.Shared
{
    /// <summary>
    /// Per-class test fixture (xUnit <see cref="IClassFixture{TFixture}"/> compatible).
    /// Each instance owns its own <see cref="IServiceProvider"/> built via
    /// <c>AddPolhemFramework</c>. The remaining process-wide setup (the shared define directory copied from
    /// <c>tests/Define</c>, <c>SysInfo</c>, the DB provider registry and the master key environment variable) runs
    /// once through <see cref="TestProcessBootstrap.EnsureInitialized"/> on the first construction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default usage (read-only access to <c>tests/Define/</c>):
    /// <code>
    /// public class MyTests : IClassFixture&lt;PolhemTestFixture&gt;
    /// {
    ///     private readonly PolhemTestFixture _fx;
    ///     public MyTests(PolhemTestFixture fx) { _fx = fx; }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// For per-class write isolation (e.g. tests calling <c>SaveSystemSettings</c>),
    /// declare a subclass that configures via <see cref="PolhemTestFixtureBuilder"/>:
    /// <code>
    /// public class WritableDefineFixture : PolhemTestFixture
    /// {
    ///     public WritableDefineFixture() : base(b =&gt; b.UseTempDefinePath()) {}
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public class PolhemTestFixture : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string? _tempDir;

        /// <summary>
        /// Gets the per-fixture service provider.
        /// </summary>
        public IServiceProvider Provider => _provider;

        /// <summary>
        /// Gets the <see cref="PathOptions"/> instance this fixture was built with.
        /// </summary>
        public PathOptions PathOptions { get; }

        /// <summary>
        /// Gets the resolved <see cref="PathOptions.DefinePath"/>.
        /// </summary>
        public string DefinePath => PathOptions.DefinePath;

        /// <summary>
        /// Creates a fixture pointing at the shared <c>tests/Define</c> directory.
        /// xUnit's <see cref="IClassFixture{TFixture}"/> calls this parameterless ctor.
        /// </summary>
        public PolhemTestFixture() : this(_ => { }) { }

        /// <summary>
        /// Creates a fixture customised via the supplied <paramref name="configure"/> callback.
        /// Subclass via <c>public MyFixture() : base(b =&gt; b.UseTempDefinePath())</c> to
        /// expose a configured fixture to xUnit through <c>IClassFixture&lt;MyFixture&gt;</c>.
        /// </summary>
        /// <param name="configure">Builder callback.</param>
        protected PolhemTestFixture(Action<PolhemTestFixtureBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            // One-time initialization of the process-wide statics (`SysInfo`, the DB provider registry,
            // `TestProcessBootstrap.LocalServices`).
            TestProcessBootstrap.EnsureInitialized();

            var builder = new PolhemTestFixtureBuilder();
            configure(builder);

            PathOptions = builder.BuildPathOptions(out _tempDir);
            _provider = builder.BuildServiceProvider(PathOptions);
        }

        /// <summary>
        /// Resolves a required service from the fixture's provider.
        /// </summary>
        public T GetRequiredService<T>() where T : notnull
            => _provider.GetRequiredService<T>();

        /// <summary>
        /// Resolves an optional service; returns <c>null</c> when not registered.
        /// </summary>
        public T? GetService<T>() where T : class
            => _provider.GetService<T>();

        /// <summary>
        /// Convenience: creates a <see cref="DbAccess"/> bound to <paramref name="databaseId"/>
        /// via the fixture's <see cref="IDbAccessFactory"/>. Use this instead of
        /// <c>new DbAccess(id)</c> in tests so the connection manager comes from the
        /// fixture-scoped DI provider rather than any process-wide static.
        /// </summary>
        public DbAccess NewDbAccess(string databaseId)
            => _provider.GetRequiredService<IDbAccessFactory>().Create(databaseId);

        /// <summary>
        /// Disposes the per-fixture provider and best-effort deletes the temp <c>DefinePath</c>
        /// directory (when <see cref="PolhemTestFixtureBuilder.UseTempDefinePath"/> was used).
        /// </summary>
        public void Dispose()
        {
            _provider.Dispose();
            if (_tempDir != null && Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
                catch (IOException)
                {
                    // Test results matter more than temp cleanup; an occasional file lock must not fail a test.
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
