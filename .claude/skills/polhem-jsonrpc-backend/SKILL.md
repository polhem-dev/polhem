---
name: polhem-jsonrpc-backend
description: >
  Build a JSON-RPC backend server from scratch with the Polhem framework packages (ASP.NET Core, POST /api), plus
  frontend client calls. Covers bootstrap, Define/ XML settings, custom Business Objects bound in ProgramSettings,
  login, session/API key, and pitfalls. Use when the user wants to "build a backend with the Polhem framework", "Polhem
  JSON-RPC server", "how to set up AddPolhemFramework", "Polhem BusinessObject / ProgramSettings / Define settings",
  "how Polhem.Api.Client calls the server", "Polhem server login / session / X-Api-Key", "connect an existing
  project's backend to Polhem", or similar. Trigger proactively even when not all of these keywords are stated.
  **Only covers the generic skeleton and settings for building the server and calling it from a client; does not
  cover business-logic design for a specific app.**
---

# Building a JSON-RPC backend with Polhem

Polhem is a **definition-driven (XML) + reflection-dispatch** JSON-RPC backend framework. The server has almost no C#:
the framework's API executor, action string parsing, payload serialization, `[ApiAccessControl]` checks and CRUD
base methods all live in the `Polhem.*` NuGet packages. The host project is a thin shell: one csproj, `Program.cs`, a
bootstrap class, a few custom BOs, and the `Define/` settings tree.

**Understanding this saves a lot of trial and error**: most behavior is triggered by `Define/*.xml`, not by code you
write. That includes which class serves each progId.

## Authoritative references (the sources to copy from)

`polhem/samples` (same version as the framework, **the best blueprint**):
- `samples/QuickStart.Server/` — minimal server (Program.cs, `EchoBusinessObject`)
- `samples/QuickStart.Console/` — minimal client (calls `Polhem.Api.Client` directly)
- `samples/Polhem.Samples.Shared/` — `DemoBackend` (`AddPolhemBackend` / `UsePolhemBackend`),
  `DemoAuthenticatingSystemBusinessObject`, `DemoCredentials`, `DemoSchemaSeeder`
- `samples/Define/` — the settings tree those samples share, including the `ProgramSettings.xml` that binds
  `System` and `Echo` to their classes

Full app (with seeder, company scope, sign-in against `st_user`): `apps/Polhem.Northwind/`.

> Before you start, always open `QuickStart.Server` + `DemoBackend.cs` side by side. Interface members and element
> names change between versions; the samples compile against the current ones.

## Not applicable (use another skill)

- Need **DB scope (common/company/log), company context, seeder** →
  **`polhem-app-scaffold`** (it builds on this skill's template and only adds those three parts)
- Need **ERP definition-driven form CRUD** (`GetList`/`Save`/`Delete` on `FormBusinessObject`) →
  same as above; this skill's BO axis is the minimal `BusinessObject`
- **Adding a form** to an app that is already wired up → **`polhem-add-form`**
- **Adding a method to the framework's own BOs** → **`polhem-add-bo-method`**

## Request flow (build the mental model first)

```
client ──POST /api──▶ MapJsonRpc endpoint (Polhem.JsonRpc.AspNetCore) ─▶ JsonRpcDispatcher
   body: {jsonrpc:"2.0", method:"Game.GetLevels", params:{format, codec, type, value}, id}
   headers: X-Api-Key (every method except System.Ping)
            Authorization: Bearer <token> (optional; without it the call is anonymous)
        │
        ▼
  split method at the first "." = (ProgId, Action)
        │  PolhemObjectFactory: check X-Api-Key and Authorization (HTTP only), then
        │  IBusinessObjectFactory.CreateBusinessObject(token, progId, isLocalCall)
        │    → IBoTypeResolver (default ProgramSettingsBoTypeResolver: ProgramItem.BusinessObject)
        ▼
  Activator.CreateInstance(boType, ctx, token, progId, isLocalCall)
  resolve Action → PolhemAccessFilter checks [ApiAccessControl] → PolhemPayloadFilter decodes the body → invoke
        │
        ▼
  result → ApiPayload in the same format and codec as the request; the client deserializes it to TResult
```

**Single args / single result** is the hard rule for every action: `TResult Action(TArgs args)` (or
`async Task<TResult>`). `JsonRpcMethod.IsResolvableAction` (Polhem.JsonRpc.Server) decides what an action name can reach.

## Build steps

Do these in order; the full code for each step is in `references/`.

### 1. Project skeleton

Web project (`Microsoft.NET.Sdk.Web`, `net10.0`). Add the packages (with central package management, versions go in
`Directory.Packages.props`):

```xml
<PackageReference Include="Polhem.JsonRpc.AspNetCore" />  <!-- the POST /api endpoint -->
<PackageReference Include="Polhem.Business" />         <!-- BO base classes -->
<PackageReference Include="Polhem.Db" />               <!-- data access / dialect -->
<PackageReference Include="Polhem.Hosting" />          <!-- AddPolhemFramework -->
<PackageReference Include="Microsoft.Data.Sqlite" />
```

Check the SQLite native bundle that `Microsoft.Data.Sqlite` pulls in for known vulnerabilities, and pin a patched
`SQLitePCLRaw.bundle_e_sqlite3` if needed; `samples/Polhem.Samples.Shared/Polhem.Samples.Shared.csproj` shows the
current choice and why.

### 2. `Program.cs` + backend bootstrap

`Program.cs` is minimal; the real wiring is wrapped in an `XxxBackend` static class (copy `DemoBackend.cs`).
**Both initialization stages are required**:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.AddXxxBackend();                       // see below
builder.Services.AddJsonRpcServer();           // on the options AddPolhemFramework registered
builder.Services.AddPolhemApiKeyGateCheck();   // startup log while no API key is issued
var app = builder.Build();
app.UseXxxBackend();                           // create the tables / seed, before the host starts
app.MapJsonRpc("/api");
app.Run();
```

The core order in `AddXxxBackend`:
```csharp
var paths = new PathOptions { DefinePath = ResolveDefinePath() };   // walk up to find Define/SystemSettings.xml
Defaults.MaterializeTo(paths.DefinePath, ...);                      // framework TableSchemas the host needs
DbProviderRegistry.Register(DatabaseType.SQLite, new SqliteProviderFactory(SqliteFactory.Instance));
DbDialectRegistry.Register(DatabaseType.SQLite, new SqliteDialectFactory());
var settings = SystemSettingsLoader.Load(paths);
SysInfo.Initialize(settings.CommonConfiguration);
builder.Services.AddPolhemFramework(settings.BackendConfiguration, paths, autoCreateMasterKey: true);
builder.Services.AddPolhemPayload(settings.CommonConfiguration.ApiPayloadOptions,   // compressor + encryptor
                                  settings.CommonConfiguration.IsDebugMode);
// Nothing else to register: ProgramSettings.xml decides which class serves each progId.
```

For the full template (with `ResolveDefinePath`, the materialized tables, master key fallback) see
`references/backend-bootstrap.md`.

### 3. No controller

`app.MapJsonRpc("/api")` of step 2 publishes `POST /api`; there is no controller to write. A check of your own on
every call is a filter: `AddJsonRpcServer(options => options.Filters.Add(new MyFilter()))`, which runs inside the
framework's checks.

### 4. The `Define/` settings tree

`Program.cs` walks up from `AppContext.BaseDirectory` to find the folder containing `SystemSettings.xml`. The role and
**minimal content** of each file are in `references/define-config.md`. Key points:
- `SystemSettings.xml` — compressor / encryptor, master key source, **`<AllowedTypeNamespaces>`** (needed when your
  own types travel in Encoded / Encrypted payloads). The body codec is **not** set here (adr-044).
- `DatabaseSettings.xml` — a `DatabaseItem` with `Id="common"` is required; point it at a SQLite file.
- `DbCategorySettings.xml`, `TableSchema/` (including the framework tables the host uses).
- `ProgramSettings.xml` — the server-side registry: progId → BO class (and repository class).
- `MenuSettings.xml` — only when a UI head shows a menu.

### 5. Login

`System.Login` works out of the box against the framework's `st_user` table: the default
`SystemBusinessObject.AuthenticateUser` verifies the password hash and reads the display name. Whatever you do about
credentials, Login also reads the user's locale from `st_user` and writes the session to `st_session`, so **both
tables must exist** (the demos materialize and create them).

To start without password hashing or user maintenance, replace the credential check only: subclass
`SystemBusinessObject`, override `AuthenticateUser(LoginArgs, out string userName)`, and bind the reserved progId
`System` to your subclass in `ProgramSettings.xml`. The resolver accepts a subclass of the framework's type for a
reserved progId. Full code in `references/business-object.md`.

### 6. Custom Business Object

A custom RPC BO inherits the **minimal `BusinessObject`** (`Polhem.Business`), **not** `FormBusinessObject` (that is
the ERP definition-driven form BO with GetList/Save/Delete; a typical app does not need it). Details in
`references/business-object.md`.

```csharp
public sealed class GameBO : BusinessObject
{
    // The factory calls Activator.CreateInstance(type, ctx, token, progId, isLocalCall),
    // and the BusinessObject base takes the same four arguments.
    public GameBO(IBusinessObjectContext ctx, Guid accessToken, string progId, bool isLocalCall = false)
        : base(ctx, accessToken, progId, isLocalCall) { }

    [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
    public GetLevelsResult GetLevels(GetLevelsArgs args) => new() { /* ... */ };
}
```
- args/result are **plain POCOs** that inherit `BusinessArgs`/`BusinessResult` (no serialization attributes).
- **Every action must be marked `[ApiAccessControl]`**, preferably on each method — methods without the attribute
  are rejected at call time (see pitfalls).
- Bind progId → BO with `BusinessObject="Ns.GameBO, Asm"` on a `ProgramItem` in `ProgramSettings.xml`. A progId with
  no binding resolves to `FormBusinessObject`; a binding that does not load fails the request rather than falling
  back. Replacing `IBoTypeResolver` in DI is possible, but a resolver that does not implement
  `Resolve(customizeId, progId)` loses the per-tenant overlay.

### 7. Client calls

The frontend only needs a reference to `Polhem.Api.Client`. **Do not use `FormApiConnector` for app actions** (it is
for ERP forms) — follow its pattern: inherit `ApiConnector` and build a **dedicated connector** that wraps each domain
action as a typed method. Details in `references/client.md`:

```csharp
public sealed class XxxApiConnector : ApiConnector
{
    public XxxApiConnector(string endpoint, Guid accessToken) : base(endpoint, accessToken) { }
    public Task<GetLevelsResponse> GetLevelsAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<GetLevelsResponse>("Game", "GetLevels", new GetLevelsRequest(), PayloadFormat.Plain, cancellationToken);
}
// Caller:
ApiClientInfo.ApiKey = "xxx-dev";       // any non-empty value passes until the deployment issues its first API key
var r = await new XxxApiConnector(endpoint, Guid.Empty).GetLevelsAsync();
```
- endpoint: desktop `http://localhost:<port>/api`; **Android emulator `10.0.2.2`**, iOS simulator `localhost`;
  cleartext must be allowed during development.
- **With `PayloadFormat.Plain`, DTOs are matched by property name**, so the client does not need the server's types.
  Encoded / Encrypted bodies name their type and are decoded into the action's parameter type, so those calls need
  the args/result types shared between server and client (see `references/client.md`).

## Pitfalls (these will stall you for a long time)

- **`AddPolhemPayload` is easy to miss**: it applies the compressor and encryptor named in `SystemSettings.xml`, and
  `AddPolhemFramework` does not read them. Miss it and the built-in defaults (gzip, aes-cbc-hmac) apply whatever the
  file says. It does **not** choose the body codec: each request declares that, and a request that declares none
  is MessagePack (adr-044, `rules/serialization.md`).
- **Every action must be marked `[ApiAccessControl]`**: `ApiAccessValidator` rejects a method with no attribute on
  itself, its base definition or its class with `UnauthorizedAccessException`. `POLHEM3001`, an analyzer that ships
  inside the `Polhem.Definition` package, reports it at build time.
- **`X-Api-Key` is required for every method except `System.Ping`.** Until the deployment has an enabled key in
  `st_api_key`, any non-empty value passes; after that the value is verified (`ApiAuthorizationValidator` remarks).
  `Authorization: Bearer` is optional at the transport; whether a method admits an anonymous caller is its
  `ApiAccessRequirement`.
- **Your own types in Encoded / Encrypted payloads**: the envelope's type name is screened against
  `AllowedTypeNamespaces` (`|`-separated), **including the assembly name**, which must equal or start with an
  allowed entry. Listing the assembly's root namespace (for example `Xxx.Server`) covers both when they share it.
- **Your own types over MessagePack on iOS**: the framework registers formatters only for its own wire types
  (`src/Polhem.Api.Core/CLAUDE.md`). A host type goes through the contractless resolver, which needs dynamic code, and
  .NET for iOS has none. Use `PayloadFormat.Plain` for Public actions, or set the connector's `PayloadCodec` to
  `PayloadCodecNames.Json`.
- **A binding in `ProgramSettings.xml` that does not load fails the request** (`ProgramSettingsBoTypeResolver`
  remarks). It does not fall back to `FormBusinessObject`.
- **Define is located by walking up**: it is not copied to output by default, so run from inside the checkout
  (`dotnet run`), or set `CopyToOutputDirectory` on Define yourself.
- **master key**: in dev you can use `autoCreateMasterKey: true` + the environment variable `POLHEM_MASTER_KEY` (a
  fixed value keeps encrypted rows decryptable across runs); in production a real key must be injected by the
  deployment mechanism.
- **Trimming**: the supported configurations are untrimmed and the SDK's default partial trim. `TrimMode=full` and
  NativeAOT are unsupported, and `POLHEM9004` warns about them (`rules/apple-mobile-trim.md`).

## Verification

1. `dotnet run --project <server>` (note the dev port).
2. Call `System.Ping` + your first action. For a Public action a `curl` with a Plain body works
   (`samples/QuickStart.Server/README.md` has one); a small console app (`Polhem.Api.Client`, see the probe at the
   end of `references/client.md`) also covers Encoded / Encrypted calls, whose envelope is impractical to build by
   hand.
3. Then connect the real frontend / mobile head.

## references/

- `backend-bootstrap.md` — full template for `Program.cs` + `XxxBackend` (walk-up, materialize, master key).
- `define-config.md` — the role of each Define XML file + minimal content you can paste.
- `business-object.md` — full code for the BO / args / result / the custom credential check.
- `client.md` — how to call `Polhem.Api.Client`, endpoints, the probe, and deserialization notes for mobile.
