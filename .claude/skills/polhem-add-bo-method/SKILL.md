---
name: polhem-add-bo-method
description: The full procedure for adding a publicly exposed BO method (FormBusinessObject / SystemBusinessObject) to polhem, spanning the contract, wire, BO, Repository and client layers. Includes 3 hard rules, per-layer templates, explicit wire registration, the surface gates, layered round-trip tests and a final checklist. Use when the user wants to "add a BO method", "add an API for progId.action", "add a method to FormBO/SystemBO", "implement GetList / Insert / Update / Delete", or similar requests.
---

# polhem: add a BO method

polhem uses a JSON-RPC 2.0 + BO two-track architecture. Adding one publicly exposed BO method
(such as `FormBusinessObject.GetList` or `SystemBusinessObject.GetDefine`) crosses the contract, wire, BO, Repository
and client layers. Naming, file locations, wire registration and DI registration all follow fixed conventions, and
several of them are enforced by tests rather than by the compiler. This skill fixes that path in writing so it does
not have to be rediscovered every time.

> Templates to compare against (keep them open while reading code):
> - System axis: `SystemBusinessObject.GetDefine`
> - Form axis: `FormBusinessObject.GetList`

## Hard rules (must not be violated)

These 3 rules are the project's architectural baseline. Violating one is a design error, not "another way to write it".

### Rule 1: a BO must never access `Polhem.Db` directly

- `Polhem.Business.csproj` does **not** reference `Polhem.Db` (the allowed edges are in `docs/en/architecture/dependency-map.md`)
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
- Inside the Repository, pass `ProgId` directly as the table name to the command builder;
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
| `AuditLogBusinessObject` | `Polhem.Definition.AuditLogActions` | — |
| Future axis | `Polhem.Definition.<Axis>Actions` | — |

- The name `SystemActions` is bound to `SystemBusinessObject`. Putting FormBO actions in it
  blurs "which kind of BO handles this action", and clutters IntelliSense
- Putting a single action into another axis's class to "save a file" → design error
- A client connector references the constant class of its own axis (`FormApiConnector` uses `FormActions`,
  not `SystemActions`)
- **The only exception**: `ExecFunc` / `ExecFuncAnonymous` are methods on the base `BusinessObject` and belong to no axis,
  so their constants live in `SystemActions` and every axis references them from there. (There used to be `ExecFuncLocal`
  as well. The BO-side method switched to the `[ExecFuncAccessControl(LocalOnly = true)]` mechanism on
  2025-10-03, and the constant and connector method were removed on 2026-08-07)
- `ActionSurfaceTests` (tests/Polhem.Business.UnitTests/Contracts) checks that every `*Actions` constant and the
  public methods of the matching BO correspond one to one. A new axis needs a row in its `s_axes` table.

## Overall flow

| # | Layer | File | Convention |
|---|----|------|------|
| 1 | Contract | `src/Polhem.Api.Contracts/<Axis>/I<Action>Request.cs` | Pure interface, no attribute; namespace `Polhem.Api.Contracts.<Axis>` |
| 2 | Contract | `src/Polhem.Api.Contracts/<Axis>/I<Action>Response.cs` | Pure interface, no attribute |
| 3 | Wire DTO | `src/Polhem.Api.Core/Messages/<Axis>/<Action>Request.cs` | Sealed POCO, no attributes, inherits `ApiRequest`, implements `I<Action>Request` |
| 4 | Wire DTO | `src/Polhem.Api.Core/Messages/<Axis>/<Action>Response.cs` | Sealed POCO, no attributes, inherits `ApiResponse`, implements `I<Action>Response` |
| 5 | Wire registration | `src/Polhem.Api.Core/MessagePack/WireContracts.<Axis>.cs` | One `WireContract.For<T>()` block per DTO, listing every member |
| 6 | Wire contract file | `wire-contracts/messages.d.ts` (+ `type-names.ts`) | Regenerated, not hand-edited (see its README) |
| 7 | Action constant | `src/Polhem.Definition/<Axis>Actions.cs` | `public const string <Action> = "<Action>"` |
| 8 | BO Args | `src/Polhem.Business/<Axis>/<Action>Args.cs` | Sealed POCO, inherits `BusinessArgs`, implements `I<Action>Request` |
| 9 | BO Result | `src/Polhem.Business/<Axis>/<Action>Result.cs` | Sealed POCO, inherits `BusinessResult`, implements `I<Action>Response` |
| 10 | BO method | New method on the axis BO (`src/Polhem.Business/<Axis>/<Axis>BusinessObject*.cs`, a partial class split by concern) | `[ApiAccessControl(...)]`, returns `<Action>Result` |
| (10b) | BO interface declaration | `src/Polhem.Business/<Axis>/I<Axis>BusinessObject.cs` | Only when server-side code calls the method (see Layer 8b) |
| (11) | Repository (only for FormSchema-driven CRUD) | `IDataFormRepository.cs` + `DataFormRepository*.cs` | Follows rule 1 |
| (12) | Client | `src/Polhem.Api.Client/Connectors/<Axis>ApiConnector.cs` | `<Action>Async`, async only |
| 13 | Surface sync | `tests/Polhem.Business.UnitTests/BoApiSurfaceTests.cs` + `docs/<lang>/api/api-method-reference.md` | Baseline and reference updated together |

`<Axis>` = `System`, `Form` or `AuditLog` (or a future axis).

## P0 exploration list (read-only, no code changes)

Answer these 3 questions before adding the method:

1. **Which services the BO needs** (DefineAccess / Repository / other)
   - `BusinessObject` already exposes `DefineAccess`, `SessionInfoService`, `LanguageService`,
     `BoFactory` and `Services` (the `IServiceProvider` escape hatch) as protected members
   - Form repositories come from `CreateDataFormRepository(progId)` / `CreateFormRepository<T>()`; other services go
     through `Services.GetRequiredService<T>()`.
     Do not widen the public `IBusinessObjectContext` signature for a single method
   - Do **not** "call `IFormCommandBuilder` / `DbAccess` directly", which violates rule 1

2. **Which types the DTO puts on the wire, and whether each one is registered**
   - Primitives, `string`, `DataTable`, `DataSet` and the framework's existing definition types and collections are
     already registered
   - **Every other type the DTO reaches must be registered explicitly** (a new definition type, a new collection, a
     new closed generic such as `List<T>` or `T?`, a new enum). The procedure is in `src/Polhem.Api.Core/CLAUDE.md`
     § Every wire type is registered explicitly; this skill does not copy it
   - A polymorphic type (such as `FilterNode` → `FilterCondition` / `FilterGroup`) needs a hand-written formatter,
     and registering the base does not cover the subtypes (`FilterNodeFormatter` plus its two adapters are the
     reference)
   - An `object` member goes through the discriminated envelope (`WireValueFormatter`), which has its own rules in
     the same CLAUDE.md

3. **Whether the BO Result maps to the wire Response by name**
   - There is no registry. `ApiOutputConverter` maps `<Action>Result` → `<Action>Response` by name, searching the
     `Polhem.Api.Core` assembly, and copies the properties with `ApiInputConverter`
   - A Result whose name breaks the convention gets no conversion: the BO object itself is sent, which is not what
     clients expect. Keep the names paired
   - The type allowlist (`SysInfo.AllowedTypeNamespaces`) already includes the framework namespaces by default
     (see `SysInfo.BuildAllowedTypeNamespaces`), so framework DTOs need no configuration

## Writing guide per layer

### Layer 1: contract interface (`Polhem.Api.Contracts`)

```csharp
namespace Polhem.Api.Contracts.<Axis>
{
    /// <summary>Contract interface for the <Action> request.</summary>
    public interface I<Action>Request
    {
        // Read-only properties + complete XML doc
        SomeType SomeProperty { get; }
    }
}
```

- The interface carries **no** attribute and **no** implementation
- `Polhem.Api.Contracts.csproj` already references `Polhem.Definition`, so definition types such as
  `FilterNode` / `SortFieldCollection` / `DefineType` can be used directly. Do not add package references to it
  (`rules/dependency-boundary.md`)
- XML doc is in **English** (public NuGet package convention)
- The interface is what keeps the two property copies complete: `ApiContractPairingTests`
  (tests/Polhem.Api.Core.UnitTests/Contracts) and `BusinessContractPairingTests` (tests/Polhem.Business.UnitTests/Contracts)
  fail when a wire type or a BO args/result type lacks its `I*` interface

### Layer 2: wire DTO (`Polhem.Api.Core/Messages/<Axis>/`)

```csharp
namespace Polhem.Api.Core.Messages.<Axis>
{
    /// <summary>API request for the <Action> operation.</summary>
    public sealed class <Action>Request : ApiRequest, I<Action>Request
    {
        /// <summary>...</summary>
        public string Foo { get; set; } = string.Empty;

        /// <summary>...</summary>
        public Bar? Bar { get; set; }
    }
}
```

- **No serialization attributes.** Wire DTOs are plain POCOs; a wire member is a public read-write property not marked
  `[JsonIgnore]`. `[MessagePackObject]` / `[Key]` / `[Union]` were removed by ADR-036; do not reintroduce them
- Align names with the contract interface: `<Action>Request` ↔ `<Action>Response`
- Properties must be `{ get; set; }` (the formatters need the setter); the interface is read-only
- Folder and namespace must match: `Messages/Form/` ↔ `namespace Polhem.Api.Core.Messages.Form`
  (IDE0130; a violation fails the strict build)
- Every public class under `Polhem.Api.Core.Messages` is published as a wire type; `MessagesNamespaceGateTests`
  fails for a class there that is not a wire message

**Then register both DTOs** in `src/Polhem.Api.Core/MessagePack/WireContracts.<Axis>.cs`, copying the shape of an
existing block (for example `DeleteRequest` in `WireContracts.Form.cs`): one `.Member(...)` per member, **including
the inherited `Parameters`**, then `.Build()`. New closed generics and enums go in `WireContracts.Generics.cs`.
`WireContractDriftTests` fails when a type or a member reachable from the messages is missing from the registrations.

**Then regenerate the published contract.** `WireContractGeneratorTests` fails when the message types no longer match
`wire-contracts/messages.d.ts`; the regeneration command is in `wire-contracts/README.md`. If the change touches the
envelope of an `object` member or the shape of a `DataTable` / enum, `WireFixtureTests` fails as well
(`wire-fixtures/README.md`). A diff in either folder turns the CI of `polhem-connector-js` red once it is released and
that repository moves to the new tag; plan that side for the same release (`rules/serialization.md` § Wire shape
changes have a downstream in another repository).

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
public sealed class <Action>Args : BusinessArgs, I<Action>Request
{
    public string Foo { get; set; } = string.Empty;
    // Same members as the wire DTO
}

public sealed class <Action>Result : BusinessResult, I<Action>Response
{
    public DataTable? Table { get; set; }
}
```

- BO POCOs carry no serialization attributes and are not registered in `WireContracts`: the wire only ever carries the
  `Polhem.Api.Core` DTOs, and the executor converts between the two
- Member names must match the wire DTO (`ApiInputConverter.Convert` copies properties by name through reflection;
  a mismatched name means that field is silently lost). Implementing the same `I*` interface on both sides is what
  makes the compiler check it

### Layer 5: BO method (goes through the Repository, obeys rule 1)

```csharp
/// <summary><English description of the method></summary>
/// <remarks>
/// (Where needed, state hard preconditions of use, e.g. GetList requires the caller to supply a Filter that narrows the result)
/// </remarks>
[ApiAccessControl(ApiProtectionLevel.Encrypted, ApiAccessRequirement.Authenticated)]
public virtual <Action>Result <Action>(<Action>Args args)
{
    ArgumentNullException.ThrowIfNull(args);

    // BusinessObject already has convenience methods that pass in the current AccessToken:
    //   CreateDataFormRepository(ProgId)        → the generic IDataFormRepository
    //   CreateFormRepository<IOrderRepository>() → the dedicated interface bound in ProgramSettings
    var repository = CreateDataFormRepository(ProgId);
    var result = repository.<DoWork>(args.X, args.Y, ...);

    return new <Action>Result { /* assemble from the repository result */ };
}
```

- `virtual` lets the host override it (multi-tenancy, specialised logic)
- `[ApiAccessControl]` goes on the method (`rules/security.md`). The values and meaning of `ApiProtectionLevel` and
  `ApiAccessRequirement` are in their XML docs (`src/Polhem.Definition/Security/`); choose the lowest protection level
  the data allows. A method with no attribute on itself, its base definition or its class is refused at call time,
  and `POLHEM3001` reports it at build time
- What an action name can reach is decided by `JsonRpcMethod.IsResolvableAction` (Polhem.JsonRpc.Server): a public, non-generic instance
  method with exactly one parameter. Keep helper methods with one parameter non-public, or they become reachable
  under a class-level attribute
- The BO does not reimplement fallback logic (fallback belongs to the Repository)

### Layer 8b: BO interface declaration (`I<Axis>BusinessObject.cs`)

A public BO method has two usage scenarios:

1. **API call**: dispatched by the JSON-RPC dispatcher reflecting on `progId.action`. **This only needs a `public` method + `[ApiAccessControl]` on the BO class**; no interface declaration is required
2. **Server-side call**: another BO, a background job or a scheduler obtains an instance through `IBusinessObjectFactory` and calls the method directly. The caller then holds an `I<Axis>BusinessObject` → **the method must be on the interface** to be callable

**Convention: add the method to the interface only when server-side code calls it (or is about to).** The remarks on
`ISystemBusinessObject` and `IFormBusinessObject` state the rule: the API surface and the interface are independent,
pure-API methods stay off the interface, and the definition methods (`GetDefine` / `SaveDefine` …) are the
documented counter-example because server code uses `IDefineAccess` directly.

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

#### How to write a cross-BO call

`IBusinessObjectFactory` lives in `Polhem.Definition` (lower layer) and `I<Axis>BusinessObject` in `Polhem.Business`
(upper layer). To avoid a reverse `Polhem.Definition → Polhem.Business` dependency, its single method
`CreateBusinessObject(Guid accessToken, string progId, bool isLocalCall)` returns `object`.

**When the caller is in `Polhem.Business` or a higher layer**, use the typed wrappers in
`BusinessObjectFactoryExtensions` (`CreateFormBO` / `CreateSystemBO`) to get the interface directly:

```csharp
// Inside another BO method
var formBo = BoFactory.CreateFormBO(AccessToken, "Employee", IsLocalCall);
var listResult = formBo.GetList(new GetListArgs { /* ... */ });
```

- `isLocalCall` has no default on purpose (see the XML doc of `IBusinessObjectFactory.CreateBusinessObject`): pass the
  current call's `IsLocalCall` rather than a literal `true`, so a remote call does not become a trusted local one
  one hop later
- Each axis maps to one fixed interface, and instances are told apart at runtime by `progId`, so there are no generic
  overloads

**When the caller is in a project that cannot reference `Polhem.Business`** (for example the dispatch path in
`Polhem.Api.Core`), call `CreateBusinessObject(token, progId, isLocalCall)` directly and operate on the `object` by
reflection.

- The design rationale is in the XML doc of `Polhem.Definition/IBusinessObjectFactory.cs`
- Do not move `IBusinessObjectFactory` into `Polhem.Business` to "avoid the cast"; that would invert the whole dependency topology
- Do not add a generic method to `IBusinessObjectFactory` through a default interface method (such as `T CreateForm<T>()`). The constraint cannot reference `IFormBusinessObject`, so type safety rests only on `where T : class`, and making callers write `<IFormBusinessObject>` explicitly is more error-prone than the fixed mapping in the helper

### Layer 9 (only for FormSchema-driven CRUD): extending the Repository abstraction

1. **`Polhem.Repository.Abstractions/Form/IDataFormRepository.cs`**: add the method signature
   - Parameter types are limited to `Polhem.Definition.*` (RepoAbs depends only on Definition)
   - It does **not** accept `Polhem.Business.*` types (that would be a reverse dependency)
2. **`Polhem.Repository/Form/DataFormRepository*.cs`**: the implementation (this layer may use `Polhem.Db`)
   - The production ctor is `(IRepositoryContext ctx, Guid accessToken, string progId)`; it loads the schema and
     resolves the database from the schema's `CategoryId`. A second ctor `(ctx, progId, schema, databaseId)` skips
     both lookups, for tests
   - The services are on the `RepositoryBase` members: `Context.ConnectionManager`, `Context.DefineAccess`,
     `Context.DbAccessFactory`, `DatabaseId`, `ProgId`. Follow `GetList`: get the dialect with
     `DbDialectRegistry.Get(connInfo.DatabaseType).CreateFormCommandBuilder(_schema, Context.DefineAccess)`, pass
     `ProgId` as the table name (rule 2), execute with `Context.DbAccessFactory.Create(DatabaseId).Execute(spec)`
3. **`Polhem.Repository/Factories/RepositoryFactory.cs`** needs no change for a new method
   - `CreateFormRepository<T>(accessToken, progId)` asks `IRepositoryTypeResolver` for the type
     (`ProgramSettingsRepositoryTypeResolver` reads `ProgramItem.Repository`; `DataFormRepository` when unspecified).
     A custom type must derive from `DataFormRepository` and is built with `ActivatorUtilities`; read the WARNING in
     the `DataFormRepository` ctor remarks before giving a subclass its own ctor parameters
   - **If the type cannot be resolved, it throws**, the same policy as the BO axis: data access has no harmless degraded mode
4. `IRepositoryFactory` is registered in `Polhem.Hosting/PolhemFrameworkServiceCollectionExtensions.cs` through
   `CreateConfigurableService`, which builds the configured type with `ActivatorUtilities`. A new ctor dependency on
   the factory must therefore be resolvable from DI; when it is not, the exception names the missing service
   (`CreateConfigurableService_UnregisteredCtorDependency_ExceptionNamesMissingService` covers that)

### Layer 10: client connector (`Polhem.Api.Client/Connectors/<Axis>ApiConnector.cs`)

```csharp
/// <summary>Asynchronously ...</summary>
public virtual async Task<<Action>Response> <Action>Async(
    <param1> p1, ..., CancellationToken cancellationToken = default)
{
    var request = new <Action>Request { /* property = parameter */ };
    return await ExecuteAsync<<Action>Response>(<Axis>Actions.<Action>, request, cancellationToken: cancellationToken)
        .ConfigureAwait(false);
}
```

- **Async only; there are no synchronous overloads.** Every connector method returns `Task` and takes a
  `CancellationToken`; `GetListAsync` on `FormApiConnector` is the template
- `FormApiConnector.ExecuteAsync<T>` defaults to `PayloadFormat.Encrypted`; pass a lower format only when the BO method
  allows it
- Exception propagation: which exception travels as which JSON-RPC error code, and whether its message travels with
  it, is declared once in `JsonRpcErrorContract` (src/Polhem.Api.Core/JsonRpc). Anything it does not cover is
  collapsed into `"Internal server error"` outside debug mode (`PolhemExceptionMapper.MapCode`)

## Tests (three layers of coverage)

| Layer | Path / template | What it verifies | What it does not verify |
|----|------------|---------|--------|
| Wire-level | `tests/Polhem.Api.Core.UnitTests/<Axis>/<Action>MessagePackTests.cs` | Member registration, polymorphic / collection / DataTable round-trip, default values | BO behaviour, SQL |
| Executor dispatch | `tests/Polhem.Api.Core.UnitTests/<Axis>/<Action>JsonRpcRoundTripTests.cs` | `progId.action` dispatch, Input/Output Converter | Actual SQL results |
| BO+DB integration | `tests/Polhem.Business.UnitTests/<Axis>/<Axis>BusinessObject<Action>Tests.cs` | BO → Repository → SQL → correctness against real data | Wire format |

Only the three layers together amount to "works end to end". Do **not** write a single "executor + real DB" integration
test: it is over-coupled and duplicates the responsibilities of the existing layers. The breadth gates
(`WireContractDriftTests`, `ApiContractSerializationTests`, the pairing tests) run automatically and do not replace
the per-method tests: they fill collection members with `null`, so only a test with real values proves a collection
survives.

### Wire-level test template

```csharp
public class <Action>MessagePackTests
{
    [Fact]
    [DisplayName("<Action>Request with every member populated round-trips intact")]
    public void <Action>Request_RoundTrip_PreservesMembers()
    {
        var request = new <Action>Request { /* fully populated */ };
        var bytes = MessagePackCodec.Serialize(request);    // internal; visible to this test project
        var restored = MessagePackCodec.Deserialize<<Action>Request>(bytes);
        // Compare values, not just Assert.NotNull
    }

    [Fact]
    [DisplayName("<Action>Request with every member at its default value round-trips to equal content")]
    public void <Action>Request_DefaultValues_RoundTrip() { /* a null collection comes back null */ }
}
```

### Dispatch test template (stub Repository)

Copy the private `StubFormRepositoryFactory` / `StubDataFormRepository` pair from
`tests/Polhem.Api.Core.UnitTests/Form/GetListJsonRpcRoundTripTests.cs`.

```csharp
public class <Action>JsonRpcRoundTripTests : IClassFixture<PolhemTestFixture>
{
    [Fact]
    [DisplayName("<ProgId>.<Action> through the JSON-RPC dispatcher dispatches to the BO and returns the stub result")]
    public async Task <Action>_ThroughJsonRpc_DispatchesAndReturnsTable()
    {
        var stubRepository = new StubDataFormRepository(/* fixed DataTable */);
        var stubFactory = new StubFormRepositoryFactory(stubRepository);
        var overrideServices = new TestOverrideServiceProvider(
            _fx.Provider,
            (typeof(IRepositoryFactory), stubFactory));

        var boFactory = new BusinessObjectFactory(
            overrideServices,
            _fx.GetRequiredService<IDefineAccess>(),
            _fx.GetRequiredService<ISessionInfoService>(),
            _fx.GetRequiredService<ILanguageService>(),
            _fx.GetRequiredService<IBoTypeResolver>());

        // `TestDispatcher` (tests/Polhem.Api.Core.UnitTests/Dispatch) sends the request as JSON, in process.
        var dispatcher = new TestDispatcher(_fx.Provider, boFactory)
        {
            AccessToken = TestSessionFactory.CreateAccessToken(_fx),
        };

        var request = new TestRpcRequest
        {
            Method = $"<ProgId>.{<Axis>Actions.<Action>}",
            Params = new JsonRpcParams { Value = new <Action>Request { /* ... */ } },
            Id = Guid.NewGuid().ToString(),
        };

        var response = await dispatcher.ExecuteAsync(request);
        Assert.Null(response.Error);
        var result = Assert.IsType<<Action>Response>(response.Result!.Value);
        // ... assertions on result + the stub's recorded arguments
    }
}
```

### BO + DB integration test template

```csharp
public class <Axis>BusinessObject<Action>Tests : IClassFixture<SharedDbFixture>
{
    [DbFact(DatabaseType.SQLite)]
    [DisplayName("...")]
    public void <Action>_Sqlite_<Scenario>()
    {
        // Get the test databaseId with TestDbConventions.GetDatabaseId(dbType, categoryId)
        // Construct a DataFormRepository and inject it into the BO context with TestOverrideServiceProvider
        // Seed test data; clean up in try/finally
        // Call bo.<Action>(args) and assert on the returned values
    }
}
```

- In the test environment the databaseId is `{categoryId}_{dbtype}` (such as `company_sqlite`), which differs from
  production. Tests pass the test databaseId explicitly (do not rely on the production Factory's resolution)
- Seeding and cleanup follow the try/finally + GUID RowId pattern of `FormBusinessObjectGetListTests`
- Use `SharedDbFixture` and `[DbFact]` for anything that touches the database (`rules/testing.md`)

## Common pitfalls

Easy to hit while writing:

1. **Adding a `Polhem.Db` ProjectReference to `Polhem.Business` = violates rule 1**: it means the BO calls Db
   directly; go through the Repository abstraction instead
2. **Folder and namespace mismatch (IDE0130)**: `Messages/Form/` must map to
   `namespace Polhem.Api.Core.Messages.Form`
3. **Forgetting the wire registration, or a member in it**: on the desktop nothing happens (the contractless resolver
   covers it), on iOS it is a `FormatterNotRegisteredException`. `WireContractDriftTests` catches it; so does the
   `-p:DynamicCodeSupport=false` gate in `src/Polhem.Api.Core/CLAUDE.md`
4. **BO Result name breaks the `XxxResult` convention**: `ApiOutputConverter` finds no Response and sends the BO
   object as is. There is no registration escape; rename it
5. **A polymorphic type registered only on its base**: a caller holding a subtype resolves the subtype's formatter.
   Register a formatter per static type the wire can see (`FilterConditionFormatter` / `FilterGroupFormatter`)
6. **Calling `MessagePackSerializer.Serialize(..., ContractlessStandardResolver.Options)` directly and
   bypassing the framework**: the registered formatters are skipped. Always go through `MessagePackCodec` (tests) or
   `MessagePackPayloadSerializer`
7. **`ApiInputConverter` matches by property name**: if BO Args property names do not match the wire DTO →
   that field is silently lost, with no compile error. The shared `I*` interface is the guard
8. **Types outside the `SysInfo.AllowedTypeNamespaces` allowlist**: the envelope's type name and every named `object`
   value are screened by `WireTypeWhitelist`; types in the framework namespaces are allowed by default
9. **A new ctor dependency on `RepositoryFactory` that DI cannot resolve**: `CreateConfigurableService` fails on
   first resolution with an exception naming the missing service
10. **Delete existing trivial POCO property tests outright when a ctor signature widens**: when a test is only a
    setter check like `new T(progId).ProgId == progId`, keeping it after a refactor costs upkeep and
    adds no real coverage; the integration tests carry the coverage
11. **`[DbFact]` only looks at environment variables and does not check that the DB is reachable**: if `.runsettings`
    sets `POLHEM_TEST_CONNSTR_SQLSERVER` but the SQL Server container is not running → the test fails
    instead of skipping. SQLite in-memory does not have this problem (no external dependency)
12. **Process-wide statics race between test classes**: see `rules/testing.md`, decision 3
13. **A nullable collection member**: `CollectionBaseFormatter` writes a null collection as nil and reads nil back as
    null; `GetListMessagePackTests.GetListRequest_DefaultValues_RoundTrip` pins it for `SortFields`. Add the same
    default-values case for your DTO
14. **The `FormApiConnector` ExecFunc family references `SystemActions`**: this is the **only** exception to rule 3.
    New FormBO-specific methods always use `FormActions.<Action>`

## Full checklist (all layers combined)

When adding an `<Action>` method, complete every step below in order.

**P0 exploration (read-only)**:
- [ ] Confirm how the BO obtains the services it needs (Repository abstraction / `Services.GetRequiredService<T>()`)
- [ ] Every type the DTO reaches is either already registered or on your list to register
- [ ] The BO Result and wire Response names pair as `<Action>Result` / `<Action>Response`

**P1 contract + wire layer** (one commit):
- [ ] `src/Polhem.Api.Contracts/<Axis>/I<Action>Request.cs`
- [ ] `src/Polhem.Api.Contracts/<Axis>/I<Action>Response.cs`
- [ ] `src/Polhem.Api.Core/Messages/<Axis>/<Action>Request.cs`
- [ ] `src/Polhem.Api.Core/Messages/<Axis>/<Action>Response.cs`
- [ ] Register both in `src/Polhem.Api.Core/MessagePack/WireContracts.<Axis>.cs` (+ `WireContracts.Generics.cs` for new closed generics / enums)
- [ ] Regenerate `wire-contracts/` (and `wire-fixtures/` if the shape changed) and read the diff
- [ ] Add `public const string <Action>` to `src/Polhem.Definition/<Axis>Actions.cs`
- [ ] Release build 0w/0e

**P2 BO + Repository** (one commit):
- [ ] `src/Polhem.Business/<Axis>/<Action>Args.cs`
- [ ] `src/Polhem.Business/<Axis>/<Action>Result.cs`
- [ ] Add the method to the axis BO (through the Repository)
- [ ] Add the signature to `I<Axis>BusinessObject.cs` only if server-side code calls it
- [ ] Add the method to `src/Polhem.Repository.Abstractions/Form/IDataFormRepository.cs` (if it needs one)
- [ ] Implement it in `src/Polhem.Repository/Form/DataFormRepository*.cs`
- [ ] BO integration test: `tests/Polhem.Business.UnitTests/<Axis>/<Axis>BusinessObject<Action>Tests.cs`
- [ ] Release build 0w/0e + SQLite tests pass

**P3 Client + wire tests** (one commit):
- [ ] Add `<Action>Async` to `src/Polhem.Api.Client/Connectors/<Axis>ApiConnector.cs` (async only)
- [ ] Wire-level round-trip test
- [ ] Executor dispatch round-trip test
- [ ] Release build 0w/0e + tests pass, including the `-p:DynamicCodeSupport=false` gate for `Polhem.Api.Core.UnitTests`

**P4 surface sync** (in the same commit as either P2 or P3):
- [ ] Update `docs/en/api/api-method-reference.md` + `docs/zh-TW/api/api-method-reference.md`: add / change the method in the table for its axis (restamp the translation, `rules/public-docs.md`)
- [ ] Update the `s_expectedSurface` baseline in `tests/Polhem.Business.UnitTests/BoApiSurfaceTests.cs` (additions, removals and `[ApiAccessControl]` changes all require it)
- [ ] `BoApiSurfaceTests` pass: they compare the baseline with reflection and with the method reference in each language

**Commit + push**:
- [ ] Commit and push following `.claude/rules/pull-request.md` (branch + pull request workflow), then wait for CI to finish and report
- [ ] On failure, handle it by the process in `.claude/rules/pull-request.md` (if clearly fixable, fix it directly, commit, push)

## Reference files (keep them open while reading code)

| Purpose | File |
|------|------|
| Contract interface template | `src/Polhem.Api.Contracts/System/IGetDefineRequest.cs` / `IGetDefineResponse.cs` (folders by axis: `System/` `Form/` `AuditLog/`) |
| Wire DTO template | `src/Polhem.Api.Core/Messages/System/GetDefineRequest.cs` / `GetDefineResponse.cs` |
| Wire registration | `src/Polhem.Api.Core/MessagePack/WireContracts.<Axis>.cs`; procedure in `src/Polhem.Api.Core/CLAUDE.md` |
| Action constant template | `src/Polhem.Definition/SystemActions.cs` / `FormActions.cs` |
| BO Args/Result template | `src/Polhem.Business/System/GetDefineArgs.cs` / `GetDefineResult.cs` |
| BO method template | `src/Polhem.Business/System/SystemBusinessObject.Define.cs` (`GetDefine`) / `src/Polhem.Business/Form/FormBusinessObject.Read.cs` (`GetList`) |
| Repository abstraction / implementation | `src/Polhem.Repository.Abstractions/Form/IDataFormRepository.cs` / `src/Polhem.Repository/Form/DataFormRepository.cs` |
| Repository Factory | `src/Polhem.Repository/Factories/RepositoryFactory.cs` |
| Client connector | `src/Polhem.Api.Client/Connectors/SystemApiConnector.cs` / `FormApiConnector.cs` |
| JSON-RPC dispatch | `src/Polhem.Api.Core/Dispatch/` (the Polhem components on the `Polhem.JsonRpc.Server` dispatcher) |
| Naming-convention conversion | `src/Polhem.Api.Core/Conversion/ApiOutputConverter.cs` / `ApiInputConverter.cs` |
| BO integration test template | `tests/Polhem.Business.UnitTests/Form/FormBusinessObjectGetListTests.cs` |
| Wire round-trip template | `tests/Polhem.Api.Core.UnitTests/Form/GetListMessagePackTests.cs` |
| Executor dispatch template | `tests/Polhem.Api.Core.UnitTests/Form/GetListJsonRpcRoundTripTests.cs` |
| Single-page API method overview | `docs/en/api/api-method-reference.md` (must be updated for every new method) |
| Surface audit test | `tests/Polhem.Business.UnitTests/BoApiSurfaceTests.cs` |
