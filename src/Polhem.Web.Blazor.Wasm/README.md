# Polhem.Web.Blazor.Wasm

> The Blazor WebAssembly host of Polhem: the remote client and the shell's language switch. The controls come from a UI package.

[繁體中文](README.zh-TW.md)

## Architecture Position

- **Layer**: Web Frontend, host package
- **Hosting model**: Blazor WebAssembly. The components run in the browser and call a Polhem API server over HTTP.
- **Position in the dependency graph**: see [Project Dependency Map](../../docs/en/architecture/dependency-map.md).
- An application references it with a UI package: [`Polhem.Web.Blazor.Mud`](../Polhem.Web.Blazor.Mud/README.md) or [`Polhem.Web.Blazor.Fluent`](../Polhem.Web.Blazor.Fluent/README.md).

## Target Framework

- `net10.0`, consumed by a project on `Microsoft.NET.Sdk.BlazorWebAssembly`

## Registration

```csharp
using Polhem.Web.Blazor.Mud.DependencyInjection;
using Polhem.Web.Blazor.Wasm.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");

builder.Services.AddPolhemBlazor(options =>
{
    options.Endpoint = "https://api.example.com/api";
    options.ApiKey = builder.Configuration["Polhem:ApiKey"] ?? string.Empty;
});
builder.Services.AddPolhemShell(shell => shell.AppTitle = "My application");
builder.Services.AddPolhemMud();

var host = builder.Build();
host.Services.ApplyPolhemShellCulture();
await host.RunAsync();
```

`ApplyPolhemShellCulture` starts the page in the language picked on the sign-in screen, kept in the browser's
`localStorage`, then in the browser's language when the shell offers it, then in the default. Call it before `RunAsync`.

## Project settings

```xml
<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
<BlazorWebAssemblyLoadAllGlobalizationData>true</BlazorWebAssemblyLoadAllGlobalizationData>
<InvariantGlobalization>false</InvariantGlobalization>
<InvariantTimezone>false</InvariantTimezone>
```

- A trimmed WebAssembly publish turns System.Text.Json reflection off, and the JSON-RPC envelope needs it. `POLHEM9004`
  warns when it is off.
- The SDK's default for this project type is `TrimMode=partial`, which Polhem supports. Do not set `TrimMode=full` or
  `PublishAot`; see [Platform Support](../../docs/en/getting-started/platform-support.md).
- The framework converts time through `TimeZoneInfo`, so time zone data must ship.

## What differs from Blazor Server

- **Remote only.** The backend cannot run in the browser, so there is no `UseLocalProvider`.
- **The API key is public.** Everything in the bundle can be read by anyone who can open the page. The key names the
  application and protects nothing.
- **Calls are not encrypted by the framework.** The browser runtime has no RSA, so the client skips the key exchange and
  sends `Encoded` payloads. The sign-in password is protected by HTTPS alone, so serve the site and the API over HTTPS.
- **Methods that require `ApiProtectionLevel.Encrypted` are refused**, such as the audit log and API key management
  actions. `ApiAccessValidator` does not accept an `Encoded` call for them.
- **A server on another origin must allow the page through CORS.** Serving the bundle from the API's own origin needs no
  policy.
- **A static host must send `index.html` for unknown paths**, or a reload on a form page returns 404.
- A reload signs out, as a new circuit does on Blazor Server. The access token stays in memory and is deliberately not
  written to browser storage.
