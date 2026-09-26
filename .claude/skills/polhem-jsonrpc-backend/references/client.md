# Client calls (Polhem.Api.Client)

The frontend only needs a reference to `Polhem.Api.Client`. Based on `QuickStart.Console` and verified on a real
project's client.

## Build a dedicated connector (recommended)

The proper client↔server seam is **`Polhem.Api.Client.Connectors.ApiConnector`** (abstract base, `protected
ExecuteAsync<T>(progId, action, value, format)`). `FormApiConnector` is a subclass of it (bound to a ProgId + a set of
form CRUD methods). **A custom app should not use `FormApiConnector` directly** (it is for ERP forms) — follow its
pattern: inherit `ApiConnector` and build a **dedicated connector** that wraps each domain action as a typed method:

```csharp
using Polhem.Api.Client.Connectors;
using Polhem.Api.Core.Messages;   // PayloadFormat

public sealed class XxxApiConnector : ApiConnector
{
    public XxxApiConnector(string endpoint, Guid accessToken) : base(endpoint, accessToken) { }
    // Local in-process: the base(accessToken) overload

    // One connector can serve several ProgIds (the base passes progId on every call).
    public Task<GetLevelsResponse> GetLevelsAsync() =>
        ExecuteAsync<GetLevelsResponse>("Game", "GetLevels", new GetLevelsRequest(), PayloadFormat.Plain);
    // Actions that require login use PayloadFormat.Encrypted (call System.Login first and pass the token to the ctor).
}

// wire DTOs are matched by property name; no need to reference the server BO types. Use string for enum fields (see below).
public sealed class GetLevelsRequest;
public sealed class GetLevelsResponse { public List<LevelDto> Levels { get; set; } = new(); }
public sealed class LevelDto { public string Name { get; set; } = ""; public string Difficulty { get; set; } = ""; /* ... */ }
```

Benefit: callers (such as your `IXxxApi` implementation) depend only on the typed methods of `XxxApiConnector` and
never touch raw action strings.

## System calls

```csharp
using Polhem.Api.Client;
using Polhem.Api.Client.Connectors;

ApiClientInfo.ApiKey = "xxx-dev";   // any non-empty value passes the default check (use a real key store in production)
var sys = new SystemApiConnector(endpoint, Guid.Empty);
await sys.PingAsync();              // System.Ping (anonymous)
```
- `endpoint`: `http://<host>:<port>/api`.

## Calls that require login

```csharp
var login = sys.Login("demo", "demo");        // or use the higher-level ClientInfo/ApplyLoginResult wrapper
var token = login.AccessToken;                // Guid
var bo = new FormApiConnector(endpoint, token, "Game");
var r = await bo.ExecuteAsync<TResult>("SomeAuthedAction", args, PayloadFormat.Encrypted);
```

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
server. Much more reliable than curl (curl makes it hard to hand-build the ApiPayload envelope).

```csharp
ApiClientInfo.ApiKey = "xxx-dev";
var sys = new SystemApiConnector("http://localhost:5180/api", Guid.Empty);
await sys.PingAsync();                                    // → ok
var game = new FormApiConnector("http://localhost:5180/api", Guid.Empty, "Game");
var r = await game.ExecuteAsync<GetLevelsResponse>("GetLevels", new GetLevelsRequest(), PayloadFormat.Plain);
Console.WriteLine($"{r.Levels.Count} levels");           // → expected count
```

## ⚠️ Wire DTO deserialization (pitfalls hit in practice)

The response of `ExecuteAsync<T>(..., PayloadFormat.Plain)` is deserialized by **reflection-based System.Text.Json**
(`Polhem.Api.Core/Conversion/ApiOutputConverter.ConvertResultValue<T>` → `JsonSerializer.Deserialize<T>(json,
new(){PropertyNameCaseInsensitive=true})`) — there is no source-gen context. Property names are **PascalCase** and
matched case-insensitively.
(Plain uses JSON; only Encoded/Encrypted use MessagePack, and only then do `[MessagePackObject]`/`[Key]` matter.)

### Pitfall 1 (the easiest to hit): enum fields — the client deserialization options **do not register `JsonStringEnumConverter`**
The server-side `JsonCodec` **does** serialize enums as **strings** (`"Master"`), but the `JsonSerializerOptions` used
by the client's `ConvertResultValue` sets **only** `PropertyNameCaseInsensitive`, with **no** enum string converter.
So if a wire DTO declares that field as `enum` or `int`, it throws `JsonException: The JSON value could not be
converted to ... Path: $.xxx.difficulty`, and deserialization of the whole response fails (inside a fire-and-forget
load it gets swallowed and looks like an "empty list / empty object").
**This has nothing to do with mobile — desktop hits it too; you just do not see it if your DTO happens not to carry
that enum field.**

**Fix**: declare the wire DTO's enum field as `string` and parse it yourself when mapping:
```csharp
public string Difficulty { get; set; } = "";   // receives "Master"
// mapping:
Difficulty = Enum.TryParse<LevelDifficulty>(w.Difficulty, ignoreCase: true, out var d) ? d : default,
```
(The same goes for `DataTable`/`DataSet` fields — `ConvertResultValue` does not register those converters either.
Keep response DTOs to flat, settable primitive types + string where possible.)

### Pitfall 2: Release full trim strips serialization metadata
`Polhem.Api.Core` has **no** `ILLink.Descriptors.xml` (only `Polhem.Definition` has one, see the framework
`CHANGELOG` v4.12). Under a Release full link (`TrimMode=full` / iOS/Android Release), the linker strips the setters
and ctors of the response DTOs and their collection element types, and reflection-based STJ silently returns default
values (reproducible on desktop with `dotnet publish -p:PublishTrimmed=true -p:TrimMode=full`; on the serialization
side it throws at `JsonSerializer.GetTypeInfo`). **Note that Debug's `AndroidLinkMode=None` does not trim, so an empty
result in Debug is usually pitfall 1, not this one.**
**Fix**: in the mobile head csproj, set the serialization-related assemblies as trim roots:
```xml
<ItemGroup>
  <TrimmerRootAssembly Include="Polhem.Api.Core" />
  <TrimmerRootAssembly Include="Polhem.Base" />
  <TrimmerRootAssembly Include="<the assembly holding your wire DTOs>" />
  <TrimmerRootAssembly Include="<the assembly holding your domain types>" />
</ItemGroup>
```

### Troubleshooting approach
1. First confirm the server is correct with a desktop console probe. **The probe DTO must cover every field
   (especially enums)**, otherwise, as happened to me, "that field just happened to be absent" fools you into
   thinking it is a mobile-only problem.
2. At the client call site, try/catch and log the **exception message** (logcat / file). The `Path` in a
   `JsonException` points straight at the mismatched field/type — much faster than guessing "trimming/Mono".
3. Confirmed enum/type mismatch → change the wire DTO (pitfall 1); confirmed Release trim → add trim roots
   (pitfall 2).

> **Verification result**: server + desktop client + **Android device against the real server** (GetLevels) all ran
> end to end (confirmed by the device hero showing the `⋅LIVE` marker added on the server side). Both pitfalls above
> were actually hit and fixed.
