using Polhem.Business;
using Polhem.Samples.Shared;
using QuickStart.Server.BusinessObjects;

namespace QuickStart.Server;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Polhem backend (in-process JSON-RPC dispatch) — shared with the Blazor demos.
        // DemoBackend handles PathOptions, SQLite registration, AddPolhemFramework, and
        // swaps in DemoAuthenticatingSystemBusinessObject so demo/demo Login works without
        // stored credentials. The common system tables are still created and seeded — overriding
        // the credential check does not remove the rest of the login path.
        builder.AddPolhemBackend();

        // Override the default resolver so progId "Echo" dispatches to the sample's
        // EchoBusinessObject. Order matters: this AddSingleton runs after
        // AddPolhemFramework's DefaultBoTypeResolver, so the last registration wins
        // when the container resolves IBoTypeResolver.
        builder.Services.AddSingleton<IBoTypeResolver, QuickStartBoTypeResolver>();

        // CORS for the Web.Js.Demo sample (cross-origin JS calling the JSON-RPC endpoint).
        // Demo-only permissive policy — production hosts must restrict origins explicitly.
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
        });

        builder.Services.AddControllers();

        var app = builder.Build();
        app.UsePolhemBackend();
        app.UseCors();
        app.MapControllers();
        app.Run();
    }
}
