using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polhem.Core;
using Polhem.Db.Manager;
using Polhem.Db.Providers.MySql;
using Polhem.Db.Providers.Oracle;
using Polhem.Db.Providers.PostgreSql;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Providers.SqlServer;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Security;
using Polhem.Definition.Settings;
using Polhem.Hosting;

namespace Polhem.DbManager;

/// <summary>
/// Builds the services the <c>db</c> commands need for a definition folder, the way an application's server does,
/// with the driver of every supported database registered.
/// </summary>
internal static class DbManagerHost
{
    // The variable Polhem.Definition's MasterKeyProvider reads when the source names none; it is private there.
    private const string DefaultMasterKeyVariable = "POLHEM_MASTER_KEY";

    // The file MasterKeyProvider reads when a file source names none.
    private const string DefaultMasterKeyFile = "Master.key";

    /// <summary>Builds the host for <paramref name="definePath"/>. The host is not started.</summary>
    /// <remarks>
    /// The master key comes from the source SystemSettings names, as for the server. When an environment-variable
    /// source has no key and SystemSettings holds no encrypted key, a key for this process only is enough, since
    /// nothing has to be decrypted; with an encrypted key the variable is required. A missing key file is created only
    /// when <paramref name="createKeyFile"/> is set (the <c>create</c> command, which sets up a new deployment), the
    /// way the framework creates one (<c>MasterKeyProvider</c>: a new key in an owner-only file); for the other
    /// commands it fails, so a mistyped path does not leave a second key behind.
    /// </remarks>
    /// <param name="definePath">The definition folder.</param>
    /// <param name="createKeyFile">Whether a missing master key file may be created.</param>
    /// <param name="createdKeyFile">The key file this call created, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The master key is needed and cannot be found.</exception>
    public static IHost Build(string definePath, bool createKeyFile, out string? createdKeyFile)
    {
        RegisterProviders();

        var paths = new PathOptions { DefinePath = definePath };
        var settings = SystemSettingsLoader.Load(paths);
        SysInfo.Initialize(settings.CommonConfiguration);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Logging.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddPolhemPayload(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);
        var keys = settings.BackendConfiguration.SecurityKeySettings;
        var keyFile = KeyFilePath(keys.MasterKeySource, definePath);
        var createFile = createKeyFile && keyFile is not null && !File.Exists(keyFile);
        builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths,
            createFile || AllowProcessMasterKey(keys));
        createdKeyFile = createFile && File.Exists(keyFile) ? keyFile : null;
        return builder.Build();
    }

    /// <summary>
    /// The master key file a file source names, resolved as <c>MasterKeyProvider</c> resolves it; <see langword="null"/>
    /// for another source.
    /// </summary>
    internal static string? KeyFilePath(MasterKeySource source, string definePath)
    {
        if (source.Type != MasterKeySourceType.File) return null;
        var path = string.IsNullOrWhiteSpace(source.Value) ? DefaultMasterKeyFile : source.Value;
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(definePath, path));
    }

    private static bool AllowProcessMasterKey(SecurityKeySettings keys)
    {
        var source = keys.MasterKeySource;
        // An empty value means the framework's default variable (MasterKeyProvider).
        var variable = string.IsNullOrWhiteSpace(source.Value) ? DefaultMasterKeyVariable : source.Value;
        if (source.Type != MasterKeySourceType.Environment
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
        {
            return false;
        }
        // The keys decrypted with the master key when the framework starts.
        if (!string.IsNullOrEmpty(keys.ApiEncryptionKey) || !string.IsNullOrEmpty(keys.ConfigEncryptionKey))
        {
            throw new InvalidOperationException(
                $"The master key environment variable '{variable}' is not set, and SystemSettings holds encrypted keys "
                + "that need it. Set it to the key the server uses.");
        }
        return true;
    }

    private static void RegisterProviders()
    {
        DbProviderRegistry.Register(DatabaseType.SQLServer, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        DbDialectRegistry.Register(DatabaseType.SQLServer, new SqlDialectFactory());
        DbProviderRegistry.Register(DatabaseType.PostgreSQL, Npgsql.NpgsqlFactory.Instance);
        DbDialectRegistry.Register(DatabaseType.PostgreSQL, new PgDialectFactory());
        DbProviderRegistry.Register(DatabaseType.MySQL, MySqlConnector.MySqlConnectorFactory.Instance);
        DbDialectRegistry.Register(DatabaseType.MySQL, new MySqlDialectFactory());
        DbProviderRegistry.Register(DatabaseType.Oracle, Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);
        DbDialectRegistry.Register(DatabaseType.Oracle, new OracleDialectFactory());
        DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
        DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());
    }
}
