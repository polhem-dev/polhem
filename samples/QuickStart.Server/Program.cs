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
