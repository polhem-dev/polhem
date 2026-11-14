using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Polhem.Core.Data;
using Polhem.Core.Serialization;
using Polhem.Db.Manager;
using Polhem.Db.Providers.Sqlite;
using Polhem.Definition;
using Polhem.Definition.Database;
using Polhem.Definition.Forms;
using Polhem.Definition.Settings;
using Polhem.Hosting.Database;
using Polhem.Hosting.DependencyInjection;
using Polhem.JsonRpc.AspNetCore;
using Polhem.Tests.Shared;

namespace Polhem.Web.Blazor.Wasm.BrowserTests
{
    /// <summary>
    /// Publishes <c>Polhem.Web.Blazor.Wasm.WireApp</c>, serves it beside a JSON-RPC backend on a SQLite database of its
    /// own, and starts a headless Chrome.
    /// </summary>
    /// <remarks>
    /// The backend is built the way an application's server is: <c>AddPolhemBackend</c> over a definition directory,
    /// the tables built by <see cref="IDatabaseInitializer.UpgradeTables"/> and the account and company created by
    /// <see cref="IDatabaseInitializer.SeedAdministrator"/>. It needs no database container, so it runs wherever Chrome
    /// does. The page is served from the same origin as the endpoint, as a deployment serves it.
    /// </remarks>
    public sealed class WireSiteFixture : IAsyncLifetime
    {
        private static readonly string[] s_categories = [DbCategoryIds.Common, DbCategoryIds.Company, DbCategoryIds.Log];

        private readonly string _root = Path.Combine(Path.GetTempPath(), $"polhem-wasm-wire-{Guid.NewGuid():N}");
        private WebApplication? _app;
        private IPlaywright? _playwright;

        /// <summary>Gets the address of the site.</summary>
        public string Address { get; private set; } = string.Empty;

        /// <summary>Gets the browser.</summary>
        public IBrowser Browser { get; private set; } = default!;

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            var site = Path.Combine(_root, "site");
            var define = Path.Combine(_root, "Define");
            Directory.CreateDirectory(_root);
            await PublishAsync(site).ConfigureAwait(false);
            WriteDefinitions(define, Path.Combine(_root, "wire.db"));

            DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
            DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = _root,
                WebRootPath = Path.Combine(site, "wwwroot"),
                EnvironmentName = Environments.Production,
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.AddPolhemBackend(options =>
            {
                options.DefinePath = define;
                options.AutoCreateMasterKey = true;
            });
            builder.Services.AddJsonRpcServer();
            _app = builder.Build();

            Seed(_app.Services.GetRequiredService<IDatabaseInitializer>());

            _app.UseStaticFiles(new StaticFileOptions { ServeUnknownFileTypes = true });
            _app.MapJsonRpc("/api");
            _app.MapFallbackToFile("index.html");
            await _app.StartAsync().ConfigureAwait(false);
            Address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "chrome", Headless = true })
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            if (Browser is not null) await Browser.DisposeAsync().ConfigureAwait(false);
            _playwright?.Dispose();
            if (_app is not null) await _app.DisposeAsync().ConfigureAwait(false);
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(_root, recursive: true); }
            catch (IOException) { /* best effort: a file the runtime still holds is left for the temp cleaner. */ }
        }

        // Only a publish trims, and trimming is where a browser build differs from the desktop the most. The
        // configuration follows this test assembly's, so a Release test run publishes the Release build it just made.
        private static async Task PublishAsync(string output)
        {
            var configuration = typeof(WireSiteFixture).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Release";
            var project = Path.Combine(RepoRoot.Find(), "tests", "Polhem.Web.Blazor.Wasm.WireApp", "Polhem.Web.Blazor.Wasm.WireApp.csproj");
            var start = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[] { "publish", project, "--configuration", configuration, "--output", output, "--nologo" })
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Publishing the wire test page failed with exit code {process.ExitCode}.\n{await stdout.ConfigureAwait(false)}\n{await stderr.ConfigureAwait(false)}");
            }
        }

        private static void WriteDefinitions(string define, string database)
        {
            Defaults.MaterializeTo(define);
            File.WriteAllText(Path.Combine(define, "SystemSettings.xml"), """
                <?xml version="1.0" encoding="utf-8"?>
                <SystemSettings>
                  <CommonConfiguration>
                    <Version>1.0.0</Version>
                    <ApiPayloadOptions>
                      <Compressor>gzip</Compressor>
                      <Encryptor>aes-cbc-hmac</Encryptor>
                    </ApiPayloadOptions>
                  </CommonConfiguration>
                  <BackendConfiguration>
                    <SecurityKeySettings>
                      <MasterKeySource>
                        <Type>File</Type>
                        <Value>master.key</Value>
                      </MasterKeySource>
                    </SecurityKeySettings>
                  </BackendConfiguration>
                  <FrontendConfiguration />
                  <WebsiteConfiguration />
                  <BackgroundServiceConfiguration />
                </SystemSettings>
                """);

            var connection = new SqliteConnectionStringBuilder { DataSource = database, Cache = SqliteCacheMode.Shared }.ConnectionString;
            var items = string.Concat(s_categories.Select(category =>
                $"""<DatabaseItem Id="{category}" CategoryId="{category}" DatabaseType="SQLite" ConnectionString="{System.Security.SecurityElement.Escape(connection)}" />"""));
            File.WriteAllText(Path.Combine(define, "DatabaseSettings.xml"),
                $"""<?xml version="1.0" encoding="utf-8"?><DatabaseSettings><Items>{items}</Items></DatabaseSettings>""");

            var schema = CreateFormSchema();
            Directory.CreateDirectory(Path.Combine(define, "FormSchema"));
            XmlCodec.SerializeToFile(schema, Path.Combine(define, "FormSchema", $"{WireSample.ProgId}.FormSchema.xml"));

            var categoriesPath = Path.Combine(define, "DbCategorySettings.xml");
            var categories = XmlCodec.DeserializeFromFile<DbCategorySettings>(categoriesPath)!;
            var company = categories.Categories![DbCategoryIds.Company];
            foreach (var table in schema.Tables!)
            {
                var tableSchema = TableSchemaGenerator.Generate(table);
                XmlCodec.SerializeToFile(tableSchema,
                    Path.Combine(define, "TableSchema", DbCategoryIds.Company, $"{tableSchema.TableName}.TableSchema.xml"));
                company.Tables!.Add(new TableItem { TableName = tableSchema.TableName, DisplayName = table.DisplayName });
            }
            XmlCodec.SerializeToFile(categories, categoriesPath);
        }

        private static FormSchema CreateFormSchema()
        {
            var schema = new FormSchema(WireSample.ProgId, "Wire sample") { CategoryId = DbCategoryIds.Company };
            var master = schema.Tables!.Add(WireSample.ProgId, "Wire sample");
            master.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            master.Fields.Add(SysFields.Id, "Code", FieldDbType.String).MaxLength = 40;
            master.Fields.Add(SysFields.Name, "Name", FieldDbType.String).MaxLength = 50;
            master.Fields.Add(WireSample.AutoIncrementField, "Sequence", FieldDbType.AutoIncrement);
            foreach (var (name, dbType) in WireSample.Fields)
            {
                var field = master.Fields.Add(name, name, dbType);
                if (dbType == FieldDbType.String) field.MaxLength = 50;
            }

            var lines = schema.Tables.Add(WireSample.LineTable, "Wire sample lines");
            lines.Fields!.Add(SysFields.RowId, "Row ID", FieldDbType.Guid);
            lines.Fields.Add(SysFields.MasterRowId, "Master", FieldDbType.Guid);
            foreach (var (name, dbType) in WireSample.LineFields)
            {
                var field = lines.Fields.Add(name, name, dbType);
                if (dbType == FieldDbType.String) field.MaxLength = 20;
            }
            return schema;
        }

        private static void Seed(IDatabaseInitializer initializer)
        {
            var report = initializer.UpgradeTables();
            if (!report.Succeeded)
            {
                var failed = report.Tables.Where(t => t.Outcome == TableUpgradeOutcome.Failed).Select(t => $"{t.TableName}: {t.Error}");
                throw new InvalidOperationException($"Building the wire test tables failed: {string.Join("; ", report.Problems.Concat(failed))}");
            }
            initializer.SeedAdministrator(new AdministratorSeed
            {
                UserId = WireSample.UserId,
                Password = WireSample.Password,
                CompanyId = WireSample.CompanyId,
                DeploymentAdmin = false,
            });
        }
    }
}
