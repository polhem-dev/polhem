# Backend bootstrap template

`Program.cs` + `XxxBackend`. Replace `Xxx` with your project name. Based on
`samples/Polhem.Samples.Shared/DemoBackend.cs` (same version as the framework) and verified on a real project's server.

## Program.cs

```csharp
using Xxx.Server;

var builder = WebApplication.CreateBuilder(args);
builder.AddXxxBackend();
builder.Services.AddControllers();

var app = builder.Build();
app.UseXxxBackend();
app.MapControllers();
app.Run();
```

## XxxBackend.cs

```csharp
using Polhem.Api.Core;
using Polhem.Base;
using Polhem.Business;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
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

        // The cache-notify poller registered by AddPolhemFramework reads st_cache_notify, so its TableSchema must
        // exist under Define/. The framework's embedded default is only a starting point for the first import:
        // skip-if-exists writes it out only when the file is missing. Commit the written file; from then on the
        // app's copy is authoritative (later changes to the default do not overwrite it, and a difference between
        // the two is not drift). Do not switch to Overwrite, and do not add it to .gitignore.
        Defaults.MaterializeTo(paths.DefinePath, new MaterializeOptions
        {
            Filter = rel => rel == "TableSchema/common/st_cache_notify.TableSchema.xml",
        });

        DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
        DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());

        var settings = SystemSettingsLoader.Load(paths);
        SysInfo.Initialize(settings.CommonConfiguration);
        ApiServiceOptions.Initialize(
            settings.CommonConfiguration.ApiPayloadOptions,
            settings.CommonConfiguration.IsDebugMode);

        builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths, autoCreateMasterKey: true);

        // Register after AddPolhemFramework, last-wins: the factory routes System.Login to demo authentication;
        // the resolver binds progId→BO.
        builder.Services.AddSingleton<IFormBoTypeResolver, BusinessObjects.XxxFormBoTypeResolver>();
        builder.Services.AddSingleton<IBusinessObjectFactory, BusinessObjects.XxxBusinessObjectFactory>();

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

## Generating a dev master key

A 64-byte AES-CBC-HMAC combined key (base64). You can borrow a dev value from the framework's sample credentials, or
generate one with `RandomNumberGenerator.GetBytes(64)` and base64-encode it. **Dev only**; production injects the key
through the deployment mechanism.

## CORS (only when a WASM/browser head calls cross-origin)

`app.UseCors(...)` must come **before** `UseXxxBackend()` so the OPTIONS preflight is answered first instead of being
blocked by access control. Pure desktop / mobile heads do not need it.
