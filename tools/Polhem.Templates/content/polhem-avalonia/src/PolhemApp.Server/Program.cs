#if (database == "SqlServer")
using Microsoft.Data.SqlClient;
using Polhem.Db.Providers.SqlServer;
#elif (database == "PostgreSql")
using Npgsql;
using Polhem.Db.Providers.PostgreSql;
#elif (database == "MySql")
using MySqlConnector;
using Polhem.Db.Providers.MySql;
#elif (database == "Oracle")
using Oracle.ManagedDataAccess.Client;
using Polhem.Db.Providers.Oracle;
#else
using Microsoft.Data.Sqlite;
using Polhem.Db.Providers.Sqlite;
#endif
using Polhem.Db.Manager;
using Polhem.Definition.Database;
using Polhem.Hosting;
using Polhem.Hosting.Database;
using Polhem.JsonRpc.AspNetCore;

#if (database == "SqlServer")
DbProviderRegistry.Register(DatabaseType.SQLServer, SqlClientFactory.Instance);
DbDialectRegistry.Register(DatabaseType.SQLServer, new SqlDialectFactory());
#elif (database == "PostgreSql")
DbProviderRegistry.Register(DatabaseType.PostgreSQL, NpgsqlFactory.Instance);
DbDialectRegistry.Register(DatabaseType.PostgreSQL, new PgDialectFactory());
#elif (database == "MySql")
DbProviderRegistry.Register(DatabaseType.MySQL, MySqlConnectorFactory.Instance);
DbDialectRegistry.Register(DatabaseType.MySQL, new MySqlDialectFactory());
#elif (database == "Oracle")
DbProviderRegistry.Register(DatabaseType.Oracle, OracleClientFactory.Instance);
DbDialectRegistry.Register(DatabaseType.Oracle, new OracleDialectFactory());
#else
DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());
#endif

var builder = WebApplication.CreateBuilder(args);
// Finds the Define folder above the build output, adds the framework's default definitions it does not have yet (the
// settings files, the system tables and the Department, Employee and AuditRule forms), reads SystemSettings and
// registers the framework. AutoCreateMasterKey creates Define/Master.key on the first run (SystemSettings names it as
// the master key source).
#if (database == "Sqlite")
var paths = builder.AddPolhemBackend(options => options.AutoCreateMasterKey = true);

// The SQLite paths in DatabaseSettings.xml are relative; resolve them against the folder above Define, whichever
// way the server was started.
Directory.SetCurrentDirectory(Path.GetDirectoryName(paths.DefinePath)!);
#else
builder.AddPolhemBackend(options => options.AutoCreateMasterKey = true);
#endif
builder.Services.AddJsonRpcServer();
builder.Services.AddPolhemApiKeyGateCheck();
#if (browser)

// Development only: the browser head is served by its own development server on another localhost port, so its calls
// to this server are cross-origin. In production, serve the published browser head from this server's origin and
// remove the policy.
const string DevelopmentCorsPolicy = "Development";
builder.Services.AddCors(options => options.AddPolicy(DevelopmentCorsPolicy, policy => policy
    .SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Host is "localhost" or "127.0.0.1")
    .AllowAnyHeader()
    .AllowAnyMethod()));
#endif

var app = builder.Build();

// `dotnet run -- db create | check | upgrade | seed-admin` runs a database command and exits without starting the server.
if (DatabaseCommandLine.TryRun(app.Services, args) is int exitCode) return exitCode;

#if (browser)
if (app.Environment.IsDevelopment()) app.UseCors(DevelopmentCorsPolicy);
#endif
app.MapJsonRpc("/api");
app.Run();
return 0;
