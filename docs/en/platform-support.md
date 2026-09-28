# Platform Support

[繁體中文](../zh-TW/platform-support.md) · [← Docs Index](README.md)

> Which application heads Polhem supports, which publish configurations work, and what a browser or mobile head has
> to set up itself

---

## Table of Contents

1. [Supported heads](#1-supported-heads)
2. [Trimming and AOT](#2-trimming-and-aot)
3. [Checklist for a browser or mobile head](#3-checklist-for-a-browser-or-mobile-head)
4. [What is verified, and where](#4-what-is-verified-and-where)

---

## 1. Supported heads

Every Polhem package targets `net10.0`. A *head* is the project that turns shared application code into an app for
one platform; the platform-specific target framework (`net10.0-ios`, `net10.0-android`, `net10.0-browser`) belongs to
the head, not to the Polhem packages.

| Head | UI package | How it reaches the backend |
|------|------------|----------------------------|
| Desktop (Windows, macOS, Linux) | `Polhem.UI.Avalonia` with `Avalonia.Desktop` | Remote (HTTP), or Local (the backend runs in the same process) |
| Browser (WebAssembly) | `Polhem.UI.Avalonia` with `Avalonia.Browser` | Remote |
| iOS | `Polhem.UI.Avalonia` with `Avalonia.iOS` | Remote |
| Android | `Polhem.UI.Avalonia` with `Avalonia.Android` | Remote |
| Blazor Server | `Polhem.Web.Blazor.Server` | Local by default, or Remote |
| A client with no .NET (JavaScript, TypeScript, …) | — | Remote, speaking JSON-RPC directly; see [JSON-RPC Frontend Integration](jsonrpc-frontend-integration.md) |

- **Remote** means the head calls the JSON-RPC endpoint of a backend host over HTTP. **Local** means the head also
  builds the backend's services (`AddPolhemFramework`), assigns the resulting provider to
  `ClientInfo.LocalServiceProvider`, and calls the backend in process. Which of the two an Avalonia head accepts is
  `ApiClientInfo.SupportedConnectTypes`; every Northwind head sets it to `Remote`.
- The four Avalonia heads share one UI library and one client runtime (`Polhem.UI.Core`'s `ClientInfo`), which holds
  one signed-in user per process. [`apps/Polhem.Northwind`](../../apps/Polhem.Northwind/README.md) builds all four
  from a single shared UI project and is the reference configuration for the checklist below.
- **Blazor Server** runs its components in the ASP.NET Core server process, so the trimming and AOT questions below
  do not apply to it. It keeps one API session per circuit rather than per process. Its default Local mode treats
  every browser user's call as a trusted in-process call (the access token and `LocalOnly` checks are skipped): use
  it only when every user of the site may see the whole backend, and call `UseRemoteProvider(endpoint)` in
  `AddPolhemBlazor` otherwise. Remote mode also needs the application's API key in `ApiClientInfo.ApiKey`, set at
  startup, or the server refuses the first call with `401 Unauthorized`. The remarks on `PolhemBlazorOptions` spell
  out what Local skips and why Remote reads the key from there; see also
  [`samples/Blazor.Server.Demo`](../../samples/Blazor.Server.Demo/README.md).

---

## 2. Trimming and AOT

### Supported configurations

| Publish configuration | Supported | What happens |
|-----------------------|-----------|--------------|
| Untrimmed | Yes | — |
| Partial trim (`TrimMode=partial`) — the default of the iOS, Mac Catalyst and Android SDKs | Yes | The Polhem assemblies are copied untouched; only the SDK assemblies are trimmed |
| Full trim (`TrimMode=full`; also `PublishTrimmed=true` with no `TrimMode` on desktop and browser-wasm, `AndroidLinkMode=Full`, `MtouchLink=Full`) | No | Requests go out without the envelope's `jsonrpc` and `id` members |
| NativeAOT (`PublishAot=true`) | No | The JSON-RPC envelope serializes to an empty object, so every call fails |
| `JsonSerializerIsReflectionEnabledByDefault=false` | No | Every envelope serialization throws |

The reason is the same in every row: the JSON-RPC envelope is serialized by System.Text.Json through reflection,
nothing roots the members it reads, and no Polhem assembly is marked `IsTrimmable` or `IsAotCompatible`. None of the
unsupported configurations fails at build time on its own, which is why the packages add a warning.

### POLHEM9004

The `buildTransitive` targets of the `Polhem.Definition` package raise **POLHEM9004** when a project that references
a Polhem package, directly or through another Polhem package, is configured in one of the unsupported ways above.
The message names the setting to change. To silence it, for example in a project that has verified its own
configuration, set:

```xml
<PropertyGroup>
  <PolhemSuppressTrimSupportWarning>true</PolhemSuppressTrimSupportWarning>
</PropertyGroup>
```

POLHEM9004 is an MSBuild warning, not a compiler diagnostic. It reaches package consumers only: a project that uses
`ProjectReference` to the Polhem sources does not import `buildTransitive/` and gets no warning. The full list of
Polhem diagnostics is in [Analyzer Rules](analyzer-rules.md).

### Code generation at run time (iOS)

The .NET for iOS SDK disables dynamic code (`Reflection.Emit`) in every configuration, Debug included, unless the app
turns on the interpreter. Android keeps the JIT. The framework's own wire does not need dynamic code: every wire type has an explicitly registered
MessagePack formatter (`WireContractDriftTests` fails when one is missing), and the framework's value types, Polhem
enums and `ParameterCollection` travel through closed generic paths.

One path does need dynamic code: a value of an **application-defined type** that travels in an `object`-typed
member (an ExecFunc parameter, a filter value) through the named-type escape hatch, that is, a type whose namespace
the application added to `SysInfo.AllowedTypeNamespaces`. Over MessagePack it goes through the non-generic
serializer, and on iOS it fails with a `NotSupportedException` that names the type. Send such values as framework
value types instead (strings, numbers, `Guid`, dates, a `ParameterCollection`).

The expression engine (`Polhem.Expressions`) is interpreted on iOS, but the interpreter still has to create a delegate
of each compiled expression's signature, and iOS cannot create one with more than two parameters. So
`DynamicExpressoEvaluator` compiles every expression to a single `object?[]` parameter, and
`InterpretedInvokerGateTests` fails when it does not. Until this was measured on 2026-09-28, an expression over three
or more fields, such as a detail line's `quantity * unit_price * (1 - discount)`, terminated an iOS app when it was
computed. Code that evaluates expressions with DynamicExpresso directly, not through `IExpressionEvaluator`, has the
same limit.

### Trimmer descriptors shipped in the packages

Two packages embed an `ILLink.Descriptors.xml` that the trimmer applies automatically. A head needs no linker file of
its own for them.

| Package | What it keeps | When it matters |
|---------|---------------|-----------------|
| `Polhem.Expressions` | The CoreLib members an expression can call by name (`Math`, `string`, `DateOnly` and the other exposed types) | Under the mobile SDKs' default partial trim, which trims CoreLib. Without it, expressions such as `Math.Round(x, 2)` stop parsing and live computation switches itself off for the form. `TrimmerDescriptorGateTests` checks that it covers every type the interpreter exposes |
| `Polhem.Definition` | The definition types that `XmlSerializer` reads by reflection | Only when `Polhem.Definition` itself is trimmed, that is under full trim or NativeAOT, which are unsupported anyway |

---

## 3. Checklist for a browser or mobile head

A desktop head needs none of this. The items marked **(browser)** apply only to a browser-wasm head.

1. **Trimming.** Keep the SDK default on iOS and Android; do not set `TrimMode=full`, `AndroidLinkMode=Full` or
   `MtouchLink=Full`. **(browser)** A trimmed browser-wasm publish without `TrimMode` is a full trim, so set either
   `PublishTrimmed=false` (what the Northwind browser head does) or `TrimMode=partial`.
2. **(browser) Keep System.Text.Json reflection on.** browser-wasm turns it off by default, and every call then fails
   before the request is sent:

   ```xml
   <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
   ```

3. **Keep time zone and globalization data.** The client converts every instant between UTC and the user's time zone
   through `TimeZoneInfo` ([Time Zones](datetime-timezone.md)), so a build without time zone data fails at the first
   date it shows. The Northwind browser and mobile heads pin both settings, because the failure only shows up on the
   device:

   ```xml
   <InvariantGlobalization>false</InvariantGlobalization>
   <InvariantTimezone>false</InvariantTimezone>
   ```

4. **Endpoint and API key storage.** `ClientInfo.EndpointStorage` and `ClientInfo.ApiKeyStorage` default to one
   `FileEndpointStorage`, which writes to a per-application folder under the per-user local application data
   directory. That folder is writable on desktop, iOS (inside the app's sandbox container; it is included in device
   backups) and Android (the app's private data directory), so those heads keep the default. The remarks on
   `FileEndpointStorage` list the resolved path per platform. **(browser)** The browser has no persistent file system:
   writes land in memory and are lost on reload. Implement `IEndpointStorage` and `IApiKeyStorage` over browser
   storage and assign both properties before calling `ClientInfo.InitializeAsync` or `ClientInfo.SetEndpointAsync`.
   The Northwind browser head's
   [`BrowserLocalStorageEndpointStorage`](../../apps/Polhem.Northwind/Polhem.Northwind.Browser/Storage/BrowserLocalStorageEndpointStorage.cs)
   is a working example over `localStorage`.
5. **Stay asynchronous.** Connect with `ClientInfo.InitializeAsync` and load definitions with the `…Async` members of
   `ClientDefineAccess`. Never block on a task (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`): the browser runtime
   is single-threaded, and blocking there throws "Cannot wait on monitors on this runtime".
6. **Dialogs.** Nothing to do for the framework's dialogs: `LookupDialog` and `RowEditDialog` open as a native window
   only in a desktop classic-window lifetime and are shown on the top level's overlay layer everywhere else.
   Your own dialogs need the same care: the iOS and Android windowing backends throw `NotSupportedException` when
   asked to create a `Window`.
7. **(browser) Fonts.** The browser sandbox has no system fonts to fall back to. Ship a font that covers every
   language you display; otherwise, for example, Chinese captions from the `zh-TW` language resources render as empty
   boxes.

Two behaviours differ by platform without any setup:

- The API client's `HttpClient` uses the platform's default handler on the browser (fetch), Android and Apple mobile
  platforms, so the system's proxy and certificate settings apply there; desktop and server hosts use
  `SocketsHttpHandler`.
- After login, `ClientInfo` makes the culture returned for the user the process culture, which drives captions,
  framework text and the display of numbers and dates.

---

## 4. What is verified, and where

- **The unsupported configurations**: POLHEM9004, in `src/Polhem.Definition/buildTransitive/Polhem.Definition.targets`.
- **Running without dynamic code**: this repository's CI runs the test projects of every package a client head ships
  (`Polhem.Base`, `Polhem.Definition`, `Polhem.Expressions`, `Polhem.Api.Core`, `Polhem.Api.Client`, `Polhem.UI.Core`,
  `Polhem.UI.Avalonia`) a second time with `DynamicCodeSupport=false`, the switch the iOS SDK sets.
- **The expression descriptor**: `TrimmerDescriptorGateTests` in `tests/Polhem.Expressions.UnitTests`.
- **The heads themselves**: the Northwind browser, iOS and Android heads are the reference configuration, but this
  repository's CI does not build them. A change to one of the settings above is checked by building and running the
  head.
