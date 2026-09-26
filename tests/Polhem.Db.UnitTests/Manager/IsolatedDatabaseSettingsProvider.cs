using Microsoft.Data.SqlClient;
using Polhem.Db.Manager;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Settings;

namespace Polhem.Db.UnitTests.Manager
{
    /// <summary>
    /// A <see cref="DatabaseSettings"/> that belongs to a single test class, for constructing
    /// <c>DbConnectionManagerService</c> directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type exists because of a measured flaky test. <c>DbConnectionManagerTests</c> and
    /// <c>DbAccessFactoryTests</c> both used to call <c>Items.Add/Remove</c> on the <b>cached instance</b> returned by
    /// <c>IDefineAccess.GetDatabaseSettings()</c>, each class with its own <c>IClassFixture</c>, and xUnit ran them in
    /// parallel. <c>KeyCollectionBase&lt;T&gt;</c> is not thread-safe: after <c>RemoveItem</c> read
    /// <c>this[index]</c>, <c>List.RemoveAt(index)</c> hit an index out of range. Running the same tests twice turned
    /// the second run red.
    /// </para>
    /// <para>
    /// The class comment at the time said "use a unique databaseId to avoid interfering with the global cache shared
    /// with other tests". <b>A unique key only avoids key collisions, not races at the collection level</b>.
    /// </para>
    /// <para>
    /// The fix did not serialize the two classes into one <c>[Collection]</c>; it stops touching the cache at all.
    /// <c>.claude/rules/definition.md</c> ("Objects in the cache must not change after init") forbids mutating a
    /// process-wide cached instance after it is loaded, and serializing would only hide the symptoms of the violation.
    /// Besides, the two classes test connection string assembly, which never needed a database.
    /// </para>
    /// </remarks>
    internal sealed class IsolatedDatabaseSettingsProvider : IDatabaseSettingsProvider
    {
        public DatabaseSettings Settings { get; } = new();

        /// <inheritdoc/>
        public DatabaseSettings Get() => Settings;

        /// <inheritdoc/>
        public DatabaseItem GetItem(string databaseId)
        {
            if (string.IsNullOrWhiteSpace(databaseId))
                throw new ArgumentNullException(nameof(databaseId));
            if (Settings.Items == null || !Settings.Items.Contains(databaseId))
                throw new KeyNotFoundException($"DatabaseItem '{databaseId}' not found.");

            return Settings.Items[databaseId];
        }

        /// <inheritdoc/>
        public void ValidateRequired()
        {
            if (Settings.Items == null || !Settings.Items.Contains(DbCategoryIds.Common))
                throw new InvalidOperationException(
                    $"DatabaseSettings must contain a DatabaseItem with Id='{DbCategoryIds.Common}'.");
        }
    }

    /// <summary>
    /// Gives tests without a fixture the provider factory of <see cref="DatabaseType.SQLServer"/>.
    /// </summary>
    /// <remarks>
    /// <c>DbConnectionManagerService.CreateConnectionInfo</c> gets the factory from <see cref="DbProviderRegistry"/>,
    /// a process-wide registry that the fixture normally fills at startup. The registered value is
    /// <c>SqlClientFactory.Instance</c>, the same instance the fixture registers, so registering it again is
    /// idempotent and changes nothing any other test sees. The WARNING on <see cref="DbProviderRegistry"/> itself
    /// states that tests register repeatedly while other tests read, which is why its backing store must be
    /// concurrent.
    /// </remarks>
    internal static class TestDbProviders
    {
        internal static void EnsureSqlServerRegistered()
            => DbProviderRegistry.Register(DatabaseType.SQLServer, SqlClientFactory.Instance);
    }
}
