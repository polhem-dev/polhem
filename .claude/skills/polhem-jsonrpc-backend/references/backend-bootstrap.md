# Backend bootstrap template

`Program.cs` + `XxxBackend`. Replace `Xxx` with your project name. Based on
`samples/Polhem.Samples.Shared/DemoBackend.cs` and `samples/QuickStart.Server/Program.cs` (same version as the
framework); when this template and those files disagree, the samples win.

## Program.cs

```csharp
using Polhem.Hosting;               // AddPolhemApiKeyGateCheck
using Polhem.JsonRpc.AspNetCore;    // AddJsonRpcServer, MapJsonRpc
using Xxx.Server;

var builder = WebApplication.CreateBuilder(args);
builder.AddXxxBackend();
builder.Services.AddJsonRpcServer();
// Logs while no API key has been issued. It runs when the host starts, after UseXxxBackend created the tables.
builder.Services.AddPolhemApiKeyGateCheck();

var app = builder.Build();
app.UseXxxBackend();
app.MapJsonRpc("/api");
app.Run();
```

## XxxBackend.cs

```csharp
using Polhem.Core;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
using Polhem.Db.Schema;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Hosting;
using Microsoft.Data.Sqlite;

namespace Xxx.Server;

public static class XxxBackend
{
    // Dev-only fixed master key: a fresh clone runs with zero setup and encrypted rows stay
    // decryptable across runs. Production MUST inject a real POLHEM_MASTER_KEY before this runs.
    private const string DevMasterKey = "<base64-64-byte-aes-cbc-hmac-key>";
    private const string CommonDatabaseId = "common";

    public static PathOptions AddXxxBackend(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POLHEM_MASTER_KEY")))
        {
            Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", DevMasterKey);
        }

        var paths = new PathOptions { DefinePath = ResolveDefinePath() };

        // Framework tables the host cannot run without:
        //   st_cache_notify — polled by the cache-notify poller AddPolhemFramework registers.
        //   st_session      — the session every successful Login persists.
        //   st_user         — read on Login for the credential check (by default) and the user's locale.
        // The framework's embedded defaults are only a starting point for the first import: skip-if-exists writes a
        // file only when it is missing. Commit the written files; from then on the app's copies are authoritative
        // (later changes to the defaults do not overwrite them, and a difference is not drift). Do not switch to
        // Overwrite, and do not add them to .gitignore.
        var requiredFrameworkTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "TableSchema/common/st_cache_notify.TableSchema.xml",
            "TableSchema/common/st_session.TableSchema.xml",
            "TableSchema/common/st_user.TableSchema.xml",
        };
        Defaults.MaterializeTo(paths.DefinePath, new MaterializeOptions
        {
            Filter = requiredFrameworkTables.Contains,
        });

        DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
        DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());

        var settings = SystemSettingsLoader.Load(paths);
        SysInfo.Initialize(settings.CommonConfiguration);
        builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths, autoCreateMasterKey: true);

        // Applies the compressor and encryptor named in SystemSettings.xml. The body codec is not configured here:
        // each request declares it (adr-044).
        builder.Services.AddPolhemPayload(
            settings.CommonConfiguration.ApiPayloadOptions,
            settings.CommonConfiguration.IsDebugMode);

        // Nothing else to register: Define/ProgramSettings.xml binds each progId (the reserved "System" included)
        // to its business object, and the framework's resolver reads it.

        return paths;
    }

    /// <summary>After the host is built: create the framework tables once (run a real seeder here if you have one).</summary>
    public static void UseXxxBackend(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var defineAccess = app.Services.GetRequiredService<IDefineAccess>();
        var connectionManager = app.Services.GetRequiredService<IDbConnectionManager>();

        var builder = new TableSchemaBuilder(CommonDatabaseId, defineAccess, connectionManager);
        builder.Execute(CommonDatabaseId, "st_cache_notify");
        builder.Execute(CommonDatabaseId, "st_session");
        builder.Execute(CommonDatabaseId, "st_user");
        // Seed st_user here, or override the credential check (business-object.md).
        // Later, call builder.Execute(categoryId, table) + seed here for each business table.
    }

    private static string ResolveDefinePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Define", "SystemSettings.xml");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"Could not locate 'Define/SystemSettings.xml' walking up from '{AppContext.BaseDirectory}'.");
    }
}
```

`samples/Polhem.Samples.Shared/DemoSchemaSeeder.cs` shows the table creation and a seeded `st_user` row together.

## Generating a dev master key

A 64-byte AES-CBC-HMAC combined key (base64). You can borrow the dev value from
`Polhem.Samples.Shared.DemoCredentials`, or generate one with `RandomNumberGenerator.GetBytes(64)` and base64-encode
it. **Dev only**; production injects the key through the deployment mechanism.

## CORS (only when a WASM/browser head calls cross-origin)

`app.UseCors(...)` must come **before** the controllers are mapped so the OPTIONS preflight is answered first instead
of being blocked by access control. `samples/QuickStart.Server/Program.cs` shows the placement. Pure desktop / mobile
heads do not need it.
