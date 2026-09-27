---
name: polhem-add-bo-method
description: The full procedure for adding a publicly exposed BO method (FormBusinessObject / SystemBusinessObject) to polhem, spanning 7-8 files across contract / wire / BO / Repository / Client. Includes 3 hard rules, per-layer templates, two-layer round-trip tests and a final checklist. Use when the user wants to "add a BO method", "add an API for progId.action", "add a method to FormBO/SystemBO", "implement GetList / Insert / Update / Delete", or similar requests.
---

# polhem: add a BO method

polhem uses a JSON-RPC 2.0 + BO two-track architecture. Adding one publicly exposed BO method
(such as `FormBusinessObject.GetList` or `SystemBusinessObject.GetDefine`) crosses
4 layers and **7-8 files**. Naming, file locations, `[Key]` numbering and DI registration all follow fixed conventions.
This skill fixes that path in writing so it does not have to be rediscovered every time.

> Templates to compare against (keep them open while reading code):
> - System axis: `SystemBusinessObject.GetDefine`
> - Form axis: `FormBusinessObject.GetList`

## Hard rules (must not be violated)

These 3 rules are the project's architectural baseline. Violating one is a design error, not "another way to write it".

### Rule 1: a BO must never access `Polhem.Db` directly

- `Polhem.Business.csproj` does **not** depend on `Polhem.Db` (dependency-map.md shows
  `Business --> Contracts / Definition / RepoAbs`, with no Db)
- FormSchema-driven SELECT / INSERT / UPDATE / DELETE **must** run through
  `IDataFormRepository` (the Repository abstraction)
- A BO is a "thin shell": unpack args, call the Repository, assemble the Result.
  Logic such as the `FormSchema.MasterTable` / `ListFields` fallback lives in the Repository and is **not** repeated in the BO
- A PR that adds a `Polhem.Db` ProjectReference to `Polhem.Business.csproj` → design error

Reason: it decouples business logic from data access. Swapping the ORM, adding sharding, adding caching or substituting
in unit tests all need this abstraction. The "FormBusinessObject →
IFormCommandBuilder → DbAccess" chain in development-cookbook.md describes **logical layering** (the BO triggers, the
Repository executes). It is not a ProjectReference path.

### Rule 2: the ProgId is the master table name; do not pass `TableName`

- Framework invariant: `FormSchema.MasterTable.TableName == ProgId`
  (see `FormSchema.MasterTable`, which takes `Tables[ProgId]`)
- BO method args, wire DTOs and Repository signatures do **not** carry a `TableName` property
- Inside the Repository, pass `_schema.ProgId` directly as the tableName to `SelectCommandBuilder.Build(...)`;
  no `MasterTable?.TableName` fallback is needed
- Listing queries on child tables (detail / aux table) belong to a separate method (such as `GetDetailList`).
  Do not add a `TableName` field to the master method for them

Reason: it keeps the contract surface small, and avoids making the common master query carry an extra field just to
support a rare child-table query.

### Rule 3: split action constant classes by BO axis

| BO axis | Constant class | Examples |
|-------|---------|------|
| `SystemBusinessObject` | `Polhem.Definition.SystemActions` | `Ping` / `Login` / `GetDefine` |
| `FormBusinessObject` | `Polhem.Definition.FormActions` | `GetList` |
| Future axis | `Polhem.Definition.<Axis>Actions` | — |

- The name `SystemActions` is bound to `SystemBusinessObject`. Putting FormBO actions in it
  blurs "which kind of BO handles this action", and clutters IntelliSense
- Putting a single action into another axis's class to "save a file" → design error
- A client connector references the constant class of its own axis (`FormApiConnector` uses `FormActions`,
  not `SystemActions`)
- **The only exception**: `ExecFunc` / `ExecFuncAnonymous` are methods on the base `BusinessObject` and belong to no axis,
  so referencing `SystemActions.ExecFunc` is a historically reasonable choice. (There used to be `ExecFuncLocal` as well.
  The BO-side method switched to the `[ExecFuncAccessControl(LocalOnly = true)]` mechanism on
  2025-10-03, and the constant and connector method were removed on 2026-08-07)

## Overall flow (4 layers, 7-8 files)

| # | Layer | File | Convention |
|---|----|------|------|
| 1 | Contract | `src/Polhem.Api.Contracts/<Axis>/I<Action>Request.cs` | Pure interface, no attribute; namespace is `Polhem.Api.Contracts.<Axis>` |
| 2 | Contract | `src/Polhem.Api.Contracts/<Axis>/I<Action>Response.cs` | Pure interface, no attribute |
| 3 | Wire DTO | `src/Polhem.Api.Core/Messages/<Axis>/<Action>Request.cs` | `[MessagePackObject]` + `[Key(n)]`, inherits `ApiRequest` |
| 4 | Wire DTO | `src/Polhem.Api.Core/Messages/<Axis>/<Action>Response.cs` | `[MessagePackObject]` + `[Key(n)]`, inherits `ApiResponse` |
| 5 | Action constant | `src/Polhem.Definition/<Axis>Actions.cs` | `public const string <Action> = "<Action>"` |
| 6 | BO Args | `src/Polhem.Business/<Axis>/<Action>Args.cs` | POCO, inherits `BusinessArgs`, implements `I<Action>Request` |
| 7 | BO Result | `src/Polhem.Business/<Axis>/<Action>Result.cs` | POCO, inherits `BusinessResult`, implements `I<Action>Response` |
| 8 | BO method | New method in `src/Polhem.Business/<Axis>/<Axis>BusinessObject.cs` | `[ApiAccessControl(...)]`, returns `<Action>Result` |
| 8b | BO interface declaration | Add the method signature to `src/Polhem.Business/<Axis>/I<Axis>BusinessObject.cs` | Lets other BOs use it through `IBusinessObjectFactory` |
| (9) | Repository (only for FormSchema-driven CRUD) | `IDataFormRepository.cs` + `DataFormRepository.cs` + `RepositoryFactory.cs` | Follows rule 1 |
| (10) | Client | `src/Polhem.Api.Client/Connectors/<Axis>ApiConnector.cs` | `<Action>Async` + synchronous wrapper |

`<Axis>` = `System` or `Form` (or a future axis).

## P0 exploration list (read-only, no code changes)

Answer these 3 questions before adding the method:

1. **Which services the BO needs** (DefineAccess / Repository / other)
   - `BusinessObject._ctx` already exposes `DefineAccess`, `SessionInfoService`,
     `BoFactory` and `Services` (the IServiceProvider escape hatch)
   - Other services (such as `IRepositoryFactory`) go through `Services.GetRequiredService<T>()`.
     Do not widen the public `IBusinessObjectContext` signature for a single method
   - Do **not** "call `IFormCommandBuilder` / `DbAccess` directly", which violates rule 1

2. **Whether the parameter types can be serialized across the wire**
   - Primitive types, `string` and `DataTable` all have MessagePack support
   - A polymorphic union (such as `FilterNode` → `FilterCondition` / `FilterGroup`) must have
     `[MessagePackObject]` + `[Union(n, typeof(T))]` on the abstract base class
   - Collections are handled by `CollectionBaseFormatter<,>` in
     `Polhem.Api.Core/MessagePack/MessagePackCodec.cs:26`. You **must** go through the public
     `MessagePackCodec` / `MessagePackPayloadSerializer` API. Do not call
     `MessagePackSerializer.Serialize(obj, ContractlessStandardResolver.Options)` directly
     (it drops collection elements without any error)

3. **Whether `ApiContractRegistry.Register<>()` is needed**
   - **Not needed by default.** The naming-convention reflection `XxxResult` → `XxxResponse` in
     `Polhem.Api.Core/Conversion/ApiOutputConverter.cs:74-86` (limited to the `Polhem.Api.Core` assembly)
     performs the deep copy from BO Result to wire Response automatically
   - `SysInfo.AllowedTypeNamespaces` (`Polhem.Base/SysInfo.cs:49`) already includes
     `Polhem.Api.Core` / `Polhem.Business` / `Polhem.Definition` by default
   - Exception: explicit registration is needed only when the BO Result name breaks the `XxxResult` convention (rare)

## Writing guide per layer

### Layer 1: contract interface (`Polhem.Api.Contracts`)

```csharp
namespace Polhem.Api.Contracts
{
    /// <summary>Contract interface for the <Action> request.</summary>
    public interface I<Action>Request
    {
        // Read-only properties + complete XML doc
        SomeProperty { get; }
    }
}
```

- The interface is **not** marked `[MessagePackObject]` and contains **no** implementation
- `Polhem.Api.Contracts.csproj` already references `Polhem.Definition`, so definition types such as
  `FilterNode` / `SortFieldCollection` / `DefineType` can be used directly
- XML doc is in **English** (public NuGet repository convention)

### Layer 2: wire DTO (`Polhem.Api.Core/Messages/<Axis>/`)

```csharp
[MessagePackObject(keyAsPropertyName: true)]
public class <Action>Request : ApiRequest, I<Action>Request
{
    public string Foo { get; set; } = string.Empty;
    public Bar? Bar { get; set; }
}
```

- **Do not number `[Key(n)]`**: since adr-030, ordinary types always use `keyAsPropertyName: true`, so the key is the
  property name and adding / removing fields needs no numbering upkeep. **The only exception is a `[Union]` polymorphic
  hierarchy** (such as `FilterNode`), which is incompatible with `keyAsPropertyName` and keeps integer `[Key]`
  permanently (see `rules/serialization.md`)
- Align names with the contract interface: `<Action>Request` ↔ `<Action>Response`
- Properties must be `{ get; set; }` (MessagePack needs the setter); the interface is read-only
- Folder and namespace must match: `Messages/Form/` ↔ `namespace Polhem.Api.Core.Messages.Form`
  (IDE0130; a violation fails the strict build)

### Layer 3: action constant (`Polhem.Definition/<Axis>Actions.cs`)

```csharp
public static class <Axis>Actions
{
    /// <summary><Description of the action></summary>
    public const string <Action> = "<Action>";
}
```

- One file per axis (rule 3)
- Aligns with the JSON-RPC `method` field: `"<progId>.<Action>"`

### Layer 4: BO Args / Result (`Polhem.Business/<Axis>/`)

```csharp
public class <Action>Args : BusinessArgs, I<Action>Request
{
    public string Foo { get; set; } = string.Empty;
    // Same fields as the wire DTO, but without [Key]
}

public class <Action>Result : BusinessResult, I<Action>Response
{
    public DataTable? Table { get; set; }
}
```

- BO POCOs carry no MessagePack attribute (through the `XxxResult` → `XxxResponse` naming convention,
  the wire side's `[MessagePackObject]` takes over when `ApiOutputConverter` reflects)
- Field names must match the wire DTO (`ApiInputConverter.Convert` copies properties by reflection;
  a mismatched name means that field is silently lost)

### Layer 5: BO method (goes through the Repository, obeys rule 1)

```csharp
/// <summary><English description of the method></summary>
/// <remarks>
/// (Where needed, state hard preconditions of use, e.g. GetList requires the caller to supply a Filter that narrows the result)
/// </remarks>
[ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
public virtual <Action>Result <Action>(<Action>Args args)
{
    ArgumentNullException.ThrowIfNull(args);

    // BusinessObject already has convenience methods that pass in the current AccessToken and ProgId:
    //   CreateDataFormRepository(ProgId)        → the generic IDataFormRepository
    //   CreateFormRepository<IOrderRepository>() → the dedicated interface bound in the registry
    var repository = CreateDataFormRepository(ProgId);
    var result = repository.<DoWork>(args.X, args.Y, ...);

    return new <Action>Result { /* assemble from the repository result */ };
}
```

- `virtual` lets the host override it (multi-tenancy, specialised logic)
- `[ApiAccessControl]` choices: `Public/Encoded/Encrypted` × `Anonymous/Authenticated`
- The BO does not reimplement fallback logic (fallback belongs to the Repository)

### Layer 8b: BO interface declaration (`I<Axis>BusinessObject.cs`)

A public BO method has two usage scenarios:

1. **API call**: dispatched by `JsonRpcExecutor` reflecting on `progId.action`. **This only needs a `public` method + `[ApiAccessControl]` on the BO class**; no interface declaration is required
2. **Cross-BO call**: another BO obtains an instance through `IBusinessObjectFactory.CreateXxxBusinessObject(...)` and calls the method directly. The caller then holds an `I<Axis>BusinessObject` → **the method must be on the interface** to be callable

**Convention: a method meant for the API is, by default, also opened to other BOs.** Add the matching signature to `I<Axis>BusinessObject`.

```csharp
// src/Polhem.Business/Form/IFormBusinessObject.cs
public interface IFormBusinessObject : IBusinessObject
{
    /// <summary><English description of the method></summary>
    /// <param name="args">The input arguments.</param>
    <Action>Result <Action>(<Action>Args args);
}
```

- Interface methods are **not** marked `[ApiAccessControl]` (the BO class carries the attribute)
- The signature is identical to the BO implementation (C# matches by signature automatically)
- The existing `CreateSession` / `GetDefine` / `SaveDefine` on `ISystemBusinessObject` are the templates
- Exception: methods that are API-only and not suitable for other BOs to call directly (such as the `Ping` health check, `Login`) may stay off the interface; this is a human judgement call

#### How to write a cross-BO call

`IBusinessObjectFactory` lives in `Polhem.Definition` (lower layer) and `I<Axis>BusinessObject` in `Polhem.Business` (upper layer). To avoid a reverse `Polhem.Definition → Polhem.Business` dependency, every factory method is declared to return `object`.

**When the caller is in `Polhem.Business` or a higher layer**, use the typed wrappers provided by `BusinessObjectFactoryExtensions` (in `Polhem.Business`) to get the interface directly, without casting yourself:

```csharp
// Inside another BO method (recommended)
var formBo = _ctx.BoFactory.CreateFormBO(AccessToken, "Employee");
var listResult = formBo.GetList(new GetListArgs { /* ... */ });

var systemBo = _ctx.BoFactory.CreateSystemBO(AccessToken);
var defineResult = systemBo.GetDefine(new GetDefineArgs { /* ... */ });
```

Each axis maps to one fixed interface (`IFormBusinessObject` / `ISystemBusinessObject`), and instances are told apart at runtime by `progId`. There is no "specialised BO interface" design, so no generic overloads are needed.

**When the caller is in a project that cannot reference `Polhem.Business`** (for example the internal dispatch path in `Polhem.Api.Core`), still call `IBusinessObjectFactory.CreateXxxBusinessObject(...)` directly, get an `object`, and cast:

```csharp
var rawBo = factory.CreateBusinessObject(token, progId);
// In this situation it is usually not cast to IFormBusinessObject (that would pull in a Polhem.Business dependency);
// it is operated on through reflection / dynamic dispatch instead.
```

- The design rationale is in the XML doc of `Polhem.Definition/IBusinessObjectFactory.cs`
- A failed cast throws `InvalidCastException`; make sure the host registers the correct BO type
- Do not move `IBusinessObjectFactory` into `Polhem.Business` to "avoid the cast"; that would invert the whole dependency topology
- Do not add a generic method to `IBusinessObjectFactory` through a default interface method (such as `T CreateForm<T>()`). The constraint cannot reference `IFormBusinessObject`, so type safety rests only on `where T : class`, and making callers write `<IFormBusinessObject>` explicitly is actually more error-prone (`FormBO` always implements `IFormBusinessObject`; that mapping should be fixed in the helper, not re-specified by the caller)

### Layer 9 (only for FormSchema-driven CRUD): extending the Repository abstraction

1. **`Polhem.Repository.Abstractions/Form/IDataFormRepository.cs`**: add the method signature
   - Parameter types are limited to `Polhem.Definition.*` (RepoAbs depends only on Definition)
   - It does **not** accept `Polhem.Business.*` types (that would be a reverse dependency)
2. **`Polhem.Repository/Form/DataFormRepository.cs`**: the implementation (this layer may use `Polhem.Db`)
   - The ctor takes `FormSchema schema`, `IDefineAccess defineAccess`,
     `IDbAccessFactory dbAccessFactory`, `IDbConnectionManager connectionManager`,
     `string databaseId`
   - Get the dialect: `DbDialectRegistry.Get(connInfo.DatabaseType)
     .CreateFormCommandBuilder(_schema, _defineAccess)`
   - Pass `_schema.ProgId` directly as the tableName (rule 2)
   - Execute: `_dbAccessFactory.Create(_databaseId).Execute(spec)`
3. **`Polhem.Repository/Factories/RepositoryFactory.cs`** already has the needed services injected
   - `CreateFormRepository<T>(accessToken, progId)` resolves the type from the registry's `ProgramItem.Repository`
     (`DataFormRepository` when unspecified); the schema's `CategoryId` determines the databaseId
   - **If the type cannot be loaded, it always throws**, the opposite of the BO axis's silent degradation: data access has no harmless degraded mode
4. The `IRepositoryFactory` registration in **`Polhem.Hosting/PolhemFrameworkServiceCollectionExtensions.cs`**
   **must** use `CreateConfigurableService`
   (DI-aware), not `CreateOrDefault` (parameterless). If you change the Factory ctor signature
   and forget to switch it → runtime InvalidOperationException

### Layer 10: client connector (`Polhem.Api.Client/Connectors/<Axis>ApiConnector.cs`)

```csharp
public async Task<<Action>Response> <Action>Async(
    <param1> p1 = default!, ..., <paramN>? pN = null)
{
    var request = new <Action>Request { /* property = parameter */ };
    return await ExecuteAsync<<Action>Response>(<Axis>Actions.<Action>, request)
        .ConfigureAwait(false);
}
```

- **Async only; there are no synchronous overloads.** Every connector method returns `Task`; callers that need a
  synchronous call handle it themselves. (Rechecked 2026-08-06: the number of synchronous overloads in
  `SystemApiConnector` / `FormApiConnector` is 0 in both files)
- `ExecuteAsync<TResponse>` defaults to `PayloadFormat.Encrypted`; override it at this layer only when the BO method is
  marked `ApiProtectionLevel.Encoded`
- Exception propagation: JsonRpcExecutor treats `ArgumentException /
  InvalidOperationException / NotSupportedException / FormatException /
  JsonRpcException` thrown by a BO as user-facing and turns them into an RpcError carrying the original message. Other
  system exceptions (including NRE and IO) are all collapsed into `"Internal server error"`

## Tests (three layers of coverage)

| Layer | Path / template | What it verifies | What it does not verify |
|----|------------|---------|--------|
| Wire-level | `tests/Polhem.Api.Core.UnitTests/<Axis>/<Action>MessagePackTests.cs` | `[Key]` numbering / union / collection / DataTable serialization | BO behaviour, SQL |
| Executor dispatch | `tests/Polhem.Api.Core.UnitTests/<Axis>/<Action>JsonRpcRoundTripTests.cs` | `progId.action` reflection dispatch, Input/Output Converter | Actual SQL results |
| BO+DB integration | `tests/Polhem.Business.UnitTests/<Axis>/<Axis>BusinessObject<Action>Tests.cs` | BO → Repository → SQL → correctness against real data | Wire format |

Only the three layers together amount to "works end to end". Do **not** write a single "executor + real DB" integration
test: it is over-coupled and duplicates the responsibilities of the existing layers.

### Wire-level test template

```csharp
public class <Action>MessagePackTests
{
    [Fact]
    public void <Action>Request_RoundTrip_PreservesUnion()
    {
        var request = new <Action>Request { /* fully populated */ };
        var bytes = MessagePackCodec.Serialize(request);
        var restored = MessagePackCodec.Deserialize<<Action>Request>(bytes);
        // Assert union / collection / DataTable round-trip
    }

    [Fact]
    public void <Action>Request_DefaultValues_RoundTrip() { /* confirm a null collection does not NRE */ }
}
```

### Executor dispatch test template (stub Repository)

```csharp
public class <Action>JsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
{
    [Fact]
    public void <Action>_ThroughJsonRpc_DispatchesAndReturnsTable()
    {
        var stubRepo = new StubDataFormRepository(/* fixed DataTable */);
        var stubFactory = new StubRepositoryFactory(stubRepo);
        var overrideServices = new TestOverrideServiceProvider(
            _fx.Provider,
            (typeof(IRepositoryFactory), stubFactory));

        var boFactory = new BusinessObjectFactory(
            overrideServices,
            _fx.GetRequiredService<IDefineAccess>(),
            _fx.GetRequiredService<ISessionInfoService>(),
            _fx.GetRequiredService<ILanguageService>(),
            _fx.GetRequiredService<IBoTypeResolver>());

        var executor = new JsonRpcExecutor(
            boFactory,
            _fx.GetRequiredService<IAccessTokenValidator>(),
            _fx.GetRequiredService<IApiEncryptionKeyProvider>())
        {
            AccessToken = TestSessionFactory.CreateAccessToken(_fx),
            IsLocalCall = true,
        };

        var request = new JsonRpcRequest
        {
            Method = $"<ProgId>.{<Axis>Actions.<Action>}",
            Params = new JsonRpcParams { Value = new <Action>Request { /* ... */ } },
            Id = Guid.NewGuid().ToString(),
        };

        var response = executor.Execute(request);
        Assert.Null(response.Error);
        var result = Assert.IsType<<Action>Response>(response.Result!.Value);
        // ... assertions on result + stub.LastXxx
    }
}
```

### BO + DB integration test template

```csharp
public class <Axis>BusinessObject<Action>Tests : IClassFixture<SharedDbFixture>
{
    [DbFact(DatabaseType.SQLite)]
    public void <Action>_Sqlite_<Scenario>()
    {
        // Get the test databaseId with TestDbConventions.GetDatabaseId(dbType, categoryId)
        // Construct a DataFormRepository and inject it into the BO Context with TestOverrideServiceProvider
        // Seed test data with InsertCommandBuilder; clean up in try/finally
        // Call bo.<Action>(args) and assert on the returned DataTable
    }
}
```

- In the test environment the databaseId is `{categoryId}_{dbtype}` (such as `company_sqlite`),
  which differs from production, where the `CategoryId` is used directly as the databaseId. Tests must pass the
  test databaseId explicitly (do not rely on the production Factory's resolution)
- Seeding and cleanup follow the try/finally + GUID RowId pattern of `EmployeeBuildSelectIntegrationTests`;
  use `InsertCommandBuilder` / `DeleteCommandBuilder` to seed data across dialects

## Common pitfalls

Easy to hit while writing, and always blocked by CI:

1. **Adding a `Polhem.Db` ProjectReference to `Polhem.Business` = violates rule 1**: it means the BO calls Db
   directly; go through the Repository abstraction instead
2. **Folder and namespace mismatch (IDE0130)**: `Messages/Form/` must map to
   `namespace Polhem.Api.Core.Messages.Form`
3. **Duplicate Key numbers**: `[Key(n)]` must not repeat within one DTO, including the base class; start at 100
4. **BO Result name breaks the `XxxResult` convention**: `ApiOutputConverter` reflection misses it.
   Either rename it or add `ApiContractRegistry.Register<I, T>()` in
   `Polhem.Hosting.AddPolhemFramework`
5. **Polymorphic union without `[Union]`**: on MessagePack deserialization every collection element becomes
   null; you must add `[Union(0, typeof(ConcreteA))]` and so on to the abstract base class
6. **Calling `MessagePackSerializer.Serialize(..., ContractlessStandardResolver.Options)` directly and
   bypassing the framework**: collection elements are all lost and no error is thrown. Always go through
   `MessagePackCodec` / `MessagePackPayloadSerializer`
7. **`ApiInputConverter` matches by property name**: if BO Args property names do not match the wire DTO →
   that field is silently lost, with no compile error
8. **Types outside the `SysInfo.AllowedTypeNamespaces` allowlist**: cross-wire deserialization is blocked by
   `SafeTypelessFormatter`; types in `Polhem.Api.Core` / `Polhem.Business` /
   `Polhem.Definition` are the safest
9. **`RepositoryFactory` ctor signature changes → the DI registration must change with it**:
   `CreateOrDefault` (parameterless) → `CreateConfigurableService` (DI-aware)
10. **Delete existing trivial POCO property tests outright when a ctor signature widens**: when a test is only a
    setter check like `new T(progId).ProgId == progId`, keeping it after a refactor costs upkeep and
    adds no real coverage; the integration tests carry the coverage
11. **`[DbFact]` only looks at environment variables and does not check that the DB is reachable**: if `.runsettings`
    sets `POLHEM_TEST_CONNSTR_SQLSERVER` but the SQL Server container is not running → the test fails
    instead of skipping. SQLite in-memory does not have this problem (no external dependency)
12. **Process-wide statics race between test classes**: once `SysInfo.TraceListener` is pointed at a capture writer by
    one test class, Tracer events from every test class running in parallel are written to
    that writer. From then on use a thread-safe container such as `ConcurrentQueue<T>`, not
    `List<T>`
13. **`CollectionBaseFormatter` behaviour on a null collection**: a bug once made
    `SortFieldCollection? = null` NRE on serialization; fixed on 2026-05-14 so that nil in gives nil
    out. Before adding a nullable collection field, confirm the fix is still in place
14. **The `FormApiConnector` ExecFunc family references `SystemActions`**: a historically reasonable choice
    (`ExecFunc` is a base BO method and belongs to no axis). This is the **only** exception to rule 3.
    New FormBO-specific methods always use `FormActions.<Action>`

## Full checklist (all layers combined)

When adding an `<Action>` method, complete every step below in order.

**P0 exploration (read-only)**:
- [ ] Confirm how the BO obtains the services it needs (Repository abstraction / `Services.GetRequiredService<T>()`)
- [ ] Serialization capability: primitive types / collection / union all walked through
- [ ] `ApiContractRegistry` needs no explicit registration (unless the BO Result name breaks the `XxxResult` convention)

**P1 contract layer** (single PR / single commit):
- [ ] `src/Polhem.Api.Contracts/<Axis>/I<Action>Request.cs`
- [ ] `src/Polhem.Api.Contracts/<Axis>/I<Action>Response.cs`
- [ ] `src/Polhem.Api.Core/Messages/<Axis>/<Action>Request.cs`
- [ ] `src/Polhem.Api.Core/Messages/<Axis>/<Action>Response.cs`
- [ ] Add `public const string <Action>` to `src/Polhem.Definition/<Axis>Actions.cs`
- [ ] Release build 0w/0e

**P2 BO + Repository** (single PR / single commit):
- [ ] `src/Polhem.Business/<Axis>/<Action>Args.cs`
- [ ] `src/Polhem.Business/<Axis>/<Action>Result.cs`
- [ ] Add the method to `src/Polhem.Business/<Axis>/<Axis>BusinessObject.cs` (through the Repository)
- [ ] Add the method signature to `src/Polhem.Business/<Axis>/I<Axis>BusinessObject.cs` (unless the method is API-only and not suitable for other BOs to call directly)
- [ ] Add the abstract method to `src/Polhem.Repository.Abstractions/Form/IDataFormRepository.cs`
- [ ] Implement it in `src/Polhem.Repository/Form/DataFormRepository.cs`
- [ ] Inject new dependencies into `src/Polhem.Repository/Factories/RepositoryFactory.cs` (if any)
- [ ] If the ctor signature changes, switch the DI registration in `src/Polhem.Hosting/...` to `CreateConfigurableService`
- [ ] BO integration test: `tests/Polhem.Business.UnitTests/<Axis>/<Axis>BusinessObject<Action>Tests.cs`
- [ ] Release build 0w/0e + SQLite tests pass

**P3 Client + wire tests** (single PR / single commit):
- [ ] Add `<Action>Async` to `src/Polhem.Api.Client/Connectors/<Axis>ApiConnector.cs` (async only)
- [ ] Wire-level round-trip test
- [ ] Executor dispatch round-trip test
- [ ] Release build 0w/0e + tests pass

**P4 surface sync** (in the same commit as either P2 or P3):
- [ ] Update `docs/en/api-method-reference.md` + `docs/zh-TW/api-method-reference.md`: add / change the method in the table for its axis
- [ ] Update the `ExpectedSurface` baseline in `tests/Polhem.Business.UnitTests/BoApiSurfaceTests.cs` (additions, removals and `[ApiAccessControl]` changes all require it)
- [ ] BoApiSurfaceTests pass (verifies that the baseline matches the actual reflection result)

**Commit + push**:
- [ ] Commit and push following `.claude/rules/pull-request.md` (branch + pull request workflow), then wait for CI to finish and report
- [ ] On failure, handle it by the process in `.claude/rules/pull-request.md` (if clearly fixable, fix it directly, commit, push)

## Reference files (keep them open while reading code)

| Purpose | File |
|------|------|
| Contract interface template | `src/Polhem.Api.Contracts/System/IGetDefineRequest.cs` / `IGetDefineResponse.cs` (folders by axis: `System/` `Form/` `AuditLog/`) |
| Wire DTO template | `src/Polhem.Api.Core/Messages/System/GetDefineRequest.cs` / `GetDefineResponse.cs` |
| Action constant template | `src/Polhem.Definition/SystemActions.cs` / `FormActions.cs` |
| BO Args/Result template | `src/Polhem.Business/System/GetDefineArgs.cs` / `GetDefineResult.cs` |
| BO method template | `src/Polhem.Business/System/SystemBusinessObject.cs` (`GetDefine`) / `src/Polhem.Business/Form/FormBusinessObject.cs` (`GetList`) |
| Repository abstraction / implementation | `src/Polhem.Repository.Abstractions/Form/IDataFormRepository.cs` / `src/Polhem.Repository/Form/DataFormRepository.cs` |
| Repository Factory | `src/Polhem.Repository/Factories/RepositoryFactory.cs` |
| Client connector | `src/Polhem.Api.Client/Connectors/SystemApiConnector.cs` / `FormApiConnector.cs` |
| JSON-RPC dispatch | `src/Polhem.Api.Core/JsonRpc/JsonRpcExecutor.cs` |
| Naming-convention reflection | `src/Polhem.Api.Core/Conversion/ApiOutputConverter.cs` |
| BO integration test template | `tests/Polhem.Business.UnitTests/Form/FormBusinessObjectGetListTests.cs` |
| Wire round-trip template | `tests/Polhem.Api.Core.UnitTests/Form/GetListMessagePackTests.cs` |
| Executor dispatch template | `tests/Polhem.Api.Core.UnitTests/Form/GetListJsonRpcRoundTripTests.cs` |
| Single-page API method overview | `docs/en/api-method-reference.md` (must be updated for every new method) |
| Surface audit test | `tests/Polhem.Business.UnitTests/BoApiSurfaceTests.cs` |
