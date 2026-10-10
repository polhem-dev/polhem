# Client calls (Polhem.Api.Client)

The frontend only needs a reference to `Polhem.Api.Client`. Based on `samples/QuickStart.Console`; when this file and
that sample disagree, the sample wins. Every connector method is asynchronous and takes a `CancellationToken`.

## Build a dedicated connector (recommended)

The proper client↔server seam is **`Polhem.Api.Client.Connectors.ApiConnector`** (abstract base, `protected
ExecuteAsync<T>(progId, action, value, format, cancellationToken)`). `FormApiConnector` is a subclass of it (bound to a
ProgId + a set of form CRUD methods). **A custom app should not use `FormApiConnector` for its own actions** (it is
for ERP forms) — follow its pattern: inherit `ApiConnector` and build a **dedicated connector** that wraps each domain
action as a typed method:

```csharp
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages;   // PayloadFormat

public sealed class XxxApiConnector : ApiConnector
{
    // The client decides in-process vs HTTP and carries the signed-in identity; the connector only reads it.
    // A host serving several users from one process creates one PolhemApiClient per user.
    public XxxApiConnector(PolhemApiClient client) : base(client) { }

    // One connector can serve several ProgIds (the base passes progId on every call).
    public Task<GetLevelsResponse> GetLevelsAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<GetLevelsResponse>("Game", "GetLevels", new GetLevelsRequest(), PayloadFormat.Plain, cancellationToken);
}
```

Benefit: callers (such as your `IXxxApi` implementation) depend only on the typed methods of `XxxApiConnector` and
never touch raw action strings.

## Which types the client needs

It depends on the `PayloadFormat` of the call:

| Format | What travels | What the client needs |
|---|---|---|
| `Plain` (Public actions only) | JSON bound by property name | Its own look-alike DTOs are enough |
| `Encoded` / `Encrypted` | A body in the declared codec, plus the body's type name | The **same** args/result types as the server, from a shared assembly, allow-listed locally |

For Encoded / Encrypted the server decodes the request body into the action's parameter type, and a type name that
does not match it is refused (`ActionPayloadType`). The response carries the server's result type name, which the
client resolves and screens against **its own** `AllowedTypeNamespaces`: `SystemApiConnector.InitializeAsync`
deliberately ignores the server's list, so set the client's with `SysInfo.Initialize` before calling it.

```csharp
// Plain look-alike DTOs (no need to reference the server)
public sealed class GetLevelsRequest;
public sealed class GetLevelsResponse { public List<LevelDto> Levels { get; set; } = new(); }
public sealed class LevelDto { public string Name { get; set; } = ""; public LevelDifficulty Difficulty { get; set; } }
```

## System calls

```csharp
using Polhem.Api.Client;

// Any non-empty key passes until the deployment issues its first API key.
var client = PolhemApiClient.CreateRemote(endpoint, "xxx-dev");
var sys = client.System;
await sys.PingAsync();              // System.Ping (anonymous, no API key needed)
await sys.InitializeAsync();        // adopt the server's compressor / encryptor before Encoded or Encrypted calls
```
- `endpoint`: `http://<host>:<port>/api`. In process (the backend runs in the same process), use
  `PolhemApiClient.CreateLocal(serviceProvider)` instead.

## Calls that require login

```csharp
await sys.LoginAsync("demo", "demo");   // signs the client in: token, session key and time zone
var game = new XxxApiConnector(client);
var r = await game.SomeAuthedActionAsync();         // wraps ExecuteAsync<T>(..., PayloadFormat.Encrypted, ...)
```

- `LoginAsync` performs the RSA handshake and stores the access token and the session encryption key in the
  client's `Session` (`PolhemApiClient.Session`). Every connector of that client, created before or after, then
  calls as the signed-in user and can send `Encrypted`; without a session key, `Encrypted` is downgraded to
  `Encoded`.
- The Encrypted format relies on TLS against an active man-in-the-middle (`SystemApiConnector.InitializeAsync`
  remarks). Use HTTPS outside development.

## Choosing the body codec

`PayloadFormat` decides encryption and compression; the body codec is a separate choice, declared per request
(adr-044). A connector that sets nothing sends MessagePack. Set `ApiConnector.PayloadCodec` to
`PayloadCodecNames.Json` to use the JSON codec for that connector's Encoded / Encrypted calls. This matters on iOS:
the framework registers MessagePack formatters for its own wire types only, and your own types fall back to a
resolver that needs dynamic code, which .NET for iOS does not have (`src/Polhem.Api.Core/CLAUDE.md`).

## endpoint (per target)

| Target | endpoint |
|---|---|
| Desktop / same machine | `http://localhost:<port>/api` |
| **Android emulator** | `http://10.0.2.2:<port>/api` (10.0.2.2 = the host's loopback) |
| iOS simulator | `http://localhost:<port>/api` |

Plaintext HTTP during development: Android needs `android:usesCleartextTraffic="true"` in `AndroidManifest` + the
`INTERNET` permission; iOS needs `NSAppTransportSecurity` to allow localhost.

```csharp
private static readonly string Endpoint = OperatingSystem.IsAndroid()
    ? "http://10.0.2.2:5180/api" : "http://localhost:5180/api";
```

## Verification probe (most reliable)

A standalone console project (referencing `Polhem.Api.Client`) that calls Ping + your first action against the local
server. It covers Encoded / Encrypted calls too, whose envelope is impractical to build by hand with curl.

```csharp
var client = PolhemApiClient.CreateRemote("http://localhost:5180/api", "xxx-dev");
await client.System.PingAsync();                          // → ok
var game = new XxxApiConnector(client);
var r = await game.GetLevelsAsync();
Console.WriteLine($"{r.Levels.Count} levels");           // → expected count
```

## Wire DTO deserialization

A Plain response is read with System.Text.Json using `ApiInputConverter.PlainReadOptions`: property names match
case-insensitively, enums are read as their names, and `DataTable` / `DataSet` have converters. Enum and table
members therefore need no special handling in a look-alike DTO. Keep response DTOs to settable properties with a
parameterless constructor.

### Trimming on mobile

The supported configurations are untrimmed and the SDK's default partial trim (`TrimMode=partial`), which leaves the
Polhem assemblies and your own app assembly untouched. `TrimMode=full` (also `AndroidLinkMode=Full` /
`MtouchLink=Full`) and NativeAOT are **unsupported**: the JSON-RPC envelope is serialized by reflection and full
trim removes members it needs. `POLHEM9004` warns about those configurations when the framework comes in as a package.
The reasoning is in `rules/apple-mobile-trim.md` § Supported trim modes.

### Troubleshooting approach

1. First confirm the server is correct with a desktop console probe whose DTOs cover every member you rely on.
2. At the client call site, catch and log the **exception message** (logcat / file). A `JsonException`'s `Path`
   points straight at the mismatched member; a "Payload type ... is not in the allowed type whitelist" message points
   at `AllowedTypeNamespaces`; a `FormatterNotRegisteredException` on iOS points at MessagePack with a host type.
3. An empty result only in a Release mobile build → check the trim mode before anything else.
