using Polhem.Api.Core;
using Polhem.Base;
using Polhem.Db;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Storage;
using Polhem.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Polhem.Samples.Shared;

/// <summary>
/// One-line bootstrap shared by the sample hosts (QuickStart.Server and Blazor.Server.Demo).
/// Resolves the shared <c>samples/Define</c> directory, registers SQLite, loads SystemSettings,
/// wires <c>AddPolhemFramework</c>; <c>Define/ProgramSettings.xml</c> binds the reserved "System" progId
/// so sign-in authenticates against <see cref="DemoCredentials"/> rather than against stored
/// credentials.
/// </summary>
/// <remarks>
/// Overriding authentication removes the need for stored credentials, but not the need for the
/// framework tables themselves: <c>Login</c> still reads the user's locale from <c>st_user</c> and
/// persists the session seed to <c>st_session</c>, and the <c>EnterCompany</c> call every client
/// makes next reads <c>st_company</c> and <c>st_user_company</c>. They are therefore defined under
/// <c>Define/</c> and created here — without them the demo authenticates fine and then fails
/// inside session construction.
/// </remarks>
public static class DemoBackend
{
    /// <summary>
    /// Registers Polhem backend services into <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The resolved <see cref="PathOptions"/> so callers can locate Define files later if needed.</returns>
    public static PathOptions AddPolhemBackend(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Demo-only: ensure a master key is available so the bundled demos can run
        // with zero setup. Production hosts MUST set POLHEM_MASTER_KEY via the real
        // deployment mechanism (K8s Secret, env file, Vault, etc.) — see README.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POLHEM_MASTER_KEY")))
        {
            Environment.SetEnvironmentVariable("POLHEM_MASTER_KEY", DemoCredentials.DemoMasterKey);
        }

        // CustomizePath turns on the tenant customization-override layer. It is set the same way
        // DefinePath is — the host computes it and hands both to AddPolhemFramework; the framework has
        // no configuration binding of its own. The directory need not exist: a tenant with no
        // override files resolves every lookup to the base layer, which is what this demo does
        // (its one company names no customization code, so SessionInfo.CustomizeId stays empty
        // after EnterCompany and the overlay short-circuits before it touches the filesystem).
        string definePath = ResolveDefinePath();
        var paths = new PathOptions
        {
            DefinePath = definePath,
            CustomizePath = ResolveCustomizePath(definePath),
        };

        // Framework tables the demo cannot run without. Their TableSchema files under Define/ are
        // the demo's own definitions, committed like the Staff ones, and DemoSchemaSeeder
        // creates them alongside those tables. The embedded defaults in Polhem.Definition are only
        // the starting point those files were first imported from: once a file exists here it is
        // authoritative, and a later change to the defaults is not meant to replace it.
        //
        // That is why this call keeps skip-if-exists. It writes a listed file only when the demo
        // does not have it yet, and leaves every existing file alone; a file it writes shows up
        // untracked, to be reviewed and committed.
        //   st_cache_notify — polled by the cache-notify poller AddPolhemFramework registers.
        //   st_session      — the session seed every successful Login persists.
        //   st_user         — read for the signing-in user's time zone and culture.
        //   st_company, st_user_company — the company and the grant EnterCompany checks.
        //   st_employee, st_role, st_role_grant, st_user_role — read by EnterCompany while it
        //     snapshots the session's record-scope identity and roles; the demo leaves them empty.
        var requiredFrameworkTables = new HashSet<string>(StringComparer.Ordinal)
        {
            "TableSchema/common/st_cache_notify.TableSchema.xml",
            "TableSchema/common/st_session.TableSchema.xml",
            "TableSchema/common/st_user.TableSchema.xml",
            "TableSchema/common/st_company.TableSchema.xml",
            "TableSchema/common/st_user_company.TableSchema.xml",
            "TableSchema/company/st_employee.TableSchema.xml",
            "TableSchema/company/st_role.TableSchema.xml",
            "TableSchema/company/st_role_grant.TableSchema.xml",
            "TableSchema/company/st_user_role.TableSchema.xml",
        };
        Defaults.MaterializeTo(paths.DefinePath, new MaterializeOptions
        {
            Filter = requiredFrameworkTables.Contains
        });

        // SQLite providers — keep dialect registration explicit so the framework does
        // not force every host to pull every ADO.NET driver.
        DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
        DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());

        var settings = SystemSettingsLoader.Load(paths);
        SysInfo.Initialize(settings.CommonConfiguration);
        ApiServiceOptions.Initialize(
            settings.CommonConfiguration.ApiPayloadOptions,
            settings.CommonConfiguration.IsDebugMode);

        builder.Services.AddPolhemFramework(
            settings.BackendConfiguration,
            paths,
            autoCreateMasterKey: true);

        // Nothing to register for the custom login: Define/ProgramSettings.xml binds the reserved
        // progId "System" to DemoAuthenticatingSystemBusinessObject, and the framework resolves it
        // from there like any other progId.

        return paths;
    }

    /// <summary>
    /// After the host is built: runs the schema seeder once.
    /// </summary>
    /// <param name="app">The built web application.</param>
    public static void UsePolhemBackend(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var defineAccess = app.Services.GetRequiredService<IDefineAccess>();
        var connectionManager = app.Services.GetRequiredService<IDbConnectionManager>();
        var dbAccessFactory = app.Services.GetRequiredService<IDbAccessFactory>();
        DemoSchemaSeeder.EnsureSchemaAndSeed(defineAccess, connectionManager, dbAccessFactory);
    }

    private static string ResolveDefinePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Define", "SystemSettings.xml");
            if (File.Exists(candidate))
                return Path.GetDirectoryName(candidate)!;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate 'Define/SystemSettings.xml' walking up from " +
            $"'{AppContext.BaseDirectory}'. Run the sample from inside the polhem checkout.");
    }

    /// <summary>
    /// Places the customization root as a sibling of <c>Define/</c>, mirroring the layout a real
    /// deployment would use: <c>Define/</c> holds the base definitions everyone shares,
    /// <c>Customize/{customizeId}/</c> holds the per-tenant overrides on top of them.
    /// </summary>
    /// <param name="definePath">The resolved base definition directory.</param>
    private static string ResolveCustomizePath(string definePath)
        => Path.Combine(Path.GetDirectoryName(definePath)!, "Customize");
}
