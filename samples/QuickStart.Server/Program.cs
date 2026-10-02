using Polhem.Hosting;
using Polhem.JsonRpc.AspNetCore;
using Polhem.Samples.Shared;

namespace QuickStart.Server;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Polhem backend (in-process JSON-RPC dispatch) — shared with the Blazor demos.
        // DemoBackend handles PathOptions, SQLite registration and AddPolhemFramework. Which
        // business object serves each progId comes from samples/Define/ProgramSettings.xml, read by
        // the framework's resolver: "System" is DemoAuthenticatingSystemBusinessObject, so demo/demo
        // Login works without stored credentials, and "Echo" is this sample's EchoBusinessObject.
        // The common system tables are still created and seeded — overriding the credential check
        // does not remove the rest of the login path.
        builder.AddPolhemBackend();

        // CORS for browser clients (for example a polhem-connector-js app) calling the JSON-RPC endpoint cross-origin.
        // Demo-only permissive policy — production hosts must restrict origins explicitly.
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
        });

        // The JSON-RPC endpoint, on the options AddPolhemFramework registered, and the startup log while no API key
        // has been issued. The check runs when the host starts, after UsePolhemBackend has seeded st_api_key.
        builder.Services.AddJsonRpcServer();
        builder.Services.AddPolhemApiKeyGateCheck();

        var app = builder.Build();
        app.UsePolhemBackend();
        app.UseCors();
        app.MapJsonRpc("/api");
        app.Run();
    }
}
