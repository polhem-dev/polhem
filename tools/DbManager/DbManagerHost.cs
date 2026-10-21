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
using Polhem.Hosting;

namespace Polhem.DbManager;

/// <summary>
/// Builds the services the <c>db</c> commands need for a definition folder, the way an application's server does,
/// with the driver of every supported database registered.
/// </summary>
internal static class DbManagerHost
{
    /// <summary>Builds the host for <paramref name="definePath"/>. The host is not started.</summary>
    /// <remarks>
    /// The master key comes from the source SystemSettings names, as for the server. An environment-variable source
    /// with no key gets a key for this process only, which is enough unless DatabaseSettings holds encrypted
    /// passwords; a file source is never created here, so a missing key file fails instead of leaving a new key in
    /// the project.
    /// </remarks>
    public static IHost Build(string definePath)
    {
        RegisterProviders();

        var paths = new PathOptions { DefinePath = definePath };
        var settings = SystemSettingsLoader.Load(paths);
        SysInfo.Initialize(settings.CommonConfiguration);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Logging.AddSimpleConsole(options => options.SingleLine = true).SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddPolhemPayload(settings.CommonConfiguration.ApiPayloadOptions, settings.CommonConfiguration.IsDebugMode);
        var autoCreateMasterKey =
            settings.BackendConfiguration.SecurityKeySettings.MasterKeySource.Type == MasterKeySourceType.Environment;
        builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths, autoCreateMasterKey);
        return builder.Build();
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
