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
using Polhem.Web.Blazor.Server.DependencyInjection;
using Polhem.Web.Blazor.Server.Shell;
using PolhemApp.Web.Components;

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

// The pages call the backend in this process. WARNING: such calls are trusted local calls. The forms still check the
// signed-in user's permissions, but the system calls that are restricted for remote clients (reading any definition,
// starting a session without credentials, managing API keys) are not restricted for them. A page of your own must not
// pass what a browser sends into those calls.
builder.Services.AddPolhemBlazor(options => options.UseLocalProvider());
builder.Services.AddPolhemShell(shell =>
{
    shell.AppTitle = "PolhemApp";
    shell.DefaultCompanyId = "POLHEM_COMPANY_ID";
    shell.DefaultUserId = "admin";
    // The language of the sign-in screen until the viewer picks another one.
    shell.DefaultCulture = "POLHEM_DEFAULT_LANGUAGE";
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// `dotnet run -- db create | check | upgrade | seed-admin` runs a database command and exits without starting the site.
if (DatabaseCommandLine.TryRun(app.Services, args) is int exitCode) return exitCode;

app.UsePolhemShellLocalization();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapPolhemShellCulture();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
return 0;
