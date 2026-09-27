# Polhem.Northwind.Browser

**English** | [繁體中文](README.zh-TW.md)

The **web (WASM) head** of the [Polhem.Northwind](../README.md) demo. It compiles the shared
`Polhem.Northwind.UI` Avalonia application to WebAssembly with the **Avalonia Browser** backend
and runs it in a browser — the same `App`, view models and views as
[`Polhem.Northwind.Desktop`](../Polhem.Northwind.Desktop), just a different platform head
(`.UseBrowser()` instead of `.UseDesktop()`). It is a thin JSON-RPC client; the backend is the
unchanged [`Polhem.Northwind.Server`](../Polhem.Northwind.Server).

## Prerequisites

The .NET 10 SDK plus the **WebAssembly tools** workload (one-time):

```bash
sudo dotnet workload install wasm-tools
```

## Running (development)

Two terminals from the repository root:

```bash
# 1. Backend (JSON-RPC on http://localhost:5100). Dev-only CORS lets the WASM
#    dev server call it cross-origin.
dotnet run --project apps/Polhem.Northwind/Polhem.Northwind.Server

# 2. Web client dev server (Avalonia WASM on http://localhost:5200)
dotnet run --project apps/Polhem.Northwind/Polhem.Northwind.Browser
```

Open <http://localhost:5200/>, then **Connect** (endpoint pre-filled with
`http://localhost:5100/api`) → **Sign in** with `demo` / `demo`.

Because the dev server (`5200`) and the API (`5100`) are different origins, the server enables
a development-only CORS policy (`PolhemDevWasm`, gated by `IsDevelopment()`) that allows any
`localhost` origin. A production deployment should serve the published WASM **same-origin** from
the API host and drop that policy.

## WASM-specific wiring

Single-threaded `browser-wasm` forces a few choices that the desktop head does not need; each is
commented at its source:

| Concern | Why | Where |
|---------|-----|-------|
| Endpoint and API key persistence via `localStorage` | the default `FileEndpointStorage` writes files, and the browser's file system is in memory, so the values would be gone after a reload | `Storage/BrowserLocalStorageEndpointStorage.cs`, set in `Program.cs` |
| `JsonSerializerIsReflectionEnabledByDefault=true` | browser-wasm disables System.Text.Json reflection by default; Polhem's `JsonCodec` is reflection-based | `Polhem.Northwind.Browser.csproj` |
| Async connect / define load | sync-over-async throws *"Cannot wait on monitors"* on the single thread — use `ClientInfo.InitializeAsync` / `ClientInfo.DefineAccess.GetMenuSettingsAsync()` | `ConnectionViewModel`, `FormsViewModel` |
| UI culture starts as `en-US` | Inter, the bundled font, has no CJK glyphs and the browser has no system font to fall back on. Signing in then applies the account's culture, as on every head, so use an account whose culture is `en-US` here | `Program.cs` |
| Overlay dialogs instead of `Window` | there are no native windows — lookup / row-edit dialogs render on the `OverlayLayer` | `Polhem.UI.Avalonia` `OverlayDialogHost` |

## Release / publish

```bash
dotnet publish apps/Polhem.Northwind/Polhem.Northwind.Browser -c Release -o <out>
```

The project sets `<PublishTrimmed>false</PublishTrimmed>`: Polhem reaches definition and message
types by reflection (System.Text.Json for the JSON-RPC envelope, XmlSerializer for definitions,
`TypeDescriptor`, `Assembly.GetType`), none of it source-generated, so IL trimming both fails
analysis (`IL2026` under `TreatWarningsAsErrors`) and would strip metadata the reflection paths
need at runtime. MessagePack is the exception: every wire type has a hand-registered formatter.
Disabling trimming trades bundle size (~16 MB gzip) for correctness. The framework does not offer
source-generated serializers, which a trim-safe bundle would need.

The published `wwwroot/` is a static bundle; serve it from any static host (or, for a same-origin
deployment, from `Polhem.Northwind.Server`).
