---
name: polhem-jsonrpc-backend
description: >
  Build a JSON-RPC backend server from scratch with the Polhem framework packages (ASP.NET Core, POST /api), plus
  frontend client calls. Covers bootstrap, Define/ XML settings, custom Business Objects, demo login, session/API key,
  and pitfalls. Use when the user wants to "build a backend with the Polhem framework", "Polhem JSON-RPC server",
  "how to set up AddPolhemFramework", "Polhem BusinessObject / ProgramSettings / Define settings", "how
  Polhem.Api.Client calls the server", "Polhem server login / session / X-Api-Key", "connect an existing project's
  backend to Polhem", or similar. Trigger proactively even when not all of these keywords are stated.
  **Only covers the generic skeleton and settings for building the server and calling it from a client; does not
  cover business-logic design for a specific app.**
---

# Building a JSON-RPC backend with Polhem

Polhem is a **definition-driven (XML) + reflection-dispatch** JSON-RPC backend framework. The server has almost no C#:
the framework's API executor, action string parsing, MessagePack serialization, `[ApiAccessControl]` checks and CRUD
base methods all live in the `Polhem.*` NuGet packages. The host project is a thin shell: one csproj, `Program.cs`, a
bootstrap class, an empty controller, a few custom BOs, and the `Define/` settings tree.

**Understanding this saves a lot of trial and error**: most behavior is triggered by `Define/*.xml`, not by code you
write.

## Authoritative references (the sources to copy from)

`polhem/samples` (same version as the framework, **the best blueprint**):
- `samples/QuickStart.Server/` — minimal server (Program.cs, empty controller, `EchoBusinessObject`, `QuickStartFormBoTypeResolver`)
- `samples/QuickStart.Console/` — minimal client (calls `Polhem.Api.Client` directly)
- `samples/Polhem.Samples.Shared/` — `DemoBackend` (`AddPolhemBackend`/`UsePolhemBackend`), `DemoBusinessObjectFactory`, `DemoAuthenticatingSystemBusinessObject`, `DemoCredentials`

Full app (with seeder, company scope, declarative `ProgramSettings` binding): `apps/Polhem.Northwind/`.

> Before you start, always open `QuickStart.Server` + `DemoBackend.cs` side by side. Version details (interface
> members, tag names) change between versions.

## Not applicable (use another skill)

- Need **DB scope (common/company/log), company context, seeder** →
  **`polhem-app-scaffold`** (it builds on this skill's template and only adds those three parts)
- Need **ERP definition-driven form CRUD** (`GetList`/`Save`/`Delete` on `FormBusinessObject`) →
  same as above; this skill's BO axis is the minimal `BusinessObject`
- **Adding a form** to an app that is already wired up → **`polhem-add-form`**

## Request flow (build the mental model first)

```
client ──POST /api──▶ ApiServiceController
   body: {jsonrpc:"2.0", method:"Game.GetLevels", params:{format,value,type}, id}
   headers: X-Api-Key (required), Authorization: Bearer <token> (only for non-anonymous methods)
        │
        ▼
  check X-Api-Key + token → split method at the first "." = (ProgId, Action)
        │  ProgId=="System" → CreateSystemBusinessObject
        │  otherwise        → CreateFormBusinessObject → IFormBoTypeResolver.Resolve(progId)
        ▼
  Activator.CreateInstance(boType, ctx, token, progId, isLocalCall)
  GetMethod(Action) → check [ApiAccessControl] → invoke by reflection: single args in, single result out
        │
        ▼
  result is wrapped back in ApiPayload (MessagePack→gzip→aes, or Plain=JSON); the client deserializes it to TResult
```

**Single args / single result** is the hard rule for every action: `TResult Action(TArgs args)` (or
`async Task<TResult>`).

## Build steps

Do these in order; the full code for each step is in `references/`.

### 1. Project skeleton

Web project (`Microsoft.NET.Sdk.Web`, `net10.0`). Add the packages (with central package management, versions go in
`Directory.Packages.props`):

```xml
<PackageReference Include="Polhem.Api.AspNetCore" />  <!-- controller + API pipeline -->
<PackageReference Include="Polhem.Business" />         <!-- BO base classes -->
<PackageReference Include="Polhem.Db" />               <!-- data access / dialect -->
<PackageReference Include="Polhem.Hosting" />          <!-- AddPolhemFramework -->
<PackageReference Include="Microsoft.Data.Sqlite" />
<!-- Pin a non-vulnerable 3.x; Microsoft.Data.Sqlite pulls in the vulnerable 2.1.x -->
<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" />
```

### 2. `Program.cs` + backend bootstrap

`Program.cs` is minimal; the real wiring is wrapped in an `XxxBackend` static class (copy `DemoBackend.cs`).
**Both initialization stages are required**:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddXxxBackend();          // see below
builder.Services.AddControllers();
var app = builder.Build();
app.UseXxxBackend();              // seeder + ApiClientInfo.LocalServiceProvider (if needed)
app.MapControllers();
app.Run();
```

The core order in `AddXxxBackend` (none of these can be skipped):
```csharp
var paths = new PathOptions { DefinePath = ResolveDefinePath() };   // walk up to find Define/SystemSettings.xml
DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());
var settings = SystemSettingsLoader.Load(paths);
SysInfo.Initialize(settings.CommonConfiguration);
ApiServiceOptions.Initialize(settings.CommonConfiguration.ApiPayloadOptions,   // ← easy to miss! sets the codec
                             settings.CommonConfiguration.IsDebugMode);
builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths, autoCreateMasterKey: true);
// Register after AddPolhemFramework (last-wins) to replace the factory / resolver:
builder.Services.AddSingleton<IFormBoTypeResolver, XxxFormBoTypeResolver>();
builder.Services.AddSingleton<IBusinessObjectFactory, XxxBusinessObjectFactory>();
```

For the full template (with `ResolveDefinePath`, st_cache_notify materialize, master key fallback) see
`references/backend-bootstrap.md`.

### 3. Empty controller

```csharp
public class ApiController : Polhem.Api.AspNetCore.Controllers.ApiServiceController { }
```
The base class already declares `[Route("api")]` + a POST handler and publishes `POST /api`; you do not write it
yourself.

### 4. The `Define/` settings tree

Five XML files (`Program.cs` walks up from `AppContext.BaseDirectory` to find the folder containing
`SystemSettings.xml`). The role and **minimal content** of each file are in `references/define-config.md`. Key points:
- `SystemSettings.xml` — payload codec (messagepack/gzip/aes-cbc-hmac), master key source,
  **`<AllowedTypeNamespaces>`** (your args/result namespaces must be listed).
- `DatabaseSettings.xml` — `common` (required by the framework) + `company`, pointing at a SQLite file.
- `DbCategorySettings.xml`, `ProgramSettings.xml`, `TableSchema/` (including the framework's `st_cache_notify`).

### 5. Demo login (start fast without seeding st_user)

The framework's `SystemBusinessObject.AuthenticateUser` returns false by default, so `System.Login` only succeeds if
you override it. The three-piece set (copy from Demo):
- `XxxCredentials` (hard-coded demo user)
- `XxxAuthenticatingSystemBusinessObject : SystemBusinessObject` (override `AuthenticateUser(LoginArgs, out userName)`)
- `XxxBusinessObjectFactory : IBusinessObjectFactory` (`CreateSystemBusinessObject` returns the class above) +
  `XxxFormBoTypeResolver`

Full code in `references/business-object.md`.

### 6. Custom Business Object

A custom RPC BO inherits the **minimal `BusinessObject`** (`Polhem.Business`), **not** `FormBusinessObject` (that is
the ERP definition-driven form BO with GetList/Save/Delete; a typical app does not need it). Details in
`references/business-object.md`.

```csharp
public sealed class GameBO : BusinessObject
{
    // 4-arg ctor (the factory uses Activator.CreateInstance(type, ctx, token, progId, isLocalCall));
    // the BusinessObject base only takes 3 args → drop progId.
    public GameBO(IPolhemContext ctx, Guid accessToken, string progId, bool isLocalCall = true)
        : base(ctx, accessToken, isLocalCall) { }

    [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
    public GetLevelsResult GetLevels(GetLevelsArgs args) => new() { /* ... */ };
}
```
- args/result are **plain POCOs** that inherit `BusinessArgs`/`BusinessResult` (no MessagePack attributes needed);
  put them in `AllowedTypeNamespaces`.
- **Every action must be marked `[ApiAccessControl]`** — methods without the attribute are rejected outright by the
  framework (see pitfalls).
- Bind progId→BO in one of two ways: the resolver's `switch` (code, AOT-friendly; it can return any `Type` with a
  matching ctor), or `BusinessObject="Ns.GameBO, Asm"` in `ProgramSettings.xml` (declarative).

### 7. Client calls

The frontend only needs a reference to `Polhem.Api.Client`. **Do not use `FormApiConnector` directly** (it is for ERP
forms) — follow its pattern: inherit `ApiConnector` and build a **dedicated connector** that wraps each domain action
as a typed method. Details in `references/client.md`:

```csharp
public sealed class XxxApiConnector : ApiConnector
{
    public XxxApiConnector(string endpoint, Guid accessToken) : base(endpoint, accessToken) { }
    public Task<GetLevelsResponse> GetLevelsAsync() =>
        ExecuteAsync<GetLevelsResponse>("Game", "GetLevels", new GetLevelsRequest(), PayloadFormat.Plain);
}
// Caller:
ApiClientInfo.ApiKey = "xxx-dev";                       // any non-empty value passes the default check
var r = await new XxxApiConnector(endpoint, Guid.Empty).GetLevelsAsync();
```
- endpoint: desktop `http://localhost:<port>/api`; **Android emulator `10.0.2.2`**, iOS simulator `localhost`;
  cleartext must be allowed during development.
- **wire DTOs are matched by property name**, so you do not need to reference the server's BO types (use `string` for
  enum fields, see pitfalls).

## Pitfalls (these will stall you for a long time)

- **`ApiServiceOptions.Initialize` is easy to miss**: it sets the codec (serialization/compression/encryption), and
  `AddPolhemFramework` does **not** do this. Miss it → payload behavior is wrong.
- **Every action must be marked `[ApiAccessControl]`**: the framework rejects any method with no attribute (and none
  on the base/declaring class either) with `UnauthorizedAccessException`. This is not optional.
- **`X-Api-Key` is always required**, even for the auth-exempt methods
  (`System.Ping`/`System.Login`/`System.GetApiPayloadOptions`); those three only skip `Authorization: Bearer`.
- **Missing entry in `AllowedTypeNamespaces`**: Encoded/Encrypted uses typeless MessagePack; if a type's namespace is
  not listed in `SystemSettings.xml`, deserialization fails.
- **The factory / resolver must be registered with `AddSingleton` after `AddPolhemFramework`** (last-wins); registered
  before, they are overwritten by the framework defaults.
- **Define is located by walking up**: it is not copied to output by default, so run from inside the checkout
  (`dotnet run`), or set `CopyToOutputDirectory` on Define yourself.
- **master key**: in dev you can use `autoCreateMasterKey: true` + the environment variable `POLHEM_MASTER_KEY` (a
  fixed value keeps encrypted rows decryptable across runs); in production a real key must be injected by the
  deployment mechanism.
- **Interface members grow between versions**: for example, in 4.14.0 `IBusinessObjectFactory` gained
  `CreateLogBusinessObject` — a custom factory must add it (delegating to the framework's `LogBusinessObject`). The
  compile error tells you exactly which member is missing.
- **wire DTO deserialization**: the client deserializes Plain responses with reflection-based STJ and **does not
  register `JsonStringEnumConverter`** — declare the DTO's enum fields as `string` and parse them yourself, otherwise
  it throws `JsonException` (hits both desktop and mobile, see `references/client.md`). A Release full trim also needs
  `TrimmerRootAssembly`.

## Verification

1. `dotnet run --project <server>` (note the dev port).
2. Write a small console app (`Polhem.Api.Client`, see the probe at the end of `references/client.md`) that calls
   `System.Ping` + your first action to confirm a real JSON-RPC round trip — **this is the fastest and most reliable
   check**, better than curl (curl makes it hard to build the ApiPayload envelope).
3. Then connect the real frontend / mobile head.

## references/

- `backend-bootstrap.md` — full template for `Program.cs` + `XxxBackend` (walk-up, materialize, master key, factory
  registration).
- `define-config.md` — the role of the five Define XML files + minimal content you can paste.
- `business-object.md` — full code for the BO / args / result / the demo login three-piece set / resolver.
- `client.md` — how to call `Polhem.Api.Client`, endpoints, the probe, and deserialization notes for mobile.
