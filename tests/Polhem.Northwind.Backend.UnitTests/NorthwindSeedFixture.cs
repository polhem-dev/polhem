using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition.Database;
using Polhem.Hosting.DependencyInjection;
using Polhem.Tests.Shared;

namespace Polhem.Northwind.Backend.UnitTests
{
    /// <summary>
    /// A Northwind backend over a copy of the demo's <c>Define</c> folder and a SQLite file of its own, built once for
    /// the test class.
    /// </summary>
    /// <remarks>
    /// One host per class rather than per test: the definition and connection caches are process-wide, so a second host
    /// over another folder in the same process would still open the first one's database.
    /// </remarks>
    public sealed class NorthwindSeedFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"polhem-northwind-seed-{Guid.NewGuid():N}");
        private readonly IHost _host;

        public NorthwindSeedFixture()
        {
            var define = Path.Combine(_root, "Define");
            CopyDefine(Path.Combine(RepoRoot.Find(), "apps", "Polhem.Northwind", "Define"), define);

            DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
            DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());

            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
            builder.AddPolhemBackend(options =>
            {
                options.DefinePath = define;
                options.AutoCreateMasterKey = true;
            });
            _host = builder.Build();
        }

        /// <summary>Gets the backend's services.</summary>
        public IServiceProvider Services => _host.Services;

        public void Dispose()
        {
            _host.Dispose();
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file the runtime still holds is left for the temp cleaner; the result does not depend on it.
                Trace.TraceWarning($"Could not delete {_root}: {ex.Message}");
            }
        }

        // The demo's databases name |DataDirectory|, a process-wide setting; the copy names its own file instead.
        private void CopyDefine(string source, string target)
        {
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }

            var settings = Path.Combine(target, "DatabaseSettings.xml");
            File.WriteAllText(settings, File.ReadAllText(settings).Replace(
                "|DataDirectory|northwind.db", Path.Combine(_root, "northwind.db"), StringComparison.Ordinal));
        }
    }
}
