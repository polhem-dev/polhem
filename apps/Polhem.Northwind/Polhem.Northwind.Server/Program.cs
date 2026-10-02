using Polhem.Hosting;
using Polhem.JsonRpc.AspNetCore;
using Polhem.Northwind.Server;

const string DevWasmCorsPolicy = "PolhemDevWasm";

var builder = WebApplication.CreateBuilder(args);

// Polhem backend (in-process JSON-RPC dispatch). AddNorthwindBackend handles PathOptions,
// SQLite registration and AddPolhemFramework, and registers nothing beyond it. The demo accounts
// sign in through the framework's own st_user check, then enter the seeded company through
// EnterCompany.
builder.AddNorthwindBackend();

// The JSON-RPC endpoint, on the options AddPolhemFramework registered, and the startup log while no API key has
// been issued. The check runs when the host starts, after UseNorthwindBackend has seeded st_api_key.
builder.Services.AddJsonRpcServer();
builder.Services.AddPolhemApiKeyGateCheck();

// Dev-only CORS so the Avalonia WASM head (served by its own dev server on a different
// localhost port) can call this JSON-RPC API cross-origin. Production should serve the WASM
// same-origin from this host and drop the policy entirely (see Polhem.Northwind.Browser/README).
builder.Services.AddCors(options =>
    options.AddPolicy(DevWasmCorsPolicy, policy => policy
        .SetIsOriginAllowed(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && uri.Host is "localhost" or "127.0.0.1")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// WARNING: Must run before UseNorthwindBackend so the CORS preflight (OPTIONS) is answered
// before any API access-control middleware can reject the unauthenticated probe.
if (app.Environment.IsDevelopment())
    app.UseCors(DevWasmCorsPolicy);

app.UseNorthwindBackend();
app.MapJsonRpc("/api");
app.Run();
